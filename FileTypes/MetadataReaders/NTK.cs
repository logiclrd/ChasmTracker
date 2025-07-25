using System.IO;

namespace ChasmTracker.FileTypes.MetadataReaders;

using ChasmTracker.FileSystem;
using ChasmTracker.Utility;

public class NTK : IFileInfoReader
{
	public bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			if (stream.ReadString(8) != "TWNNSNG2")
				return false;

			stream.Position = 9;

			string title = stream.ReadString(15);

			file.Description = "NoiseTrekker";
			/*file.Extension = "ntk";*/
			file.Title = title;
			file.Type = FileTypes.ModuleMOD;    /* ??? */

			return true;
		}
		catch
		{
			return false;
		}
	}
}