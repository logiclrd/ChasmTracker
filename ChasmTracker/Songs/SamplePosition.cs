using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ChasmTracker.Songs;

/* this might seem totally insane, but it makes this shit easier to change when
 * it needs to be changed (see: 32.16 precision to 32.32 precision) */
public struct SamplePosition
{
	long _value;

	public int Whole => unchecked((int)(_value >> 32));
	public uint Fraction => unchecked((uint)(_value & uint.MaxValue));

	public static readonly SamplePosition Zero = new SamplePosition(0, 0);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public SamplePosition(int whole, uint frac)
	{
		_value = ((long)whole << 32) | frac;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static SamplePosition Ratio(int dividend, uint divisor)
		=> new SamplePosition(dividend, 0) / divisor;

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static implicit operator long(SamplePosition @this) => @this._value;
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static explicit operator SamplePosition(long value) => new SamplePosition() { _value = value };

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static SamplePosition operator +(SamplePosition a, SamplePosition b) => (SamplePosition)(a._value + b._value);
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static SamplePosition operator +(SamplePosition a, int b) => (SamplePosition)(a._value + ((long)b << 32));
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static SamplePosition operator -(SamplePosition a, SamplePosition b) => (SamplePosition)(a._value - b._value);
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static SamplePosition operator -(SamplePosition a, int b) => (SamplePosition)(a._value - ((long)b << 32));
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static SamplePosition operator *(SamplePosition a, long b) => (SamplePosition)(a._value * b);
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static SamplePosition operator /(SamplePosition a, long b) => (SamplePosition)(a._value / b);
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static long operator /(SamplePosition a, SamplePosition b) => (a._value / b._value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator <(SamplePosition a, SamplePosition b) => (a._value < b._value);
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator >(SamplePosition a, SamplePosition b) => (a._value > b._value);
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator <=(SamplePosition a, SamplePosition b) => (a._value <= b._value);
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator >=(SamplePosition a, SamplePosition b) => (a._value >= b._value);
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator ==(SamplePosition a, SamplePosition b) => (a._value == b._value);
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator !=(SamplePosition a, SamplePosition b) => (a._value != b._value);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator <(SamplePosition a, long b) => (a._value < ((long)b << 32));
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator >(SamplePosition a, long b) => (a._value > ((long)b << 32));
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator <=(SamplePosition a, long b) => (a._value <= ((long)b << 32));
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator >=(SamplePosition a, long b) => (a._value >= ((long)b << 32));
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator ==(SamplePosition a, long b) => (a._value == ((long)b << 32));
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static bool operator !=(SamplePosition a, long b) => (a._value != ((long)b << 32));

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static SamplePosition operator -(SamplePosition a) => (SamplePosition)(-a._value);

	public SamplePosition Abs() => (SamplePosition)Math.Abs(_value);
	public SamplePosition Floor() => (SamplePosition)(_value & ~0xFFFFFFFFL);
	public SamplePosition Ceiling() => (SamplePosition)(((_value - 1) | 0xFFFFFFFF) + 1);

	public override bool Equals([NotNullWhen(true)] object? other)
	{
		if (other is SamplePosition otherSamplePosition)
			return this == otherSamplePosition;
		else if (other is long value)
			return _value == value;
		else
			return false;
	}

	public override int GetHashCode() => _value.GetHashCode();
}
