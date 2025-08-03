namespace ChasmTracker.Configurations;

public class GeneralConfiguration : ConfigurationSection
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

	public override void PrepareToSave()
	{
		StopOnLoad = !Status.Flags.HasFlag(StatusFlags.PlayAfterLoad);
	}
}
