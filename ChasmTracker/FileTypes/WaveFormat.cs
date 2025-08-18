namespace ChasmTracker.FileTypes;

public class WaveFormat
{
	public WaveFormatTypes Format; // PCM (1)
	public short Channels;         // 1:mono, 2:stereo
	public int FreqHz;             // sampling freq
	public int BytesSec;           // bytes/sec=freqHz*samplesize
	public short SampleSize;       // sizeof(sample)
	public short BitsPerSample;    // bits per sample (8/16)
}
