using System;

namespace ChasmTracker.Pages;

using ChasmTracker.Dialogs;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class PatternEditorVaryCommandDialog : Dialog
{
	ThumbBarWidget thumbBarVaryDepth;
	ButtonWidget buttonOK;
	ButtonWidget buttonCancel;

	public PatternEditorVaryCommandDialog(int varyDepth)
		: base(new Point(22, 25), new Size(36, 11))
	{
		thumbBarVaryDepth = new ThumbBarWidget(new Point(26, 30), 26, 0, 50);
		thumbBarVaryDepth.Value = varyDepth;

		buttonOK = new ButtonWidget(new Point(31, 33), 6, "OK", 3);
		buttonOK.Changed += DialogButtonYes;

		buttonCancel = new ButtonWidget(new Point(41, 33), 6, "Cancel", 1);
		buttonCancel.Changed += DialogButtonCancel;

		Widgets.Add(thumbBarVolumePercent);
		Widgets.Add(buttonOK);
		Widgets.Add(buttonCancel);

		ActionYes = OK;
	}

	public override void DrawConst(VGAMem vgaMem)
	{
		vgaMem.DrawText("Vary depth limit %", new Point(31, 27), 0, 2);
		vgaMem.DrawBox(new Point(25, 29), new Point(52, 31), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
	}

	public event Action<int>? AcceptDialog;

	void OK(object? data)
	{
		AcceptDialog?.Invoke(thumbBarVaryDepth.Value);
	}
}
