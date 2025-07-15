using System.Runtime.InteropServices;

namespace libFLAC;

[StructLayout(LayoutKind.Explicit)]
public struct SubframeInnerUnion
{
	[FieldOffset(0)]
	public Subframe_Constant Constant;
	[FieldOffset(0)]
	public Subframe_Fixed Fixed;
	[FieldOffset(0)]
	public Subframe_LPC LPC;
	[FieldOffset(0)]
	public Subframe_Verbatim Verbatim;
}

[StructLayout(LayoutKind.Sequential)]
public struct Subframe
{
	public SubframeType Type;
	public SubframeInnerUnion Data;
}
