using ChasmTracker.Pages;

namespace ChasmTracker.Configurations;

public class PatternEditorConfiguration : ConfigurationSection
{
	[ConfigurationKey("keyjazz_noteoff")]
	public bool KeyjazzNoteOff = false;
	[ConfigurationKey("keyjazz_write_noteoff")]
	public bool KeyjazzWriteNoteOff = false;
	public int KeyjazzRepeat;
	[ConfigurationKey("keyjazz_capslock")]
	public bool KeyjazzCapsLock = false;

	public CopySearchMode MaskCopySearchMode;

	public bool InvertHomeEnd;
}