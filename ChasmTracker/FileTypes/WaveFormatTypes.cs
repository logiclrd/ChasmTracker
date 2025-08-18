namespace ChasmTracker.FileTypes;

public enum WaveFormatTypes : short
{
	PCM = 1,
	IEEEFloatingPoint = 3, // IEEE float
	ALaw = 6, // 8-bit ITU-T G.711 A-law
	µLaw = 7, // 8-bit ITU-T G.711 µ-law
	Extensible = unchecked((short)0xFFFE),
}
