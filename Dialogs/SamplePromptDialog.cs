namespace ChasmTracker.Dialogs;

using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

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

	public override void DrawConst()
	{
		VGAMem.DrawText(Title, _titlePosition, (0, 2));
		VGAMem.DrawText(Secondary, _secondaryPosition, (0, 2));

		VGAMem.DrawBox(
			textEntryInput.Position.Advance(-1, -1),
			textEntryInput.Position.Advance(textEntryInput.Size.Width, 1),
			BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}
}
