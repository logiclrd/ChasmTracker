namespace ChasmTracker.Clipboard;

public abstract class ClippyBackend
{
	public virtual bool Initialize() => true;
	public virtual void Quit() { }

	public abstract bool HaveSelection { get; }
	public virtual string? GetSelection() => null;
	public virtual void SetSelection(string value) { }

	public abstract bool HaveClipboard { get; }
	public virtual string? GetClipboard() => null;
	public virtual void SetClipboard(string value) { }
	
}
