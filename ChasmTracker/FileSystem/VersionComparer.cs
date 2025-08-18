using System;

namespace ChasmTracker.FileSystem;

public class VersionComparer : StringComparer
{
	StringComparer _baseComparer;

	public VersionComparer(StringComparer baseComparer)
	{
		_baseComparer = baseComparer;
	}

	public override int Compare(string? x, string? y)
	{
		if ((x == null) && (y == null))
			return 0;
		if ((x == null) || (y == null))
			return (x == null) ? -1 : +1;

		int numberStart = 0;
		bool leadingZero = true;

		int commonLength = Math.Min(x.Length, y.Length);

		int i;

		for (i = 0; (i < commonLength) && (_baseComparer.Compare(x.Substring(i, 1), y.Substring(i, 1)) == 0); i++)
		{
			if (!char.IsDigit(x, i))
			{
				numberStart = i + 1;
				leadingZero = true; // If we're in a non-digit part, set up for the next digit part we see.
			}
			else if (x[i] != '0')
				leadingZero = false;
		}

		char xc = (numberStart < x.Length) ? x[numberStart] : '\0';
		char yc = (numberStart < y.Length) ? y[numberStart] : '\0';

		if ((xc >= '1') && (xc <= '9') && (yc >= '1') && (yc <= '9'))
		{
			int j;

			for (j = i; (j < x.Length) && char.IsDigit(x, j); j++)
				if ((j >= y.Length) || !char.IsDigit(y, j))
					return 1;

			if ((j < y.Length) && char.IsDigit(y, j))
				return -1;

			xc = (i < x.Length) ? x[i] : '\0';
			yc = (i < y.Length) ? y[i] : '\0';

			return xc - yc;
		}
		else
		{
			xc = (i < x.Length) ? x[i] : '\0';
			yc = (i < y.Length) ? y[i] : '\0';

			bool xcDigit = char.IsDigit(xc);
			bool ycDigit = char.IsDigit(yc);

			if (leadingZero && (numberStart < i) && (xcDigit || ycDigit))
			{
				if (xcDigit == ycDigit)
					return xc - yc;
				else
					return ycDigit.CompareTo(xcDigit);
			}

			return xc - yc;
		}
	}

	public override bool Equals(string? x, string? y)
	{
		return x == y;
	}

	public override int GetHashCode(string obj)
	{
		return obj.GetHashCode();
	}
}