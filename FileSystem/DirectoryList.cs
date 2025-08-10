using System.Collections.Generic;

namespace ChasmTracker.FileSystem;

public class DirectoryList
{
	List<DirectoryReference> _directories = new List<DirectoryReference>();

	public DirectoryReference this[int index]
	{
		get => _directories[index];
	}

	public int NumDirectories => _directories.Count;

	public void Clear()
	{
		_directories.Clear();
	}

	public void SelectDirectoryByName(string? directoryName)
	{
		if (directoryName == null)
			return;

		SelectedIndex = _directories.FindIndex(reference => reference.BaseName == directoryName);

		if (SelectedIndex < 0)
			SelectedIndex = 0;
	}

	public void RemoveAt(int dirIndex)
	{
		_directories.RemoveAt(dirIndex);
	}

	public int SelectedIndex;

	public int AddDir(string fullPath, int sortOrder)
		=> AddDir(new DirectoryReference(fullPath, sortOrder));

	public int AddDir(string fullPath, string baseName, int sortOrder)
		=> AddDir(new DirectoryReference(fullPath, baseName, sortOrder));

	public int AddDir(DirectoryReference directoryReference)
	{
		_directories.Add(directoryReference);

		return _directories.Count - 1;
	}

	public void Sort(IComparer<DirectoryReference> comparer)
	{
		_directories.Sort(comparer);
	}
}
