namespace ChasmTracker.Pages;

public class InstrumentLibraryPage : InstrumentFileListPageBase
{
	protected override bool IsLibraryMode => true;

	public InstrumentLibraryPage()
		: base(PageNumbers.InstrumentLibrary, "Instrument Library (Ctrl-F4)")
	{
	}
}
