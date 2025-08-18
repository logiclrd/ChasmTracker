namespace ChasmTracker.Pages;

public class InstrumentLoadPage : InstrumentFileListPageBase
{
	protected override bool IsLibraryMode => false;

	public InstrumentLoadPage()
		: base(PageNumbers.InstrumentLoad, "Load Instrument")
	{
	}
}
