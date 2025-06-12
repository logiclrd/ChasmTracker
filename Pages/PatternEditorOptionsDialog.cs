namespace ChasmTracker.Pages;

using ChasmTracker.Dialogs;
using ChasmTracker.Songs;
using ChasmTracker.Widgets;

public class PatternEditorOptionsDialog : Dialog
{
	int _lastOctave;
	dialog = dialog_create_custom(10, 18, 60, 26, options_widgets, 8, options_selected_widget,
				      options_draw_const, NULL);
	dialog->action_yes = options_close;
	if (status.flags & CLASSIC_MODE) {
		dialog->action_cancel = options_close;
	} else {
		dialog->action_cancel = options_close_cancel;
	}
	dialog->data = dialog;


	public PatternEditorOptionsDialog()
		: base(new Point(10, 18), new Size(60, 26))
	{
		Data = this;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* options dialog */
	/* the base octave is changed directly when the thumbbar is changed.
	 * anything else can wait until the dialog is closed. */
	protected override void Initialize()
	{
		var thumbBarBaseOctave = new ThumbBarWidget(new Point(40, 23), 2, 0, 8);
		var thumbBarSkip = new ThumbBarWidget(new Point(40, 26), 3, 0, 16);
		var thumbBarRowHighlightMinor = new ThumbBarWidget(new Point(40, 29), 5, 0, 32);
		var thumbBarRowHighlightMajor = new ThumbBarWidget(new Point(40, 32), 17, 0, 128);
		var thumbBarPatternLength = new ThumbBarWidget(new Point(40, 35), 22, 32, 200);
		var toggleButtonCommandValueLink = new ToggleButtonWidget(new Point(40, 38), 8, "Link", 3, groupNumber: 1);
		var toggleButtonCommandValueSplit = new ToggleButtonWidget(new Point(52, 38), 9, "Split", 3, groupNumber: 1);

		thumbBarBaseOctave.Changed += BaseOctaveChanged;

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
			new ButtonWidget(new Point(35, 41), 8, new WidgetNext(5, 0, 7, 7, 7, 0), "Done", 3)
			.AddChangedHandler(Dialog.DialogButtonYes));

		_lastOctave = Keyboard.CurrentOctave;

		thumbBarBaseOctave.Value = _lastOctave;
		thumbBarSkip.Value = AllPages.PatternEditor.SkipValue;
		thumbBarRowHighlightMinor.Value = Song.CurrentSong!.RowHighlightMinor;
		thumbBarRowHighlightMajor.Value = Song.CurrentSong!.RowHighlightMajor;
		thumbBarPatternLength.Value = Song.CurrentSong!.CurrentPattern.Length;

		toggleButtonCommandValueLink.SetState(AllPages.PatternEditor.LinkEffectColumn);
		toggleButtonCommandValueSplit.SetState(!AllPages.PatternEditor.LinkEffectColumn);
	}

	static int options_selected_widget = 0;
	static int options_last_octave = 0;

	static void options_close_cancel(SCHISM_UNUSED void* data)
	{
		kbd_set_current_octave(options_last_octave);
	}
	static void options_close(void* data)
	{
		int old_size, new_size;

		options_selected_widget = ((struct dialog *) data)->selected_widget;

	skip_value = options_widgets[1].d.thumbbar.value;
	current_song->row_highlight_minor = options_widgets[2].d.thumbbar.value;
	current_song->row_highlight_major = options_widgets[3].d.thumbbar.value;
	link_effect_column = !!(options_widgets[5].d.togglebutton.state);
	status.flags |= SONG_NEEDS_SAVE;

	old_size = song_get_pattern(current_pattern, NULL);
	new_size = options_widgets[4].d.thumbbar.value;
	if (old_size != new_size) {
		song_pattern_resize(current_pattern, new_size);
	current_row = MIN(current_row, new_size - 1);
	pattern_editor_reposition();
}
}

static void options_draw_const(void)
{
	draw_text("Pattern Editor Options", 28, 19, 0, 2);
	draw_text("Base octave", 28, 23, 0, 2);
	draw_text("Cursor step", 28, 26, 0, 2);
	draw_text("Row hilight minor", 22, 29, 0, 2);
	draw_text("Row hilight major", 22, 32, 0, 2);
	draw_text("Number of rows in pattern", 14, 35, 0, 2);
	draw_text("Command/Value columns", 18, 38, 0, 2);

	draw_box(39, 22, 42, 24, BOX_THIN | BOX_INNER | BOX_INSET);
	draw_box(39, 25, 43, 27, BOX_THIN | BOX_INNER | BOX_INSET);
	draw_box(39, 28, 45, 30, BOX_THIN | BOX_INNER | BOX_INSET);
	draw_box(39, 31, 57, 33, BOX_THIN | BOX_INNER | BOX_INSET);
	draw_box(39, 34, 62, 36, BOX_THIN | BOX_INNER | BOX_INSET);
}

static void BaseOctaveChanged(void)
{
	kbd_set_current_octave(options_widgets[0].d.thumbbar.value);
}
}