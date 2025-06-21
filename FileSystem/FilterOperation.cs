using System;

namespace ChasmTracker.FileSystem;

public class FilterOperation
{
	FileList _fileList;
	Func<FileReference, bool> _filter;
	int _nextFileIndex = 0;
	SharedInt? _filePointer;
	bool _finished;

	public event Action? ChangeMade;

	public FilterOperation(FileList fileList, Func<FileReference, bool> filter, SharedInt? filePointer = null)
	{
		_fileList = fileList;
		_filter = filter;
		_filePointer = filePointer;
	}

	/* TODO:
	- create a one-shot filter that runs all its files at once
	- make these filters not actually drop the files from the list, but instead set the hidden flag
	- add a 'num_unfiltered' variable to the struct that indicates the total number
	*/

	public bool TakeStep()
	{
		if (_finished)
			return false;

		if (_nextFileIndex >= _fileList.NumFiles)
		{
			_finished = true;
			ChangeMade?.Invoke();
			return false;
		}

		if (_filter(_fileList[_nextFileIndex]))
			_nextFileIndex++;
		else
		{
			_fileList.RemoveAt(_nextFileIndex);

			if (_fileList.NumFiles == _nextFileIndex)
			{
				_finished = true;
				ChangeMade?.Invoke();
				return false;
			}

			if (_filePointer != null)
			{
				if (_filePointer.Value >= _nextFileIndex)
				{
					_filePointer.Value--;
					ChangeMade?.Invoke();
				}

				if (_filePointer.Value >= _fileList.NumFiles)
				{
					_filePointer.Value = _fileList.NumFiles - 1;
					ChangeMade?.Invoke();
				}

				Status.Flags |= StatusFlags.NeedUpdate;
			}
		}

		return true;
	}
}
