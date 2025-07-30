using System;
using System.IO;
using ChasmTracker.Utility;

namespace ChasmTracker.FileTypes;

/* --------------------------------------------------------------------- */
/* implementation specialized for sf2 stuff
 * it allows reading from two different places in a file as if they
 * were sequential, since sf2 allows for stereo samples to not be
 * in the split stereo format schism likes to have. */
public class SF2SampleStream : Stream
{
	Stream _src;
	Data[] _data = new Data[2];
	int _current;
	/* original position from before we mutilated it */
	long _origPos;
	bool _disposed;

	public SF2SampleStream(Stream @in, long off1, long len1, long off2, long len2)
	{
		_src = @in;
		_data[0].Offset = off1;
		_data[0].Length = len1;
		_data[1].Offset = off2;
		_data[1].Length = len2;

		_origPos = @in.Length;
	}

	class Data
	{
		public long Offset;
		public long Length;
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		int read = 0;

		if (_current < _data.Length - 1)
		{
			int left = (int)(_data[_current].Offset + _data[_current].Length - _src.Position);

			if (left < 0)
				return 0; /* ??? */

			if (left < count)
			{
				int tread = _src.Read(buffer.Slice(read, left));
				if (tread != left)
					return tread;

				read += tread;
				count -= tread;

				/* start over at the new offset */
				_src.Position = _data[++_current].Offset;
			}
		}

		if (count > 0)
			read += _src.Read(buffer.Slice((int)read, count));

		return read;
	}

	public override void Write(byte[] buffer, int offset, int count)
		=> throw new NotSupportedException();

	public override bool CanRead => true;
	public override bool CanSeek => true;
	public override bool CanWrite => false;

	public override void Flush()
	{
	}

	public override long Length => _data[0].Length + _data[1].Length;

	public override long Position
	{
		get
		{
			long len = 0;

			for (int i = 0; i < _current; i++)
				len += _data[i].Length;

			return len + _src.Position - _data[_current].Offset;
		}
		set => Seek(value, SeekOrigin.Begin);
	}

	public override long Seek(long off, SeekOrigin whence)
	{
		long len = Length;

		switch (whence)
		{
			default:
			case SeekOrigin.Begin:
				break;
			case SeekOrigin.Current:
				off += Position;
				break;
			case SeekOrigin.End:
				off += len;
				break;
		}

		if ((off < 0) || (off > len))
			throw new IOException($"Offset out of range ({off} not in [0,{len})).");

		len = 0;

		for (int i = 0; i < _data.Length; i++)
		{
			if (off >= len && off < len + _data.Length)
			{
				_current = i;

				return _src.Seek(_data[i].Offset + off - len, SeekOrigin.Begin) - _data[_current].Offset + len;
			}

			len += _data[i].Length;
		}

		/* ? - should be impossible */
		_current = _data.Length - 1;

		return _src.Seek(_data[_current].Offset + off - len, SeekOrigin.Begin) - _data[_current].Offset + len;
	}

	public override void SetLength(long value)
		=> throw new NotSupportedException();

	protected override void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			_src.Position = _origPos;
			_disposed = false;
		}
	}
}