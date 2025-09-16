/*
#define D00_ENABLE_BROKEN_LEVELPULS

To enable broken levelpuls support.
This is broken because somewhere in the replayer, effects are ignored
after noteoff, and I don't care enough to fix it right now.
*/

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.SongConverters;

using System.Collections.Generic;
using System.Linq;
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

	static D00Header ReadHeaderV1(Stream stream)
	{
		/* reads old D00 header. this doesn't have a lot of identifying
		 * info, besides some assumptions we make that I am going to abuse */
		long streamLength = stream.Length - stream.Position;

		if (streamLength <= 15 || streamLength > ushort.MaxValue)
			throw new FormatException();

		var hdr = new D00Header();

		hdr.Version = stream.ReadStructure<byte>();
		/* old headers should never be higher than version 1 */
		if (hdr.Version < 1)
			throw new FormatException();

		hdr.Speed = stream.ReadStructure<byte>();
		hdr.Subsongs = stream.ReadStructure<byte>();
		hdr.Tpoin = stream.ReadStructure<short>();
		hdr.SequenceParaptr = stream.ReadStructure<short>();
		hdr.InstrumentParaptr = stream.ReadStructure<short>();
		hdr.InfoParaptr = stream.ReadStructure<short>();
		hdr.SpfxParaptr = stream.ReadStructure<short>(); /* actually levelpuls */
		hdr.EndMark = stream.ReadStructure<short>();

		return hdr;
	}

	static D00Header ReadHeaderNew(Stream stream)
	{
		int fplen = Marshal.SizeOf<D00Header>();

		// we check if the length is larger than ushort.MaxValue because
		// the parapointers wouldn't be able to fit all of the bits
		// otherwise. 119 is just the size of the header.
		if (fplen <= 119 || fplen > ushort.MaxValue)
			throw new FormatException();

		byte[] magic = new byte[6];

		stream.ReadExactly(magic);

		if (magic.ToStringZ() != "JCH\x26\x02\x66")
			throw new FormatException();

		long fpofs = stream.Position;

		stream.Position++;

		int version = stream.ReadByte();

		if (version.HasBitSet(0x80))
		{
		/* from adplug: "reheadered old-style song" */
			stream.Position = 0x68;

			Log.Append(1, " D00: This is a reheadered old-style song!");

			return ReadHeaderV1(stream);
		}

		stream.Position = fpofs;

		byte[] bytes = new byte[fplen];

		stream.ReadExactly(bytes);

		var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);

		try
		{
			var hdr = Marshal.PtrToStructure<D00Header>(pin.AddrOfPinnedObject());

			/* this should always be zero? */
			if (hdr.Type != 0)
				throw new FormatException();

			/* TODO: handle other versions */
			if (hdr.Version != 4)
				throw new FormatException();

			/* > EdLib always sets offset 0009h to 01h. You cannot make more than
			* > one piece of music at a time in the editor. */

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

	static D00Header ReadHeader(Stream stream)
	{
		/* this function is responsible for reading and verification
		 * most of this verification isn't useful for v2-v4 d00 files,
		 * but v0-v1 d00 files have virtually no identifying information
		 * if they haven't been reheadered. */

		D00Header hdr;

		try
		{
			hdr = ReadHeaderNew(stream);
		}
		catch (FormatException)
		{
			stream.Position = 0;
			hdr = ReadHeaderV1(stream);
		}

		/* should always be zero */
		if (hdr.Type != 0)
			throw new FormatException();

		if (hdr.Version > 4)
			throw new FormatException();

		/* ignore files with more than one subsong.
		 * we can't handle them anyway. */
		if (hdr.Subsongs != 1)
			throw new FormatException();

		/* make sure that none of our pointers point inside
		 * the file header (good for v0-v1, less-so for >v2) */
		long streamPosition = stream.Position;
		if (streamPosition < 0)
			throw new FormatException(); /* wut */

		if (hdr.Tpoin < streamPosition
		 || hdr.SequenceParaptr < streamPosition
		 || hdr.InstrumentParaptr < streamPosition
		 || hdr.InfoParaptr < streamPosition
		 || hdr.SpfxParaptr < streamPosition)
			throw new FormatException();

		/* endmark should ALWAYS be 0xFFFF
		 * this is actually a pretty good identifier of sorts... */
		if (hdr.EndMark != unchecked((short)0xFFFF))
			throw new FormatException();

		/* AdPlug: overwrite speed with 70hz if version is 0 */
		if (hdr.Version == 0)
			hdr.Speed = 70;

	 	return hdr;
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

	static void HzToSpeedTempo(byte ver, int hz, out int pspeed, out int ptempo)
	{
		if (ver >= 3)
		{
			/* have to do a bit more work here...
			 *
			 * this is really just a guesstimate.
			 * "AAAAARGGGHHH" BPM is 131-ish, and the hz value is 32.
			 * hence we just multiple by 131/32. :) */
			double tempo;
			int speed;

			speed = 3;
			tempo = hz * (131.0 / 32.0);

			while (tempo > 255.0)
			{
				/* divide until we get a valid tempo */
				speed *= 2;
				tempo /= 2;
			}

			pspeed = speed;
			ptempo = (int)Math.Round(tempo);
		}
		else
		{
			/* hz is basically just speed. */
			pspeed = hz;
			ptempo = 131;
 		}
	}

	const int D00PatternRows = 64;

	ref SongNote GetNote(Song song, int pattern, int row, int channel)
	{
		while (pattern >= song.Patterns.Count)
			song.Patterns.Add(new Pattern(D00PatternRows));

		return ref song.Patterns[pattern]!.Rows[row][channel];
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
#if !D00_ENABLE_BROKEN_LEVELPULS
		[Description("Levelpuls not implemented")]
		LevelPuls = 4,
#endif
		[Description("Instrument finetune ignored")]
		FineTune = 8,
	}

	static byte EdLibVolumeToITVolume(int x)
		=> unchecked((byte)((63 - (x & 63)) * 64 / 63));

#if D00_ENABLE_BROKEN_LEVELPULS
	/* return of false means catastrophic failure */
	static bool LoadLevelpuls(short paraptr, int tunelev,
		SongInstrument ins, SongSample smp, Stream fp)
	{
		/* FIXME: The first iteration of the tunelev takes in a "timer",
		 * which is weird-speak for sustain for given amount of ticks. */
		var loop = new System.Collections.BitArray(255);
		int x = 0;
		long start = fp.Position;;

		if (ins.VolumeEnvelope == null)
			return false;

		int i;

		for (i = 0; i < 25 /* node array size? */; i++)
		{
			if (tunelev == 0xFF)
				break; /* end of levelpuls. FIXME i think we need to loop this point */

			fp.Position = paraptr + tunelev * 4;

			loop[tunelev] = true;

			int level = fp.ReadByte();
			if (level < 0)
				return false;

			/* int8_t voladd; -- ignored */

			int duration = fp.ReadByte();
			if (duration < 0)
				return false;

			while (i >= ins.VolumeEnvelope.Nodes.Count)
				ins.VolumeEnvelope.Nodes.Add((0, 0));

			ins.VolumeEnvelope.Nodes[i].Tick = x;

			if (level != 0xFF)
				ins.VolumeEnvelope.Nodes[i].Value = EdLibVolumeToITVolume(level);
			else if (i > 0)
			{
				/* total guesstimate */
				ins.VolumeEnvelope.Nodes[i].Value = ins.VolumeEnvelope.Nodes[i - 1].Value;
			}
			else
				ins.VolumeEnvelope.Nodes[i].Value = 64;

			x += Math.Max(duration / 20, 1) + 1;

			tunelev = fp.ReadByte();
			if (tunelev < 0)
				return false;

			/* XXX set loop here */
			if (loop[tunelev])
				break;
		}

		if ((i >= 2) && (ins.VolumeEnvelope.Nodes.Count > i))
			ins.VolumeEnvelope.Nodes.RemoveRange(i, ins.VolumeEnvelope.Nodes.Count - i);

		fp.Position = start;

		/* I guess */
		return true;
	}
#endif

	public override Song LoadSong(Stream stream, LoadFlags flags)
	{
		long startPosition = stream.Position;

		int nInst = 0;
		Warnings warn = Warnings.Experimental;

		var song = new Song();

		var hdr = ReadHeader(stream);

		song.Title = hdr.Title;

		/* read in song info (message).
		 * this is USUALLY simply 0xFFFF (end marking) but in some
		 * old songs (e.g. "the alibi.d00") it contains real data */
		stream.Position = hdr.InfoParaptr;

		var messageBuffer = new List<byte>();

		for (int msgp = 0; msgp < Constants.MaxMessage; )
		{
			int ch = stream.ReadByte();

			if (ch < 0)
			{
				/* end of file before end marker == error
				 * OR we've misidentified a file :( */
				throw new FormatException();
			}

			if (ch == 0xFF)
			{
				int ch2 = stream.ReadByte();

				if (ch2 < 0)
					throw new FormatException();

				if (ch2 == 0xFF)
					break; /* message end */
				else
				{
					messageBuffer.Append((byte)ch);

					if (messageBuffer.Count >= Constants.MaxMessage)
						break;

					messageBuffer.Append((byte)ch2);
				}
			}
			else
				messageBuffer.Append((byte)ch);
		}

		song.Message = messageBuffer.ToArray().AsSpan().FromCP437(zeroTerminated: false);

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
				ptrs[i] = BitConverter.ToInt16(ptrBytes, i * 2);

			for (int c = 0; c < 9; c++)
			{
				/* ... sigh */
				int[] ordTranspose = new int[Constants.MaxOrders];
				bool transposeSet = false; /* stupid hack */
				int[] ords = new int[Constants.MaxOrders - 2]; /* need space for ORDER_LAST */
				ushort memInstrument = 0; /* current instrument for the channel */
				VolumeEffects memVolumeEffect = VolumeEffects.None; ;
				byte memVolumeParameter = 0;
				Effects memEffect = Effects.None;
				byte memParameter = 0;

				if (ptrs[c] != 0)
				{
					// I think this actually just adds onto the existing volume,
					// instead of averaging them together ???????
					song.Channels[c].Volume = EdLibVolumeToITVolume(volume[c]);
				}
				else
				{
					song.Channels[c].Flags |= ChannelFlags.Mute;
					continue; /* wut */
				}

				stream.Position = startPosition + ptrs[c];

				try
				{
					speeds[c + 1] = stream.ReadStructure<short>();
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

					ords[nOrds] = ord;

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

					int patternParaptr = stream.ReadStructure<ushort>();

					stream.Position = startPosition + patternParaptr;

					for (; pattern < Constants.MaxPatterns; FixRow(ref pattern, ref row))
					{
					D00_readnote: /* this goto is kind of ugly... */
						int @event;

						try
						{
							@event = stream.ReadStructure<ushort>();
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
							switch (note & 0x7F)
							{
								case 0: /* "REST" */
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

									/* reset fx */
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
							/* it's probably possible to have multiple effects
							 * on one track. we should be able to handle this! */

							byte fx = (byte)(@event >> 12);
							ushort fxop = (ushort)(@event & 0x0FFF);

							switch (fx)
							{
								case 6: /* Cut/Stop Voice */
									sn.Note = SpecialNotes.NoteCut;
									row += fxop + 1;
									continue;
								case 7: /* Vibrato */
									memEffect = Effects.Vibrato;

									/* these are flipped in the fxop */
									{
										/* this is a total guess, mostly just based
										* on what sounds "correct" */
										byte depth = (byte)(((fxop >> 8) & 0xFF) * 4 / 3);
										byte speed = (byte)((fxop & 0xFF) * 4 / 3);

										depth = Math.Min(depth, (byte)0xF);
										speed = Math.Min(speed, (byte)0xF);

										memParameter = (byte)((speed << 4) | depth);
									}
									break;
								case 9: /* New Level (in layman's terms, volume) */
									memVolumeEffect = VolumeEffects.Volume;
									/* volume is backwards, WTF */
									memVolumeParameter = EdLibVolumeToITVolume(fxop);
									break;
								case 0xB: /* Set spfx (need to handle this appropriately...) */
									if (hdr.Version == 4)
									{
										/* SPFX is a linked list.
										*
										* Yep; there's a `ptr` value within the structure, which
										* points to the next spfx structure to process. This is
										* terrible for us, but we can at least haphazardly
										* grab the instrument number from the first one, and
										* hope it fits... */
										long oldPos = stream.Position;

										stream.Position = startPosition + hdr.SpfxParaptr + fxop;

										memInstrument = stream.ReadStructure<ushort>();
										nInst = Math.Max(nInst, memInstrument);

										/* other values:
										*  - int8_t halfnote;
										*  - uint8_t modlev;
										*  - int8_t modlevadd;
										*  - uint8_t duration;
										*  - uint16_t ptr; (seriously?)
									  * it's likely that we can transform these into an instrument. */


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
									memParameter = (byte)(fxop * 5 / 2);
									break;
								case 0xE: /* Pitch slide down */
									memEffect = Effects.PortamentoDown;
									memParameter = (byte)(fxop * 5 / 2);
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
					song.Patterns[c] = Pattern.CreateEmpty();

			if (song.Patterns[maxPattern]!.Rows.Count != maxRow)
			{
				/* insert an effect to jump back to the start
				 * this effect may be on the 10th channel if we can't
				 * fit it anywhere else. */
				for (int c = 0; c < Constants.MaxChannels; c++)
				{
					ref var note = ref song.Patterns[maxPattern]!.Rows[maxRow][c + 1];

					if (note.Effect != Effects.None)
						continue;

					note.Effect = Effects.PositionJump;
					note.Parameter = 0;
				}
			}

			for (int c = 0; c <= maxPattern; c++)
				song.OrderList.Add(c);

			song.OrderList.Add(SpecialOrders.Last);
		}

		/* -------------------------------------------------------------------- */
		/* find the most common speed, and use it */

		{
			/* FIXME: this isn't very good, we should be doing per-channel
			 * speed stuff or else we get broken modules
			 *
			 * basically each channel ought to have an increment. if each speed
			 * is the same this is okay, and we can probably just ignore the
			 * "song speed" altogether. though i don't know how many songs
			 * actually use different speeds for each channel, and such
			 * songs are likely incredibly rare. */
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

			HzToSpeedTempo(hdr.Version, mode, out song.InitialSpeed, out song.InitialTempo);
		}

		/* start reading instrument data */

		stream.Position = startPosition + hdr.InstrumentParaptr;

		nInst = Math.Min(nInst, Constants.MaxSamples);

		/* enable instrument mode so we can read in levelpuls info */
#if D00_ENABLE_BROKEN_LEVELPULS
		if (hdr.Version == 1 || hdr.Version == 2)
			song.Flags |= SongFlags.InstrumentMode;
#endif

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

#if D00_ENABLE_BROKEN_LEVELPULS
			var inst = song.GetInstrument(c + 1);
			inst.FadeOut = 256 << 5;
#endif

			int tunelev = stream.ReadByte();
			if (tunelev < 0)
				break; /* truncated file?? */

			switch (hdr.Version)
			{
				case 1:
				case 2:
				{
					if (tunelev == 0xFF)
						break;

#if D00_ENABLE_BROKEN_LEVELPULS
					if (!LoadLevelpuls(hdr.SpfxParaptr, tunelev, inst, smp, stream))
						throw new FormatException(); /* WUT */
#else
					warn |= Warnings.LevelPuls;
#endif
					break;
				}
				case 4:
					warn |= Warnings.FineTune;
					break;
				default:
					/* just padding? */
					break;
			}

			/* It's probably safe to ignore these (?)
			 * I think Adplug also ignores these values if SPFX isn't used ... */
#if false
			Log.Append(1, "timer: {0}", stream.ReadByte());
			Log.Append(1, "sr: {0}", stream.ReadByte());
			Log.Append(1, "unknown bytes: {0}, {1}", stream.ReadByte(), stream.ReadByte());
			Log.AppendNewLine();
#else
			stream.Position += 1; /* "timer" */
			stream.Position += 1; /* "sr" */
			stream.Position += 2; /* unknown bytes (padding, probably) */
#endif
		}

		/* TODO for older D00 files we can use the levelpuls structure
		 * to create an instrument that plays stuff properly */

		for (int c = 9; c < Constants.MaxChannels; c++)
			song.Channels[c].Flags |= ChannelFlags.Mute;

		if (hdr.Version == 4)
			song.TrackerID = "EdLib Tracker";
		/* otherwise... it's probably some random tracker */

		foreach (var warning in Enum.GetValues<Warnings>())
			if (warn.HasAllFlags(warning))
				Log.Append(4, " Warning: {0}", warning.GetDescription());

		return song;
	}
}
