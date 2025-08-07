namespace ChasmTracker.Configurations;

public class MIDIPortConfiguration : ConfigurationSection
{
	public string Name = "";
	public string Provider = "";
	public bool Input;
	public bool Output;
}
