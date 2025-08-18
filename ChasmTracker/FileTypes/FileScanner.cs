using System;
using System.IO;
using System.Linq;
using ChasmTracker.FileSystem;

namespace ChasmTracker.FileTypes;

public class FileScanner
{
	static IFileInfoReader[] s_metadataReaders =
		typeof(FileScanner).Assembly.GetTypes()
		.Where(t => typeof(IFileInfoReader).IsAssignableFrom(t) && !t.IsAbstract)
		.Select(t => (IFileInfoReader)Activator.CreateInstance(t)!)
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

			foreach (var metadataReader in s_metadataReaders)
			{
				stream.Position = 0;

				try
				{
					if (metadataReader.FillExtendedData(stream, fileReference))
					{
						if (fileReference.Artist != null)
							fileReference.Artist = fileReference.Artist.Trim();

						fileReference.Title = fileReference.Title.Trim();

						return FillResult.Success;
					}
				}
				catch { }
			}

			return FillResult.Unsupported;
		}
	}
}
