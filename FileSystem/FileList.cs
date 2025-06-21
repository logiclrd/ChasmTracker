using System.Collections.Generic;
using System.IO;

namespace ChasmTracker.FileSystem;

public class FileList
{
	List<FileReference> _files = new List<FileReference>();

	public FileReference this[int index]
	{
		get => _files[index];
	}

	public int NumFiles => _files.Count;

	public void RemoveAt(int fileIndex)
	{
		_files.RemoveAt(fileIndex);
	}

	public int SelectedIndex;

	public int AddFile(string fullPath, int sortOrder)
		=> AddFile(new FileReference(fullPath, sortOrder));

	public int AddFile(string fullPath, string baseName, int sortOrder)
		=> AddFile(new FileReference(fullPath, baseName, sortOrder));

	public int AddFile(FileReference fileReference)
	{
		_files.Add(fileReference);

		return _files.Count - 1;
	}

	public void Sort()
	{
		_files.Sort();
	}
}