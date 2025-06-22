namespace ChasmTracker.Dialogs;

using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class TimeJumpDialog : Dialog
{
	NumberEntryWidget numEntryMinute;
	NumberEntryWidget numEntrySecond;
	ButtonWidget buttonOK;
	ButtonWidget buttonCancel;

	public TimeJumpDialog()
		: base(new Point(26, 24), new Size(30, 8))
	{
		ActionYes = AcceptDialog();
	}

	protected override void Initialize()
	{
		numEntryMinute = new NumberEntryWidget(new Point(44, 26), 2, 0, 21, new SharedInt());
		numEntrySecond = new NumberEntryWidget(new Point(47, 26), 2, 0, 59, new SharedInt());
		buttonOK = new ButtonWidget(new Point(30, 29), 8, "OK", 4);
		buttonCancel = new ButtonWidget(new Point(42, 29), 8, "Cancel", 2);

		numEntryMinute.HandleUnknownKey += numEntryMinute_HandleUnknownKey;
		numEntryMinute.Reverse = true;

		numEntrySecond.Reverse = true;

		buttonOK.Clicked += AcceptDialog;
		buttonCancel.Clicked += DialogButtonCancel;

		Widgets.Add(numEntryMinute);
		Widgets.Add(numEntrySecond);
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Jump to time:", new Point(30, 26), (0, 2));

		VGAMem.DrawCharacter(':', new Point(46, 26), (3, 0));
		VGAMem.DrawBox(new Point(43, 25), new Point(49, 27), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
	}

	public override bool HandleKey(KeyEvent k)
	{
		if (k.Sym == KeySym.Backspace)
		{
			if (SelectedWidgetIndex == 1 && _timejump_widgets[1].d.numentry.value == 0)
			{
				if (k->state == KEY_RELEASE) widget_change_focus_to(0);
				return true;
			}
		}
		if (k.Sym == KeySym.Colon || k.Sym == KeySym.Semicolon) {
			if (k->state == KEY_RELEASE) {
				if (*selected_widget == 0) {
					widget_change_focus_to(1);
				}
			}
			return true;
		}

		return false;
	}

	void AcceptDialog()
	{
		unsigned long sec;
		int no, np, nr;
		sec = (_timejump_widgets[0].d.numentry.value * 60)
			+ _timejump_widgets[1].d.numentry.value;
		song_get_at_time(sec, &no, &nr);
		set_current_order(no);
		np = current_song->orderlist[no];
		if (np < 200) {
			set_current_pattern(np);
			set_current_row(nr);
			set_page(PAGE_PATTERN_EDITOR);
		}
	}
}
