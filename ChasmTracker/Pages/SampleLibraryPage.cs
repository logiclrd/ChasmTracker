namespace ChasmTracker.Pages;

public class SampleLibraryPage : SampleFileListPageBase
{
	public SampleLibraryPage()
		: base("Sample Library (Ctrl-F3)", PageNumbers.SampleLibrary, HelpTexts.Global)
	{
	}

	public override void SetPage()
	{
		SetLibraryMode(true);
		CommonSetPage();
	}
}
