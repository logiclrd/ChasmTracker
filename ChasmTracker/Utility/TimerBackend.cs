using System;

namespace ChasmTracker.Utility;

public abstract class TimerBackend
{
	public abstract bool Oneshot(TimeSpan delay, Action callback);
	public abstract void Quit();
}
