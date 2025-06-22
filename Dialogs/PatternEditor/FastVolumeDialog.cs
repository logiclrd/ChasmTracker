using System;

namespace ChasmTracker.Dialogs.PatternEditor;

using ChasmTracker.Dialogs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class FastVolumeDialog : Dialog
{
	ThumbBarWidget thumbBarVolumePercent;
	ButtonWidget buttonOK;
	ButtonWidget buttonCancel;

	public FastVolumeDialog(int fastVolumePercent)
		: base(new Point(22, 25), new Size(36, 11))
	{
		thumbBarVolumePercent = new ThumbBarWidget(new Point(33, 30), 11, 10, 90);
		thumbBarVolumePercent.Value = fastVolumePercent;

		buttonOK = new ButtonWidget(new Point(31, 33), 6, "OK", 3);
		buttonOK.Clicked += DialogButtonYes;

		buttonCancel = new ButtonWidget(new Point(41, 33), 6, "Cancel", 1);
		buttonCancel.Clicked += DialogButtonCancel;

		Widgets.Add(thumbBarVolumePercent);
		Widgets.Add(buttonOK);
		Widgets.Add(buttonCancel);

		ActionYes = OK;
		ActionCancel = Cancel;
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Volume Amplification %", new Point(29, 27), (0, 2));
		VGAMem.DrawBox(new Point(32, 29), new Point(44, 31), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
	}

	public event Action<int>? AcceptDialog;

	void OK(object? data)
	{
		AcceptDialog?.Invoke(thumbBarVolumePercent.Value);
		Status.FlashText("Alt-I / Alt-J fast volume changes enabled");
	}

	void Cancel(object? data)
	{
		Status.FlashText("Alt-I / Alt-J fast volume changes not enabled");
	}
}
