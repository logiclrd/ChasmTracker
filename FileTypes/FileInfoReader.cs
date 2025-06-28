using System.IO;

namespace ChasmTracker.FileTypes;

using ChasmTracker.FileSystem;

public abstract class FileInfoReader
{
	public abstract bool ReadInfo(Stream stream, FileReference file);
}
