using ChasmTracker.Configurations;

namespace ChasmTracker;

public enum NumLockHandling
{
	[ConfigurationValue("off")]
	AlwaysOff = 0,
	[ConfigurationValue("on")]
	AlwaysOn = 1,
	[ConfigurationValue(everythingElse: true)]
	Honour = -1, /* don't fix it */
	[ConfigurationValue(notSet: true)]
	Guess = -2, /* don't fix it... except on non-ibook macs */
}
