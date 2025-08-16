using System;

using SDL3;

namespace ChasmTracker.SDLBackends;

using ChasmTracker.Utility;

public class SDLTimerBackend : TimerBackend
{
	public SDLTimerBackend()
	{
		SDLLifetime.Initialize();
	}

	public override void Quit()
	{
		SDLLifetime.Quit();
	}

	public override bool Oneshot(TimeSpan delay, Action callback)
	{
		if (delay <= TimeSpan.Zero)
		{
			callback();
			return true;
		}

		uint timerID = SDL.AddTimer(
			(uint)delay.TotalMilliseconds,
			(userData, timerID, interval) =>
			{
				callback();
				return 0u;
			},
			IntPtr.Zero);

		return (timerID != 0);
	}
}