using System.Collections.Generic;

namespace ChasmTracker.Configurations;

public interface IGatherConfigurationSections<T> : IGatherConfigurationSections
	where T : ConfigurationSection
{
	void GatherConfiguration(IList<T> list);
}

public interface IGatherConfigurationSections
{
}