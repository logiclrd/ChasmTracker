using System.IO;

namespace ChasmTracker.FileTypes.MetadataReaders;

using ChasmTracker.FileSystem;
using ChasmTracker.Utility;

public class F2R : IFileInfoReader
{
	/* --------------------------------------------------------------------- */

	/* TODO: test this code */

	public bool FillExtendedData(Stream stream, FileReference file)
	{
		byte[] magic = new byte[3];

		stream.ReadExactly(magic);

		if (magic.ToStringZ() != "F2R")
			return false;

		byte[] title = new byte[40];

		stream.Position += 3;
		stream.ReadExactly(title);

		file.Description = "Farandole 2 (linear)";
		/*file.Extension = "f2r";*/
		file.Title = title.ToStringZ();
		file.Type = FileSystem.FileTypes.ModuleS3M;

		return true;
	}
}
