namespace ChasmTracker.Configurations;

public class VideoConfiguration
{
	public VideoInterpolationMode Interpolation = VideoInterpolationMode.NearestNeighbour;
	public string Format;
	public bool FullScreen;
	public bool WantFixed;
	public Size WantFixedSize;
	public MouseCursorMode MouseCursor = MouseCursorMode.Emulated;
	public Size Size;
	public bool Hardware;
	public bool WantMenuBar;
}
