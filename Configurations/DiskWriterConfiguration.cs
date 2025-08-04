namespace ChasmTracker.Configurations;

public class DiskWriterConfiguration : ConfigurationSection
{
	public int Rate = 44100;
	public int Bits = 16;
	public int Channels = 2;
}
