namespace ChasmTracker.Dialogs.Samples;

using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class AmplifyDialog : Dialog
{
	ThumbBarWidget? thumbBarPercent;
	ButtonWidget? buttonOK;
	ButtonWidget? buttonCancel;

	int _sampleNumber;

	public int Percent => thumbBarPercent!.Value;

	public AmplifyDialog(int sampleNumber)
		: base(new Point(9, 25), new Size(61, 11))
	{
		_sampleNumber = sampleNumber;
	}

	protected override void Initialize()
	{
		int percent = 100;

		if (Song.CurrentSong.GetSample(_sampleNumber) is SongSample sample)
			percent = SampleEditOperations.GetAmplifyAmount(sample);

		percent = percent.Clamp(0, 400);

		thumbBarPercent = new ThumbBarWidget(new Point(13, 30), 51, 0, 400);
		buttonOK = new ButtonWidget(new Point(31, 33), 6, "OK", 3);
		buttonCancel = new ButtonWidget(new Point(41, 33), 6, "Cancel", 1);

		buttonOK.Clicked += DialogButtonYes;
		buttonCancel.Clicked += DialogButtonCancel;

		thumbBarPercent.Value = percent;

		Widgets.Add(thumbBarPercent);
		Widgets.Add(buttonOK);
		Widgets.Add(buttonCancel);
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Sample Amplification %", new Point(29, 27), (0, 2));
		VGAMem.DrawBox(new Point(12, 29), new Point(64, 31), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
	}
}
