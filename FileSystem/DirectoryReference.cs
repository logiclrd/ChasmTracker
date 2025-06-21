using System;

namespace ChasmTracker.FileSystem;

using ChasmTracker.Utility;

public class DirectoryReference : Reference // dmoz_dir
{
	public DirectoryReference(string fullPath, int sortOrder)
		: base(fullPath, sortOrder)
	{
	}

	public DirectoryReference(string fullPath, string baseName, int sortOrder)
		: this(fullPath, sortOrder)
	{
		BaseName = baseName;
	}

	public int CompareTo(DirectoryReference? other)
	{
		if (other == null)
			return -1;

		if (SortOrder < other.SortOrder)
			return -1; /* this goes first */
		if (SortOrder > other.SortOrder)
			return 1; /* other goes first */

		return DirectoryComparer(this, other);
	}

	public static Func<DirectoryReference, DirectoryReference, int> DirectoryComparer = GetComparer(SortMode.StrCaseVersCmp);

	public static Func<DirectoryReference, DirectoryReference, int> GetComparer(SortMode sortMode)
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
				return (a, b) => 0;
			case SortMode.StrCaseVersCmp:
				return (a, b) => StringUtility.StrVersCmp(a.BaseName, b.BaseName, ignoreCase: true);
		}

		return GetComparer(SortMode.StrCmp);
	}
}