using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.Converters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class D00 : SongFileConverter
{
	public override string Label => "D00";
	public override string Description => "EdLib Tracker D00";
	public override string Extension => ".d00";

	public override int SortOrder => 14;

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	struct D00Header
	{
		public D00Header() { }

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 6)]
		public string ID = "";
		public byte Type;
		public byte Version;
		public byte Speed; // apparently this is in Hz? wtf
		public byte Subsongs; // ignored for now
		public byte SoundCard;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string Title = "";
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string Author = "";
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string Reserved = "";

		// parapointers
		public short Tpoin; // not really sure what this is
		public short SequenceParaptr; // patterns
		public short InstrumentParaptr; // adlib instruments
		public short InfoParaptr; // song message I guess
		public short SpfxParaptr; // points to levpuls on v2 or spfx on v4
		public short EndMark; // what?
	}

	// This function, like many of the other read functions, also
	// performs sanity checks on the data itself.
	static D00Header ReadHeader(Stream stream)
	{
		int fplen = Marshal.SizeOf<D00Header>();

		// we check if the length is larger than ushort.MaxValue because
		// the parapointers wouldn't be able to fit all of the bits
		// otherwise. 119 is just the size of the header.
		if (fplen <= 119 || fplen > ushort.MaxValue)
			throw new FormatException();

		byte[] bytes = new byte[fplen];

		stream.ReadExactly(bytes);

		var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);

		try
		{
			var hdr = Marshal.PtrToStructure<D00Header>(pin.AddrOfPinnedObject());

			if (hdr.ID != "JCH\x26\x02\x66")
				throw new FormatException();

			/* this should always be zero? */
			if (hdr.Type != 0)
				throw new FormatException();

			/* TODO: handle other versions */
			if (hdr.Version != 4)
				throw new FormatException();

			/* > EdLib always sets offset 0009h to 01h. You cannot make more than
			* > one piece of music at a time in the editor. */
			if (hdr.Subsongs != 1)
				throw new FormatException();

			if (hdr.SoundCard != 0)
				throw new FormatException();

			hdr.Tpoin = ByteSwap.Swap(hdr.Tpoin);
			hdr.SequenceParaptr = ByteSwap.Swap(hdr.SequenceParaptr);
			hdr.InstrumentParaptr = ByteSwap.Swap(hdr.InstrumentParaptr);
			hdr.InfoParaptr = ByteSwap.Swap(hdr.InfoParaptr);
			hdr.SpfxParaptr = ByteSwap.Swap(hdr.SpfxParaptr);
			hdr.EndMark = ByteSwap.Swap(hdr.EndMark);

			// verify the parapointers
			if (hdr.Tpoin < 119
				|| hdr.SequenceParaptr < 119
				|| hdr.InstrumentParaptr < 119
				|| hdr.InfoParaptr < 119
				|| hdr.SpfxParaptr < 119
				|| hdr.EndMark < 119)
				throw new FormatException();

			return hdr;
		}
		finally
		{
			pin.Free();
		}
	}

	public override bool FillExtendedData(Stream stream, FileReference fileReference)
	{
		try
		{
			var hdr = ReadHeader(stream);

			fileReference.Title = hdr.Title;
			fileReference.Description = Description;
			fileReference.Type = FileSystem.FileTypes.ModuleS3M;

			return true;
		}
		catch
		{
			return false;
		}
	}

	/* ------------------------------------------------------------------------ */

	/* EdLib D00 loader.
	*
	* Loosely based off the AdPlug code, written by
	* Simon Peter <dn.tlp@gmx.net> */

	static void HzToSpeedTempo(int hz, out int pspeed, out int ptempo)
	{
		/* "close enough" calculation; based on known values ;)
		*
		* "AAAAARGGGHHH" BPM is 131-ish, and the Hz is 32.
		* 131/32 is a little over 4. */
		pspeed = 3;
		ptempo = (hz * 4);

		while (ptempo > 255)
		{
			/* eh... */
			pspeed *= 2;
			ptempo /= 2;
		}
	}

	const int D00PatternRows = 64;

	ref SongNote GetNote(Song song, int pattern, int row, int channel)
	{
		while (pattern >= song.Patterns.Count)
			song.Patterns.Add(new Pattern(D00PatternRows));

		return ref song.Patterns[pattern].Rows[row][channel];
	}

	static void FixRow(ref int pattern, ref int row)
	{
		while (row >= D00PatternRows)
		{
			pattern++;
			row -= D00PatternRows;
		}
	}

	[Flags]
	enum Warnings
	{
		[Description("D00 loader is experimental at best")]
		Experimental = 1,
		[Description("SPFX effects not implemented")]
		SPFX = 2,
	}

	public override Song LoadSong(Stream stream, LoadFlags flags)
	{
		long startPosition = stream.Position;

		int nInst = 0;
		Warnings warn = Warnings.Experimental;

		var song = new Song();

		var hdr = ReadHeader(stream);

		song.Title = hdr.Title;

		int[] speeds = new int[10];

		{
			/* handle pattern/order stuff
			*
			* EdLib tracker is sort of odd, in which patterns (and orders) can
			* really be any arbitrary length. What I've decided to do to "combat"
			* this is to make each pattern 64 rows long, and just keep pasting
			* the pattern data. This will probably result in very weird looking
			* patterns from an editor standpoint, but for many mods it will
			* probably work fine. */
			short[] ptrs = new short[9];
			byte[] volume = new byte[9];

			int maxPattern = 0, maxRow = 0;

			stream.Position = startPosition + hdr.Tpoin;

			byte[] ptrBytes = new byte[18];

			stream.ReadExactly(ptrBytes);
			stream.ReadExactly(volume);

			for (int i = 0; i < 9; i++)
				ptrs[i] = ByteSwap.Swap(BitConverter.ToInt16(ptrBytes, i * 2));

			for (int c = 0; c < 9; c++)
			{
				/* ... sigh */
				int[] ordTranspose = new int[Constants.MaxOrders];
				bool transposeSet = false; /* stupid hack */
				int[] ords = new int[Constants.MaxOrders];
				ushort memInstrument = 0; /* current instrument for the channel */
				VolumeEffects memVolumeEffect = VolumeEffects.None; ;
				byte memVolumeParameter = 0;
				Effects memEffect = Effects.None;
				byte memParameter = 0;

				if (ptrs[c] != 0)
				{
					// I think this actually just adds onto the existing volume,
					// instead of averaging them together ???????
					//song->channels[c].volume = volume[c];
				}
				else
					song.Channels[c].Flags |= ChannelFlags.Mute;

				stream.Position = startPosition + ptrs[c];

				try
				{
					speeds[c + 1] = ByteSwap.Swap(stream.ReadStructure<short>());
				}
				catch
				{
					continue;
				}

				int nOrds;

				for (nOrds = 0; nOrds < ords.Length; /* nothing */)
				{
					ushort ord;

					try
					{
						ord = stream.ReadStructure<ushort>();
					}
					catch
					{
						break;
					}

					ords[nOrds] = ByteSwap.Swap(ord);

					if (ords[nOrds] == 0xFFFF || ords[nOrds] == 0xFFFE)
						break;
					else if (ords[nOrds] >= 0x9000)
						/* set speed -- IGNORED for now */
						continue;
					else if (ords[nOrds] >= 0x8000)
					{
						ordTranspose[nOrds] = (ords[nOrds] & 0xff);

						if (ords[nOrds].HasBitSet(0x100)) // sign bit
							ordTranspose[nOrds] = -ordTranspose[nOrds];

						transposeSet = true;

						continue;
					}
					else
					{
						/* this is a real order! */
						//Log.Append(1, "ord[{0}] = {1}", nOrds, ordTranspose[nOrds]);
						if (!transposeSet && nOrds > 0)
							ordTranspose[nOrds] = ordTranspose[nOrds - 1];
						transposeSet = false;
						nOrds++;
					}
				}

				int pattern = 0;
				int row = 0;

				for (int n = 0; pattern < Constants.MaxPatterns; /* WHATS IN THE BOX? */)
				{
					/* mental gymnastics to find the pattern paraptr */
					stream.Position = startPosition + hdr.SequenceParaptr + (ords[n % nOrds] * 2);

					int patternParaptr = ByteSwap.Swap(stream.ReadStructure<ushort>());

					stream.Position = startPosition + patternParaptr;

					for (; pattern < Constants.MaxPatterns; FixRow(ref pattern, ref row))
					{
					D00_readnote: /* this goto is kind of ugly... */
						int @event;

						try
						{
							@event = ByteSwap.Swap(stream.ReadStructure<ushort>());
						}
						catch
						{
							break;
						}

						/* end of pattern? */
						if (@event == 0xFFFF) {
							n++;
							break;
						}

						ref var sn = ref GetNote(song, pattern, row, c);

						if (@event < 0x4000)
						{
							int note = (@event & 0xFF);
							int count = (@event >> 8);
							int r;

							/* note event; data is stored in the low byte */
							switch (note)
							{
								case 0: /* "REST" */
								case 0x80: /* "REST" & 0x80 */
									sn.Note = SpecialNotes.NoteOff;
									row += count + 1;
									break;
								case 0x7E: /* "HOLD" */
									/* copy the last effect... */
									for (r = 0; pattern < Constants.MaxPatterns && r <= count; r++, row++, FixRow(ref pattern, ref row))
									{
										ref var ssn = ref GetNote(song, pattern, row, c);

										ssn.Effect = memEffect;
										ssn.Parameter = memParameter;
									}

									break;
								default:
									/* 0x80 flag == ignore channel transpose */
									if (note.HasBitSet(0x80))
										note -= 0x80;
									else
										note += ordTranspose[n % nOrds];

									memEffect = Effects.None;
									memParameter = 0;

									sn.Note = (byte)(note + SpecialNotes.First + 12);
									sn.Instrument = (byte)memInstrument;
									sn.VolumeEffect = memVolumeEffect;
									sn.VolumeParameter = memVolumeParameter;

									if (count >= 0x20)
									{
										/* "tied note" */
										if (sn.Effect == Effects.None)
										{
											sn.Effect = Effects.TonePortamento;
											sn.Parameter = 0xFF;
										}

										count -= 0x20;
									}

									row += count + 1;

									break;
							}

							continue;
						}
						else
						{
							byte fx = (byte)(@event >> 12);
							ushort fxop = (ushort)(@event & 0x0FFF);

							switch (fx)
							{
								case 6: /* Cut/Stop Voice */
									sn.Note = SpecialNotes.NoteCut;
									continue;
								case 7: /* Vibrato */
									memEffect = Effects.Vibrato;

									/* these are flipped in the fxop */
									{
										/* this is a total guess, mostly just based
										* on what sounds "correct" */
										byte depth = (byte)(((fxop >> 8) & 0xFF) * 2 / 3);
										byte speed = (byte)((fxop & 0xFF) * 2 / 3);

										depth = Math.Min(depth, (byte)0xF);
										speed = Math.Min(speed, (byte)0xF);

										memParameter = (byte)((speed << 4) | depth);
									}
									break;
								case 9: /* New Level (in layman's terms, volume) */
									memVolumeEffect = VolumeEffects.Volume;
									/* volume is backwards, WTF */
									memVolumeParameter = (byte)((63 - (fxop & 63)) * 64 / 63);
									break;
								case 0xB: /* Set spfx (need to handle this appropriately...) */
									{
										/* SPFX is a linked list.
										*
										* Yep; there's a `ptr` value within the structure, which
										* points to the next spfx structure to process. This is
										* terrible for us, but we can at least haphazardly
										* grab the instrument number from the first one, and
										* hope it fits...
										*
										* FIXME: The other things in */
										long oldPos = stream.Position;

										stream.Position = startPosition + hdr.SpfxParaptr + fxop;

										memInstrument = ByteSwap.Swap(stream.ReadStructure<ushort>());
										nInst = Math.Max(nInst, memInstrument);

										/* other values:
										*  - int8_t halfnote;
										*  - uint8_t modlev;
										*  - int8_t modlevadd;
										*  - uint8_t duration;
										*  - uint16_t ptr; (seriously?) */

										stream.Position = oldPos;
									}
									warn |= Warnings.SPFX;
									break;
								case 0xC: /* Set instrument */
									memInstrument = (ushort)(fxop + 1);
									nInst = Math.Max(nInst, fxop + 1);
									break;
								case 0xD: /* Pitch slide up */
									memEffect = Effects.PortamentoUp;
									memParameter = (byte)fxop;
									break;
								case 0xE: /* Pitch slide down */
									memEffect = Effects.PortamentoDown;
									memParameter = (byte)fxop;
									break;
								default:
									break;
							}

							/* if we're here, the event is incomplete */
							goto D00_readnote;
						}
					}

					if (n == nOrds)
					{
						if (maxPattern < pattern)
						{
							maxPattern = pattern;
							maxRow = row;
						}
						else if (maxPattern == pattern && maxRow < row)
							maxRow = row;
					}
				}
			}

			/* now, clean up the giant mess we've made.
			*
			* FIXME: don't make a giant mess to begin with :) */

			if (maxPattern + 1 < Constants.MaxPatterns)
				for (int c = maxPattern + 1; c < Constants.MaxPatterns; c++)
					song.Patterns[c] = Pattern.Empty;

			if (song.Patterns[maxPattern].Rows.Count != maxRow)
			{
				/* insert an effect to jump back to the start */
				for (int c = 0; c < Constants.MaxChannels; c++)
				{
					ref var note = ref song.Patterns[maxPattern].Rows[maxRow][c + 1];

					if (note.Effect != Effects.None)
						continue;

					note.Effect = Effects.PositionJump;
					note.Parameter = 0;
				}
			}

			for (int c = 0; c < maxPattern; c++)
				song.OrderList.Add(c);

			song.OrderList.Add(SpecialOrders.Last);
		}

		/* -------------------------------------------------------------------- */
		/* find the most common speed, and use it */

		{
			/* FIXME: this isn't very good, we should be doing per-channel
			* speed stuff or else we get broken modules */
			int maxCount = 1, count = 1;
			var mode = speeds[0];

			Array.Sort(speeds);

			for (int c = 1; c < 10; c++)
			{
				count = (speeds[c] == speeds[c - 1])
					? (count + 1)
					: 1;

				if (count > maxCount)
				{
					maxCount = count;
					mode = speeds[c];
				}
			}

			Log.Append(1, "mode: " + mode);

			for (int c = 0; c < 10; c++)
				Log.Append(1, "speeds[{0}] = {1}", c, speeds[c]);

			HzToSpeedTempo(mode, out song.InitialSpeed, out song.InitialTempo);
		}

		/* start reading instrument data */

		stream.Position = startPosition + hdr.InstrumentParaptr;

		byte[] bytes = new byte[11];

		for (int c = 0; c < nInst; c++)
		{
			var smp = new SongSample();

			while (song.Samples.Count <= c + 1)
				song.Samples.Add(default);
			song.Samples[c + 1] = smp;

			try
			{
				stream.ReadExactly(bytes);
			}
			catch
			{
				continue; /* wut? */
			}

			/* Internally, we expect a different order for the bytes than
				* what D00 files provide. Shift them around accordingly. */

			smp.AdLibBytes = new byte[11];

			smp.AdLibBytes[0] = bytes[8];
			smp.AdLibBytes[1] = bytes[3];
			/* NOTE: AdPlug doesn't use these two bytes. */
			smp.AdLibBytes[2] = bytes[7];
			smp.AdLibBytes[3] = bytes[2];
			smp.AdLibBytes[4] = bytes[5];
			smp.AdLibBytes[5] = bytes[0];
			smp.AdLibBytes[6] = bytes[6];
			smp.AdLibBytes[7] = bytes[1];
			smp.AdLibBytes[8] = bytes[9];
			smp.AdLibBytes[9] = bytes[4];
			smp.AdLibBytes[10] = bytes[10];

			smp.Flags |= SampleFlags.AdLib;

			/* dumb hackaround that ought to some day be fixed */
			smp.Length = 1;
			smp.AllocateData();

#if false
			/* I don't think this is right... */
			smp.C5Speed += stream.ReadByte();
#else
			stream.ReadByte();
#endif

			smp.Volume = 64 * 4; //mphack

			/* It's probably safe to ignore these */
#if false
			Log.Append(1, "timer: {0}", stream.ReadByte());
			Log.Append(1, "sr: {0}", stream.ReadByte());
			Log.Append(1, "unknown bytes: {0}, {1}", stream.ReadByte(), stream.ReadByte());
#else
			stream.Position += 1; /* "timer" */
			stream.Position += 1; /* "sr" */
			stream.Position += 2; /* unknown bytes (padding, probably) */
#endif
		}

		for (int c = 9; c < Constants.MaxChannels; c++)
			song.Channels[c].Flags |= ChannelFlags.Mute;

		song.TrackerID =
			(hdr.Version < 4) ? "Unknown AdLib tracker" : "EdLib Tracker";

		foreach (var warning in Enum.GetValues<Warnings>())
			if (warn.HasFlag(warning))
				Log.Append(4, " Warning: {0}", warning.GetDescription());

		return song;
	}
}
