using System;
using System.Runtime.InteropServices;

namespace libFLAC;

/** LPC subframe.  (c.f. <A HREF="https://xiph.org/flac/format.html#subframe_lpc">format specification</A>)
 */
[StructLayout(LayoutKind.Sequential)]
public struct Subframe_LPC
{
	public EntropyCodingMethod EntropyCodingMethod;
	/**< The residual coding method. */

	public uint Order;
	/**< The FIR order. */

	public uint QLPCoefficientPrecision;
	/**< Quantized FIR filter coefficient precision in bits. */

	public int QuantizationLevel;
	/**< The qlp coeff shift needed. */

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = FLACConstants.MaxLPCOrder)]
	public int[] QLPCoefficients;
	/**< FIR filter coefficients. */

	[MarshalAs(UnmanagedType.ByValArray, SizeConst = FLACConstants.MaxLPCOrder)]
	public long[] Warmup;
	/**< Warmup samples to prime the predictor, length == order. */

	public IntPtr Residual;
	/**< The residual signal, length == (blocksize minus order) samples. */
}
