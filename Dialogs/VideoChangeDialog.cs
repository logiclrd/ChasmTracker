using System;

namespace ChasmTracker.Dialogs;

using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class VideoChangeDialog : Dialog
{
	ButtonWidget? buttonOK;
	ButtonWidget? buttonCancel;

	int _countdown;
	DateTime _deadline;

	public VideoChangeDialog()
		: base(new Point(20, 17), new Size(40, 14))
	{
		_countdown = 10;
		_deadline = DateTime.UtcNow.AddSeconds(_countdown);
	}

	protected override void Initialize()
	{
		buttonOK = new ButtonWidget(new Point(28, 28), 8, "OK", 4);
		buttonCancel = new ButtonWidget(new Point(42, 28), 8, "Cancel", 2);

		buttonOK.Clicked += DialogButtonYes;
		buttonCancel.Clicked += DialogButtonNo;

		Widgets.Add(buttonOK);
		Widgets.Add(buttonCancel);
	}

	protected override void SetInitialFocus()
	{
		ChangeFocusTo(buttonCancel!);
	}

	public override void DrawConst()
	{
		if (_deadline != default)
		{
			int newCountdown = (int)(_deadline - DateTime.UtcNow).TotalSeconds;

			if (newCountdown != _countdown)
			{
				_countdown = newCountdown;

				Status.Flags |= StatusFlags.NeedUpdate;

				if (_countdown == 0)
				{
					Destroy();
					DialogButtonCancel();
				}
			}
		}

		VGAMem.DrawText("Your video settings have been changed.", new Point(21, 19), (0, 2));
		VGAMem.DrawText("In " + _countdown + " seconds, your changes will be", new Point(23, 21), (0, 2));
		VGAMem.DrawText("reverted to the last known-good", new Point(21, 22), (0, 2));
		VGAMem.DrawText("settings.", new Point(21, 23), (0, 2));
		VGAMem.DrawText("To use the new video mode, and make", new Point(21, 24), (0, 2));
		VGAMem.DrawText("it default, select OK.", new Point(21, 25), (0, 2));
	}
}
