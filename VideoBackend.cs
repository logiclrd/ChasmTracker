namespace ChasmTracker;

using ChasmTracker.VGA;

public abstract class VideoBackend
{
	public abstract bool Initialize();
	public abstract void Quit();

	public abstract bool Startup();
	public abstract void Shutdown();

	public abstract void Report();

	public abstract string? DriverName { get; }

	public abstract bool IsFocused();
	public abstract bool IsVisible();
	public abstract bool IsWindowManagerAvailable();
	public abstract bool IsHardware();
	public abstract void Setup(VideoInterpolationMode interpolation);
	public abstract void Fullscreen(bool? newFSFlag);
	public abstract bool IsScreenSaverEnabled();
	public abstract void ToggleScreenSaver(bool enabled);
	public abstract Point Translate(Point v);
	public abstract Point GetLogicalCoordinates(Point p);
	public abstract bool IsInputGrabbed();
	public abstract void SetInputGrabbed(bool enabled);
	public abstract void WarpMouse(Point p);
	public abstract Point GetMouseCoordinates();
	public abstract bool HaveMenu();
	public abstract void ToggleMenu(bool on);
	public abstract void Blit();
	public abstract WMData? GetWMData();
	public abstract void ShowCursor(bool enabled);
	public abstract void NotifyMouseCursorChanged();
}
