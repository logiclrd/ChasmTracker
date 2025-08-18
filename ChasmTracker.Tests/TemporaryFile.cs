using System;
using System.IO;
using System.Text;

namespace ChasmTracker.Tests;

public class TemporaryFile : IDisposable
{
	string? _filePath;

	public string FilePath => _filePath ?? throw new ObjectDisposedException(nameof(TemporaryFile));

	public TemporaryFile()
	{
		_filePath = Path.GetTempFileName();
	}

	public void Dispose()
	{
		if (_filePath != null)
		{
			try
			{
				File.Delete(_filePath);
			}
			catch { }

			_filePath = null;
		}
	}

	public TemporaryFile(string template) : this(Encoding.UTF8.GetBytes(template)) { }

	public TemporaryFile(byte[] template) : this()
	{
		File.WriteAllBytes(_filePath!, template);
	}
}
