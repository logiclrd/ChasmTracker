using System.Text;

namespace ChasmTracker.Utility;

public static class StringBuilderExtensions
{
	public static int GetNumLines(this StringBuilder text)
	{
		int n = 0;

		bool lastCR = false;

		for (int index = 0; index < text.Length; index++)
		{
			char ch = text[index];

			bool cr = (ch == '\r');
			bool lf = (ch == '\n');

			if (cr || (lf && !lastCR))
				n++;

			lastCR = cr;
		}

		return n;
	}
}