using System;
using System.Runtime.CompilerServices;

namespace ChasmTracker.Songs;

using ChasmTracker.Utility;

public struct SampleWindow
{
	byte[] _rawData;
	int _offset;
	int _bps;

	public byte[] RawData => _rawData;
	public int BytesPerSample => _bps;

	public SampleWindow(byte[] rawData, int bps)
		: this(rawData, bps, SongSample.AllocatePrepend)
	{
	}

	SampleWindow(byte[] rawData, int bps, int offset)
	{
		_rawData = rawData;
		_bps = bps;
		_offset = offset;
	}

	public static readonly SampleWindow Empty = new SampleWindow(new byte[SongSample.AllocatePrepend + SongSample.AllocateAppend], 1, SongSample.AllocatePrepend);

	public bool IsEmpty => Length <= 0;

	public byte this[int index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _rawData[index + _offset * _bps];
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set => _rawData[index + _offset * _bps] = value;
	}

	public int Length =>
		_rawData == null
		? 0
		: _rawData.Length - _bps * (SongSample.AllocateAppend + _offset); // _offset already chops off the prepend, but we need to account for the append

	public Span<byte> AsSpan() => Slice(0, Length);

	public Span<byte> AsExtendedSpan() => Slice(0);

	public SampleWindow Shift(int deltaSamples)
	{
		if ((_offset + deltaSamples) < 0)
			throw new ArgumentOutOfRangeException();
		if (deltaSamples > Length)
			throw new ArgumentOutOfRangeException();

		return new SampleWindow(_rawData, _bps, _offset + deltaSamples);
	}

	public Span<byte> Slice(int byteIndex)
		=> Slice(byteIndex, _rawData.Length - _offset * _bps - byteIndex);

	public Span<byte> Slice(int byteIndex, int length)
	{
		return _rawData.Slice(byteIndex + _offset * _bps, length);
	}
}
