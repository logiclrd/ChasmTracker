using System.IO;

namespace ChasmTracker.FileTypes.MetadataReaders;

using ChasmTracker.FileSystem;

public class MP3 : IFileInfoReader
{
	/* --------------------------------------------------------------------- */

	public bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			using (var mp3 = new Id3.Mp3(stream))
			{
				var tag = mp3.GetTag(Id3.Id3TagFamily.Version2X);

				if (tag == null)
					tag = mp3.GetTag(Id3.Id3TagFamily.Version1X);

				if (tag == null)
					return false;

				file.Title = tag.Title;
				file.Artist = tag.Artists;
				file.Description = "MPEG Layer 3";
				file.Type = FileSystem.FileTypes.SampleCompressed;

				return true;
			}
		}
		catch
		{
			return false;
		}
	}
}
