using System;

namespace ChasmTracker.Dialogs.PatternEditor;

using ChasmTracker.Input;
using ChasmTracker.Pages;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class OptionsDialog : Dialog
{
	int _lastOctave;

	public int LastOctave => _lastOctave;

	ThumbBarWidget? thumbBarBaseOctave;
	ThumbBarWidget? thumbBarSkip;
	ThumbBarWidget? thumbBarRowHighlightMinor;
	ThumbBarWidget? thumbBarRowHighlightMajor;
	ThumbBarWidget? thumbBarPatternLength;
	ToggleButtonWidget? toggleButtonCommandValueLink;
	ToggleButtonWidget? toggleButtonCommandValueSplit;

	public int Skip => thumbBarSkip!.Value;
	public int RowHighlightMinor => thumbBarRowHighlightMinor!.Value;
	public int RowHighlightMajor => thumbBarRowHighlightMajor!.Value;
	public bool CommandValueLink => toggleButtonCommandValueLink!.State;
	public int PatternLength => thumbBarPatternLength!.Value;

	public event Action? ApplyOptions;
	public event Action? RevertOptions;

	static int s_selectedWidgetIndex;

	public OptionsDialog()
		: base(new Point(10, 18), new Size(60, 26))
	{
		SelectedWidgetIndex.Value = s_selectedWidgetIndex;

		ActionYes = Close;
		if (Status.Flags.HasFlag(StatusFlags.ClassicMode))
			ActionCancel = Close;
		else
			ActionCancel = CloseCancel;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* options dialog */
	/* the base octave is changed directly when the thumbbar is changed.
	 * anything else can wait until the dialog is closed. */
	protected override void Initialize()
	{
		thumbBarBaseOctave = new ThumbBarWidget(new Point(40, 23), 2, 0, 8);
		thumbBarSkip = new ThumbBarWidget(new Point(40, 26), 3, 0, 16);
		thumbBarRowHighlightMinor = new ThumbBarWidget(new Point(40, 29), 5, 0, 32);
		thumbBarRowHighlightMajor = new ThumbBarWidget(new Point(40, 32), 17, 0, 128);
		thumbBarPatternLength = new ThumbBarWidget(new Point(40, 35), 22, 32, 200);
		toggleButtonCommandValueLink = new ToggleButtonWidget(new Point(40, 38), 8, "Link", 3, groupNumber: 1);
		toggleButtonCommandValueSplit = new ToggleButtonWidget(new Point(52, 38), 9, "Split", 3, groupNumber: 1);

		thumbBarBaseOctave.Changed += thumbBarBaseOctave_Changed;

		Widgets.Add(thumbBarBaseOctave);
		Widgets.Add(thumbBarSkip);
		Widgets.Add(thumbBarRowHighlightMinor);
		Widgets.Add(thumbBarRowHighlightMajor);

		/* Although patterns as small as 1 row can be edited properly (as of c759f7a0166c), I have
		discovered it's a bit annoying to hit 'home' here expecting to get 32 rows but end up with
		just one row instead. so I'll allow editing these patterns, but not really provide a way to
		set the size, at least until I decide how to present the option nonintrusively. */
		Widgets.Add(thumbBarPatternLength);

		Widgets.Add(toggleButtonCommandValueLink);
		Widgets.Add(toggleButtonCommandValueSplit);

		Widgets.Add(
			new ButtonWidget(new Point(35, 41), 8, "Done", 3)
			.AddChangedHandler(Dialog.DialogButtonYes));

		_lastOctave = Keyboard.CurrentOctave;

		var pattern = Song.CurrentSong?.GetPattern(AllPages.PatternEditor.CurrentPattern);

		thumbBarBaseOctave.Value = _lastOctave;
		thumbBarSkip.Value = AllPages.PatternEditor.SkipValue;
		thumbBarRowHighlightMinor.Value = Song.CurrentSong!.RowHighlightMinor;
		thumbBarRowHighlightMajor.Value = Song.CurrentSong!.RowHighlightMajor;
		thumbBarPatternLength.Value = pattern?.Rows?.Count ?? 64;

		toggleButtonCommandValueLink.SetState(AllPages.PatternEditor.LinkEffectColumn);
		toggleButtonCommandValueSplit.SetState(!AllPages.PatternEditor.LinkEffectColumn);
	}

	void thumbBarBaseOctave_Changed()
	{
		Keyboard.CurrentOctave = thumbBarBaseOctave!.Value;
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Pattern Editor Options", new Point(28, 19), (0, 2));
		VGAMem.DrawText("Base octave", new Point(28, 23), (0, 2));
		VGAMem.DrawText("Cursor step", new Point(28, 26), (0, 2));
		VGAMem.DrawText("Row hilight minor", new Point(22, 29), (0, 2));
		VGAMem.DrawText("Row hilight major", new Point(22, 32), (0, 2));
		VGAMem.DrawText("Number of rows in pattern", new Point(14, 35), (0, 2));
		VGAMem.DrawText("Command/Value columns", new Point(18, 38), (0, 2));

		VGAMem.DrawBox(new Point(39, 22), new Point(42, 24), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(39, 25), new Point(43, 27), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(39, 28), new Point(45, 30), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(39, 31), new Point(57, 33), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(39, 34), new Point(62, 36), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
	}

	void CloseCancel()
	{
		RevertOptions?.Invoke();
	}

	void Close()
	{
		s_selectedWidgetIndex = SelectedWidgetIndex;

		ApplyOptions?.Invoke();

		Status.Flags |= StatusFlags.SongNeedsSave;
	}
}
