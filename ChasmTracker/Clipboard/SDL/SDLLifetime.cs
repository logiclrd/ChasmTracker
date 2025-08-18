using ChasmTracker;

using System.Threading;

using SDL3;

public static class SDLLifetime
{
	static int s_roll;

	public static bool Initialize()
	{
		if (s_roll == 0)
		{
			var ver = SDL.GetVersion();

			if (!SDLVersionAtLeast(ver, 3, 0, 0))
				return false;

			if (!SDL.Init(default))
			{
				OS.ShowMessageBox("SDL3 failed to initialize!", SDL.GetError(), OSMessageBoxTypes.Error);
				return false;
			}
		}

		Interlocked.Increment(ref s_roll);
		return true;
	}

	public static void Quit()
	{
		if (s_roll > 0)
		{
			if (Interlocked.Decrement(ref s_roll) == 0)
				SDL.Quit();
		}
	}

	static bool SDLVersionAtLeast(int sdlVersion, int major, int minor, int micro)
	{
		int sdlMajor = SDL.VersionNumMajor(sdlVersion);

		if (sdlMajor > major)
			return true;
		if (sdlMajor < major)
			return false;

		int sdlMinor = SDL.VersionNumMinor(sdlVersion);

		if (sdlMinor > minor)
			return true;
		if (sdlMinor < minor)
			return false;

		int sdlMicro = SDL.VersionNumMicro(sdlVersion);

		return (sdlMicro >= micro);
	}
}
