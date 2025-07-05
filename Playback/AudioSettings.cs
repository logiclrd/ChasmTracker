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

	public static EQBand[] EQBands = new EQBand[4];

	public static bool NoRamping;
}
