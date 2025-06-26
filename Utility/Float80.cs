using System;

namespace ChasmTracker.Utility;

public static class Float80
{
	public static double FromIEEE80Bytes(byte[] bytes)
	{
		if (bytes.Length != 10)
			throw new FormatException();

		bool sign = (bytes[0] & 0x80) != 0;
		int expon = ((bytes[0] & 0x7F) << 8) | (bytes[1] & 0xFF);
		uint hiMant =
			((uint)(bytes[2] & 0xFF) << 24) |
			((uint)(bytes[3] & 0xFF) << 16) |
			((uint)(bytes[4] & 0xFF) << 8) |
			((uint)(bytes[5] & 0xFF));
		uint loMant =
			((uint)(bytes[6] & 0xFF) << 24) |
			((uint)(bytes[7] & 0xFF) << 16) |
			((uint)(bytes[8] & 0xFF) << 8) |
			((uint)(bytes[9] & 0xFF));

		if (expon == 0 && hiMant == 0 && loMant == 0)
			return 0.0;

		if (expon == 0x7FFF)
		{
			bool isNaN = ((hiMant & 0x7FFFFFFF) != 0) || (loMant != 0);

			if (isNaN)
				return double.NaN;
			else
				return sign ? double.PositiveInfinity : double.NegativeInfinity;
		}

		expon -= 16383;

		double f = (double)unchecked(hiMant + int.MinValue) - int.MinValue;
		double g = (double)unchecked(loMant + int.MinValue) - int.MinValue;

		f *= Math.Pow(2.0, expon -= 31);
		g *= Math.Pow(2.0, expon -= 32);

		f += g;

		if (sign)
			f = -f;

		return f;
	}

	public static byte[] ToIEEE80Bytes(this double num)
	{
		int sign = 0;
		int expon = 0;
		uint hiMant = 0;
		uint loMant = 0;

		if (num < 0)
		{
			sign = 0x8000;
			num = -num;
		}

		if (double.IsNaN(num) || double.IsInfinity(num))
		{
			expon = 0x7FFF;
			sign = double.IsPositiveInfinity(num) ? 0x8000 : 0;

			if (double.IsNaN(num))
				loMant = 1;
		}
		else if (num != 0)
		{
			double fMant = frexp(num, ref expon);

			expon += 16382;

			if (expon < 0)
			{
				/* denormalized */
				fMant *= Math.Pow(2.0, expon);
				expon = 0;
			}

			fMant *= 4294967296.0;

			double fsMant = Math.Floor(fMant);

			hiMant = unchecked((uint)checked((int)(fsMant + int.MinValue) - int.MinValue));

			fMant = (fMant - fsMant) * 4294967296.0;

			fsMant = Math.Floor(fMant);

			loMant = unchecked((uint)checked((int)(fsMant + int.MinValue) - int.MinValue));
		}

		expon |= sign;

		var bytes = new byte[10];

		unchecked
		{
			bytes[0] = (byte)(expon >> 8);
			bytes[1] = (byte)expon;
			bytes[2] = (byte)(hiMant >> 24);
			bytes[3] = (byte)(hiMant >> 16);
			bytes[4] = (byte)(hiMant >> 8);
			bytes[5] = (byte)hiMant;
			bytes[6] = (byte)(loMant >> 24);
			bytes[7] = (byte)(loMant >> 16);
			bytes[8] = (byte)(loMant >> 8);
			bytes[9] = (byte)loMant;
		}

		return bytes;
	}

	static double frexp(double number, ref int exponent)
	{
  	const long DBL_EXP_MASK = 0x7ff0000000000000L;
		const int DBL_MANT_BITS = 52;
		const long DBL_SGN_MASK = -1 - 0x7fffffffffffffffL;
		const long DBL_MANT_MASK = 0x000fffffffffffffL;
		const long DBL_EXP_CLR_MASK = DBL_SGN_MASK | DBL_MANT_MASK;

		long bits = System.BitConverter.DoubleToInt64Bits(number);
		int exp = (int)((bits & DBL_EXP_MASK) >> DBL_MANT_BITS);
		exponent = 0;

		if (exp == 0x7ff || number == 0D)
			number += number;
		else
		{
			// Not zero and finite.
			exponent = exp - 1022;
			if (exp == 0)
			{
				// Subnormal, scale number so that it is in [1, 2).
				number *= System.BitConverter.Int64BitsToDouble(0x4350000000000000L); // 2^54
				bits = System.BitConverter.DoubleToInt64Bits(number);
				exp = (int)((bits & DBL_EXP_MASK) >> DBL_MANT_BITS);
				exponent = exp - 1022 - 54;
			}
			// Set exponent to -1 so that number is in [0.5, 1).
			number = System.BitConverter.Int64BitsToDouble((bits & DBL_EXP_CLR_MASK) | 0x3fe0000000000000L);
		}

		return number;
	}
}