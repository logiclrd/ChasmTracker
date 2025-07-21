using System;
using System.Collections.Generic;
using ChasmTracker.VGA;

namespace ChasmTracker;

public class Log
{
	const int NumLines = 1000;

	static List<LogLine> s_lines = new List<LogLine>();

	public static IList<LogLine> Lines => s_lines;

	public static void Append(int colour, string format, params object[] args)
		=> Append(new LogLine(colour, string.Format(format, args), false));

	public static void AppendWithUnderline(int colour, string format, params object[] args)
		=> Append(new LogLine(colour, string.Format(format, args), false) { Underline = true });

	public static void Append(bool biosFont, int colour, string format, params object[] args)
		=> Append(new LogLine(colour, string.Format(format, args), biosFont));

	public static void AppendWithUnderline(bool biosFont, int colour, string format, params object[] args)
		=> Append(new LogLine(colour, string.Format(format, args), biosFont) { Underline = true });

	public static void Append(LogLine logLine)
	{
		if (Status.Flags.HasFlag(StatusFlags.Headless))
			Console.WriteLine(logLine.Text);
		else
		{
			s_lines.Add(logLine);

			while (s_lines.Count > NumLines)
				s_lines.RemoveAt(0);

			if (Status.CurrentPageNumber == PageNumbers.Log)
				Status.Flags |= StatusFlags.NeedUpdate;
		}

		if (logLine.Underline)
			AppendUnderline(logLine.Text.Length);
	}

	public static void AppendNewLine()
		=> Append(new LogLine(VGAMem.DefaultForeground, "", false));

	public static void AppendUnderline(int chars)
		=> Append(new LogLine(2, new string('\x81', chars), false));

	public static void AppendException(Exception ex, string prefix = "")
	{
		if (string.IsNullOrWhiteSpace(prefix))
			Append(new LogLine(4, ex.GetType().Name + ": " + ex.Message, false));
		else
			Append(new LogLine(4, prefix + ": " + ex.GetType().Name + ": " + ex.Message, false));
	}
}
