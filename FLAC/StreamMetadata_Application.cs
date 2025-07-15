using System;
using System.Runtime.InteropServices;

namespace libFLAC;

/** FLAC APPLICATION structure.  (c.f. <A HREF="https://xiph.org/flac/format.html#metadata_block_application">format specification</A>)
 */
[StructLayout(LayoutKind.Sequential)]
public struct StreamMetadata_Application
{
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
	public byte[] ID;
	public IntPtr Data;
}
