namespace ChasmTracker.Configurations;

public class KeyboardConfiguration : ConfigurationSection
{
	// If these are set to zero, it means to use the
	// system key repeat or the default fallback values.

	public int KeyboardRepeatDelay;
	public int KeyboardRepeatRate;
}
