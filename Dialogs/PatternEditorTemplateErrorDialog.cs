namespace ChasmTracker.Dialogs;

using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class PatternEditorTemplateErrorDialog : Dialog
{
	ButtonWidget? buttonOK;

	public PatternEditorTemplateErrorDialog()
		: base(new Point(20, 23), new Size(40, 12))
	{
	}

	protected override void Initialize()
	{
		buttonOK = new ButtonWidget(new Point(36, 32), 6, "OK", 3);

		buttonOK.Clicked += DialogButtonYes;

		Widgets.Add(buttonOK);
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Template Error", new Point(33, 25), (0, 2));
		VGAMem.DrawText("No note in the top left position", new Point(23, 27), (0, 2));
		VGAMem.DrawText("of the clipboard on which to", new Point(25, 28), (0, 2));
		VGAMem.DrawText("base translations.", new Point(31, 29), (0, 2));
	}
}
