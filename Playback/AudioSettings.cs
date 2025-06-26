namespace ChasmTracker.Playback;

public class AudioSettings
{
	public static int SampleRate;
	public static int Bits;
	public static int Channels;
	public static int BufferSize;
	public static int ChannelLimit;
	public static SourceMode InterpolationMode;

	public class Master
	{
		public static int Left;
		public static int Right;
	}

	public static bool SurroundEffect;

	public static readonly int[] EQFreq = new int[4];
	public static readonly int[] EQGain = new int[4];

	public static bool NoRamping;
}
