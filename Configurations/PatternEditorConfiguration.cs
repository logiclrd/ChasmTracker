using ChasmTracker.Pages;

namespace ChasmTracker.Configurations;

public class PatternEditorConfiguration : ConfigurationSection
{
	public bool LinkEffectColumn = false;
	public bool DrawDivisions = true;
	public bool CentraliseCursor = false;
	public bool HighlightCurrentRow = false;
	[SerializeEnumAsInt]
	public PatternEditorMask EditCopyMask = PatternEditorMask.Note | PatternEditorMask.Instrument | PatternEditorMask.Volume;
	public int VolumePercent = 100;
	public int FastVolumePercent = 67;
	public bool FastVolumeMode = false;
	[ConfigurationKey("keyjazz_noteoff")]
	public bool KeyJazzNoteOff = false;
	[ConfigurationKey("keyjazz_write_noteoff")]
	public bool KeyJazzWriteNoteOff = false;
	[ConfigurationKey("keyjazz_repeat")]
	public bool KeyJazzRepeat = false;
	[ConfigurationKey("keyjazz_capslock")]
	public bool KeyJazzCapsLock = false;
	[SerializeEnumAsInt]
	public CopySearchMode MaskCopySearchMode = CopySearchMode.Off;
	public bool InvertHomeEnd = false;
	public bool CrayolaMode = false;
	public string TrackViewScheme = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
	public string ChannelMulti = "----------------------------------------------------------------";

	public override void FinalizeLoad()
	{
		AllPages.PatternEditor.LoadConfiguration();
	}


}