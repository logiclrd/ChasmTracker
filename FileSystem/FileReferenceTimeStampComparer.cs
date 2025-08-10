using System;
using System.Collections.Generic;

namespace ChasmTracker.FileSystem;

public class FileReferenceTimeStampComparer : IComparer<FileReference>
{
	public FileReferenceTimeStampComparer()
	{
	}

	public int Compare(FileReference? x, FileReference? y)
	{
		if ((x == null) || (y == null))
			return (y == null).CompareTo(x == null);
		else
			return x.TimeStamp.CompareTo(y.TimeStamp);
	}
}