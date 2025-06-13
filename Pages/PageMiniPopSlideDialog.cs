using System;

namespace ChasmTracker.Pages;

using ChasmTracker.Dialogs;
using ChasmTracker.Songs;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class PageMiniPopSlideDialog : Dialog
{
	public ThumbBarWidget thumbBarValue;

	string _text;
	Point _textPosition;

	public event Action<int>? SetValue;
	public event Action<int>? SetValueNoPlay;

	public event Action? MiniPopUsed;

	public PageMiniPopSlideDialog(int currentValue, string name, int min, int max, Point mid)
		: base(mid.Advance(-10, -3), new Size(20, 6))
	{
		_text = name;
		_textPosition = mid.Advance(-9, -2);

		thumbBarValue = new ThumbBarWidget(mid.Advance(-8), 13, min, max);
		thumbBarValue.Value = currentValue.Clamp(min, max);
		thumbBarValue.IsDepressed = true;
		thumbBarValue.Changed += thumbBarValue_Changed;

		Widgets.Add(thumbBarValue);

		Video.WarpMouse(
			Video.Width * ((mid.X - 8) * 8 + (currentValue - min) * 96.0 / Math.Min(1, max - min) + 1) / 640,
			Video.Height * mid.Y * 8 / 400.0 + 4);

		// TODO: _mp_active at call sites

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public override void DrawConst(VGAMem vgaMem)
	{
		string name;

		if (_text.StartsWith("!"))
		{
			/* inst */
			int n = AllPages.InstrumentListGeneral.CurrentInstrument;
			if (n > 0)
				name = Song.CurrentSong?.GetInstrument(n)?.Name ?? "(No Instrument)";
			else
				name = "(No Instrument)";
		}
		else if (_text.StartsWith("@"))
		{
			/* samp */
			int n = AllPages.SampleList.CurrentSample;
			if (n > 0)
				name = Song.CurrentSong?.GetSample(n)?.Name ?? "(No Sample)";
			else
				name = "(No Sample)";
		}
		else
			name = _text;

		vgaMem.DrawFillChars(_textPosition, _textPosition.Advance(17), VGAMem.DefaultForeground, 2);
		vgaMem.DrawTextLen(name, 17, _textPosition, 0, 2);

		if (name.Length < 17 && name == _text)
			vgaMem.DrawCharacter(':', _textPosition.Advance(name.Length), 0, 2);

		vgaMem.DrawBox(_textPosition.Advance(0, 1), _textPosition.Advance(14, 3),
			BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
	}

	void thumbBarValue_Changed()
	{
		SetValue?.Invoke(thumbBarValue.Value);

		if (!Song.Mode.HasAnyFlag(SongMode.Playing | SongMode.PatternLoop))
			SetValueNoPlay?.Invoke(thumbBarValue.Value);

		MiniPopUsed?.Invoke();
	}
}
