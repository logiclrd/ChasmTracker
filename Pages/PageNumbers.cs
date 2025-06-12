namespace ChasmTracker;

public enum PageNumbers
{
	Blank,
	Help,
	About,
	Log,

	PatternEditor,
	SampleList,
	// InstrumentList doesn't exist
	Info,

	Config,
	Preferences,

	MIDI,
	MIDIOutput,

	ModuleLoad,
	ModuleSave,
	ModuleExport,

	OrderListPanning,
	OrderListVolumes,

	SongVariables,
	Message,

	TimeInformation,

	InstrumentList,
	/* don't use these directly with set_page */
	InstrumentListGeneral,
	InstrumentListVolume,
	InstrumentListPanning,
	InstrumentListPitch,

	SampleLoad,
	SampleLibrary,
	InstrumentLoad,
	InstrumentLibrary,

	PaletteEditor,
	FontEditor,

	Waterfall,

	MaxValue,
}
