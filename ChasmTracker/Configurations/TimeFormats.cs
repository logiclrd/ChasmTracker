namespace ChasmTracker.Configurations;

public enum TimeFormats
{
	[ConfigurationValue("12hr")]
	_12hr, // 11:27 PM (North America, Australia, default),
	[ConfigurationValue("24hr")]
	_24hr, // 23:27    (everyone else)

	// special constant.
	[ConfigurationDefault]
	Default = -1,
}