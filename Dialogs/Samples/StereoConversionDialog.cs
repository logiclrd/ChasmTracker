using System;

namespace ChasmTracker.Dialogs.Samples;

using ChasmTracker.Input;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class StereoConversionDialog : Dialog
{
	ButtonWidget? buttonLeft;
	ButtonWidget? buttonBoth;
	ButtonWidget? buttonRight;

	public event Action<StereoConversionSelection>? SelectionMade;

	public StereoConversionDialog()
		: base(new Point(24, 25), new Size(33, 8))
	{
	}

	protected override void Initialize()
	{
		buttonLeft = new ButtonWidget(new Point(27, 30), 6, "Left", 2);
		buttonBoth = new ButtonWidget(new Point(37, 30), 6, "Both", 2);
		buttonRight = new ButtonWidget(new Point(47, 30), 6, "Right", 1);

		buttonLeft.Clicked += () => SelectionMade?.Invoke(StereoConversionSelection.Left);
		buttonBoth.Clicked += () => SelectionMade?.Invoke(StereoConversionSelection.Both);
		buttonRight.Clicked += () => SelectionMade?.Invoke(StereoConversionSelection.Right);

		AddWidget(buttonLeft);
		AddWidget(buttonBoth);
		AddWidget(buttonRight);

		SelectedWidgetIndex.Value = 1;
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Loading Stereo Sample", new Point(30, 27), (0, 2));
	}

	public override bool HandleKey(KeyEvent k)
	{
		if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
			return false;

		/* trap the default dialog keys - we don't want to escape this dialog without running something */
		switch (k.Sym)
		{
			case KeySym.Return:
				Console.WriteLine("why am I here");
				goto case KeySym.Escape;
			case KeySym.Escape: case KeySym.o: case KeySym.c:
				return true;
			case KeySym.l:
				if (k.State == KeyState.Release)
					SelectionMade?.Invoke(StereoConversionSelection.Left);
				return true;
			case KeySym.r:
				if (k.State == KeyState.Release)
					SelectionMade?.Invoke(StereoConversionSelection.Right);
				return true;
			case KeySym.s:
			case KeySym.b:
				if (k.State == KeyState.Release)
					SelectionMade?.Invoke(StereoConversionSelection.Both);
				return true;
		}

		return false;
	}

}
