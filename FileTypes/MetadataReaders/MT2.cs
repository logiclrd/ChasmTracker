using System.IO;

namespace ChasmTracker.FileTypes.MetadataReaders;

using ChasmTracker.FileSystem;
using ChasmTracker.Utility;

public class MT2 : IFileInfoReader
{
	/* FIXME:
	* - this is wrong :)
	* - look for an author name; if it's not "Unregistered" use it */

	public bool FillExtendedData(Stream stream, FileReference file)
	{
		if (stream.Length <= 106)
			return false;

		if (stream.ReadString(4) != "MT20")
			return false;

		stream.Position = 42;

		string title = stream.ReadString(64);

		file.Description = "MadTracker 2 Module";
		/*file.Extension = "mt2";*/
		file.Title = title;
		file.Type = FileTypes.ModuleXM;

		return true;
	}
}