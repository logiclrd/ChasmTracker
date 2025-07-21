using ChasmTracker.FileTypes;
using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

namespace ChasmTracker.Dialogs.Instruments;

public class ExportInstrumentDialog : Dialog
{
	TextEntryWidget? textEntryFileName;
	ButtonWidget? buttonOK;
	ButtonWidget? buttonCancel;
	OtherWidget? otherEventSink;

	string _fileName;
	int _exportFormat;

	InstrumentFileConverter[] _saveFormats;

	public string FileName => _fileName;
	public InstrumentFileConverter InstrumentConverter => _saveFormats[_exportFormat];

	public ExportInstrumentDialog(string fileName, InstrumentFileConverter[] saveFormats)
		: base(new Point(21, 20), new Size(39, 18))
	{
		_fileName = fileName;

		_saveFormats = saveFormats;
	}

	protected override void Initialize()
	{
		textEntryFileName = new TextEntryWidget(new Point(33, 24), 18, FileName, Constants.MaxNameLength - 1);

		buttonOK = new ButtonWidget(new Point(31, 35), 6, "OK", 3);
		buttonOK.Clicked += DialogButtonYes;

		buttonCancel = new ButtonWidget(new Point(42, 35), 6, "Cancel", 1);
		buttonCancel.Clicked += DialogButtonCancel;

		otherEventSink = new OtherWidget();
		otherEventSink.OtherHandleKey += otherEventSink_HandleKey;
		otherEventSink.OtherRedraw += otherEventSink_Draw;

		Widgets.Add(textEntryFileName);
		Widgets.Add(buttonOK);
		Widgets.Add(buttonCancel);
		Widgets.Add(otherEventSink);
	}

	void otherEventSink_Draw()
	{
		bool isFocused = (SelectedWidgetIndex.Value == 3);

		VGAMem.DrawFillCharacters(new Point(53, 24), new Point(56, 31), (VGAMem.DefaultForeground, 0));

		for (int n = 0; n < _saveFormats.Length; n++)
		{
			int fg = 6, bg = 0;

			if (isFocused && n == _exportFormat)
			{
				fg = 0;
				bg = 3;
			}
			else if (n == _exportFormat)
				bg = 14;

			VGAMem.DrawTextLen(_saveFormats[n].Label, 4, new Point(53, 24 + n), (fg, bg));
		}
	}

	bool otherEventSink_HandleKey(KeyEvent k)
	{
		int newFormat = _exportFormat;

		if (k.State == KeyState.Release)
			return false;

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newFormat--;
				break;
			case KeySym.Down:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newFormat++;
				break;
			case KeySym.PageUp:
			case KeySym.Home:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newFormat = 0;
				break;
			case KeySym.PageDown:
			case KeySym.End:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				newFormat = _saveFormats.Length - 1;
				break;
			case KeySym.Tab:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					ChangeFocusTo(0);
					return true;
				}
				/* fall through */
				goto case KeySym.Left;
			case KeySym.Left:
			case KeySym.Right:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				ChangeFocusTo(0); /* should focus 0/1/2 depending on what's closest */
				return true;
			default:
				return false;
		}

		newFormat = newFormat.Clamp(0, _saveFormats.Length - 1);

		if (newFormat != _exportFormat)
		{
			/* update the option string */
			_exportFormat = newFormat;
			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return true;
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Export Instrument", new Point(34, 21), (0, 2));

		VGAMem.DrawText("Filename", new Point(24, 24), (0, 2));
		VGAMem.DrawBox(new Point(32, 23), new Point(51, 25), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawBox(new Point(52, 23), new Point(57, 32), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}
}
