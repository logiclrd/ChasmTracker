using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChasmTracker.Utility;

public static class StructureSerializer
{
	public static unsafe T MarshalFromBytes<T>(byte[] data)
	{
		var t = typeof(T);

		if (t.IsEnum)
		{
			var ut = t.GetEnumUnderlyingType();

			// In order of decreasing frequency in this codebase, without regard
			// given to whether the types are actually used in serialization.
			if (ut == typeof(int))
				return (T)(object)BitConverter.ToInt32(data);
			if (ut == typeof(byte))
				return (T)(object)data[0];
			if (ut == typeof(uint))
				return (T)(object)BitConverter.ToUInt32(data);
			if (ut == typeof(ushort))
				return (T)(object)BitConverter.ToUInt16(data);
			if (ut == typeof(ulong))
				return (T)(object)BitConverter.ToUInt64(data);

			Debugger.Break();
			throw new Exception("Enum type " + t + " needs a deserialization case in MarshalFromBytes");
		}

		fixed (byte *dataPointer = &data[0])
			return Marshal.PtrToStructure<T>((IntPtr)dataPointer)!;
	}

	public static unsafe T MarshalFromBytes<T>(Memory<byte> data)
	{
		var t = typeof(T);

		if (t.IsEnum)
		{
			var ut = t.GetEnumUnderlyingType();

			// In order of decreasing frequency in this codebase, without regard
			// given to whether the types are actually used in serialization.
			if (ut == typeof(int))
				return (T)(object)BitConverter.ToInt32(data.Span);
			if (ut == typeof(byte))
				return (T)(object)data.Span[0];
			if (ut == typeof(uint))
				return (T)(object)BitConverter.ToUInt32(data.Span);
			if (ut == typeof(ushort))
				return (T)(object)BitConverter.ToUInt16(data.Span);
			if (ut == typeof(ulong))
				return (T)(object)BitConverter.ToUInt64(data.Span);

			Debugger.Break();
			throw new Exception("Enum type " + t + " needs a deserialization case in MarshalFromBytes");
		}

		using (var pin = data.Pin())
			return Marshal.PtrToStructure<T>((IntPtr)pin.Pointer)!;
	}

	public static unsafe T MarshalFromBytes<T>(byte[] data, int dataLength)
		where T : struct
	{
		var t = typeof(T);

		if (t.IsEnum)
		{
			var ut = t.GetEnumUnderlyingType();

			// In order of decreasing frequency in this codebase, without regard
			// given to whether the types are actually used in serialization.
			if (ut == typeof(int))
				return (T)(object)BitConverter.ToInt32(data.Slice(0, dataLength).Slice(0, 4));
			if (ut == typeof(byte))
				return (T)(object)data.Slice(0, dataLength)[0];
			if (ut == typeof(uint))
				return (T)(object)BitConverter.ToUInt32(data.Slice(0, dataLength).Slice(0, 4));
			if (ut == typeof(ushort))
				return (T)(object)BitConverter.ToUInt16(data.Slice(0, dataLength).Slice(0, 2));
			if (ut == typeof(ulong))
				return (T)(object)BitConverter.ToUInt64(data.Slice(0, dataLength).Slice(0, 8));

			Debugger.Break();
			throw new Exception("Enum type " + t + " needs a deserialization case in MarshalFromBytes");
		}

		if (dataLength < Marshal.SizeOf<T>())
			return default;

		fixed (byte *dataPointer = &data[0])
			return Marshal.PtrToStructure<T>((IntPtr)dataPointer)!;
	}

	[ThreadStatic]
	static byte[]? s_buf = null;

	public static unsafe byte[] MarshalToBytes<T>(T structure)
		where T : notnull
	{
		return MarshalToBytes(structure, ref s_buf);
	}

	public static unsafe byte[] MarshalToBytes<T>(T structure, ref byte[]? buffer)
		where T : notnull
	{
		Type t = typeof(T);

		if (t.IsEnum)
		{
			var ut = t.GetEnumUnderlyingType();

			// In order of decreasing frequency in this codebase, without regard
			// given to whether the types are actually used in serialization.
			if (ut == typeof(int))
				return MarshalToBytes((int)(object)structure);
			if (ut == typeof(byte))
				return MarshalToBytes((byte)(object)structure);
			if (ut == typeof(uint))
				return MarshalToBytes((uint)(object)structure);
			if (ut == typeof(ushort))
				return MarshalToBytes((ushort)(object)structure);
			if (ut == typeof(ulong))
				return MarshalToBytes((ulong)(object)structure);

			Debugger.Break();
			throw new Exception("Enum type " + t + " needs a deserialization case in MarshalToBytes");
		}

		int size = Marshal.SizeOf<T>();

		if ((buffer == null) || (buffer.Length < size))
			buffer = new byte[size];

		fixed (byte *dataPointer = &buffer[0])
			Marshal.StructureToPtr(Convert.ChangeType(structure, t), (IntPtr)dataPointer, fDeleteOld: false);

		return buffer;
	}
}
