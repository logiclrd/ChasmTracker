using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

namespace ChasmTracker.Dialogs.Samples;

public class ResampleSampleDialog : Dialog
{
	NumberEntryWidget? numberEntryNewC5Speed;
	ButtonWidget? buttonCancel;

	int _startingC5Speed;

	public int NewC5Speed => numberEntryNewC5Speed!.Value;

	public ResampleSampleDialog(int startingC5Speed)
		: base(new Point(26, 22), new Size(28, 11))
	{
		_startingC5Speed = startingC5Speed;
	}

	protected override void Initialize()
	{
		numberEntryNewC5Speed = new NumberEntryWidget(new Point(44, 27), 7, 0, 9999999, new Shared<int>());
		numberEntryNewC5Speed.Value = _startingC5Speed;

		buttonCancel = new ButtonWidget(new Point(37, 30), 6, "Cancel", 1);
		buttonCancel.Clicked += DialogButtonCancel;

		AddWidget(numberEntryNewC5Speed);
		AddWidget(buttonCancel);
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Resample Sample", new Point(33, 24), (3, 2));
		VGAMem.DrawText("New Sample Rate", new Point(28, 27), (0, 2));
		VGAMem.DrawBox(new Point(43, 26), new Point(51, 28), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}
}
