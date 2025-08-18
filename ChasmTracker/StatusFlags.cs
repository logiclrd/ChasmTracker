namespace ChasmTracker;

public enum StatusFlags
{
	NeedUpdate = 1 << 0,

	/* is the current palette "backwards"? (used to make the borders look right) */
	InvertedPalette = 1 << 1,

	ModulesDirectoryChanged = 1 << 2,
	SamplesDirectoryChanged = 1 << 3,
	InstrumentsDirectoryChanged = 1 << 4,

	/* if this is set, some stuff behaves differently
	 * (grep the source files for what stuff ;) */
	ClassicMode = 1 << 5,

	/* make a backup file (song.it~) when saving a module? */
	MakeBackups = 1 << 6,
	NumberedBackups = 1 << 7, /* song.it.3~ */

	LazyRedraw = 1 << 8,

	/* this is here if anything is "changed" and we need to whine to
	the user if they quit */
	SongNeedsSave = 1 << 9,

	/* if the software mouse pointer moved.... */
	SoftwareMouseMoved = 1 << 10,

	/* pasting is done by setting a flag here, the main event loop then synthesizes
	 * the various events... after we return
	 *
	 * FIXME this sucks, we should just call clippy directly */
	ClippyPasteSelection = 1 << 11,
	ClippyPasteBuffer = 1 << 12,

	/* if the disko is active */
	DiskWriterActive = 1 << 13,
	DiskWriterActiveForPattern = 1 << 14, /* recording only a single pattern */

	/* mark... set by midi core when received new midi event */
	MIDIEventChanged = 1 << 15,

	/* fontedit */
	StartupFontEdit = 1 << 16,

	/* key hacks -- should go away when keyboard redefinition is possible */
	MetaIsControl = 1 << 17,
	AltGrIsAlt = 1 << 18,

	/* Devi Ever's hack */
	CrayolaMode = 1 << 20,

	NoNetwork = 1 << 22,

	/* Play MIDI events using the same semantics as tracker samples */
	MIDILikeTracker = 1 << 23,

	/* if true, don't stop playing on load, and start playing new song afterward
	(but only if the last song was already playing before loading) */
	PlayAfterLoad = 1 << 24,

	/* --headless passed (generally this should be default with --diskwrite) */
	Headless = 1 << 25,
}
