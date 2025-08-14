using System;

namespace ChasmTracker.Utility;

public class EnvironmentVariableScope : IDisposable
{
	string _name;
	string? _oldValue;
	bool _isDisposed;

	public EnvironmentVariableScope(string name, string? value)
	{
		_name = name;
		_oldValue = Environment.GetEnvironmentVariable(name);

		Environment.SetEnvironmentVariable(name, value);
	}

	public void Dispose()
	{
		if (!_isDisposed)
		{
			Environment.SetEnvironmentVariable(_name, _oldValue);
			_isDisposed = true;
		}
	}
}