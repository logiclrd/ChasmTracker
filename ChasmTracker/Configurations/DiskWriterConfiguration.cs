namespace ChasmTracker.Configurations;

public class DiskWriterConfiguration : ConfigurationSection
{
	[ConfigurationKey("rate")]
	public int Rate = 44100;
	public int Bits = 16;
	public int Channels = 2;
}
