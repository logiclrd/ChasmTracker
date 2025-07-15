using System;
using System.Runtime.InteropServices;

namespace libFLAC;

/** VERBATIM subframe.  (c.f. <A HREF="https://xiph.org/flac/format.html#subframe_verbatim">format specification</A>)
 */
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public class Subframe_Verbatim
{
	public IntPtr Data; /**< A FLAC__int32 or FLAC__int64 pointer to verbatim signal. */
	public VerbatimSubframeDataType DataType;
}
