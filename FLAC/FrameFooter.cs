using System.Runtime.InteropServices;

namespace libFLAC;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public class FrameFooter
{
	public ushort CRC;
}
