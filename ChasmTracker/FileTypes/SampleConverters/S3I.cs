using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileTypes.SampleConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class S3I : SampleFileConverter
{
	public override string Label => "S3I";
	public override string Description => "Scream Tracker";
	public override string Extension => ".s3i";

	public override int SortOrder => 2;

	/* TODO: S3M.cs should use this */
	static bool LoadSample(Stream fp, SongSample smp, bool withData)
	{
		/*
		if (fp.Length - fp.Position < 0x50)
			return 0;
		*/
		try
		{
			long startPosition = fp.Position;

			fp.Position += 0x4C;

			string magic = fp.ReadString(4);

			if ((magic != "SCRS") && (magic != "SCRI"))
				return false;

			fp.Position = startPosition;

			var type = (S3IType)fp.ReadByte();

			if ((type != S3IType.PCM) && (type != S3IType.AdMel))
				return false;

			smp.FileName = fp.ReadString(12);

			fp.Position = startPosition + 20;

			smp.LoopStart = fp.ReadStructure<ushort>();
			smp.LoopEnd = fp.ReadStructure<ushort>();

			smp.Volume = fp.ReadByte() * 4; /* mphack */

			fp.Position += 2;

			var flags = (S3IFormatFlags)fp.ReadByte();

			smp.C5Speed = fp.ReadStructure<ushort>();

			fp.Position += 12;

			smp.Name = fp.ReadString(28);

			smp.Flags = 0;

			if (type == S3IType.PCM)
			{
				int bytesPerSample = flags.HasAllFlags(S3IFormatFlags.Stereo) ? 2 : 1;

				fp.Position = startPosition + 15;

				smp.Length = fp.ReadStructure<ushort>();

				if (fp.Length < startPosition + 0x50 + smp.Length * bytesPerSample)
					return false;

				/* convert flags */
				if (flags.HasAllFlags(S3IFormatFlags.Loop))
					smp.Flags |= SampleFlags.Loop;

				if (flags.HasAllFlags(S3IFormatFlags.Stereo))
					smp.Flags |= SampleFlags.Stereo;

				if (flags.HasAllFlags(S3IFormatFlags._16Bit))
					smp.Flags |= SampleFlags._16Bit;

				if (withData)
				{
					SampleFormat format = SampleFormat.Mono | SampleFormat.LittleEndian; // endianness; channels
					format |= smp.Flags.HasAnyFlag(SampleFlags._16Bit)
						? (SampleFormat._16 | SampleFormat.PCMSigned)
						: (SampleFormat._8 | SampleFormat.PCMUnsigned); // bits; encoding

					fp.Position = startPosition + 80;
					ReadSample(smp, format, fp);
				}
			}
			else if (type == S3IType.AdMel)
			{
				smp.Flags |= SampleFlags.AdLib;
				smp.Flags &= ~(SampleFlags.Loop | SampleFlags._16Bit);

				fp.Position = startPosition + 16;

				byte[] adLibBytes = new byte[12];

				fp.ReadExactly(adLibBytes);

				smp.AdLibBytes = adLibBytes;

				// dumb hackaround that ought to some day be fixed:
				smp.Length = 1;
				smp.AllocateData();
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			var smp = LoadSample(stream);

			file.FillFromSample(smp);

			file.Description = "Scream Tracker Sample";
			file.Title = smp.Name;
			file.Type = FileSystem.FileTypes.SampleExtended | FileSystem.FileTypes.InstrumentOther;

			return true;
		}
		catch
		{
			return false;
		}
	}

	public override SongSample LoadSample(Stream stream)
	{
		var smp = new SongSample();

		// what the crap?
		bool success = LoadSample(stream, smp, withData: true);

		if (!success)
			throw new FormatException();

		return smp;
	}

	/* ---------------------------------------------------- */

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct S3IHeaderPCM
	{
		static byte[] s_junk = new byte[12];

		public S3IHeaderPCM()
		{
		}

		public S3IType Type = S3IType.PCM;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
		public string FileName = "";

		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
		public byte[] MemSeg = new byte[3];
		public int Length;
		public int LoopStart;
		public int LoopEnd;

		public byte Volume;
		public byte X;
		public byte Pack; // 0
		public S3IFormatFlags Flags; // 1=loop 2=stereo 4=16-bit
		public int C5Speed;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
		public byte[] Junk = s_junk;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 28)]
		public string Name = "";
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
		public string Tag = ""; // SCRS/SCRI/whatever
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
	struct S3IHeaderAdMel
	{
		static byte[] s_junk = new byte[12];

		public S3IHeaderAdMel() { }

		public S3IType Type = S3IType.AdMel;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
		public string FileName = "";
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
		public byte[] Zero = new byte[3];
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
		public byte[] AdLibBytes = Array.Empty<byte>();

		public byte Volume;
		public byte Dsk;
		public byte Pack; // 0
		public byte Padding; // flags for PCM, zero for adlib
		public int C5Speed;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
		public byte[] Junk = s_junk;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 28)]
		public string Name = "";
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
		public string Tag = ""; // SCRS/SCRI/whatever
	}

	public void WriteHeader(SongSample smp, uint sdata, Stream fp)
	{
		if (smp.Flags.HasAllFlags(SampleFlags.AdLib))
		{
			var hdr = new S3IHeaderAdMel();

			hdr.AdLibBytes = smp.AdLibBytes ?? throw new Exception("Sample does not have AdLib bytes");
			hdr.Tag = "SCRI";

			hdr.FileName = smp.FileName;
			hdr.Volume = (byte)(smp.Volume / 4); //mphack
			hdr.C5Speed = smp.C5Speed;
			hdr.Name = smp.Name.Replace('\0', ' ').TrimEnd().TrimToLength(25);

			fp.WriteStructure(hdr);
		}
		else if (smp.HasData)
		{
			var hdr = new S3IHeaderPCM();

			hdr.MemSeg[0] = (byte)((sdata >> 20) & 0xff); // wat
			hdr.MemSeg[1] = (byte)((sdata >> 4) & 0xff);  //
			hdr.MemSeg[2] = (byte)((sdata >> 12) & 0xff); //
			hdr.Length = smp.Length;
			hdr.LoopStart = smp.LoopStart;
			hdr.LoopEnd = smp.LoopEnd;
			hdr.Flags = (smp.Flags.HasAllFlags(SampleFlags.Loop) ? S3IFormatFlags.Loop : 0)
				| (smp.Flags.HasAllFlags(SampleFlags.Stereo) ? S3IFormatFlags.Stereo : 0)
				| (smp.Flags.HasAllFlags(SampleFlags._16Bit) ? S3IFormatFlags._16Bit : 0);
			hdr.Tag = "SCRS";

			hdr.FileName = smp.FileName;
			hdr.Volume = (byte)(smp.Volume / 4); //mphack
			hdr.C5Speed = smp.C5Speed;
			hdr.Name = smp.Name.Replace('\0', ' ').TrimEnd().TrimToLength(25);

			fp.WriteStructure(hdr);
		}
		else
		{
			var empty = new S3IHeaderPCM();

			empty.Type = S3IType.None;

			fp.WriteStructure(empty);
		}
	}

	public override SaveResult SaveSample(SongSample sample, Stream stream)
	{
		WriteHeader(sample, 0, stream);

		if (sample.Flags.HasAllFlags(SampleFlags.AdLib))
			return SaveResult.Success; // already done
		else if (sample.HasData)
		{
			SampleFormat format = SampleFormat.Mono | SampleFormat.LittleEndian; // endianness; channels

			format |= sample.Flags.HasAllFlags(SampleFlags._16Bit)
				? (SampleFormat._16 | SampleFormat.PCMSigned)
				: (SampleFormat._8 | SampleFormat.PCMUnsigned); // bits; encoding

			WriteSample(stream, sample, format, uint.MaxValue);
		}

		return SaveResult.Success;
	}
}
