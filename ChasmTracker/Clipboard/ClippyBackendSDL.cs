using SDL3;

namespace ChasmTracker.Clipboard;

public class ClippyBackendSDL : ClippyBackend
{
	public override bool Initialize() => SDLLifetime.Initialize();
	public override void Quit() => SDLLifetime.Quit();

	public override bool HaveSelection => SDL.HasPrimarySelectionText();
	public override bool HaveClipboard => SDL.HasClipboardText();

	public override void SetSelection(string value) => SDL.SetPrimarySelectionText(value);
	public override void SetClipboard(string value) => SDL.SetClipboardText(value);

	public override string? GetSelection() => HaveSelection ? SDL.GetPrimarySelectionText() : null;
	public override string? GetClipboard() => HaveClipboard ? SDL.GetClipboardText() : null;
}
