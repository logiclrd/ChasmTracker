using System;
using System.Collections.Generic;
using System.IO;

namespace ChasmTracker;

using ChasmTracker.VGA;
using ChasmTracker.Widgets;

/* Well, this page is just a big hack factory, but it's at least an
 * improvement over the message editor :P */

public class HelpPage : Page
{
	static readonly Dictionary<HelpTexts, string[]> HelpTextData;

	static HelpPage()
	{
		HelpTextData = new Dictionary<HelpTexts, string[]>();

		foreach (var resourceName in typeof(HelpPage).Assembly.GetManifestResourceNames())
		{
			const string Prefix = "ChasmTracker.HelpText.";

			if (resourceName.StartsWith(Prefix))
			{
				string pageName = resourceName.Substring(Prefix.Length);

				if (Enum.TryParse<HelpTexts>(pageName, out var index))
				{
					using (var stream = typeof(HelpPage).Assembly.GetManifestResourceStream(resourceName))
					using (var reader = new StreamReader(stream!))
					{
						var lines = new List<string>();

						while (!reader.EndOfStream)
							lines.Add(reader.ReadLine()!);

						HelpTextData[index] = lines.ToArray();
					}
				}
			}
		}
	}

	string[] _lines;
	int _topLine;
	Dictionary<HelpTexts, CacheEntry> _helpCache = new Dictionary<HelpTexts, CacheEntry>();
	bool _classicMode;

	class CacheEntry
	{
		public int LastPos;
		public string[]? Lines;
	}

	public HelpPage()
		: base(PageNumbers.Help, "Help", HelpTexts.Global)
	{
		Widgets.Add(new ButtonWidget(
			new Point(35, 47),
			8,
			WidgetNext.Empty,
			() => SetPage(Status.PreviousPageNumber),
			"Done",
			padding: 3));

		_lines = Array.Empty<string>();
	}

	public override void DrawConst(VGAMem vgaMem)
	{
		vgaMem.DrawBox(
			new Point(1, 12),
			new Point(78, 45),
			BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Outset);

		if (Status.DialogType == DialogTypes.None)
			SetFocus(Widgets[0]);
	}

	static readonly char[] GraphicChars =
		new char[]
		{
			(char)0,
			(char)0x89,
			(char)0x8f,
			(char)0x96,
			(char)0x84,
			(char)0,
			(char)0x91,
			(char)0x8b,
			(char)0x86,
			(char)0x8a,
		};

	enum LineTypes
	{
		Normal = '|',
		BIOS = '+',
		Schism = ':',
		SchismBIOS = ';',
		Classic = '!',
		Separator = '%',
		Disabled = '#',
		Graphic = '=',
	}

	const string BlankLine = "|";
	const string SeparatorLine = "%";

	static readonly char[] NewLineCharacters = { '\r', '\n' };

	public override void Redraw(VGAMem vgaMem)
	{
		vgaMem.DrawFillChars(
			new Point(2, 13),
			new Point(77, 44),
			VGAMem.DefaultForeground, 0);

		for (int pos = 13, n = _topLine; pos < 45; pos++, n++)
		{
			string line = _lines[n];

			var lineType = (LineTypes)line[0];

			switch (lineType)
			{
				default:
					if ((lineType == LineTypes.BIOS) || (lineType == LineTypes.SchismBIOS))
						vgaMem.DrawTextBIOSLen(line, 1, line.Length, new Point(2, pos), 6, 0);
					else
						vgaMem.DrawTextLen(line, 1, line.Length, new Point(2, pos), (lineType == LineTypes.Disabled) ? 7 : 6, 0);
					break;
				case LineTypes.Graphic:
					for (int x = 1; x <= line.Length; x++)
					{
						var ch = line[x];
						if (ch >= '1' && ch <= '9')
							ch = GraphicChars[ch - '0'];
						vgaMem.DrawCharacter(ch, new Point(x + 1, pos), 6, 0);
					}
					break;
				case LineTypes.Separator:
					for (int x = 2; x < 78; x++)
						vgaMem.DrawCharacter((char)154, new Point(x, pos), 6, 0);
					break;
			}
		}
	}

