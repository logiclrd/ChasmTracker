using System;
using System.IO;

namespace ChasmTracker.FileSystem;

using ChasmTracker.FileTypes;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class FileReference : Reference, IComparable<FileReference> // dmoz_file
{
	public FileTypes Type;

	/*struct stat stat;*/
	public DateTime TimeStamp; /* stat.st_mtime */
	public long FileSize; /* stat.st_size */

	/* if (!type.HasAnyFlag(FileTypes.ExtendedDataMask)) nothing below this point will
	be defined (call dmoz_{fill,filter}_ext_data to define it) */

	public string Description; /* i.e. "Impulse Tracker sample" */
	public string? Artist; /* (may be -- and usually is -- null) */
	public string Title;

	/* This will usually be null; it is only set when browsing samples within a library, or if
	a sample was played from within the sample browser. */
	public SongSample? Sample;
	public int SampleCount; /* number of samples (for instruments) */
	public int InstrumentNumber;

	/* loader MAY fill this stuff in */
	public string? SampleFileName;
	public int SampleSpeed;
	public int SampleLoopStart;
	public int SampleLoopEnd;
	public int SampleSustainStart;
	public int SampleSustainEnd;
	public int SampleLength;
	public SampleFlags SampleFlags;

	public int SampleDefaultVolume;
	public int SampleGlobalVolume;
	public int SampleVibratoSpeed;
	public int SampleVibratoDepth;
	public int SampleVibratoRate;

	const string DescriptionDirectory = "Directory";

	/* note: this has do be split up like this; otherwise it gets read as '\x9ad' which is the Wrong Thing. */
	const string TitleDirectory = "\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a" +
		"Directory\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a";

	public FileReference(string fullPath, string baseName, int sortOrder)
		: this(fullPath, sortOrder)
	{
		BaseName = baseName;
	}

	public FileReference(string fullPath, int sortOrder)
		: base(fullPath, sortOrder)
	{
		Description = "";
		Title = "";
		InstrumentNumber = -1;

		if (Directory.Exists(fullPath))
		{
			Type = FileTypes.Directory;
			/* have to fill everything in for directories */
			Description = DescriptionDirectory;
			Title = TitleDirectory;
		}
		else if (Paths.IsRegularFile(fullPath))
			Type = FileTypes.FileMask; /* really ought to have a separate TYPE_UNCHECKED_FILE... */
		else
			Type = FileTypes.NonRegular;

		try
		{
			var info = new FileInfo(fullPath);

			TimeStamp = info.LastWriteTimeUtc;
			FileSize = info.Length;
		}
		catch { }
	}

	public int CompareTo(FileReference? other)
	{
		if (other == null)
			return -1;

		if (other.Type.HasFlag(FileTypes.Hidden) && !Type.HasFlag(FileTypes.Hidden))
			return -1; /* this goes first */
		if (!other.Type.HasFlag(FileTypes.Hidden) && Type.HasFlag(FileTypes.Hidden))
			return 1; /* other goes first */
		if (SortOrder < other.SortOrder)
			return -1; /* this goes first */
		if (SortOrder > other.SortOrder)
			return 1; /* other goes first */

		return FileComparer(this, other);
	}

	public static Func<FileReference, FileReference, int> FileComparer = GetComparer(SortMode.StrCaseVersCmp);

	public static Func<FileReference, FileReference, int> GetComparer(SortMode sortMode)
	{
		switch (sortMode)
		{
			case SortMode.StrCmp:
				return (a, b) => string.Compare(a.BaseName, b.BaseName);
			case SortMode.StrCaseCmp:
				return (a, b) => string.Compare(a.BaseName, b.BaseName, ignoreCase: true);
			case SortMode.StrVersCmp:
				return (a, b) => StringUtility.StrVersCmp(a.BaseName, b.BaseName);
			case SortMode.TimeStamp:
				return (a, b) => a.TimeStamp.CompareTo(b.TimeStamp);
			case SortMode.StrCaseVersCmp:
				return (a, b) => StringUtility.StrVersCmp(a.BaseName, b.BaseName, ignoreCase: true);
		}

		return GetComparer(SortMode.StrCmp);
	}

	public static implicit operator DirectoryReference(FileReference file)
	{
		if (file.Type != FileTypes.Directory)
			throw new Exception("Cannot construct a DirectoryReference to a non-directory");

		return new DirectoryReference(file.FullPath, file.SortOrder);
	}

	public bool FillExtendedData()
	{
		if (Type.HasFlag(FileTypes.ExtendedDataMask))
			return true;
		if (Type == FileTypes.Directory)
			return true;

		try
		{
			switch (FileScanner.FillExtendedData(this))
			{
				case FillResult.Success:
					return true;

				case FillResult.Unsupported:
					Description = "Unsupported file format"; /* used to be "Unsupported module format" */
					break;

				case FillResult.Empty:
					Description = "Empty file";
					break;
			}
		}
		catch (Exception e)
		{
			Description = "File error: " + e.Message;
		}

		Type = FileTypes.Unknown;
		Title = "";

		return false;
	}
}