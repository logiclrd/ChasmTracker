using System;

namespace ChasmTracker.Songs;

public class SongSample
{
	public int Length;
	public int LoopStart;
	public int LoopEnd;
	public int SustainStart;
	public int SustainEnd;
	public sbyte[]? Data8;
	public short[]? Data16;
	public int C5Speed = 8363;
	public int Panning;
	public int Volume = 64 * 4;
	public int GlobalVolume = 64;
	public SampleFlags Flags;
	public VibratoType VibratoType;
	public int VibratoRate;
	public int VibratoDepth;
	public int VibratoSpeed;
	public string Name = "";
	public string FileName = "";
	public int? DiskWriterBoundPattern;

	public bool IsPlayed; // for note playback dots
	public int SavedGlobalVolume; // for muting individual samples

	public byte[]? AdlibBytes;

	public Array? Data => Flags.HasFlag(SampleFlags._16Bit) ? Data16 : Data8;
}
