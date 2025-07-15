using System.Runtime.InteropServices;

namespace libFLAC;

/** CONSTANT subframe.  (c.f. <A HREF="https://xiph.org/flac/format.html#subframe_constant">format specification</A>)
 */
[StructLayout(LayoutKind.Sequential)]
public class Subframe_Constant
{
	public long Value; /**< The constant signal value. */
}