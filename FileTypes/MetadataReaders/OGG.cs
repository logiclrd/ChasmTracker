using System;
using System.IO;

using NVorbis;

namespace ChasmTracker.FileTypes.MetadataReaders;

using ChasmTracker.FileSystem;

public class OGG : IFileInfoReader
{
	// TODO: make into a full-fledged sample file converter?

	/* --------------------------------------------------------------------- */

	public bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			VorbisReader reader = new VorbisReader(stream, leaveOpen: true);

			file.Description = "Ogg Vorbis";
			file.Title = reader.Tags.Title;
			/*file.Extension = "ogg";*/
			file.Type = FileTypes.SampleCompressed;

			return true;
		}
		catch
		{
			return false;
		}
	}
}
