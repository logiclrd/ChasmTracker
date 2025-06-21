using System;
using System.Text;

namespace ChasmTracker.Utility;

public static class StringExtensions
{
	/* adapted from glib. in addition to the normal c escapes, this also escapes the hashmark and semicolon
	 * (comment characters). if space is true, the first/last character is also escaped if it is a space. */
	public static string Escape(this string str, bool spaceHack = false)
	{
		// TODO: test me
		var dest = new StringBuilder();
		int n = 0;

		if (spaceHack && str[n] == ' ')
		{
			dest.Append(@"\040");
			n++;
		}

		while (n < str.Length)
		{
			char ch = str[n];

			switch (ch)
			{
				case '\a': dest.Append(@"\a"); break;
				case '\b': dest.Append(@"\b"); break;
				case '\f': dest.Append(@"\f"); break;
				case '\n': dest.Append(@"\n"); break;
				case '\r': dest.Append(@"\r"); break;
				case '\t': dest.Append(@"\t"); break;
				case '\v': dest.Append(@"\v"); break;

				case '\\':
				case '"':
					dest.Append('\\').Append(ch);
					break;

				default:
					if ((ch < ' ') || (ch >= 127) || (spaceHack && (ch == ' ') && (n + 1 == str.Length))
					 || (ch == '#') || (ch == ';'))
						dest.Append('\\').Append(Convert.ToString((int)ch, 8));
					else
						dest.Append(ch);

					break;
			}

			n++;
		}

		return dest.ToString();
	}

	/* opposite of str_escape. (this is glib's 'compress' function renamed more clearly) */
	public static string Unescape(this string str)
	{
		// TODO: test me
		var dest = new StringBuilder();
		int n = 0;

		while (n < str.Length)
		{
			char ch = str[n];

			if (ch != '\\')
				dest.Append(ch);
			else
			{
				n++;

				if (n >= str.Length)
				{
					// trailing backslash?
					dest.Append('\\');
					break;
				}

				ch = str[n];

				if ((ch >= '0') && (ch <= '7'))
				{
					ch -= '0';

					int end = Math.Min(str.Length, n + 3);

					n++;

					while ((n < end) && (str[n] >= '0') && (str[n] <= '7'))
					{
						ch = unchecked((char)(ch * 8 + (str[n] - '0')));
						n++;
					}

					dest.Append(ch);

					continue;
				}

				switch (ch)
				{
					case 'a': dest.Append('\a'); break;
					case 'b': dest.Append('\b'); break;
					case 'f': dest.Append('\f'); break;
					case 'n': dest.Append('\n'); break;
					case 'r': dest.Append('\r'); break;
					case 't': dest.Append('\t'); break;
					case 'v': dest.Append('\v'); break;

					case 'x':
						if (ReadHex(ch) >= 0)
						{
							ch = unchecked((char)ReadHex(ch));

							int end = Math.Min(str.Length, n + 2);

							n++;

							while ((n < end) && (ReadHex(str[n]) >= 0))
							{
								ch = unchecked((char)(ch * 16 + ReadHex(str[n])));
								n++;
							}

							dest.Append(ch);

							if (n < end)
								n--;

							break;
						}

						/* fall through */
						goto default;
					default:
						dest.Append(ch);
						break;
				}
			}
		}

		return dest.ToString();
	}

	static int ReadHex(char ch)
	{
		if ((ch >= '0') && (ch <= '9'))
			return ch - '0';
		if ((ch >= 'A') && (ch <= 'F'))
			return ch - 'A' + 10;
		if ((ch >= 'a') && (ch <= 'f'))
			return ch - 'a' + 10;

		return -1;
	}

	public static int GetNumLines(this string text)
	{
		int n = 0;

		bool lastCR = false;

		foreach (char ch in text)
		{
			bool cr = (ch == '\r');
			bool lf = (ch == '\n');

			if (cr || (lf && !lastCR))
				n++;

			lastCR = cr;
		}

		return n;
	}

	public static string ToString99(this int n)
	{
		if ((n >= 0) && (n < 100))
			return n.ToString("d2");
		else if (n <= 256)
		{
			n -= 100;

			return string.Concat("HIJKLMNOPQRSTUVWXYZ"[n / 10], n % 10);
		}

		/* This is a bug */
		return "";
	}

	public static string ToString99(this byte n) => ((int)n).ToString99();

	public static int Parse99(this string? s)
	{
		// aaarghhhh
		if ((s == null) || (s.Length == 0))
			return -1;

		int n = 0;

		if (s.Length >= 2)
		{
			// two chars

			char c = char.ToLower(s[0]);

			if ((c >= '0') && (c <= '9'))
				n = c - '0';
			else if ((c >= 'a') && (c <= 'g'))
				n = c - 'a' + 10;
			else if ((c >= 'h') && (c <= 'z'))
				n = c - 'h' + 10;
			else
				return -1;

			n *= 10;
			s = s.Substring(1);
		}

		if ((s[0] >= '0') && (s[0] <= '9'))
			return n + s[0] - '0';
		else
			return -1;
	}
}
