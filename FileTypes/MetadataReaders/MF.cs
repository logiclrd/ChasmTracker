using System.IO;

namespace ChasmTracker.FileSystem.MetadataReaders;

using ChasmTracker.FileTypes;
using ChasmTracker.Utility;

public class MF : IFileInfoReader
{
	public bool FillExtendedData(Stream stream, FileReference file)
	{
		string moonfish = stream.ReadString(8);

		if (moonfish != "MOONFISH")
			return false;

		stream.Position = 25;

		int titleLength = stream.ReadByte();

		if (titleLength < 0)
			return false;

		string title = stream.ReadString(titleLength);

		file.Description = "MoonFish";
		/*file.Extension = "mf";*/
		file.Title = title.TrimZ();
		file.Type = FileTypes.ModuleMOD;    /* ??? */

		return true;
	}
}
