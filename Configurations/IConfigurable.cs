namespace ChasmTracker.Configurations;

public interface IConfigurable
{
}

public interface IConfigurable<T> : IConfigurable
	where T : ConfigurationSection
{
	void SaveConfiguration(T config);
	void LoadConfiguration(T config);
}