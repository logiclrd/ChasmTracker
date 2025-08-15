using System;

using SDL3;

namespace ChasmTracker.SDLBackends;

public class SDLHintScope : IDisposable
{
	string _name;
	bool _isDisposed;

	public SDLHintScope(string name, string value)
	{
		_name = name;

		SDL.SetHintWithPriority(name, value, SDL.HintPriority.Override);
	}

	public void Dispose()
	{
		if (!_isDisposed)
		{
			SDL.ResetHint(_name);
			_isDisposed = true;
		}
	}
}