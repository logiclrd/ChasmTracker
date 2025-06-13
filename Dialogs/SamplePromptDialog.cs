using System.Security.Cryptography.X509Certificates;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;
using X11;

namespace ChasmTracker.Dialogs;

public class SamplePromptDialog : NumberPromptDialog
{
	public string Secondary;

	ButtonWidget? buttonCancel;

	public SamplePromptDialog(string title, string prompt)
		: base(title, '\0')
	{
		Secondary = prompt;

		_titlePosition = new Point((81 - Title.Length) / 2, 25);
		_secondaryPosition = new Point(41 - Secondary.Length, 27);

		ActionYes = FinishWithValue99;
	}

	protected override void Initialize()
	{
		textEntryInput = new TextEntryWidget(new Point(42, 27), 3, "", 2);

		buttonCancel = new ButtonWidget(new Point(36, 30), 6, "Cancel", 1);
		buttonCancel.Clicked += DialogButtonCancel;

		Widgets.Add(textEntryInput);
		Widgets.Add(buttonCancel);
	}

	Point _titlePosition;
	Point _secondaryPosition;

	public void FinishWithValue99(object? data)
		=> Finish?.Invoke(textEntryInput.Text.Parse99());

	public override void DrawConst(VGAMem vgaMem)
	{
		vgaMem.DrawText(Title, _titlePosition, 0, 2);
		vgaMem.DrawText(Secondary, _secondaryPosition, 0, 2);

		vgaMem.DrawBox(
			textEntryInput.Position.Advance(-1, -1),
			textEntryInput.Position.Advance(textEntryInput.Width, 1),
			BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}
}
