using System;
using System.Runtime.InteropServices;

namespace libFLAC;

[StructLayout(LayoutKind.Sequential)]
public class EntropyCodingMethodPartitionedRice
{
	public uint Order;
	/**< The partition order, i.e. # of contexts = 2 ^ \a order. */

	public IntPtr Contents;
	/**< The context's Rice parameters and/or raw bits. */
}
