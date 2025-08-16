using System;

namespace ChasmTracker.Utility;

using ChasmTracker.SDLBackends;

public static class Timer
{
	static TimerBackend? s_backend;

	public static void Initialize()
	{
		s_backend = new SDLTimerBackend();
	}

	public static void Quit()
	{
		s_backend?.Quit();
		s_backend = null;
	}

	public static bool Oneshot(TimeSpan delay, Action callback)
	{
		return s_backend?.Oneshot(delay, callback) ?? false;
	}
}
