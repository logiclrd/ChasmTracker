using System.Collections.Generic;
using System.IO;
using ChasmTracker.Utility;

namespace ChasmTracker.FileSystem;

public class FileList
{
	List<FileReference> _files = new List<FileReference>();

	public FileReference this[int index]
	{
		get => _files[index];
	}

	public int NumFiles => _files.Count;

	public void Clear()
	{
		_files.Clear();
	}

	public void RemoveAt(int fileIndex)
	{
		_files.RemoveAt(fileIndex);
	}

	public SharedInt SelectedIndexRef = new SharedInt();

	public int SelectedIndex
	{
		get => SelectedIndexRef;
		set => SelectedIndexRef.Value = value;
	}

	public FileReference? SelectedFile
	{
		get
		{
			if ((SelectedIndex >= 0) && (SelectedIndex < NumFiles))
				return this[SelectedIndex];
			else
				return null;
		}
	}

	public void SelectFileByName(string? fileName)
	{
		if (fileName == null)
			return;

		SelectedIndex = _files.FindIndex(reference => reference.BaseName == fileName);

		if (SelectedIndex < 0)
			SelectedIndex = 0;
	}

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