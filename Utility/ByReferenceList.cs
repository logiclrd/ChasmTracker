using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ChasmTracker.Utility;

public class ByReferenceList<T> : IEnumerable<T>
	where T : struct
{
	const int DefaultCapacity = 4;

	T[] _items;
	int _size;

	public ByReferenceList()
	{
		_items = Array.Empty<T>();
	}

	public ByReferenceList(int capacity)
	{
		if (capacity < 0)
			throw new ArgumentOutOfRangeException(nameof(capacity));

		if (capacity == 0)
			_items = Array.Empty<T>();
		else
			_items = new T[capacity];
	}

	public int Capacity
	{
		get => _items.Length;
		set
		{
			if (value < _size)
				throw new ArgumentOutOfRangeException(nameof(value), "New capacity is too small for the contents of the list.");

			if (value != _items.Length)
			{
				if (value > 0)
				{
					var newItems = new T[value];

					Array.Copy(_items, 0, newItems, 0, _size);

					_items = newItems;
				}
				else
					_items = Array.Empty<T>();
			}
		}
	}

	public int Count => _size;

	public ref T this[int index]
	{
		get
		{
			if (index >= _size)
				throw new ArgumentOutOfRangeException(nameof(index));

			return ref _items[index];
		}
	}

	public void Add(T item)
	{
		var items = _items;
		int size = _size;

		if (size < items.Length)
		{
			_size = size + 1;
			items[size] = item;
		}
		else
			AddWithResize(item);
	}

	public void AddByRef(ref T item)
	{
		var items = _items;
		int size = _size;

		if (size < items.Length)
		{
			_size = size + 1;
			items[size] = item;
		}
		else
			AddWithResize(item);
	}

	void AddWithResize(T item)
	{
		Debug.Assert(_size == _items.Length);

		int size = _size;

		Grow(size + 1);

		_size = size + 1;
		_items[size] = item;
	}

	public void AddRange(IEnumerable<T>? collection)
	{
		if (collection == null)
			throw new ArgumentNullException(nameof(collection));

		if (collection is ICollection<T> c)
		{
			int count = c.Count;

			if (count > 0)
			{
				if (_items.Length - _size < count)
					Grow(_size + count);

				c.CopyTo(_items, _size);
				_size += count;
			}
		}
		else
		{
			using (var en = collection.GetEnumerator())
				while (en.MoveNext())
					Add(en.Current);
		}
	}

	public void Clear()
	{
		_size = 0;

		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			Array.Clear(_items);
	}

	public void CopyTo(T[] array)
	{
		CopyTo(array, 0);
	}

	public void CopyTo(T[] array, int arrayIndex)
	{
		Array.Copy(_items, 0, array, arrayIndex, _size);
	}

	public int EnsureCapacity(int capacity)
	{
		if (capacity < 0)
			throw new ArgumentOutOfRangeException(nameof(capacity));

		if (_items.Length < capacity)
			Grow(capacity);

		return _items.Length;
	}

	void Grow(int capacity)
	{
		Capacity = GetNewCapacity(capacity);
	}

	void GrowForInsertion(int indexToInsert, int insertionCount = 1)
	{
		Debug.Assert(insertionCount > 0);

		int requiredCapacity = checked(_size + insertionCount);
		int newCapacity = GetNewCapacity(requiredCapacity);

		// Inline and adapt logic from set_Capacity

		T[] newItems = new T[newCapacity];

		if (indexToInsert != 0)
			Array.Copy(_items, newItems, length: indexToInsert);

		if (_size != indexToInsert)
			Array.Copy(_items, indexToInsert, newItems, indexToInsert + insertionCount, _size - indexToInsert);

		_items = newItems;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int GetNewCapacity(int capacity)
	{
		Debug.Assert(_items.Length < capacity);

		int newCapacity = _items.Length == 0 ? DefaultCapacity : 2 * _items.Length;

		// Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
		// Note that this check works even when _items.Length overflowed thanks to the (uint) cast
		if ((uint)newCapacity > Array.MaxLength) newCapacity = Array.MaxLength;

		// If the computed capacity is still less than specified, set to the original argument.
		// Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
		if (newCapacity < capacity) newCapacity = capacity;

		return newCapacity;
	}

	public IEnumerator<T> GetEnumerator()
	{
		for (int i = 0; i < _size; i++)
			yield return _items[i];
	}

	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
	{
		for (int i = 0; i < _size; i++)
			yield return _items[i];
	}

	public void Insert(int index, T item)
	{
		// Note that insertions at the end are legal.
		if ((uint)index > (uint)_size)
			throw new ArgumentOutOfRangeException(nameof(index));

		if (_size == _items.Length)
			GrowForInsertion(index, 1);
		else if (index < _size)
			Array.Copy(_items, index, _items, index + 1, _size - index);

		_items[index] = item;
		_size++;
	}

	public void RemoveAt(int index)
	{
		if ((uint)index >= (uint)_size)
			throw new ArgumentOutOfRangeException(nameof(index));

		_size--;

		if (index < _size)
			Array.Copy(_items, index + 1, _items, index, _size - index);

		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			_items[_size] = default!;
	}

	public void RemoveRange(int index, int count)
	{
		if (index < 0)
			throw new ArgumentOutOfRangeException(nameof(index));

		if (count < 0)
			throw new ArgumentOutOfRangeException(nameof(count));

		if (_size - index < count)
			throw new ArgumentException("Invalid range");

		if (count > 0)
		{
			_size -= count;

			if (index < _size)
				Array.Copy(_items, index + count, _items, index, _size - index);

			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
				Array.Clear(_items, _size, count);
		}
	}

	public void TrimExcess()
	{
		int threshold = (int)(_items.Length * 0.9);

		if (_size < threshold)
			Capacity = _size;
	}
}