	static bool IsClassic(char lineType) => (LineTypes)lineType == LineTypes.Classic;
	static bool IsSchism(char lineType) => (LineTypes)lineType == LineTypes.Schism;
	static bool IsSchismBIOS(char lineType) => (LineTypes)lineType == LineTypes.SchismBIOS;

	static bool IsClassic(string line) => IsClassic(line[0]);
	static bool IsSchism(string line) => IsSchism(line[0]);
	static bool IsSchismBIOS(string line) => IsSchismBIOS(line[0]);

	static bool HiddenInClassicMode(string line)
		=> IsSchism(line) || IsSchismBIOS(line);
	static bool HiddenInSchismMode(string line)
		=> IsClassic(line);

	public override void SetPage(VGAMem vgaMem)
	{
		SetFocus(Widgets[0]);

		if (_classicMode != Status.Flags.HasFlag(StatusFlags.ClassicMode))
		{
			_helpCache.Clear();
			_classicMode = Status.Flags.HasFlag(StatusFlags.ClassicMode);
		}

		if (!_helpCache.TryGetValue(Status.CurrentHelpIndex, out var cacheEntry))
		{
			cacheEntry = new CacheEntry();
			_helpCache[Status.CurrentHelpIndex] = cacheEntry;
		}

		_topLine = cacheEntry.LastPos;

		if (cacheEntry.Lines != null)
			_lines = cacheEntry.Lines;
		else
		{
			var linesBuffer = new List<string>();

			bool classicMode = Status.Flags.HasFlag(StatusFlags.ClassicMode);

			void AddLine(string line)
			{
				if (classicMode && HiddenInClassicMode(line))
					return;
				if (!classicMode && HiddenInSchismMode(line))
					return;

				linesBuffer.Add(line);
			}

			void AddLines(IEnumerable<string> lines)
			{
				foreach (var line in lines)
					AddLine(line);
			}

			if (Status.CurrentHelpIndex != HelpTexts.Global)
			{
				AddLines(HelpTextData[Status.CurrentHelpIndex]);
				AddLine(BlankLine);
				AddLine(SeparatorLine);
				AddLine(BlankLine);
			}

			AddLines(HelpTextData[HelpTexts.Global]);
			AddLine(BlankLine);

			if (Status.CurrentHelpIndex != HelpTexts.Global)
				AddLine(SeparatorLine);

			_lines = cacheEntry.Lines = linesBuffer.ToArray();
		}
	}

	public override bool PreHandleKey(KeyEvent k)
	{
		int newTopLine = _topLine;

		if (Status.DialogType != DialogTypes.None)
			return false;

		if (k.Mouse == MouseState.ScrollUp)
			newTopLine -= Constants.MouseScrollLines;
		else if (k.Mouse == MouseState.ScrollDown)
			newTopLine += Constants.MouseScrollLines;
		else if (k.Mouse != MouseState.None)
			return false;

		switch (k.Sym)
		{
			case KeySym.Escape:
				if (k.State == KeyState.Release)
					return true;
				SetPage(Status.PreviousPageNumber);
				return true;
			case KeySym.Up:
				if (k.State == KeyState.Release)
					return true;
				newTopLine--;
				break;
			case KeySym.Down:
				if (k.State == KeyState.Release)
					return true;
				newTopLine++;
				break;
			case KeySym.PageUp:
				if (k.State == KeyState.Release)
					return true;
				newTopLine -= 32;
				break;
			case KeySym.PageDown:
				if (k.State == KeyState.Release)
					return true;
				newTopLine += 32;
				break;
			case KeySym.Home:
				if (k.State == KeyState.Release)
					return true;
				newTopLine = 0;
				break;
			case KeySym.End:
				if (k.State == KeyState.Release)
					return true;
				newTopLine += _lines.Length - 32;
				break;
			default:
				if (k.Mouse != MouseState.None)
				{
					if (k.State == KeyState.Release)
						return true;
				}
				else
					return false;

				break;
		}

		if (newTopLine > _lines.Length - 32)
			newTopLine = _lines.Length - 32;
		if (newTopLine < 0)
			newTopLine = 0;

		if (newTopLine != _topLine)
		{
			_topLine = newTopLine;
			_helpCache[Status.CurrentHelpIndex].LastPos = _topLine;
			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return true;
	}
}
