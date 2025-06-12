namespace ChasmTracker.Clipboard;

using System.Runtime.InteropServices;

using MAUIClipboard = Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard;

public class ClippyBackendMAUI : ClippyBackend
{
	// MAUI supports Windows and OS X. It does not support Linux or FreeBSD.
	public override bool Initialize() =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
		RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

	// MAUI doesn't expose this, win32 doesn't have it.
	public override bool HaveSelection => false;

	public override bool HaveClipboard => MAUIClipboard.HasText;

	public override string? GetClipboard() => MAUIClipboard.GetTextAsync().Result;
	public override void SetClipboard(string value) => MAUIClipboard.SetTextAsync(value).Wait();
}
