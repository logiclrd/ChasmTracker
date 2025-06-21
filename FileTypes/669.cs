using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.FileTypes;

using ChasmTracker.FileSystem;
using ChasmTracker.Utility;

public class _669 : FileConverter
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Header669
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public byte[] Sig;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)] public byte[] SongMessage;
		public byte Samples;
		public byte Patterns;
		public byte RestartPos;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] Orders;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] TempoList;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] Breaks;

		public string SigString => Encoding.ASCII.GetString(Sig);
		public string SongMessageString => Encoding.ASCII.GetString(SongMessage);
	}

	Header669 ReadHeader(Stream stream)
	{
		byte[] buffer = new byte[Marshal.SizeOf<Header669>()];

		stream.ReadExactly(buffer);

		return StructureSerializer.MarshalFromBytes<Header669>(buffer);
	}

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			var hdr = ReadHeader(stream);

			string sig = hdr.SigString;

			/* Impulse Tracker identifies any 669 file as a "Composer 669 Module",
					regardless of the signature tag. */
			if (sig == "if")
				file.Description = "Composer 669 Module";
			else if (sig == "JN")
				file.Description = "Extended 669 Module";
			else
				return false;

			if (hdr.Samples == 0 || hdr.Patterns == 0
					|| hdr.Samples > 64 || hdr.Patterns > 128
					|| hdr.RestartPos > 127)
				return false;

			for (int i = 0; i < 128; i++)
				if (hdr.Breaks[i] > 0x3f)
					return false;

			file.Title = hdr.SongMessageString;
			file.Type = FileSystem.FileTypes.ModuleS3M;

			return true;
		}
		catch
		{
			return false;
		}
	}
}
