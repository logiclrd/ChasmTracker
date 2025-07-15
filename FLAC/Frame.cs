using System.Runtime.InteropServices;

namespace libFLAC;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public class Frame
{
	public FrameHeader Header = new FrameHeader();
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = FLACConstants.MaxChannels)]
	public Subframe[] Subframes = new Subframe[FLACConstants.MaxChannels];
	public FrameFooter Footer = new FrameFooter();
}
