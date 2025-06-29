namespace ChasmTracker.Configurations;

public class GeneralConfiguration
{
	public int KeyRepeatDelay;
	public int KeyRepeatRate;
	public string? Font;
	public bool StopOnLoad;
	public TimeFormats TimeFormat = TimeFormats.Default;
	public DateFormats DateFormat = DateFormats.Default;

	public bool MetaIsCtrl;
	public bool AltGrIsAlt;

	[ConfigurationKey("palette_cur")]
	public int CurrentPalette;
}
