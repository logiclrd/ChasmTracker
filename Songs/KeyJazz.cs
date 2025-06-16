namespace ChasmTracker.Songs;

public static class KeyJazz
{
	/* Sample/inst slots 1+ are used "normally"; the sample loader uses slot #0 for preview playback -- but reports
	KEYJAZZ_INST_FAKE to keydown/up, since zero conflicts with the standard "use previous sample for this channel"
	behavior which is normally internal, but is exposed on the pattern editor where it's possible to explicitly
	select sample #0. (note: this is a hack to work around another hack) */
	public const int CurrentChannel = 0;
	// For automatic channel allocation when playing chords in the instrument editor.
	public const int AutomaticChannel = -1;
	public const int NoInstrument = -1;
	public const int DefaultVolume = -1;
	public const int FakeInstrument = -2;
}
