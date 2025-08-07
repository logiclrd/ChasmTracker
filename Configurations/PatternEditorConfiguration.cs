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
	public bool KeyjazzNoteOff = false;
	[ConfigurationKey("keyjazz_write_noteoff")]
	public bool KeyjazzWriteNoteOff = false;
	public int KeyjazzRepeat;
	[ConfigurationKey("keyjazz_capslock")]
	public bool KeyjazzCapsLock = false;
	[SerializeEnumAsInt]
	public CopySearchMode MaskCopySearchMode = CopySearchMode.Off;
	public bool InvertHomeEnd = false;
}