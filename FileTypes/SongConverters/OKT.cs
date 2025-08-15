using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.SongConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class OKT : SongFileConverter
{
	public override string Label => "OKT";
	public override string Description => "Amiga Oktalyzer";
	public override string Extension => ".okt";

	public override int SortOrder => 8;

	/* --------------------------------------------------------------------- */

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		if (stream.Length < 16)
			return false;

		if (stream.ReadString(8) != "OKTASONG")
			return false;

		file.Description = "Amiga Oktalyzer";
		/* okts don't have names? */
		file.Title = "";
		file.Type = FileSystem.FileTypes.ModuleMOD;

		return true;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	class OKTBlockTypes
	{
		public const string CMOD = "CMOD";
		public const string SAMP = "SAMP";
		public const string SPEE = "SPEE";
		public const string SLEN = "SLEN";
		public const string PLEN = "PLEN";
		public const string PATT = "PATT";
		public const string PBOD = "PBOD";
		public const string SBOD = "SBOD";
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct OKTSample
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
		public string Name;
		public int Length;
		public short LoopStart;
		public short LoopLength;
		public short Volume;
		public short Mode;
	}

	[Flags]
	enum ReadFlags
	{
		HasCMOD = 1 << 0,
		HasSAMP = 1 << 1,
		HasSPEE = 1 << 2,
		HasPLEN = 1 << 3,
		HasPATT = 1 << 4,
	}

	/* return: number of channels */
	static int ReadCMOD(Stream stream, Song song)
	{
		int cn = 0;

		for (int t = 0; t < 4; t++)
		{
			if ((stream.ReadByte() | stream.ReadByte()) != 0)
				song.Channels[cn].Panning = Tables.ProTrackerPanning(t);

			song.Channels[cn].Panning = Tables.ProTrackerPanning(t);
		}

		for (int t = cn; t < 64; t++)
			song.Channels[t].Flags |= ChannelFlags.Mute;

		return cn;
	}


	static void ReadSAMPBlock(Stream stream, Song song, int len, SampleFormat[] smpFormat)
	{
		if ((len % 32) != 0)
			Log.Append(4, " Warning: Sample data is misaligned");

		len /= 32;

		if (len >= Constants.MaxSamples)
		{
			Log.Append(4, " Warning: Too many samples in file");
			len = Constants.MaxSamples - 1;
		}

		for (int n = 1; n <= len; n++)
		{
			var osmp = stream.ReadStructure<OKTSample>();

			osmp.Length = ByteSwap.Swap(osmp.Length);
			osmp.LoopStart = ByteSwap.Swap(osmp.LoopStart);
			osmp.LoopLength = ByteSwap.Swap(osmp.LoopLength);
			osmp.Volume = ByteSwap.Swap(osmp.Volume);
			osmp.Mode = ByteSwap.Swap(osmp.Mode);

			var ssmp = song.EnsureSample(n);

			ssmp.Name = osmp.Name;
			ssmp.Length = osmp.Length & ~1; // round down

			if (osmp.LoopLength > 2 && osmp.LoopLength + osmp.LoopStart < ssmp.Length)
			{
				ssmp.SustainStart = osmp.LoopStart;
				ssmp.SustainEnd = osmp.LoopStart + osmp.LoopLength;
				if (ssmp.SustainStart < ssmp.Length && ssmp.SustainEnd < ssmp.Length)
					ssmp.Flags |= SampleFlags.SustainLoop;
				else
					ssmp.SustainStart = 0;
			}
			ssmp.LoopStart *= 2;
			ssmp.LoopEnd *= 2;
			ssmp.SustainStart *= 2;
			ssmp.SustainEnd *= 2;
			ssmp.Volume = Math.Min(osmp.Volume, (byte)64) * 4; //mphack

			smpFormat[n] = (osmp.Mode == 0 || osmp.Mode == 2) ? SampleFormat._7 : SampleFormat._8;

			ssmp.C5Speed = 8287;
			ssmp.GlobalVolume = 64;
		}
	}


	/* Octalyzer effects list, straight from the internal help (acquired by running "strings octalyzer1.57") --
		- Effects Help Page --------------------------
		1 Portamento Down (4) (Period)
		2 Portamento Up   (4) (Period)
		A Arpeggio 1      (B) (down, orig,   up)
		B Arpeggio 2      (B) (orig,   up, orig, down)
		C Arpeggio 3      (B) (  up,   up, orig)
		D Slide Down      (B) (Notes)
		U Slide Up        (B) (Notes)
		L Slide Down Once (B) (Notes)
		H Slide Up   Once (B) (Notes)
		F Set Filter      (B) <>00:ON
		P Pos Jump        (B)
		S Speed           (B)
		V Volume          (B) <=40:DIRECT
		O Old Volume      (4)   4x:Vol Down      (VO)
					5x:Vol Up        (VO)
					6x:Vol Down Once (VO)
					7x:Vol Up   Once (VO)
	Note that 1xx/2xx are apparently inverted from Protracker.
	I'm not sure what "Old Volume" does -- continue a slide? reset to the sample's volume? */

	[Flags]
	enum OKTEffects
	{
		None = 0,

		PortamentoDown = 1 << 1,
		PortamentoUp = 1 << 2,
		Arpeggio1 = 1 << 10,
		Arpeggio2 = 1 << 11,
		Arpeggio3 = 1 << 12,
		NoteSlideDown = 1 << 13,
		SetFilter = 1 << 15,
		NoteSlideUpOnce = 1 << 17,
		NoteSlideDownOnce = 1 << 21,
		OldVolumeSlide = 1 << 24,
		PositionJump = 1 << 25,
		ReleaseSample = 1 << 27,
		Speed = 1 << 28,
		NoteSlideUp = 1 << 30,
		VolumeSlide = 1 << 31,
	}

	/* return: mask indicating effects that aren't implemented/recognized */
	static OKTEffects ReadPBODBlock(Stream stream, Song song, int pat, int nChn)
	{
		// bitset for effect warnings: (effwarn & (1 << (okteffect - 1)))
		// bit 1 is set if out of range values are encountered (Xxx, Yxx, Zxx, or garbage data)
		OKTEffects effectWarn = 0;

		var rows = stream.ReadStructure<short>();

		rows = ByteSwap.Swap(rows);
		rows = rows.Clamp(1, 200);

		var pattern = song.GetPattern(pat, create: true, rows)!;

		for (int row = 0; row < rows; row++)
		{
			for (int chn = 0; chn < nChn; chn++)
			{
				ref var note = ref pattern.Rows[row][chn + 1];

				note.Note = (byte)stream.ReadByte();
				note.Instrument = (byte)stream.ReadByte();
				int e = stream.ReadByte();
				note.Parameter = (byte)stream.ReadByte();

				if ((note.Note != 0) && (note.Note <= 36))
				{
					note.Note += 48;
					note.Instrument++;
				}
				else
				{
					note.Instrument = 0; // ?
				}

				/* blah -- check for read error */
				if (e < 0)
					return effectWarn;

				var effect = (OKTEffects)(1 << e);

				switch (effect)
				{
					case OKTEffects.None: // Nothing
						break;

					/* 1/2 apparently are backwards from .mod? */
					case OKTEffects.PortamentoDown: // 1 Portamento Down (Period)
						note.Effect = Effects.PortamentoDown;
						note.Parameter &= 0xf;
						break;
					case OKTEffects.PortamentoUp: // 2 Portamento Up (Period)
						note.Effect = Effects.PortamentoUp;
						note.Parameter &= 0xf;
						break;

#if false
					/* these aren't like Jxx: "down" means to *subtract* the offset from the note.
					For now I'm going to leave these unimplemented. */
					case OKTEffects.Arpeggio1: // A Arpeggio 1 (down, orig, up)
					case OKTEffects.Arpeggio2: // B Arpeggio 2 (orig, up, orig, down)
						if (note.Parameter)
							note.Effect = Effects.WeirdOKTArpeggio;
						break;
#endif

					/* This one is close enough to "standard" arpeggio -- I think! */
					case OKTEffects.Arpeggio3: // C Arpeggio 3 (up, up, orig)
						if (note.Parameter != 0)
							note.Effect = Effects.Arpeggio;
						break;

					case OKTEffects.NoteSlideDown: // D Slide Down (Notes)
						if (note.Parameter != 0)
						{
							note.Effect = Effects.NoteSlideDown;
							note.Parameter = (byte)(0x10 | Math.Min((byte)0xf, note.Parameter));
						}
						break;

					case OKTEffects.NoteSlideUp: // U Slide Up (Notes)
						if (note.Parameter != 0)
						{
							note.Effect = Effects.NoteSlideUp;
							note.Parameter = (byte)(0x10 | Math.Min((byte)0xf, note.Parameter));
						}
						break;

					case OKTEffects.NoteSlideDownOnce: // L Slide Down Once (Notes)
						/* We don't have fine note slide, but this is supposed to happen once
						per row. Sliding every 5 (non-note) ticks kind of works (at least at
						speed 6), but implementing fine slides would of course be better. */
						if (note.Parameter != 0)
						{
							note.Effect = Effects.NoteSlideDown;
							note.Parameter = (byte)(0x50 | Math.Min((byte)0xf, note.Parameter));
						}
						break;

					case OKTEffects.NoteSlideUpOnce: // H Slide Up Once (Notes)
						if (note.Parameter != 0)
						{
							note.Effect = Effects.NoteSlideUp;
							note.Parameter = (byte)(0x50 | Math.Min((byte)0xf, note.Parameter));
						}
						break;

					case OKTEffects.SetFilter: // F Set Filter <>00:ON
						/* Not implemented, but let's import it anyway... */
						note.Effect = Effects.Special;
						note.Parameter = (note.Parameter != 0) ? (byte)1 : (byte)0;
						break;

					case OKTEffects.PositionJump: // P Pos Jump
						note.Effect = Effects.PositionJump;
						break;

					case OKTEffects.ReleaseSample: // R Release sample (apparently not listed in the help!)
						note.Note = SpecialNotes.NoteOff;
						note.Instrument = note.EffectByte = note.Parameter = 0;
						break;

					case OKTEffects.Speed: // S Speed
						note.Effect = Effects.Speed; // or tempo?
						break;

					case OKTEffects.VolumeSlide: // V Volume
						note.Effect = Effects.VolumeSlide;

						switch (note.Parameter >> 4)
						{
							case 4:
								if (note.Parameter != 0x40)
								{
									note.Parameter &= 0xf; // D0x
									break;
								}
								// 0x40 is set volume -- fall through
								goto case 0;
							case 0:
							case 1:
							case 2:
							case 3:
								note.VolumeEffect = VolumeEffects.Volume;
								note.VolumeParameter = note.Parameter;
								note.Effect = Effects.None;
								note.Parameter = 0;
								break;
							case 5:
								note.Parameter = (byte)((note.Parameter & 0xf) << 4); // Dx0
								break;
							case 6:
								note.Parameter = (byte)(0xf0 | Math.Min(note.Parameter & 0xf, 0xe)); // DFx
								break;
							case 7:
								note.Parameter = (byte)((Math.Min(note.Parameter & 0xf, 0xe) << 4) | 0xf); // DxF
								break;
							default:
								// Junk.
								note.EffectByte = note.Parameter = 0;
								break;
						}
						break;

#if false
					case OKTEffects.OldVolumeSlide: // O Old Volume
						/* ? */
						note.Effect = Effects.VolumeSlide;
						note.Parameter = 0;
						break;
#endif

					default:
						//Log.Append(2, " Pattern {0}, row {1}: effect {2} {3:X2}",
						//        pat, row, e, note.Parameter);
						effectWarn |= (effect == 0) ? (OKTEffects)1 : effect;
						note.Effect = Effects.Unimplemented;
						break;
				}
			}
		}

		return effectWarn;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		ReadFlags readFlags = 0;
		int pLen = 0; // how many positions in the orderlist are valid
		int nPat = 0; // next pattern to read
		int nSmp = 1; // next sample (data, not header)
		int nChn = 0; // how many channels does this song use?

		long[] patSeek = new long[Constants.MaxPatterns];
		long[] smpSeek = new long[Constants.MaxSamples + 1];
		int[] smpSize = new int[Constants.MaxSamples + 2];
		SampleFormat[] smpFormat = new SampleFormat[Constants.MaxSamples + 1];

		OKTEffects effectWarn = 0; // effect warning mask

		string tag = stream.ReadString(8);

		if (tag != "OKTASONG")
			throw new NotSupportedException();

		var song = new Song();

		while (stream.Position < stream.Length)
		{
			tag = stream.ReadString(4);

			int blockLength = stream.ReadStructure<int>();

			blockLength = ByteSwap.Swap(blockLength);

			long nextPos = stream.Position + blockLength;

			switch (tag)
			{
				case OKTBlockTypes.CMOD:
					if (!readFlags.HasAllFlags(ReadFlags.HasCMOD))
					{
						readFlags |= ReadFlags.HasCMOD;
						nChn = ReadCMOD(stream, song);
					}
					break;
				case OKTBlockTypes.SAMP:
					if (!readFlags.HasAllFlags(ReadFlags.HasSAMP))
					{
						readFlags |= ReadFlags.HasSAMP;
						ReadSAMPBlock(stream, song, blockLength, smpFormat);
					}
					break;
				case OKTBlockTypes.SPEE:
					if (!readFlags.HasAllFlags(ReadFlags.HasSPEE))
					{
						readFlags |= ReadFlags.HasSPEE;

						var w = stream.ReadStructure<short>();

						w = ByteSwap.Swap(w);

						song.InitialSpeed = w.Clamp(1, 255);
						song.InitialTempo = 125;
					}
					break;
				case OKTBlockTypes.SLEN:
					// Don't care.
					break;
				case OKTBlockTypes.PLEN:
					if (!readFlags.HasAllFlags(ReadFlags.HasPLEN))
					{
						readFlags |= ReadFlags.HasPLEN;
						short w = stream.ReadStructure<short>();
						pLen = ByteSwap.Swap(w);
					}
					break;
				case OKTBlockTypes.PATT:
					if (!readFlags.HasAllFlags(ReadFlags.HasPATT))
					{
						readFlags |= ReadFlags.HasPATT;

						byte[] orderListBytes = new byte[Math.Min(blockLength, Constants.MaxOrders)];

						stream.ReadExactly(orderListBytes);

						for (int i = 0; i < orderListBytes.Length; i++)
							song.OrderList.Add(orderListBytes[i]);
					}
					break;
				case OKTBlockTypes.PBOD:
					/* Need the channel count (in CMOD) in order to read these */
					if (nPat < Constants.MaxPatterns)
					{
						if (blockLength > 0)
							patSeek[nPat] = stream.Position;
						nPat++;
					}
					break;
				case OKTBlockTypes.SBOD:
					if (nSmp < Constants.MaxSamples)
					{
						smpSeek[nSmp] = stream.Position;
						smpSize[nSmp] = blockLength;
						if (smpSize[nSmp] > 0)
							nSmp++;
					}
					break;

				default:
					//Log.Append(4, " Warning: Unknown block of type '{0}' at 0x{1:x}",
					//        tag, stream.Position - 8);
					break;
			}

			if (nextPos > stream.Length)
			{
				Log.Append(4, " Warning: Unexpected end of file");
				break;
			}

			stream.Position = nextPos;
		}

		if (!readFlags.HasAllFlags(ReadFlags.HasCMOD | ReadFlags.HasSPEE))
			throw new FormatException();

		if (!lflags.HasAllFlags(LoadFlags.NoPatterns))
		{
			for (int pat = 0; pat < nPat; pat++)
			{
				stream.Position = patSeek[pat];
				effectWarn |= ReadPBODBlock(stream, song, pat, nChn);
			}

			if (effectWarn != 0)
			{
				if (effectWarn.HasAllFlags((OKTEffects)1))
					Log.Append(4, " Warning: Out-of-range effects (junk data?)");

				for (int e = 2; e <= 32; e++)
				{
					var unimplementedEffect = (OKTEffects)(1 << (e - 1));

					if (effectWarn.HasAllFlags(unimplementedEffect))
					{
						Log.Append(4, " Warning: Unimplemented effect {0}xx",
							(char)(e + (e < 10 ? '0' : ('A' - 10))));
					}
				}
			}
		}

		if (!lflags.HasAllFlags(LoadFlags.NoSamples))
		{
			int sh;
			int sd = 1;

			for (sh = 1; sh < Constants.MaxSampleLength && (smpSize[sd] != 0); sh++)
			{
				var ssmp = song.EnsureSample(sh);

				if (ssmp.Length == 0)
					continue;

				if (ssmp.Length != smpSize[sd])
				{
					Log.Append(4, " Warning: Sample %d: header/data size mismatch ({0}/{1})", sh,
						ssmp.Length, smpSize[sd]);
					ssmp.Length = Math.Min(smpSize[sd], ssmp.Length);
				}

				stream.Position = smpSeek[sd];

				SampleFileConverter.ReadSample(ssmp, SampleFormat.BigEndian | SampleFormat.Mono | SampleFormat.PCMSigned | smpFormat[sh], stream);

				sd++;
			}

			// Make sure there's nothing weird going on
			for (; sh < Constants.MaxSamples; sh++)
			{
				var sample = song.GetSample(sh);

				if ((sample != null) && (sample.Length != 0))
				{
					Log.Append(4, " Warning: Sample {0}: file truncated", sh);
					sample.Length = 0;
				}
			}
		}

		song.PanSeparation = 64;
		song.OrderList.Add(SpecialOrders.Last);
		song.TrackerID = Description;

		return song;
	}
}