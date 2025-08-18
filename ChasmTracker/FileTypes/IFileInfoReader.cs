using System.IO;

namespace ChasmTracker.FileTypes;

using ChasmTracker.FileSystem;

public interface IFileInfoReader
{
	bool FillExtendedData(Stream stream, FileReference file);
}
