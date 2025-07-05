using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ChasmTracker.Pages;

using ChasmTracker.Widgets;

public static class AllPages
{
	public static BlankPage Blank = new BlankPage();
	public static HelpPage Help = new HelpPage();
	public static AboutPage About = new AboutPage();
	public static LogPage Log = new LogPage();

	public static PatternEditorPage PatternEditor = new PatternEditorPage();
	public static SampleListPage SampleList = new SampleListPage();
	// InstrumentList doesn't exist
	public static InfoPage Info = new InfoPage();

	public static ConfigurationPage Config = new ConfigurationPage();
	public static PreferencesPage Preferences = new PreferencesPage();

	public static MIDIPage MIDI = new MIDIPage();
	public static MIDIOutputPage MIDIOutput = new MIDIOutputPage();

	public static ModuleLoadPage ModuleLoad = new ModuleLoadPage();
	public static ModuleSavePage ModuleSave = new ModuleSavePage();
	public static ModuleExportPage ModuleExport = new ModuleExportPage();

	public static OrderListPanningPage OrderListPanning = new OrderListPanningPage();
	public static OrderListVolumesPage OrderListVolumes = new OrderListVolumesPage();

	public static SongVariablesPage SongVariables = new SongVariablesPage();
	public static MessagePage Message = new MessagePage();

	public static TimeInformationPage TimeInformation = new TimeInformationPage();

	/* don't use these directly with set_page */
	public static InstrumentListGeneralPage InstrumentListGeneral = new InstrumentListGeneralPage();
	public static InstrumentListVolumePage InstrumentListVolume = new InstrumentListVolumePage();
	public static InstrumentListPanningPage InstrumentListPanning = new InstrumentListPanningPage();
	public static InstrumentListPitchPage InstrumentListPitch = new InstrumentListPitchPage();

	public static SampleLoadPage SampleLoad = new SampleLoadPage();
	public static SampleLibraryPage SampleLibrary = new SampleLibraryPage();
	public static InstrumentLoadPage InstrumentLoad = new InstrumentLoadPage();
	public static InstrumentLibraryPage InstrumentLibrary = new InstrumentLibraryPage();

	public static PaletteEditorPage PaletteEditor = new PaletteEditorPage();
	public static FontEditorPage FontEditor = new FontEditorPage();

	public static WaterfallPage Waterfall = new WaterfallPage();

	// Updated dynamically every time an InstrumentList__ page is set.
	public static InstrumentListPage InstrumentList = InstrumentListGeneral;
	public static OrderListPage OrderList = OrderListPanning;

	static Page[] s_pages;
	static Dictionary<PageNumbers, Page> s_byPageNumber;

	static AllPages()
	{
		s_pages = typeof(AllPages).GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(field => typeof(Page).IsAssignableFrom(field.FieldType))
			.Select(field => field.GetValue(null))
			.OfType<Page>()
			.ToArray();

		s_byPageNumber = s_pages.ToDictionary(page => page.PageNumber);

		foreach (var page in s_pages)
		{
			ToggleButtonWidget.BuildGroups(page.Widgets);
			WidgetNext.Initialize(page.Widgets);
		}
	}

	public static IEnumerable<Page> EnumeratePages()
	{
		return s_pages;
	}

	public static Page ByPageNumber(PageNumbers pageNumber)
	{
		if (s_byPageNumber.TryGetValue(pageNumber, out var page))
			return page;

		throw new Exception("Invalid page number: " + pageNumber);
	}
}
