using System;

namespace ChasmTracker.Dialogs.PatternEditor;

using ChasmTracker.Dialogs;
using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class VolumeAmplifyDialog : Dialog
{
	ThumbBarWidget thumbBarVolumePercent;
	ButtonWidget buttonOK;
	ButtonWidget buttonCancel;

	public VolumeAmplifyDialog(int volumePercent)
		: base(new Point(22, 25), new Size(36, 11))
	{
		thumbBarVolumePercent = new ThumbBarWidget(new Point(26, 30), 26, 0, 200);
		thumbBarVolumePercent.Value = volumePercent;

		buttonOK = new ButtonWidget(new Point(31, 33), 6, "OK", 3);
		buttonOK.Clicked += DialogButtonYes;

		buttonCancel = new ButtonWidget(new Point(41, 33), 6, "Cancel", 1);
		buttonCancel.Clicked += DialogButtonCancel;

		Widgets.Add(thumbBarVolumePercent);
		Widgets.Add(buttonOK);
		Widgets.Add(buttonCancel);

		ActionYes = OK;
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Volume Amplification %", new Point(29, 27), (0, 2));
		VGAMem.DrawBox(new Point(25, 29), new Point(52, 31), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
	}

	public override bool HandleKey(KeyEvent keyEvent)
	{
		if ((keyEvent.State == KeyState.Press)
		 && keyEvent.Modifiers.HasFlag(KeyMod.Alt)
		 && keyEvent.Sym == KeySym.j)
		{
			DialogButtonYes();
			return true;
		}

		return false;
	}

	public event Action<int>? AcceptDialog;

	void OK(object? data)
	{
		AcceptDialog?.Invoke(thumbBarVolumePercent.Value);
	}
}
