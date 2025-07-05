using System.ComponentModel;

namespace ChasmTracker.Playback;

public enum SourceMode
{
	[Description("Non-Interpolated")]
	Nearest,
	[Description("Linear")]
	Linear,
	[Description("Cubic Spline")]
	Spline,
	[Description("8-Tap FIR Filter")]
	Polyphase,
}
