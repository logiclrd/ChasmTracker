using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

namespace ChasmTracker.Dialogs.Instruments;

public class EnvelopeADSRDialog : Dialog
{
	ThumbBarWidget? thumbBarAttack;
	ThumbBarWidget? thumbBarDecay;
	ThumbBarWidget? thumbBarSustain;
	ThumbBarWidget? thumbBarRelease;
	ButtonWidget? buttonCancel;

	public int Attack => thumbBarAttack!.Value;
	public int Decay => thumbBarDecay!.Value;
	public int Sustain => thumbBarSustain!.Value;
	public int Release => thumbBarRelease!.Value;

	public EnvelopeADSRDialog()
		: base(new Point(25, 21), new Size(31, 12))
	{
		thumbBarAttack = new ThumbBarWidget(new Point(34, 24), 17, 0, 128);
		thumbBarDecay = new ThumbBarWidget(new Point(34, 25), 17, 0, 128);
		thumbBarSustain = new ThumbBarWidget(new Point(34, 26), 17, 0, 128);
		thumbBarRelease = new ThumbBarWidget(new Point(34, 27), 17, 0, 128);
		buttonCancel = new ButtonWidget(new Point(36, 30), 6, "Cancel", 1);

		buttonCancel.Clicked += DialogButtonCancel;

		Widgets.Add(thumbBarAttack);
		Widgets.Add(thumbBarDecay);
		Widgets.Add(thumbBarSustain);
		Widgets.Add(thumbBarRelease);
		Widgets.Add(buttonCancel);
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Envelope Generator", new Point(32, 22), (0, 2));
		VGAMem.DrawText("Attack", new Point(27, 24), (0, 2));
		VGAMem.DrawText("Decay", new Point(28, 25), (0, 2));
		VGAMem.DrawText("Sustain", new Point(26, 26), (0, 2));
		VGAMem.DrawText("Release", new Point(26, 27), (0, 2));

		VGAMem.DrawBox(new Point(33, 23), new Point(51, 28), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}
}
