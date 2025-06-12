namespace ChasmTracker;

public enum NumLockHandling
{
	AlwaysOff = 0,
	AlwaysOn = 1,
	Honour = -1, /* don't fix it */
	Guess = -2, /* don't fix it... except on non-ibook macs */
}
