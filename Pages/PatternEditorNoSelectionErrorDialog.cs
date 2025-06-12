using ChasmTracker;
using ChasmTracker.Dialogs;

public class PatternEditorNoSelectionErrorDialog : Dialog
{
	public PatternEditorNoSelectionErrorDialog()
		: base(DialogTypes.OK, "    No block is marked    ", null, null, 0, null)
	{
	}
}
