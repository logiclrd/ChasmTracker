using System;

namespace ChasmTracker.Dialogs;

using ChasmTracker.Input;
using ChasmTracker.Pages;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class TimeJumpDialog : Dialog
{
	NumberEntryWidget? numEntryMinute;
	NumberEntryWidget? numEntrySecond;
	ButtonWidget? buttonOK;
	ButtonWidget? buttonCancel;

	public TimeJumpDialog()
		: base(new Point(26, 24), new Size(30, 8))
	{
		ActionYes = _ => AcceptDialog();
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

	void numEntryMinute_HandleUnknownKey(KeyEvent k)
	{
		HandleKey(k);
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
			if (SelectedWidgetIndex == 1 && numEntrySecond!.Value == 0)
			{
				if (k.State == KeyState.Release)
					Page.ChangeFocusTo(0);
				return true;
			}
		}
		if (k.Sym == KeySym.Colon || k.Sym == KeySym.Semicolon)
		{
			if (k.State == KeyState.Release)
			{
				if ((Page.SelectedActiveWidgetIndex != null) && (Page.SelectedActiveWidgetIndex == 0))
					Page.ChangeFocusTo(1);
			}

			return true;
		}

		return false;
	}

	void AcceptDialog()
	{
		int second = numEntryMinute!.Value * 60 + numEntrySecond!.Value;

		var (order, row) = Song.CurrentSong.GetAtTime(TimeSpan.FromSeconds(second));

		AllPages.OrderList.CurrentOrder = order;

		if (order < Song.CurrentSong.OrderList.Count)
		{
			int pattern = Song.CurrentSong.OrderList[order];

			if (pattern < Song.CurrentSong.Patterns.Count)
			{
				AllPages.PatternEditor.CurrentPattern = pattern;
				AllPages.PatternEditor.CurrentRow = row;

				Page.SetPage(PageNumbers.PatternEditor);
			}
		}
	}
}
