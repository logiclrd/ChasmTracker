namespace ChasmTracker;

public class Constants
{
	public const int NativeScreenWidth = 640;
	public const int NativeScreenHeight = 400;

	public const string WindowTitle = "Chasm Tracker";

	public const int MouseScrollLines = 3;

	public const int MaxSampleLength = 0x10000000; /* borrowed from OpenMPT; originally 16000000 */
	public const int MaxSampleRate = 192000;
	public const int MaxOrders = 256;
	public const int MaxPatterns = 240;
	public const int MaxSamples = 236;
	public const int MaxInstruments = MaxSamples;
	public const int MaxChannels = 64;
	public const int MaxEnvelopePoints = 80;
	public const int MaxEQBands = 6;
	public const int MaxMessage = 8000;
	public const int MaxInterpolationLookahead = 4;
	public const int MaxInterpolationLookaheadBufferSize = 16;
	public const int MaxSamplingPointSize = 4;

	public const int MaxMIDIChannels = 16;
	public const int MaxMIDIMacro = 32;

	public const int MaxVoices = 256;

	public const int DefaultPatternLength = 64;

	public const int MixingAttenuation = 5;
	public const int MixingClipMin = -0x04000000;
	public const int MixingClipMax = 0x03FFFFFF;
	public const int VolumeRampPrecision = 12;
	public const int FilterPrecision = (sizeof(int) * 8) - 8; /* faithfully stolen from openmpt */

	public const int MaxPathLength = 8192; // 8 KiB (should be more than enough)
}
