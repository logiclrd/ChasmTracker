namespace ChasmTracker.Pages;

public class SampleLibraryPage : SampleFileListPageBase
{
	public override void SetPage()
	{
		SetLibraryMode(true);
		CommonSetPage();
	}
}
