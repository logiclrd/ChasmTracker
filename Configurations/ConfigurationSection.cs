namespace ChasmTracker.Configurations;

public abstract class ConfigurationSection
{
	public virtual void Parse() { }
	public virtual void PrepareToSave() { }
}
