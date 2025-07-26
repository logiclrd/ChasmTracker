using System;
using System.IO;
using ChasmTracker.FileSystem;
using ChasmTracker.Songs;

namespace ChasmTracker.FileTypes.SampleConverters;

public class RAW : SampleFileConverter
{
	public override string Label => "RAW";
	public override string Description => "Raw";
	public override string Extension => ".raw";

	public override int SortOrder => 6;

	public override bool CanSave => true;

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		return false;
	}

	/* --------------------------------------------------------------------- */

	// Impulse Tracker handles raw sample data as unsigned, EXCEPT when saving a 16-bit sample as raw.

	public override SongSample LoadSample(Stream stream)
	{
		long len = stream.Length;

		var smp = new SongSample();

		smp.C5Speed = 8363;
		smp.Volume = 64 * 4;
		smp.GlobalVolume = 64;
		smp.Length = (int)Math.Min(len, 1 << 22); /* max of 4MB */

		ReadSample(smp, SampleFormat.LittleEndian | SampleFormat._8 | SampleFormat.PCMUnsigned | SampleFormat.Mono, stream);

		return smp;
	}

	public override SaveResult SaveSample(SongSample sample, Stream stream)
	{
		WriteSample(stream, sample, SampleFormat.LittleEndian
			| (sample.Flags.HasFlag(SampleFlags._16Bit) ? SampleFormat._16 | SampleFormat.PCMSigned : SampleFormat._8 | SampleFormat.PCMUnsigned)
			| (sample.Flags.HasFlag(SampleFlags.Stereo) ? SampleFormat.StereoInterleaved : SampleFormat.Mono),
			uint.MaxValue);

		return SaveResult.Success;
	}
}
