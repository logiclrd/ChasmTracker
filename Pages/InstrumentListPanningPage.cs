namespace ChasmTracker.Pages;

using ChasmTracker.Widgets;

public class InstrumentListPanningPage : InstrumentListPage
{
	public InstrumentListPanningPage()
		: base(PageNumbers.InstrumentListPanning)
	{
		InitializeSubPageOf(AllPages.InstrumentList);
	}
}
