using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.SongConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class MID : SongFileConverter
{
	public override string Label => "MID";
	public override string Description => "MIDI";
	public override string Extension => ".mid";

	/*
	some thoughts...

	really, we don't even need to adhere to the same channel numbering -- the notes
	could go pretty much anywhere, as long as note-off events are handled properly.
	but it'd be nice to have the channels at least sort of resemble the midi file,
	whether it's arranged by track or by midi channel.

	- try to allocate channels that aren't in use when possible, to avoid stomping on playing notes
	- set instrument NNA to continue with dupe cut (or note off?)
	*/

	/* --------------------------------------------------------------------------------------------------------- */
	// structs, local defines, etc.
	const int RowsPerPattern = 200;

	// Pulse/row calculations are done in fixed point for better accuracy
	const int FractionalBits = 12;
	const int FractionalMask = (1 << FractionalBits) - 1;

	enum MIDIFormat : short
	{
		SingleTrack,
		MultiTrack,
		MultiSong,
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct MIDIFileHeader
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
		public string Tag; // MThd
		public int HeaderLength;
		public MIDIFormat Format; // 0 = single-track, 1 = multi-track, 2 = multi-song
		public short NumTracks; // number of track chunks
		public short Division; // delta timing value: positive = units/beat; negative = smpte compatible units (?)
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct MIDIFileTrack
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
		public string Tag; // MTrk
		public int Length; // number of bytes of track data following
	};

	bool ReadMIDIMThd(Stream stream, ref MIDIFileHeader hdr)
	{
		hdr.Tag = stream.ReadString(4);

		stream.Position -= 4;

		if (hdr.Tag == "RIFF") // Stupid MS crap.
			stream.Position += 16;

		hdr = stream.ReadStructure<MIDIFileHeader>();

		if (hdr.Tag != "MThd")
			return false;

		hdr.HeaderLength = ByteSwap.Swap(hdr.HeaderLength);
		// don't care about format, either there's one track or more than one track. whoop de doo.
		// (format 2 MIDs will probably be hilariously broken, but I don't have any and also don't care)
		hdr.Format = ByteSwap.Swap(hdr.Format);
		hdr.NumTracks = ByteSwap.Swap(hdr.NumTracks);
		hdr.Division = ByteSwap.Swap(hdr.Division);

		stream.Position += hdr.HeaderLength - 6; // account for potential weirdness

		return true;
	}

	bool ReadMIDIMTrk(Stream stream, ref MIDIFileTrack mtrk)
	{
		mtrk = stream.ReadStructure<MIDIFileTrack>();

		if (mtrk.Tag != "MTrk")
		{
			Log.Append(4, " Warning: Invalid track header (corrupt file?)");
			return false;
		}

		mtrk.Length = ByteSwap.Swap(mtrk.Length);

		return true;
	}

	class Event
	{
		public uint Pulse; // the PPQN-tick, counting from zero, when this midi-event happens
		public byte Chan; // target channel (0-based!)
		public SongNote Note; // the note data (new data will overwrite old data in same channel+row)
		public Event? Next;

		public Event(ref SongNote note)
		{
			Note = note;
		}

		public Event(uint pulse, byte chan, ref SongNote note, Event? next)
		{
			Pulse = pulse;
			Chan = chan;
			Note = note;
			Next = next;
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */
	// support functions

	const int EOF = -1;

	uint ReadVarLen(Stream stream)
	{
		uint v = 0;

		// This will fail tremendously if a value overflows. I don't care.
		int b;

		do
		{
			b = stream.ReadByte();

			if (b == EOF)
				return 0; // truncated?!

			v <<= 7;
			v |= unchecked((uint)b) & 0x7f;
		} while (b.HasBitSet(0x80));

		return v;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	// info (this is ultra lame)

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			var tmpsong = LoadSong(stream, LoadFlags.NoSamples | LoadFlags.NoPatterns);

			file.Description = "Standard MIDI File";
			file.Title = tmpsong.Title;
			file.Type = FileTypes.ModuleMOD;

			return true;
		}
		catch
		{
			return false; // wahhhh
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */
	// load

	struct Channel
	{
		public byte ForegroundNote;
		public byte BackgroundNote;
		public byte Instrument;
	}

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		MIDIFileHeader mthd = new MIDIFileHeader();
		MIDIFileTrack mtrk = new MIDIFileTrack();

		Event? eventQueue;

		Channel[] midich = new Channel[16];

		var song = new Song();

		byte[] patchSamples = new byte[128];

		int nextFreeSampleNumber = 1; // Next free sample

		if (!ReadMIDIMThd(stream, ref mthd))
			throw new NotSupportedException();

		/* We'll count by "pulses" here, which are basically MIDI-speak for ticks, except that there are a heck
		of a lot more of them. (480 pulses/quarter is fairly common, that's like A78, if the tempo could be
		adjusted high enough to make practical use of that speed)
		Also, we'll use a 32-bit value and hopefully not overflow -- which is unlikely anyway, as it'd either
		require PPQN to be very ridiculously high, or a file that's several *hours* long.

		Stuff a useless event at the start of the event queue. */
		SongNote note = new SongNote();

		note.Note = SpecialNotes.None;

		eventQueue = new Event(ref note);

		uint pulse; // cumulative time from start of track

		Event? prev = null;
		Event? cur = eventQueue;

		for (int trkNum = 0; trkNum < mthd.NumTracks; trkNum++)
		{
			cur = eventQueue.Next;
			prev = eventQueue;

			pulse = 0;

			if (!ReadMIDIMTrk(stream, ref mtrk))
				break;

			long nextPos = stream.Position + mtrk.Length; // where this track is supposed to end

			bool foundEnd = false;

			int runningStatus = 0; // running status byte

			while (!foundEnd && stream.Position < nextPos)
			{
				var delta = ReadVarLen(stream); // time since last event (read from file) -- delta-time

				pulse += delta; // 'real' pulse count

				// get status byte, if there is one
				int status = stream.ReadByte(); // THIS status byte (as opposed to runningStatus)

				if ((status >= 0) && status.HasBitSet(0x80))
				{
				}
				else if (runningStatus.HasBitSet(0x80))
				{
					stream.Position--;
					status = runningStatus;
				}
				else
				{
					// garbage?
					continue;
				}

				note = new SongNote();

				int hi = status >> 4;
				int lo = status & 0xf;

				int cn = lo; //or: trkNum * CHANNELS_PER_TRACK + lo % CHANNELS_PER_TRACK;

				switch (hi)
				{
					case 0x8: // note off - x, y
					{
						runningStatus = status;
						int x = stream.ReadByte(); // note
						int y = stream.ReadByte(); // release velocity

						// clamp is wrong, but whatever
						// if the last note in the channel is the same as this note, just write ===
						// otherwise, if there is a note playing, assume our note got backgrounded
						// and write S71 (past note off)
						x = (x + SpecialNotes.First).Clamp(SpecialNotes.First, SpecialNotes.Last);

						if (midich[cn].ForegroundNote == x)
						{
							note = new SongNote();
							note.Note = SpecialNotes.NoteOff;
							midich[cn].ForegroundNote = SpecialNotes.None;
						}
						else
						{
							// S71, past note off
							note = new SongNote();
							note.Effect = Effects.Special;
							note.Parameter = 0x71;
							midich[cn].BackgroundNote = SpecialNotes.None;
						}
						break;
					}
					case 0x9: // note on - x, y (velocity zero = note off)
					{
						runningStatus = status;
						int x = stream.ReadByte(); // note
						int y = stream.ReadByte(); // attack velocity
						x = (x + SpecialNotes.First).Clamp(SpecialNotes.First, SpecialNotes.Last); // see note off above.

						if (lo == 9)
						{
							// ignore percussion for now
						}
						else if (y == 0)
						{
							// this is actually another note-off, see above
							// (maybe that stuff should be split into a function or blahblah)
							if (midich[cn].ForegroundNote == x)
							{
								note = new SongNote();
								note.Note = SpecialNotes.NoteOff;
								midich[cn].ForegroundNote = SpecialNotes.None;
							}
							else
							{
								// S71, past note off
								note = new SongNote();
								note.Effect = Effects.Special;
								note.Parameter = 0x71;
								midich[cn].BackgroundNote = SpecialNotes.None;
							}
						}
						else
						{
							if (nextFreeSampleNumber == 1 && !lflags.HasFlag(LoadFlags.NoSamples))
							{
								// no samples defined yet - fake a program change
								patchSamples[0] = 1;

								song.Samples[1] = new SongSample();

								FMPatches.Apply(song.Samples[1]!, 0);

								nextFreeSampleNumber++;
							}

							note = new SongNote();
							note.Note = (byte)x;
							note.Instrument = patchSamples[midich[cn].Instrument];
							note.VolumeEffect = VolumeEffects.Volume;
							note.VolumeParameter = (byte)((y & 0x7f) * 64 / 127);

							midich[cn].ForegroundNote = (byte)x;
							midich[cn].BackgroundNote = midich[cn].ForegroundNote;
						}
						break;
					}
					case 0xa: // polyphonic key pressure (aftertouch) - x, y
					{
						runningStatus = status;
						int x = stream.ReadByte();
						int y = stream.ReadByte();
						// TODO polyphonic aftertouch channel=lo note=x pressure=y
						continue;
					}
					case 0xb: // controller OR channel mode - x, y
					{
						runningStatus = status;
						// controller if first data byte 0-119
						// channel mode if first data byte 120-127
						int x = stream.ReadByte();
						int y = stream.ReadByte();
						// TODO controller change channel=lo controller=x value=y
						continue;
					}
					case 0xc: // program change - x (instrument/voice selection)
					{
						runningStatus = status;
						int x = stream.ReadByte();
						midich[cn].Instrument = (byte)x;
						// look familiar? this was copied from the .mus loader
						if ((patchSamples[x] == 0) && !lflags.HasFlag(LoadFlags.NoSamples))
						{
							// New sample!
							patchSamples[x] = (byte)nextFreeSampleNumber;
							song.Samples[nextFreeSampleNumber] = new SongSample();
							FMPatches.Apply(song.Samples[nextFreeSampleNumber]!, x);
							nextFreeSampleNumber++;
							// Log.Append(4, " Warning: Too many samples");
						}

						note = new SongNote();
						note.Instrument = patchSamples[x];
						break;
					}
					case 0xd: // channel pressure (aftertouch) - x
					{
						runningStatus = status;
						int x = stream.ReadByte();
						// TODO channel aftertouch channel=lo pressure=x
						continue;
					}
					case 0xe: // pitch bend - x, y
					{
						runningStatus = status;
						int x = stream.ReadByte();
						int y = stream.ReadByte();
						// TODO pitch bend channel=lo lsb=x msb=y
						continue;
					}
					case 0xf: // system messages
					{
						switch (lo)
						{
							case 0xf: // meta-event (text and stuff)
							{
								int x = stream.ReadByte(); // type
								var vlen = ReadVarLen(stream); // some other generic varlen number -- value length
								switch (x)
								{
									case 0x1: // text
									case 0x2: // copyright
									case 0x3: // track name
									case 0x4: // instrument name
									case 0x5: // lyric
									case 0x6: // marker
									case 0x7: // cue point
									{
										string message = stream.ReadString((int)vlen);

										if (x == 3 && !string.IsNullOrEmpty(message) && string.IsNullOrEmpty(song.Title))
											song.Title = message.Substring(0, Math.Min(message.Length, 25));

										song.Message += message;

										if (!song.Message.EndsWith("\n"))
											song.Message += '\n';

										vlen -= (uint)message.Length;

										break;
									}
									case 0x20: // MIDI channel (FF 20 len* cc)
														 // specifies which midi-channel sysexes are assigned to
									case 0x21: // MIDI port (FF 21 len* pp)
														 // specifies which port/bus this track's events are routed to
										break;

									case 0x2f:
										foundEnd = true;
										break;
									case 0x51: // set tempo
									{
										// read another stupid kind of variable length number
										// hopefully this fits into 8 bytes - if not, too bad!
										// (what is this? friggin' magic?)
										int y = (int)Math.Min(vlen, 8);

										byte[] buffer = new byte[8];

										stream.ReadExactly(buffer.Slice(8 - y, y));

										long value = BitConverter.ToInt64(buffer);

										value = ByteSwap.Swap(value);

										int bpm = unchecked((int)value);
										bpm = (60000000 / (bpm != 0 ? bpm : 1)).Clamp(0x20, 0xff);

										note = new SongNote();
										note.Effect = Effects.Tempo;
										note.Parameter = (byte)bpm;
										vlen -= (uint)y;
										break;
									}
									case 0x54: // SMPTE offset (what time in the song this track starts)
														 // (what?!)
										break;
									case 0x58: // time signature (FF 58 len* nn dd cc bb)
									case 0x59: // key signature (FF 59 len* sf mi)
														 // TODO care? don't care?
										break;
									case 0x7f: // some proprietary crap
										break;

									default:
										// some mystery crap
										Log.Append(2, " Unknown meta-event FF {0:X2)", x);
										break;
								}

								stream.Position += vlen;
								break;
							}
							/* sysex */
							case 0x0:
							/* syscommon */
							case 0x1:
							case 0x2:
							case 0x3:
							case 0x4:
							case 0x5:
							case 0x6:
							case 0x7:
								runningStatus = 0; // clear running status
								goto case 0x8;
							/* sysrt */
							case 0x8:
							case 0x9:
							case 0xa:
							case 0xb:
							case 0xc:
							case 0xd:
							case 0xe:
								// 0xf0 - sysex
								// 0xf1-0xf7 - common
								// 0xf8-0xff - sysrt
								// sysex and common cancel running status
								// TODO handle these, or at least skip them coherently
								continue;
						}

						break;
					}
				}

				// skip past any events with a lower pulse count (from other channels)
				while ((cur != null) && (pulse > cur.Pulse))
				{
					prev = cur;
					cur = cur.Next;
				}

				// and now, cur is either NULL or has a higher timestamp, so insert before it
				prev.Next = new Event(pulse, (byte)cn, ref note, cur);
				prev = prev.Next;
			}

			if (stream.Position != nextPos)
			{
				Log.Append(2, " Track {0} ended {1} bytes from boundary",
					trkNum, stream.Position - nextPos);
				stream.Position = nextPos;
			}
		}

		song.InitialSpeed = 3;
		song.InitialTempo = 120;

		if (lflags.HasFlag(LoadFlags.NoPatterns))
			return song;

		prev = null;
		cur = eventQueue;

		// okey doke! now let's write this crap out to the patterns
		Pattern? pattern = null;
		int row = RowsPerPattern; // what row of the pattern rowdata is pointing to (fixed point)
		int rowFractional = 0; // how much is left over
		int pat = 0; // next pattern number to create

		pulse = 0; // PREVIOUS event pulse.

		while (cur != null)
		{
			/* calculate pulse delta from previous event
			* calculate row count from the pulse count using ppqn (assuming 1 row = 1/32nd note? 1/64?)
			* advance the row as required
			it'd be nice to aim for the "middle" of ticks instead of the start of them, that way if an
				event is just barely off, it won't end up shifted way ahead.
			*/
			uint delta = cur.Pulse - pulse;

			if (delta > 0)
			{
				// Increment position
				row <<= FractionalBits;
				row += (int)(8 * (delta << FractionalBits) / mthd.Division); // times 8 -> 32nd notes
				row += rowFractional;
				rowFractional = row & FractionalMask;
				row >>= FractionalBits;
			}

			pulse = cur.Pulse;

			while (row >= RowsPerPattern)
			{
				// New pattern time!
				/*
				if (pat >= Constants.MaxPathLength)
				{
					Log.Append(4, " Warning: Too many patterns, song is truncated");
					return song;
				}
				*/
				pattern = song.SetPattern(pat, new Pattern(RowsPerPattern));
				song.OrderList.Add(pat);
				pat++;
				row -= RowsPerPattern;
			}

			var rowdata = pattern!.Rows[row];

			if (cur.Note.Note != 0)
			{
				rowdata[cur.Chan].Note = cur.Note.Note;
				rowdata[cur.Chan].Instrument = cur.Note.Instrument;
			}

			if (cur.Note.VolumeEffectByte != 0)
			{
				rowdata[cur.Chan].VolumeEffect = cur.Note.VolumeEffect;
				rowdata[cur.Chan].VolumeParameter = cur.Note.VolumeParameter;
			}

			if (cur.Note.EffectByte != 0)
			{
				rowdata[cur.Chan].Effect = cur.Note.Effect;
				rowdata[cur.Chan].Parameter = cur.Note.Parameter;
			}

			prev = cur;
			cur = cur.Next;
		}

		return song;
	}
}