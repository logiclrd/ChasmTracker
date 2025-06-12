namespace ChasmTracker.Pages;

using ChasmTracker.Dialogs;
using ChasmTracker.Songs;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class PatternEditorLengthDialog : Dialog
{
	/* --------------------------------------------------------------------------------------------------------- */
	/* pattern length dialog */
	ThumbBarWidget thumbBarPatternLength;
	ThumbBarWidget thumbBarStartPattern;
	ThumbBarWidget thumbBarEndPattern;
	ButtonWidget buttonOK;

	int _currentPattern;

	public PatternEditorLengthDialog(int initialValue, int currentPattern)
		: base(new Point(15, 19), new Size(51, 15))
	{
		thumbBarPatternLength =
			new ThumbBarWidget(new Point(34, 24), 22, 32, 200)
			{
				Value = initialValue
			};

		thumbBarStartPattern =
			new ThumbBarWidget(new Point(34, 27), 26, 0, 199)
			{
				Value = currentPattern
			};

		thumbBarEndPattern =
			new ThumbBarWidget(new Point(34, 27), 26, 0, 199)
			{
				Value = currentPattern
			};

		buttonOK = new ButtonWidget(new Point(35, 31), 8, "OK", 4);

		buttonOK.Changed += DialogButtonYes;

		_currentPattern = currentPattern;

		Widgets.Add(thumbBarPatternLength);
		Widgets.Add(thumbBarStartPattern);
		Widgets.Add(thumbBarEndPattern);
		Widgets.Add(buttonOK);

		ActionYes = Close;
	}

	public override void DrawConst(VGAMem vgaMem)
	{
		vgaMem.DrawBox(new Point(33, 23), new Point(56, 25), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		vgaMem.DrawBox(new Point(33, 26), new Point(60, 29), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		vgaMem.DrawText("Set Pattern Length", new Point(31, 21), 0, 2);
		vgaMem.DrawText("Pattern Length", new Point(19, 24), 0, 2);
		vgaMem.DrawText("Start Pattern", new Point(20, 27), 0, 2);
		vgaMem.DrawText("End Pattern", new Point(22, 28), 0, 2);
	}

	void Close(object? data)
	{
		int nl = thumbBarPatternLength.Value;

		Status.Flags |= StatusFlags.SongNeedsSave;

		for (int patternIndex = thumbBarStartPattern.Value; patternIndex <= thumbBarEndPattern.Value; patternIndex++)
		{
			var pattern = Song.CurrentSong?.GetPattern(patternIndex, false);

			if (pattern != null)
				pattern.Resize(nl);

			if (patternIndex == _currentPattern)
			{
				Status.Flags |= StatusFlags.NeedUpdate;
				// TODO: move these to the place that shows the dialog, triggered by an event
				//current_row = MIN(current_row, nl - 1);
				//pattern_editor_reposition();
			}
		}
	}
}
