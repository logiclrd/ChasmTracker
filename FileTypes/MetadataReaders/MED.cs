using System.IO;

namespace ChasmTracker.FileSystem.MetadataReaders;

using ChasmTracker.FileTypes;
using ChasmTracker.Utility;

public class MED : IFileInfoReader
{
	public bool FillExtendedData(Stream stream, FileReference file)
	{
		string magic = stream.ReadString(4);

		if (magic != "MMD0")
			return false;

		file.Description = "OctaMed";
		file.Title = ""; // TODO actually read the title
		file.Type = FileTypes.ModuleMOD; // err, more like XM for Amiga

		return true;
	}
}
