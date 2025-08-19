using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class SubStream : Stream
{
	Stream _wrapped;
	long _startOffset;
	long _subStreamLength;
	bool _ownsStream;
	bool _isClosed;

	public SubStream(Stream toWrap, long startOffset, long subStreamLength, bool ownsStream = false)
	{
		_wrapped = toWrap;
		_startOffset = startOffset;
		_subStreamLength = subStreamLength;
		_ownsStream = ownsStream;
		_isClosed = false;
	}

	void EnsureOpen()
	{
		if (_isClosed)
			throw new InvalidOperationException("The substream is closed.");
	}

	public override bool CanRead { get { EnsureOpen(); return _wrapped.CanRead; } }
	public override bool CanWrite { get { EnsureOpen(); return _wrapped.CanWrite; } }
	public override bool CanSeek { get { EnsureOpen(); return _wrapped.CanSeek; } }
	public override bool CanTimeout { get { EnsureOpen(); return _wrapped.CanTimeout; } }

	public override int ReadTimeout
	{
		get { EnsureOpen(); return _wrapped.ReadTimeout; }
		set { EnsureOpen(); _wrapped.ReadTimeout = value; }
	}

	public override int WriteTimeout
	{
		get { EnsureOpen(); return _wrapped.WriteTimeout; }
		set { EnsureOpen(); _wrapped.WriteTimeout = value; }
	}

	public override long Length { get { EnsureOpen(); return _subStreamLength; } }

	public override long Position
	{
		get { EnsureOpen(); return _wrapped.Position - _startOffset; }
		set => Seek(value, SeekOrigin.Begin);
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		EnsureOpen();

		switch (origin)
		{
			case SeekOrigin.Current: offset += Position; break;
			case SeekOrigin.End: offset += _subStreamLength; break;
		}

		if ((offset < 0) || (offset > _subStreamLength))
			throw new ArgumentOutOfRangeException();

		return _wrapped.Seek(offset + _startOffset, SeekOrigin.Begin) - _startOffset;
	}

	public override void SetLength(long value)
	{
		EnsureOpen();
		_wrapped.SetLength(value + _startOffset);
	}

	public override int Read(Span<byte> buffer)
	{
		EnsureOpen();

		long overrun = (Position + buffer.Length) - Length;

		if (overrun > 0)
			buffer = buffer.Slice(0, (int)(buffer.Length - overrun));

		return _wrapped.Read(buffer);
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		EnsureOpen();

		long overrun = (Position + count) - Length;

		if (overrun > 0)
			count = (int)(count - overrun);

		return _wrapped.Read(buffer, offset, count);
	}

	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		EnsureOpen();

		long overrun = (Position + buffer.Length) - Length;

		if (overrun > 0)
			buffer = buffer.Slice(0, (int)(buffer.Length - overrun));

		return _wrapped.ReadAsync(buffer, cancellationToken);
	}

	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		EnsureOpen();

		long overrun = (Position + count) - Length;

		if (overrun > 0)
			count = (int)(count - overrun);

		return _wrapped.ReadAsync(buffer, offset, count, cancellationToken);
	}

	public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
	{
		EnsureOpen();

		long overrun = (Position + count) - Length;

		if (overrun > 0)
			count = (int)(count - overrun);

		return _wrapped.BeginRead(buffer, offset, count, callback, state);
	}

	public override int EndRead(IAsyncResult asyncResult)
	{
		EnsureOpen();
		return _wrapped.EndRead(asyncResult);
	}

	public override int ReadByte()
	{
		EnsureOpen();

		if (Position < Length)
			return _wrapped.ReadByte();
		else
			return -1;
	}

	public override void Write(ReadOnlySpan<byte> buffer)
	{
		EnsureOpen();

		long overrun = (Position + buffer.Length) - Length;

		if (overrun > 0)
			buffer = buffer.Slice(0, (int)(buffer.Length - overrun));

		_wrapped.Write(buffer);
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		EnsureOpen();

		long overrun = (Position + count) - Length;

		if (overrun > 0)
			count = (int)(count - overrun);

		_wrapped.Write(buffer, offset, count);
	}

	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		EnsureOpen();

		long overrun = (Position + buffer.Length) - Length;

		if (overrun > 0)
			buffer = buffer.Slice(0, (int)(buffer.Length - overrun));

		return _wrapped.WriteAsync(buffer, cancellationToken);
	}

	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		EnsureOpen();

		long overrun = (Position + count) - Length;

		if (overrun > 0)
			count = (int)(count - overrun);

		return _wrapped.WriteAsync(buffer, offset, count, cancellationToken);
	}

	public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
	{
		EnsureOpen();

		long overrun = (Position + count) - Length;

		if (overrun > 0)
			count = (int)(count - overrun);

		return _wrapped.BeginWrite(buffer, offset, count, callback, state);
	}

	public override void EndWrite(IAsyncResult asyncResult)
	{
		EnsureOpen();

		_wrapped.EndWrite(asyncResult);
	}

	public override void WriteByte(byte value)
	{
		EnsureOpen();

		if (Position >= Length)
			throw new EndOfStreamException("Cannot extend fixed-lenth stream");

		_wrapped.WriteByte(value);
	}

	public override void Flush()
	{
		EnsureOpen();
		_wrapped.Flush();
	}

	public override Task FlushAsync(CancellationToken cancellationToken)
	{
		EnsureOpen();
		return _wrapped.FlushAsync(cancellationToken);
	}

	public override void CopyTo(Stream destination, int bufferSize)
	{
		EnsureOpen();

		long overrun = (Position + bufferSize) - Length;

		if (overrun > 0)
			bufferSize = (int)(bufferSize - overrun);

		_wrapped.CopyTo(destination, bufferSize);
	}

	public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
	{
		EnsureOpen();

		long overrun = (Position + bufferSize) - Length;

		if (overrun > 0)
			bufferSize = (int)(bufferSize - overrun);

		return _wrapped.CopyToAsync(destination, bufferSize, cancellationToken);
	}

	public override void Close()
	{
		if (_isClosed)
			throw new InvalidOperationException("The substream is already closed.");

		if (_ownsStream)
			_wrapped.Close();

		_isClosed = true;
	}

	protected override void Dispose(bool disposing)
	{
		if (_ownsStream)
			_wrapped.Dispose();

		_isClosed = true;

		base.Dispose(disposing);
	}

	public override async ValueTask DisposeAsync()
	{
		await _wrapped.DisposeAsync();
		await base.DisposeAsync();
	}
}
