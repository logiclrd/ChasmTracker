using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ChasmTracker.Pages;

public static class AllPages
{
	public static Page ByPageNumber(PageNumbers pageNumber)
	{
		foreach (var field in typeof(AllPages).GetFields(BindingFlags.Public | BindingFlags.Static))
		{
			if ((field.GetValue(default) is Page page)
			 && (page.PageNumber == pageNumber))
				return page;
		}

		throw new Exception("Invalid page number: " + pageNumber);
	}

	static Page[]? s_pages;

	public static IEnumerable<Page> EnumeratePages()
	{
		if (s_pages == null)
		{
			s_pages = typeof(AllPages).GetFields(BindingFlags.Public | BindingFlags.Static)
				.Where(field => typeof(Page).IsAssignableFrom(field.FieldType))
				.Select(field => field.GetValue(null))
				.OfType<Page>()
				.ToArray();
		}

		return s_pages;
	}

	public static BlankPage Blank = new BlankPage();
	public static HelpPage Help = new HelpPage();
	public static AboutPage About = new AboutPage();
	public static LogPage Log = new LogPage();

	public static PatternEditorPage PatternEditor = new PatternEditorPage();
	public static SampleListPage SampleList = new SampleListPage();
	// InstrumentList doesn't exist
	public static InfoPage Info = new InfoPage();

	public static ConfigPage Config;
	public static PreferencesPage Preferences;

	public static MIDIPage MIDI;
	public static MIDIOutputPage MIDIOutput;

	public static ModuleLoadPage ModuleLoad;
	public static ModuleSavePage ModuleSave;
	public static ModuleExportPage ModuleExport;

	public static OrderListPanningPage OrderListPanning;
	public static OrderListVolumesPage OrderListVolumes;

	public static SongVariablesPage SongVariables;
	public static MessagePage Message;

	public static TimeInformationPage TimeInformation;

	/* don't use these directly with set_page */
	public static InstrumentListGeneralPage InstrumentListGeneral = new InstrumentListGeneralPage();
	public static InstrumentListVolumePage InstrumentListVolume = new InstrumentListVolumePage();
	public static InstrumentListPanningPage InstrumentListPanning = new InstrumentListPanningPage();
	public static InstrumentListPitchPage InstrumentListPitch = new InstrumentListPitchPage();

	public static SampleLoadPage SampleLoad;
	public static SampleLibraryPage SampleLibrary;
	public static InstrumentLoadPage InstrumentLoad;
	public static InstrumentLibraryPage InstrumentLibrary;

	public static PaletteEditorPage PaletteEditor;
	public static FontEditorPage FontEditor;

	public static WaterfallPage Waterfall;

	// Updated dynamically every time an InstrumentList__ page is set.
	public static InstrumentListPage InstrumentList = InstrumentListGeneral;
	public static OrderListPage OrderList = OrderListPanning;
}
