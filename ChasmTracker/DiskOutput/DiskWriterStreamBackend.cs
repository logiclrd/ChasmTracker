using System;
using System.IO;

namespace ChasmTracker.DiskOutput;

// ---------------------------------------------------------------------------
// stream backend
public class DiskWriterStreamBackend : DiskWriterBackend
{
	string _fileName;
	string _tempName;
	Stream _stream;
	bool _isDisposed;

	public override int Length => (int)_stream.Length;

	public override void Truncate(int newLength)
	{
		if (newLength < _stream.Length)
			_stream.SetLength(newLength);
	}

	public override Stream AsStream(bool read)
	{
		if (read)
			return File.OpenRead(_tempName);
		else
			return _stream;
	}

	public static DiskWriterStreamBackend OpenWrite(string fileName)
	{
		string tempName = Path.GetTempFileName();

		var stream = File.OpenWrite(tempName);

		return new DiskWriterStreamBackend(fileName, tempName, stream);
	}

	DiskWriterStreamBackend(string fileName, string tempName, Stream stream)
	{
		_fileName = fileName;
		_tempName = tempName;
		_stream = stream;
	}

	public DiskWriterStreamBackend(Stream stream)
	{
		_fileName = "unknown";
		_tempName = "";
		_stream = stream;
	}

	public override void Write(Span<byte> buf)
	{
		if (_isDisposed)
			throw new ObjectDisposedException(nameof(DiskWriterStreamBackend));

		_stream.Write(buf);
	}

	public override void Seek(long position, SeekOrigin whence)
	{
		if (_isDisposed)
			throw new ObjectDisposedException(nameof(DiskWriterStreamBackend));

		_stream.Seek(position, whence);
	}

	public override long Tell()
	{
		if (_isDisposed)
			throw new ObjectDisposedException(nameof(DiskWriterStreamBackend));

		return _stream.Position;
	}

	public override void Close(DiskWriterBackupMode backupMode)
	{
		if (_isDisposed)
			throw new ObjectDisposedException(nameof(DiskWriterStreamBackend));

		if (!_isDisposed)
		{
			_stream.Close();
			_isDisposed = true;
		}

		if (!string.IsNullOrEmpty(_tempName))
		{
			if (backupMode != DiskWriterBackupMode.NoBackup)
				DiskWriter.MakeBackup(_fileName, backupMode);

			File.Move(_tempName, _fileName, overwrite: true);
			_tempName = "";
		}
	}
}
