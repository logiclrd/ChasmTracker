using System;
using System.Runtime.InteropServices;

namespace ChasmTracker.Utility;

public static class StructureSerializer
{
	public static unsafe T MarshalFromBytes<T>(byte[] data)
	{
		fixed (byte *dataPointer = &data[0])
			return Marshal.PtrToStructure<T>((IntPtr)dataPointer)!;
	}

	public static unsafe byte[] MarshalToBytes<T>(T structure)
		where T : notnull
	{
		byte[] buffer = new byte[Marshal.SizeOf<T>()];

		fixed (byte *dataPointer = &buffer[0])
			Marshal.StructureToPtr(structure, (IntPtr)dataPointer, fDeleteOld: false);

		return buffer;
	}
}
