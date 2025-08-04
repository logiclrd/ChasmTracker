using System;
using System.IO;
using ChasmTracker.Utility;
using ChasmTracker.Utility.MemoryMemoryStream;

namespace ChasmTracker.DiskOutput;

// ---------------------------------------------------------------------------
// memory backend
public class DiskWriterMemoryBackend : DiskWriterBackend
{
	byte[] _data;
	int _pos;
	int _length;

	public override int Length => _length;

	public override void Truncate(int newLength)
	{
		_length = Math.Min(_length, newLength);
	}

	public Memory<byte> Buffer => _data.AsMemory().Slice(0, _length);

	public override Stream AsStream(bool read)
	{
		var stream = MemoryStreamFactory.Create(Buffer, isReadOnly: read);

		if (!read)
			stream.Position = _pos;

		return stream;
	}

	public DiskWriterMemoryBackend(int initialSize = 1024)
	{
		_data = new byte[initialSize];
	}

	const double Phi = 1.61803398874989; // (1.0 + Math.Sqrt(5.0)) / 2.0;

	// 0 => memory error, abandon ship
	bool BufCheck(int extend)
	{
		if (Error)
			return false; /* punt */

		if (_pos + extend < _length)
			return true;

		_length = Math.Max(_length, _pos + extend);

		if (_length >= _data.Length)
		{
			int newSize = (int)(_data.Length * Phi);

			if (newSize < _length)
				newSize = _length;

			byte[] newData = new byte[newSize];

			_data.CopyTo(newData.AsMemory());
			_data = newData;
		}

		return true;
	}

	public override void Write(Span<byte> buf)
	{
		if (BufCheck(buf.Length))
		{
			buf.CopyTo(_data.Slice(_pos));
			_pos += buf.Length;
		}
	}

	public override void Seek(long offsetLong, SeekOrigin whence)
	{
		if ((offsetLong < int.MinValue) || (offsetLong > int.MaxValue))
			throw new ArgumentOutOfRangeException(nameof(offsetLong));

		int offset = (int)offsetLong;

		// mostly from slurp_seek
		switch (whence)
		{
			default:
			case SeekOrigin.Begin:
				break;
			case SeekOrigin.Current:
				offset += _pos;
				break;
			case SeekOrigin.End:
				offset += _length;
				break;
		}

		if (offset < 0)
			throw new ArgumentException(nameof(offset));

		/* note: seeking doesn't cause a buffer resize. This is consistent with the behavior of stdio streams.
		Consider:
			FILE *f = fopen("f", "w");
			fseek(f, 1000, SEEK_SET);
			fclose(f);
		This will produce a zero-byte file; the size is not extended until data is written. */
		_pos = offset;
	}

	public override long Tell()
	{
		return _pos;
	}

	public override void Close(DiskWriterBackupMode backupMode)
	{
	}
}
