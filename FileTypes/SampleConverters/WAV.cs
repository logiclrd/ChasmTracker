using System.IO;

namespace ChasmTracker.FileTypes.SampleConverters;

using System;
using ChasmTracker.FileSystem;
using ChasmTracker.Songs;

public class WAV : SampleFileConverter
{
	public override string Label => "WAV";
	public override string Description => "WAV";
	public override string Extension => ".wav";

	public override int SortOrder => 5;

	public override bool CanSave => true;

	public override SongSample LoadSample(Stream stream)
	{
		return WAVFile.Load(stream) ?? throw new Exception("Failed to load sample");
	}

	public override SaveResult SaveSample(SongSample sample, Stream stream)
	{
		return WAVFile.Save(stream, sample);
	}

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			var smp = LoadSample(stream);

			file.SampleFlags = smp.Flags;
			file.SampleSpeed = smp.C5Speed;
			file.SampleLength = smp.Length;
			file.SampleLoopStart = smp.LoopStart;
			file.SampleLoopEnd = smp.LoopEnd;
			file.SampleSustainStart = smp.SustainStart;
			file.SampleSustainEnd = smp.SustainEnd;

			file.Description = "IBM/Microsoft RIFF Audio";
			file.Type = FileTypes.SamplePlain;
			file.SampleFileName = file.BaseName;

			return true;
		}
		catch
		{
			return false;
		}
	}
}
