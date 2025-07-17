using System.IO;
using ChasmTracker.FileSystem;
using ChasmTracker.FileTypes;
using ChasmTracker.Utility;

namespace ChasmTracker.FileType.MetadataReaders;

public class LIQ : IFileInfoReader
{
	public bool FillExtendedData(Stream stream, FileReference file)
	{
		string magic1 = stream.ReadString(14);

		if (magic1 != "Liquid Module:")
			return false;

		stream.Position = 64;

		int magic2 = stream.ReadByte();

		if (magic2 != 0x1A)
			return false;

		stream.Position = 14;

		string title = stream.ReadString(30);

		stream.Position = 44;

		string artist = stream.ReadString(20);

		file.Description = "Liquid Tracker";
		/*file.Extension = str_dup("liq");*/
		file.Title = title;
		file.Artist = artist;
		file.Type = FileSystem.FileTypes.ModuleS3M;

		return true;
	}
}
