using System;

namespace ChasmTracker.Dialogs;

using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class NumberPromptDialog : Dialog
{
	public string Title;

	public Action<int>? Finish;

	protected TextEntryWidget? textEntryInput;

	string _initialText;

	const int Y = 26; // an indisputable fact of life

	static (Point Position, Size Size, int EntryX) ComputePositionAndSize(string prompt)
	{
		/* Dialog is made up of border, padding (2 left, 1 right), frame around the text entry, the entry
		itself, and the prompt; the text entry is offset from the left of the dialog by 4 chars (padding +
		borders) plus the length of the prompt. */
		int dialogWidth = 2 + 3 + 2 + 4 + prompt.Length;
		int dialogX = (80 - dialogWidth) / 2;
		int entryX = dialogX + 4 + prompt.Length;

		return (new Point(dialogX, Y - 2), new Size(dialogWidth, 5), entryX);
	}

	static Point ComputePosition(string prompt)
		=> ComputePositionAndSize(prompt).Position;

	static Size ComputeSize(string prompt)
		=> ComputePositionAndSize(prompt).Size;

	public NumberPromptDialog(string prompt, char initialCharacter)
		: base(ComputePosition(prompt), ComputeSize(prompt))
	{
		Title = prompt;

		_initialText = (initialCharacter == '\0') ? "" : initialCharacter.ToString();
	}

	protected override void Initialize()
	{
		textEntryInput = new TextEntryWidget(new Point(ComputePositionAndSize(Title).EntryX, Y), 4, Title, 3);

		textEntryInput.Activated += textEntryInput_Activated;
		textEntryInput.CursorPosition = _initialText.Length;

		AddWidget(textEntryInput);
	}

	public override void DrawConst()
	{
		VGAMem.DrawText(Title, textEntryInput!.Position.Advance(-Title.Length - 1), (3, 2));
		VGAMem.DrawBox(textEntryInput.Position.Advance(-1, -1), textEntryInput.Position.Advance(textEntryInput.Size.Width, 1), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}

	/* since this dialog might be called from another dialog as well as from a page, it can't use the
	normal dialog_yes handler -- it needs to destroy the prompt dialog first so that ACTIVE_WIDGET
	points to whatever thumbbar actually triggered the dialog box. */
	void textEntryInput_Activated()
	{
		Dialog.Destroy();

		if (int.TryParse(textEntryInput!.Text, out var n))
			Finish?.Invoke(n);
	}
}
