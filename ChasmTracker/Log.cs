using System;
using System.Collections.Generic;
using System.Linq;

namespace ChasmTracker;

using ChasmTracker.Configurations;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class Log
{
	const int NumLines = 1000;

	public const int MaxLineLength = 74;

	static List<LogLine> s_lines = new List<LogLine>();

	public static IList<LogLine> Lines => s_lines;

	public static void Append(byte colour, string format, params object?[] args)
		=> Append(new LogLine(colour, string.Format(format, args)));

	public static void AppendWithUnderline(byte colour, string format, params object?[] args)
		=> Append(new LogLine(colour, string.Format(format, args)) { Underline = true });

	public static void Append(bool biosFont, byte colour, string format, params object?[] args)
		=> Append(biosFont, colour, false, format, args);
	public static void AppendWithUnderline(bool biosFont, byte colour, string format, params object?[] args)
		=> Append(biosFont, colour, true, format, args);

	public static void Append(bool biosFont, byte colour, bool underline, string format, params object?[] args)
	{
		string text = string.Format(format, args);

		if (biosFont)
		{
			char[] unicodeChars = new char[text.Length];

			for (int i=0; i < text.Length; i++)
				unicodeChars[i] = ((byte)text[i]).FromCP437();

			text = new string(unicodeChars);
		}

		var logLine = new LogLine(colour, text);

		logLine.Underline = underline;

		Append(logLine);
	}

	public static void Append(LogLine logLine)
	{
		if (Status.Flags.HasAllFlags(StatusFlags.Headless))
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
			AppendUnderlineImpl(logLine.Text.Length);
	}

	public static void AppendNewLine()
		=> Append(new LogLine(VGAMem.DefaultForeground, ""));

	static void AppendUnderlineImpl(int chars)
		=> Append(new LogLine(2, new string('‾', chars)));

	public static void AppendUnderline()
		=> AppendUnderlineImpl(s_lines.Last().Text.Length);

	public static void AppendTimestamp(byte colour, string text)
	{
		var now = DateTime.Now;

		string dateStr = now.ToString(Configuration.General.DateFormat.GetFormatString());
		string timeStr = now.ToString(Configuration.General.TimeFormat.GetFormatString());

		string timestamp = $" [{dateStr} {timeStr}]";

		if (text.Length + timestamp.Length > MaxLineLength)
		{
			/* no space for a timestamp;
			 * just duplicate the string in this case. */
			Append(colour, text);
		}
		else
		{
			Append(colour, text.PadRight(MaxLineLength - timestamp.Length) + timestamp);
		}
	}

	public static void AppendTimestamp(byte colour, string format, params object?[] args)
		=> AppendTimestamp(colour, string.Format(format, args));

	public static void AppendTimestampWithUnderline(byte colour, string text)
	{
		AppendTimestamp(colour, text);
		AppendUnderline();
	}

	public static void AppendTimestampWithUnderline(byte colour, string format, params object?[] args)
		=> AppendTimestampWithUnderline(colour, string.Format(format, args));

	public static void AppendException(Exception ex, string prefix = "")
	{
		if (string.IsNullOrWhiteSpace(prefix))
			Append(new LogLine(4, ex.GetType().Name + ": " + ex.Message));
		else
			Append(new LogLine(4, prefix + ": " + ex.GetType().Name + ": " + ex.Message));
	}
}
