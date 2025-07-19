namespace ChasmTracker.Dialogs.Samples;

using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class TextSynthDialog : Dialog
{
	TextEntryWidget? textEntryEntry;
	ButtonWidget? buttonOK;
	ButtonWidget? buttonCancel;

	public string Entry => textEntryEntry!.Text;

	public TextSynthDialog()
		: base(new Point(9, 25), new Size(61, 11))
	{
	}

	protected override void Initialize()
	{
		// TODO copy the current sample into the entry?

		textEntryEntry = new TextEntryWidget(new Point(13, 30), 53, "", 65535);
		buttonOK = new ButtonWidget(new Point(31, 33), 6, "OK", 3);
		buttonCancel = new ButtonWidget(new Point(41, 33), 6, "Cancel", 1);

		buttonOK.Clicked += DialogButtonYes;
		buttonCancel.Clicked += DialogButtonCancel;

		Widgets.Add(textEntryEntry);
		Widgets.Add(buttonOK);
		Widgets.Add(buttonCancel);
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Enter a text string (e.g. ABCDCB for a triangle-wave)", new Point(13, 27), (0, 2));
		VGAMem.DrawBox(new Point(12, 29), new Point(66, 31), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
	}
}
