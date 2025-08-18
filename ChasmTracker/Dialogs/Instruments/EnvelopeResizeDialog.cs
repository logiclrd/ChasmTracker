using System.Linq;

namespace ChasmTracker.Dialogs.Instruments;

using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class EnvelopeResizeDialog : Dialog
{
	NumberEntryWidget? numberEntryNewTickLength;
	ButtonWidget? buttonCancel;

	public int NewTickLength => numberEntryNewTickLength!.Value;

	Envelope _env;

	public EnvelopeResizeDialog(Envelope env)
		: base(new Point(26, 22), new Size(29, 11))
	{
		_env = env;
	}

	protected override void Initialize()
	{
		numberEntryNewTickLength = new NumberEntryWidget(new Utility.Point(42, 27), 7, 0, 9999, new Shared<int>());
		numberEntryNewTickLength.Value = _env.Nodes.Last().Tick;

		buttonCancel = new ButtonWidget(new Point(36, 30), 6, "Cancel", 1);
		buttonCancel.Clicked += DialogButtonCancel;

		AddWidget(numberEntryNewTickLength);
		AddWidget(buttonCancel);
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Resize Envelope", new Point(34, 24), (3, 2));
		VGAMem.DrawText("New Length", new Point(31, 27), (0, 2));
		VGAMem.DrawBox(new Point(41, 26), new Point(49, 28), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}
}
