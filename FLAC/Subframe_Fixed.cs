using System;
using System.Runtime.InteropServices;

namespace libFLAC;

/** FIXED subframe.  (c.f. <A HREF="https://xiph.org/flac/format.html#subframe_fixed">format specification</A>)
 */
[StructLayout(LayoutKind.Sequential)]
public class Subframe_Fixed
{
	public EntropyCodingMethod EntropyCodingMethod = new EntropyCodingMethod();
	/**< The residual coding method. */

	public int Order;
	/**< The polynomial order. */

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = FLACConstants.MaxFixedOrder)]
	public long[] Warmup = default!;
	/**< Warmup samples to prime the predictor, length == order. */

	public IntPtr Residual;
	/**< The residual signal, length == (blocksize minus order) samples. */
}
