using ChasmTracker.VGA;

namespace ChasmTracker.Configurations;

public class GeneralConfiguration : ConfigurationSection
{
	// If these are set to zero, it means to use the
	// system key repeat or the default fallback values.
	public int KeyRepeatDelay;
	public int KeyRepeatRate;

	public string Font = "font.cfg";
	public bool StopOnLoad;
	public bool ClassicMode;
	public bool MakeBackups;
	public bool NumberedBackups;
	[SerializeEnumAsInt]
	public TrackerTimeDisplay TimeDisplay = TrackerTimeDisplay.PlayElapsed;
	[SerializeEnumAsInt, ConfigurationKey("vis_style")]
	public TrackerVisualizationStyle VisualizationStyle = TrackerVisualizationStyle.Oscilloscope;

	public bool AccidentalsAsFlats = false;

	public TimeFormats TimeFormat = TimeFormats.Default;
	public DateFormats DateFormat = DateFormats.Default;

	[ConfigurationKey("meta_is_ctrl")]
	public bool MetaIsControl = false;
	public bool AltGrIsAlt = true;
	public NumLockHandling NumlockSetting = NumLockHandling.Guess;

	public bool MIDILikeTracker = false;

	public int Palette = 2;
	[ConfigurationKey("palette_cur")]
	public string CurrentPalette = "";
}
