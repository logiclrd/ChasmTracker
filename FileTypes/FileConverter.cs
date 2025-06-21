namespace ChasmTracker.FileTypes;

using System.IO;
using ChasmTracker.FileSystem;

public abstract class FileConverter
{
	// public abstract Song LoadSong();
	// public abstract void SaveSong(Song song);
	public abstract bool FillExtendedData(Stream stream, FileReference fileReference);
}
