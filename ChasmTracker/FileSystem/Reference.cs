using System;
using System.IO;

namespace ChasmTracker.FileSystem;

public abstract class Reference
{
	/* A brief description of the sort_order field:

	When sorting the lists, items with a lower sort_order are given higher placement, and ones with a
	higher sort order are placed toward the bottom. Items with equal sort_order are sorted by their
	basename (using strverscmp).

	Defined sort orders:
		-1024 ... 0     System directories, mount points, volumes, etc.
		-10             Parent directory
		0               Subdirectories of the current directory
		>= 1            Files. Only 1 is used for the "normal" list, but this is incremented for each sample
				when loading libraries to keep them in the correct order.
				Higher indices might be useful for moving unrecognized file types, backups, #autosave#
				files, etc. to the bottom of the list (rather than omitting these files entirely). */

	public string FullPath; /* the full path to the file */
	public string BaseName; /* the basename */
	public int SortOrder; /* where to sort it */

	public Reference(string fullPath, int sortOrder)
	{
		FullPath = fullPath;
		BaseName = Path.GetFileName(fullPath);
		SortOrder = sortOrder;

		if (string.IsNullOrEmpty(BaseName))
			BaseName = FullPath;
	}
}