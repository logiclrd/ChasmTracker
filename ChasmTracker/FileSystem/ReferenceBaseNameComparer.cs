using System;
using System.Collections.Generic;

namespace ChasmTracker.FileSystem;

public class ReferenceBaseNameComparer<T> : IComparer<T>
	where T : Reference
{
	StringComparer _nameComparer;

	public ReferenceBaseNameComparer(StringComparer nameComparer)
	{
		_nameComparer = nameComparer;
	}

	public int Compare(T? x, T? y)
	{
		return _nameComparer.Compare(x?.BaseName, y?.BaseName);
	}
}