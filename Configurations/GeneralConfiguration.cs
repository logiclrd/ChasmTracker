using ChasmTracker.VGA;

namespace ChasmTracker.Configurations;

public class GeneralConfiguration : ConfigurationSection
{
	// If these are set to zero, it means to use the
	// system key repeat or the default fallback values.
	public int KeyRepeatDelay;
	public int KeyRepeatRate;

	public string? Font;
	public bool StopOnLoad;
	public TimeFormats TimeFormat = TimeFormats.Default;
	public DateFormats DateFormat = DateFormats.Default;

	public bool MetaIsCtrl;
	public bool AltGrIsAlt;
	public NumLockHandling NumlockSetting = NumLockHandling.Guess;

	public int Palette = 2;
	[ConfigurationKey("palette_cur")]
	public string CurrentPalette = "";

	public override void FinalizeLoad()
	{
		if (CurrentPalette.Length >= 48)
			VGAMem.CurrentPalette.SetFromString(CurrentPalette);

		if ((Palette >= 0) && (Palette < Palettes.Presets.Length))
			VGAMem.CurrentPalette = Palettes.Presets[Palette];
	}

	public override void PrepareToSave()
	{
		StopOnLoad = !Status.Flags.HasFlag(StatusFlags.PlayAfterLoad);

		Palette = VGAMem.CurrentPalette.Index;
		CurrentPalette = VGAMem.CurrentPalette.ToString();
	}
}
