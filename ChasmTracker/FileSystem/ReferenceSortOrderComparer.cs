using System.Collections.Generic;

namespace ChasmTracker.FileSystem;

public class ReferenceSortOrderComparer<T> : IComparer<T>
	where T : Reference
{
	IComparer<T> _thenBy;

	public ReferenceSortOrderComparer(IComparer<T> thenBy)
	{
		_thenBy = thenBy;
	}

	public int Compare(T? x, T? y)
	{
		if ((x == null) || (y == null))
			return (y == null).CompareTo(x == null);

		int sortOrder = x.SortOrder.CompareTo(y.SortOrder);

		if (sortOrder == 0)
			sortOrder = _thenBy.Compare(x, y);

		return sortOrder;
	}
}