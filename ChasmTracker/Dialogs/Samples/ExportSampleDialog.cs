using System;
using System.Linq;
using ChasmTracker.FileTypes;
using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

namespace ChasmTracker.Dialogs.Samples;

public class ExportSampleDialog : Dialog
{
	TextEntryWidget? textEntryFileName;
	ButtonWidget? buttonOK;
	ButtonWidget? buttonCancel;
	OtherWidget? otherFormatList;

	SampleFileConverter[] _saveFormats;

	string _fileName;
	int _saveFormatIndex;

	public string FileName => _fileName;
	public SampleFileConverter SampleConverter => _saveFormats[_saveFormatIndex];

	public ExportSampleDialog(string fileName)
		: base(new Point(21, 20), new Size(39, 18))
	{
		_fileName = fileName;
		_saveFormats = SampleFileConverter.EnumerateImplementations().Where(format => format.IsEnabled).ToArray();
	}

	protected override void Initialize()
	{
		textEntryFileName = new TextEntryWidget(new Point(33, 24), 18, _fileName, Constants.MaxNameLength);
		buttonOK = new ButtonWidget(new Point(31, 35), 6, "OK", 3);
		buttonCancel = new ButtonWidget(new Point(42, 35), 6, "Cancel", 1);
		otherFormatList = new OtherWidget(new Point(53, 24), new Size(4, Math.Min(_saveFormats.Length, 8)));

		buttonOK.Clicked += DialogButtonYes;
		buttonCancel.Clicked += DialogButtonCancel;

		otherFormatList.OtherHandleKey += otherFormatList_HandleKey;
		otherFormatList.OtherRedraw += otherFormatList_Draw;
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Export Sample", new Point(34, 21), (0, 2));

		VGAMem.DrawText("Filename", new Point(24, 24), (0, 2));
		VGAMem.DrawBox(new Point(32, 23), new Point(51, 25), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawBox(new Point(52, 23), new Point(57, 32), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}

	void otherFormatList_Draw()
	{
		bool focused = (SelectedWidget == otherFormatList);

		VGAMem.DrawFillCharacters(
			otherFormatList!.Position,
			otherFormatList.Position.Advance(otherFormatList.Size).Advance(-1, -1),
			(VGAMem.DefaultForeground, 0));

		for (int c = 0; c < _saveFormats.Length; c++)
		{
			var format = _saveFormats[c];

			int fg = 6, bg = 0;

			if (focused && c == _saveFormatIndex)
			{
				fg = 0;
				bg = 3;
			}
			else if (c == _saveFormatIndex)
				bg = 14;

			VGAMem.DrawTextLen(format.Label, 4, new Point(53, 24 + c), (fg, bg));
		}
	}

	bool otherFormatList_HandleKey(KeyEvent k)
	{
		int newSelectedFormat = _saveFormatIndex;

		if (k.State == KeyState.Release)
			return false;

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newSelectedFormat--;
				break;
			case KeySym.Down:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newSelectedFormat++;
				break;
			case KeySym.PageUp:
			case KeySym.Home:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newSelectedFormat = 0;
				break;
			case KeySym.PageDown:
			case KeySym.End:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newSelectedFormat = _saveFormats.Length - 1;
				break;
			case KeySym.Tab:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					ChangeFocusTo(textEntryFileName!);
					return true;
				}
				/* fall through */
				goto case KeySym.Left;
			case KeySym.Left:
			case KeySym.Right:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				ChangeFocusTo(textEntryFileName!); /* should focus 0/1/2 depending on what's closest */
				return true;
			default:
				return false;
		}

		newSelectedFormat = newSelectedFormat.Clamp(0, _saveFormats.Length - 1);

		if (newSelectedFormat != _saveFormatIndex)
		{
			/* update the option string */
			_saveFormatIndex = newSelectedFormat;
			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return true;
	}
}
