using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

namespace ChasmTracker.Dialogs.Samples;

public class ResizeSampleDialog : Dialog
{
	NumberEntryWidget? numberEntryNewLength;
	ButtonWidget? buttonCancel;

	int _startingLength;

	public int NewLength => numberEntryNewLength!.Value;

	public ResizeSampleDialog(int startingLength)
		: base(new Point(26, 22), new Size(29, 11))
	{
		_startingLength = startingLength;
	}

	protected override void Initialize()
	{
		numberEntryNewLength = new NumberEntryWidget(new Point(42, 27), 7, 0, 9999999, new Shared<int>());
		numberEntryNewLength.Value = _startingLength;

		buttonCancel = new ButtonWidget(new Point(36, 30), 6, "Cancel", 1);
		buttonCancel.Clicked += DialogButtonCancel;

		AddWidget(numberEntryNewLength);
		AddWidget(buttonCancel);
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Resize Sample", new Point(34, 24), (3, 2));
		VGAMem.DrawText("New Length", new Point(31, 27), (0, 2));
		VGAMem.DrawBox(new Point(41, 26), new Point(49, 28), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}
}
