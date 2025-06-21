using ChasmTracker.Input;
using ChasmTracker.Utility;

namespace ChasmTracker.Configurations;

public class VideoConfiguration
{
	public VideoInterpolationMode Interpolation = VideoInterpolationMode.NearestNeighbour;
	public string? Format;
	[ConfigurationKey("fullscreen")]
	public bool FullScreen;
	public int Width;
	public int Height;
	public bool WantFixed;
	public int WantFixedWidth;
	public int WantFixedHeight;
	public MouseCursorMode MouseCursor = MouseCursorMode.Emulated;
	public bool Hardware;
	public bool WantMenuBar;

	public Size Size => new Size(Width, Height);
	public Size WantFixedSize => new Size(WantFixedWidth, WantFixedHeight);
}
