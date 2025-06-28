using System;
using System.IO;
using System.Linq;
using ChasmTracker.FileSystem;

namespace ChasmTracker.FileTypes;

public class FileScanner
{
	static SongFileConverter[] s_converters =
		typeof(FileScanner).Assembly.GetTypes()
		.Where(t => typeof(SongFileConverter).IsAssignableFrom(t) && !t.IsAbstract)
		.Select(t => (SongFileConverter)Activator.CreateInstance(t)!)
		.ToArray();

	public static FillResult FillExtendedData(FileReference fileReference)
	{
		using (var stream = File.OpenRead(fileReference.FullPath))
		{
			if (stream.Length == 0)
				return FillResult.Empty;

			fileReference.Artist = null;
			fileReference.Title = "";
			fileReference.SampleDefaultVolume = 64;
			fileReference.SampleGlobalVolume = 64;

			foreach (var converter in s_converters)
			{
				stream.Position = 0;

				if (converter.FillExtendedData(stream, fileReference))
				{
					if (fileReference.Artist != null)
						fileReference.Artist = fileReference.Artist.Trim();

					fileReference.Title = fileReference.Title.Trim();

					return FillResult.Success;
				}
			}

			return FillResult.Unsupported;
		}
	}
}
