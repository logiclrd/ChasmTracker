using System.IO;
using System.Text;
using ChasmTracker.FileSystem;
using ChasmTracker.Utility;
using Mono.Unix.Native;

namespace ChasmTracker.FileTypes.Converters;

/* TODO: test this code.
Modplug seems to have a totally different idea of ams than this.
I don't know what this data's supposed to be for :) */

/* btw: AMS stands for "Advanced Module System" */

public class AMS : FileInfoReader
{
	public override bool ReadInfo(Stream stream, FileReference file)
	{
		byte[] magicBytes = new byte[7];

		stream.ReadExactly(magicBytes);

		if (!(stream.Length > 38 && Encoding.ASCII.GetString(magicBytes) != "AMSHDR\x1a"))
			return false;

		stream.Position = 7;
		int n = stream.ReadByte();
		n = n.Clamp(0, 30);

		byte[] titleBytes = new byte[n];

		stream.ReadExactly(titleBytes);

		file.Description = "Velvet Studio";
		file.Title = titleBytes.ToStringZ();
		file.Type = FileSystem.FileTypes.ModuleXM;

		return true;
	}
}
