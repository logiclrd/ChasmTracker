using System.IO;
using ChasmTracker.FileSystem;
using ChasmTracker.Utility;

namespace ChasmTracker.FileTypes.Converters;

public class EDI : FileInfoReader
{
	bool LoadFile(Stream stream, out MemoryStream memStream)
	{
		long startPosition = stream.Position;

		memStream = new MemoryStream();

		byte[] buffer = new byte[4];

		stream.ReadExactly(buffer);

		// these are here in every EDL file I can find
		if ((buffer[0] != 0x00)
		 || (buffer[1] != 0x06)
		 || (buffer[2] != 0xFE)
		 || (buffer[3] != 0xFD))
			return false;

		// go back to the beginning
		stream.Position = startPosition;

		var decompressor = new HuffmanDecompressor();

		if (decompressor.Decompress(stream, memStream) == 0)
			return false;

		// EDL files are basically just a dump of whatever is in memory.
		// This means that it's very easy to check whether a given EDL
		// file is legitimate or not by just comparing the length to
		// a magic value.
		if ((memStream.Length != 195840) && (memStream.Length != 179913))
			return false;

		return true;
	}

	public override bool ReadInfo(Stream stream, FileReference file)
	{
		if (!LoadFile(stream, out var fakeStream))
			return false;

		fakeStream.Position = 0x1FE0B;

		byte[] title = new byte[32];

		stream.ReadExactly(title);

		file.Title = title.ToStringZ();
		file.Description = "EdLib Tracker EDL";
		file.Type = FileSystem.FileTypes.ModuleS3M;

		return true;
	}
}
