namespace ChasmTracker;

using ChasmTracker.Utility;
using ChasmTracker.VGA;

public abstract class VideoBackend
{
	public abstract bool Initialize();
	public abstract void Quit();

	public abstract bool Startup();
	public abstract void Shutdown();

	public abstract void Report();

	public abstract string? DriverName { get; }

	public abstract int Width { get; }
	public abstract int Height { get; }

	public abstract bool IsFocused { get; }
	public abstract bool IsVisible { get; }
	public abstract bool IsWindowManagerAvailable { get; }
	public abstract bool IsHardware { get; }
	public abstract void SetHardware(bool newValue);
	public abstract void SetUp(VideoInterpolationMode interpolation);
	public abstract bool IsFullScreen { get; }
	public abstract void Fullscreen(bool? newFSFlag);
	public abstract void Resize(Size newSize);
	public abstract bool IsScreenSaverEnabled { get; }
	public abstract void ToggleScreenSaver(bool enabled);
	public abstract Point Translate(Point v);
	public abstract Point GetLogicalCoordinates(Point p);
	public abstract bool IsInputGrabbed { get; }
	public abstract void SetInputGrabbed(bool enabled);
	public abstract void WarpMouse(Point p);
	public abstract Point GetMouseCoordinates();
	public abstract bool HaveMenu { get; }
	public abstract void ToggleMenu(bool on);
	public abstract void Blit();
	public abstract WMData? GetWMData();
	public abstract void ShowCursor(bool enabled);
	public abstract void NotifyMouseCursorChanged();
	public abstract void SetPalette(ref ChannelData colours);
}
