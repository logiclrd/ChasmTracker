namespace ChasmTracker.Utility;

public class StringUtility
{
	public static int StrVersCmp(string a, string b, bool ignoreCase = false)
	{
		int i;

		int digitsStart = -1;

		for (i = 0; (i < a.Length) && (i < b.Length); i++)
		{
			char ac = a[i];
			char bc = b[i];

			if (ignoreCase)
			{
				ac = char.ToLowerInvariant(ac);
				bc = char.ToLowerInvariant(bc);
			}

			if (!char.IsDigit(ac))
				digitsStart = -1;
			else if (digitsStart < 0)
				digitsStart = i;

			if (ac == bc)
				continue;

			if (!char.IsDigit(ac) || !char.IsDigit(bc))
				return bc - ac;

			// Leading zeroes? treat as though decimal, so lexicographic sort is okay.
			if (a[digitsStart] == '0')
				return bc - ac;

			// If we get here, then we've hit a difference in digits, where all preceding
			// digits (if any) matched and the first digit in the sequence of digits is
			// non-zero.
			//
			// Two cases:
			// - One of the numbers has fewer digits. In this case, it always goes first.
			// - They both have the same number of digits. In this case, sort lexicographically.

			i++;

			while ((i < a.Length) && (i < b.Length))
			{
				bool aDigit = char.IsDigit(a, i);
				bool bDigit = char.IsDigit(b, i);

				if (!aDigit || !bDigit)
				{
					if (bDigit) // b is longer
						return -1;
					if (aDigit) // a is longer
						return +1;

					// Same length
					return bc - ac;
				}
			}
		}

		return b.Length - a.Length;
	}
}
