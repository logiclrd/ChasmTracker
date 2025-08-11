using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.Converters;

using ChasmTracker.FileSystem;
using ChasmTracker.FM;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class MUS : SongFileConverter
{
	public override string Label => "MUS";
	public override string Description => "Doom Music File";
	public override string Extension => ".mus";

	public override int SortOrder => 10;

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct Header
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
		public string ID; // MUS\x1a
		public short ScoreLength;
		public short ScoreStart;
		public short Channels;
		public short SecondaryChannels;
		public short InstrumentCount;
		public short Dummy;
	}

	bool ReadHeader(Stream stream, out Header header)
	{
		header = stream.ReadStructure<Header>();

		if (header.ID != "MUS\x1A")
			return false;

		if (header.ScoreStart + header.ScoreLength > stream.Length)
			return false;

		return true;
	}

	/* --------------------------------------------------------------------- */

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		if (!ReadHeader(stream, out var header))
			return false;

		file.Description = "Doom Music File";
		file.Title = "";
		file.Type = FileSystem.FileTypes.ModuleMOD;

		return true;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* I really don't know what I'm doing here -- I don't know much about either midi or adlib at all, and I've
	never even *played* Doom. Frankly, I'm surprised that this produces something that's actually listenable.

	Some things yet to tackle:
	- Pitch wheel support is nonexistent. Shouldn't be TOO difficult; keep track of the target pitch value and how
		much of a slide has already been done, insert EFx/FFx effects, adjust notes when inserting them if the pitch
		wheel is more than a semitone off, and keep the speed at 1 if there's more sliding to do.
	- Percussion channel isn't handled. Get a few adlib patches from some adlib S3Ms?
	- Volumes for a couple of files are pretty screwy -- don't know whether I'm doing something wrong here, or if
		adlib's doing something funny with the volume, or maybe it's with the patches I'm using...
	- awesomus/d_doom.mus has some very strange timing issues: I'm getting note events with thousands of ticks.
	- Probably ought to clean up the warnings so messages only show once... */

	const int RowsPerPattern = 200;
	const int SpeedChannelNumber = 16; // where the speed adjustments go (counted from 0 -- 15 is the drum channel)
	const int BreakChannelNumber = SpeedChannelNumber - 1;
	const int TickAdjustmentFirstChannelNumber = BreakChannelNumber + 1; // S6x tick adjustments go here *and subsequent channels!!*

	// Tick calculations are done in fixed point for better accuracy
	const int FracBits = 12;
	const int FracMask = (1 << FracBits) - 1;

	class ChannelState
	{
		public byte Note; // the last note played in this channel
		public byte Instrument; // 1 -> 128
		public byte Volume; // 0 -> 64
	}

	public override Song LoadSong(Stream stream, LoadFlags flags)
	{
		bool finished = false;
		int tickFrac = 0; // fixed point

		var chanState = new ChannelState[16];

		byte prevSpeed = 1;

		byte[] patchSamples = new byte[128];
		byte[] patchPercussion = new byte[128];

		byte nextSample = 1; // Next free sample

		if (!ReadHeader(stream, out var hdr))
			throw new NotSupportedException();

		var song = new Song();

		for (int n = 16; n < 64; n++)
			song.Channels[n].Flags |= ChannelFlags.Mute;

		for (int i = 0; i < chanState.Length; i++)
			chanState[i] = new ChannelState();

		stream.Position = hdr.ScoreStart;

		// Narrow the data buffer to simplify reading
		var endPosition = Math.Min(stream.Length, hdr.ScoreStart + hdr.ScoreLength);

		/* start the first pattern */
		int pat = 0;
		int row = 0;

		var pattern = song.GetPattern(pat, create: true, RowsPerPattern)!;

		song.OrderList.Add(pat);

		while (!finished && stream.Position < endPosition)
		{
			int @event = stream.ReadByte();

			int type = (@event >> 4) & 7;
			int ch = @event & 15;

			ref var note = ref pattern.Rows[row][ch + 1];

			int b1, b2;

			switch (type)
			{
				case 0: // Note off - figure out what channel the note was playing in and stick a === there.
					b1 = stream.ReadByte() & 127; // & 127 => note number
					b1 = Math.Min((b1 & 127) + 1, SpecialNotes.Last);
					if (chanState[ch].Note == b1)
					{
						// Ok, we're actually playing that note
						if (!note.NoteIsNote)
							note.Note = SpecialNotes.NoteOff;
					}
					break;
				case 1: // Play note
					b1 = stream.ReadByte(); // & 128 => volume follows, & 127 => note number
					if (b1.HasBitSet(128))
					{
						chanState[ch].Volume = (byte)(((stream.ReadByte() & 127) + 1) >> 1);
						b1 &= 127;
					}

					chanState[ch].Note = (byte)Math.Min(b1 + 1, SpecialNotes.Last);

					if (ch == 15)
					{
						// Percussion
						b1 = b1.Clamp(24, 84); // ?

						if (patchPercussion[b1] == 0)
						{
							if (nextSample < Constants.MaxSamples)
							{
								// New sample!
								patchPercussion[b1] = nextSample;

								var sample = song.EnsureSample(nextSample);

								sample.Name = Tables.MIDIPercussionNames[b1 - 24];

								nextSample++;
							}
							else
							{
								// Phooey.
								Log.Append(4, " Warning: Too many samples");
								note.Note = SpecialNotes.NoteOff;
							}
						}
#if false
						note.Note = SpecialNotes.MiddleC;
						note.Instrument = patchPercussion[b1];
#else
						/* adlib is broken currently: it kind of "folds" every 9th channel, but only
						for SOME events ... what this amounts to is attempting to play notes from
						both of any two "folded" channels will cause everything to go haywire.
						for the moment, ignore the drums. even if we could load them, the playback
						would be completely awful.
						also reset the channel state, so that random note-off events don't stick ===
						into the channel, that's even enough to screw it up */
						chanState[ch].Note = SpecialNotes.None;
#endif
					}
					else
					{
						if (chanState[ch].Instrument != 0)
						{
							note.Note = chanState[ch].Note;
							note.Instrument = chanState[ch].Instrument;
						}
					}
					note.VolumeEffect = VolumeEffects.Volume;
					note.VolumeParameter = chanState[ch].Volume;
					break;
				case 2: // Pitch wheel (TODO)
					b1 = stream.ReadByte();
					break;
				case 3: // System event
					b1 = stream.ReadByte() & 127;
					switch (b1)
					{
						case 10: // All sounds off
							for (int n = 0; n < 16; n++)
							{
								note.Note = chanState[ch].Note = SpecialNotes.NoteCut;
								note.Instrument = 0;
							}
							break;
						case 11: // All notes off
							for (int n = 0; n < 16; n++) {
								note.Note = chanState[ch].Note = SpecialNotes.NoteOff;
								note.Instrument = 0;
							}
							break;
						case 14: // Reset all controllers
							/* ? */
							for (int i = 0; i < chanState.Length; i++)
								chanState[i] = new ChannelState();
							break;
						case 12: // Mono
						case 13: // Poly
							break;
					}
					break;
				case 4: // Change controller
					b1 = stream.ReadByte() & 127; // controller
					b2 = stream.ReadByte() & 127; // new value
					switch (b1)
					{
						case 0: // Instrument number
							if (ch == 15)
							{
								// don't fall for this nasty trick, this is the percussion channel
								break;
							}
							if (patchSamples[b2] == 0)
							{
								if (nextSample < Constants.MaxSamples)
								{
									// New sample!
									patchSamples[b2] = nextSample;
									FMPatches.Apply(song.EnsureSample(nextSample), b2);
									nextSample++;
								} else {
									// Don't have a sample number for this patch, and never will.
									Log.Append(4, " Warning: Too many samples");
									note.Note = SpecialNotes.NoteOff;
								}
							}
							chanState[ch].Instrument = patchSamples[b2];
							break;
						case 3: // Volume
							b2 = (b2 + 1) >> 1;
							chanState[ch].Volume = (byte)b2;
							note.VolumeEffect = VolumeEffects.Volume;
							note.VolumeParameter = chanState[ch].Volume;
							break;
						case 1: // Bank select
						case 2: // Modulation pot
						case 4: // Pan
						case 5: // Expression pot
						case 6: // Reverb depth
						case 7: // Chorus depth
						case 8: // Sustain pedal (hold)
						case 9: // Soft pedal
							/* I have no idea */
							break;
					}
					break;
				case 6: // Score end
					finished = true;
					break;
				default: // Unknown (5 or 7)
					/* Hope it doesn't take any parameters, otherwise things are going to end up broken */
					Log.Append(4, " Warning: Unknown event type {0}", type);
					break;
			}

			if (finished)
			{
				int leftover = (tickFrac + (1 << FracBits)) >> FracBits;
				pattern.Rows[row][BreakChannelNumber].Effect = Effects.PatternBreak;
				pattern.Rows[row][BreakChannelNumber].Parameter = 0;
				if ((leftover != 0) && (leftover != prevSpeed))
				{
					pattern.Rows[row][BreakChannelNumber].Effect = Effects.Speed;
					pattern.Rows[row][BreakChannelNumber].Parameter = (byte)leftover;
				}
			}
			else if (@event.HasBitSet(0x80))
			{
				// Read timing information and advance the row
				int ticks = 0;

				int b;

				do
				{
					b = stream.ReadByte();
					ticks = 128 * ticks + (b & 127);
					if (ticks > 0xffff)
						ticks = 0xffff;
				} while (b.HasBitSet(128));

				ticks = Math.Min(ticks, (0x7fffffff / 255) >> 12); // protect against overflow

				ticks <<= FracBits; // convert to fixed point
				ticks = ticks * 255 / 350; // 140 ticks/sec * 125/50hz => tempo of 350 (scaled)
				ticks += tickFrac; // plus whatever was leftover from the last row
				tickFrac = ticks & FracMask; // save the fractional part
				ticks >>= FracBits; // and back to a normal integer

				if (ticks < 1)
				{
#if false
					// There's only part of a tick - compensate by skipping one tick later
					tickFrac -= 1 << FracBits;
					ticks = 1;
#else
					/* Don't advance the row: if there's another note right after one of the ones
					inserted already, the existing note will be rendered more or less irrelevant
					anyway, so just allow any following events to overwrite the data.
					Also, there's no need to write the speed, because it'd just be trampled over
					later anyway.
					The only thing that would necessitate advancing the row is if there's a pitch
					adjustment that's at least 15/16 of a semitone; in that case, "steal" a tick
					(see above). */
					continue;
#endif
				}
				else if (ticks > 255)
				{
					/* Too many ticks for a single row with Axx.
					We can increment multiple rows easily, but that only allows for exact multiples
					of some number of ticks, so adding in some "padding" is necessary. Since there
					is no guarantee that rows after the current one even exist, any adjusting has
					to happen on *this* row. */

					int adjust = ticks % 255;
					int s6xch = TickAdjustmentFirstChannelNumber;
					while (adjust > 0)
					{
						int s6x = Math.Min(adjust, 0xf);
						pattern.Rows[row][s6xch].Effect = Effects.Special;
						pattern.Rows[row][s6xch].Parameter = (byte)(0x60 | s6x);
						adjust -= s6x;
						s6xch++;
					}
				}

				if (prevSpeed != Math.Min(ticks, 255))
				{
					prevSpeed = (byte)Math.Min(ticks, 255);
					pattern.Rows[row][SpeedChannelNumber].Effect = Effects.Special;
					pattern.Rows[row][SpeedChannelNumber].Parameter = prevSpeed;
				}

				ticks = ticks / 255 + 1;
				row += ticks;
			}

			while (row >= RowsPerPattern)
			{
				/* Make a new pattern. */
				pat++;
				row -= RowsPerPattern;
				if (pat >= Constants.MaxPatterns)
				{
					Log.Append(4, " Warning: Too much note data");
					finished = true;
					break;
				}

				pattern = song.GetPattern(pat, create: true, RowsPerPattern)!;

				song.OrderList.Add(pat);

				pattern.Rows[0][SpeedChannelNumber].Effect = Effects.Speed;
				pattern.Rows[0][SpeedChannelNumber].Parameter = prevSpeed;
			}
		}

		song.Flags |= SongFlags.NoStereo;
		song.InitialSpeed = 1;
		song.InitialTempo = 255;

		song.TrackerID = Description; // ?

		return song;
	}
}
