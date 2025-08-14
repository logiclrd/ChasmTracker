using ChasmTracker.Input;
using ChasmTracker.Utility;

namespace ChasmTracker.Configurations;

public class VideoConfiguration : ConfigurationSection
{
	public VideoInterpolationMode Interpolation = VideoInterpolationMode.NearestNeighbour;
	public new string Format = "";
	[ConfigurationKey("fullscreen")]
	public bool FullScreen = false;
	public int Width = 640;
	public int Height = 400;
	public bool WantFixed = false;
	public int WantFixedWidth = 640 * 5;
	public int WantFixedHeight = 400 * 6;
	public MouseCursorMode MouseCursor = MouseCursorMode.Emulated;
	public bool Hardware = true;
	public bool WantMenuBar = true;

	public bool LazyRedraw = false;

	public Size Size => new Size(Width, Height);
	public Size WantFixedSize => new Size(WantFixedWidth, WantFixedHeight);
}
