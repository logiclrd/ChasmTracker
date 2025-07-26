using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.FileTypes.SampleConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class AU : SampleFileConverter
{
	public override string Label => "AU";
	public override string Description => "Sun/NeXT";
	public override string Extension => ".au";

	public override int SortOrder => 4;

	enum AUEncoding
	{
		µLaw = 1, /* µ-law */
		PCM8 = 2, /* 8-bit linear PCM (RS_PCM8U in ModPlug) */
		PCM16 = 3, /* 16-bit linear PCM (RS-PCM16M) */
		PCM24 = 4, /* 24-bit linear PCM */
		PCM32 = 5, /* 32-bit linear PCM */
		IEEE32 = 6, /* 32-bit IEEE floating point */
		IEEE64 = 7, /* 64-bit IEEE floating point */
		ISDNµLawADPCM = 23, /* 8-bit ISDN µ-Law (CCITT G.721 ADPCM compressed) */
	}

	struct AUHeader
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
		public string Magic; /* ".snd" */
		public int DataOffset;
		public int DataSize;
		public AUEncoding Encoding;
		public int SampleRate;
		public int Channels;
	}

	bool ReadHeader(Stream fp, out AUHeader hdr)
	{
		long memSize = fp.Length - fp.Position;

		var reader = new BinaryReader(fp, Encoding.ASCII, leaveOpen: true);

		hdr = new AUHeader();

		hdr.Magic = reader.ReadPlainString(4);

		if (hdr.Magic != ".snd")
			return false;

		hdr.DataOffset = ByteSwap.Swap(reader.ReadInt32());
		hdr.DataSize = ByteSwap.Swap(reader.ReadInt32());
		hdr.Encoding = (AUEncoding)ByteSwap.Swap(reader.ReadInt32());
		hdr.SampleRate = ByteSwap.Swap(reader.ReadInt32());
		hdr.Channels = ByteSwap.Swap(reader.ReadInt32());

		if (hdr.DataOffset < 24
			|| hdr.DataOffset > memSize
			|| hdr.DataSize > memSize - hdr.DataOffset)
			return false;

		switch (hdr.Channels)
		{
			case 1:
			case 2:
				break;
			default:
				return false;
		}

		return true;
	}

	/* --------------------------------------------------------------------- */
	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		if (!ReadHeader(stream, out var au))
			return false;

		/* calculate length and flags */
		file.SampleLength = au.DataSize / au.Channels;
		file.SampleFlags = default;
		switch (au.Encoding)
		{
			case AUEncoding.PCM16:
				file.SampleFlags |= SampleFlags._16Bit;
				file.SampleLength /= 2;
				break;
			case AUEncoding.PCM24:
				file.SampleFlags |= SampleFlags._16Bit;
				file.SampleLength /= 3;
				break;
			case AUEncoding.PCM32:
			case AUEncoding.IEEE32:
				file.SampleFlags |= SampleFlags._16Bit;
				file.SampleLength /= 4;
				break;
			case AUEncoding.IEEE64:
				file.SampleFlags |= SampleFlags._16Bit;
				file.SampleLength /= 8;
				break;
			default:
				return false;
		}

		if (au.Channels == 2)
			file.SampleFlags |= SampleFlags.Stereo;

		file.SampleSpeed = au.SampleRate;
		file.SampleDefaultVolume = 64;
		file.SampleGlobalVolume = 64;
		file.Description = "AU Sample";
		file.SampleFileName = file.BaseName;
		file.Type = FileTypes.SamplePlain;

		/* now we can grab the title */
		if (au.DataOffset > 24)
		{
			int extLen = au.DataOffset - 24;

			stream.Position = 24;

			var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

			file.Title = reader.ReadPlainString(extLen);

			int terminator = file.Title.IndexOf('\0');

			if (terminator >= 0)
				file.Title = file.Title.Substring(0, terminator);
		}

		return true;
	}

	/* --------------------------------------------------------------------- */

	public override SongSample LoadSample(Stream stream)
	{
		if (!ReadHeader(stream, out var au))
			throw new NotSupportedException();

		SampleFormat sflags = SampleFormat.BigEndian;

		var smp = new SongSample();

		smp.C5Speed = au.SampleRate;
		smp.Volume = 64 * 4;
		smp.GlobalVolume = 64;
		smp.Length = au.DataSize;

		switch (au.Encoding)
		{
			case AUEncoding.PCM8:
				sflags |= SampleFormat._8 | SampleFormat.PCMSigned;
				break;
			case AUEncoding.PCM16:
				sflags |= SampleFormat._16 | SampleFormat.PCMSigned;
				smp.Length /= 2;
				break;
			case AUEncoding.PCM24:
				sflags |= SampleFormat._24 | SampleFormat.PCMSigned;
				smp.Length /= 3;
				break;
			case AUEncoding.PCM32:
				sflags |= SampleFormat._32 | SampleFormat.PCMSigned;
				smp.Length /= 4;
				break;
			case AUEncoding.IEEE32:
				sflags |= SampleFormat._32 | SampleFormat.IEEEFloatingPoint;
				smp.Length /= 4;
				break;
			case AUEncoding.IEEE64:
				sflags |= SampleFormat._64 | SampleFormat.IEEEFloatingPoint;
				smp.Length /= 8;
				break;
			default:
				throw new NotSupportedException();
		}

		switch (au.Channels)
		{
			case 1:
				sflags |= SampleFormat.Mono;
				break;
			case 2:
				sflags |= SampleFormat.StereoInterleaved;
				smp.Length /= 2;
				break;
			default:
				throw new NotSupportedException();
		}

		if (au.DataOffset > Marshal.SizeOf(au))
		{
			int extLen = au.DataOffset - Marshal.SizeOf(au);

			var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

			smp.Name = reader.ReadPlainString(extLen);

			int terminator = smp.Name.IndexOf('\0');

			if (terminator >= 0)
				smp.Name = smp.Name.Substring(0, terminator);
		}

		if (ReadSample(smp, sflags, stream) == 0)
			throw new NotSupportedException();

		return smp;
	}

	public override SaveResult SaveSample(SongSample smp, Stream stream)
	{
		if (smp.Flags.HasFlag(SampleFlags.AdLib))
			return SaveResult.Unsupported;

		var au = new AUHeader();

		au.Magic = ".snd";

		au.DataOffset = ByteSwap.Swap(49); // header is 24 bytes, sample name is 25

		int ln = smp.Length;

		if (smp.Flags.HasFlag(SampleFlags._16Bit))
		{
			ln *= 2;
			au.Encoding = ByteSwap.Swap(AUEncoding.PCM16);
		}
		else
			au.Encoding = ByteSwap.Swap(AUEncoding.PCM8);

		au.SampleRate = ByteSwap.Swap(smp.C5Speed);

		if (smp.Flags.HasFlag(SampleFlags.Stereo))
		{
			ln *= 2;
			au.Channels = ByteSwap.Swap(2);
		}
		else
			au.Channels = ByteSwap.Swap(1);

		au.DataSize = ByteSwap.Swap(ln);

		stream.WriteStructure(au);

		byte[] sampleName = Encoding.ASCII.GetBytes(smp.Name);

		if (sampleName.Length < 25)
		{
			stream.Write(sampleName);
			for (int i = sampleName.Length; i < 25; i++)
				stream.WriteByte(0);
		}
		else
			stream.Write(sampleName, 0, 25);

		WriteSample(stream, smp, SampleFormat.BigEndian | SampleFormat.PCMSigned
				| (smp.Flags.HasFlag(SampleFlags._16Bit) ? SampleFormat._16 : SampleFormat._8)
				| (smp.Flags.HasFlag(SampleFlags.Stereo) ? SampleFormat.StereoInterleaved : SampleFormat.Mono),
				uint.MaxValue);

		return SaveResult.Success;
	}
}
