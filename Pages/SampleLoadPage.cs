namespace ChasmTracker.Pages;

public class SampleLoadPage : SampleFileListPageBase
{
	public SampleLoadPage()
		: base("Load Sample", PageNumbers.SampleLoad, HelpTexts.Global)
	{
	}

	public override void SetPage()
	{
		SetLibraryMode(false);
		CommonSetPage();
	}
}
