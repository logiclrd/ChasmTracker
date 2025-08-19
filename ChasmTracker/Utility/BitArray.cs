using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ChasmTracker.Utility;

public class BitArray<T> : System.Collections.ICollection, IEnumerable<T>
	where T : struct, Enum
{
	static System.Collections.BitArray s_defined;

	System.Collections.BitArray _storage;

	public void SetMask(uint mask)
	{
		SetAll(false);

		foreach (int index in s_defined)
		{
			uint value = 1u << index;

			if (mask.HasBitSet(value))
				_storage[index] = true;
		}
	}

	public void SetMask(int mask)
		=> SetMask(unchecked((uint)mask));

	public void SetMask(T mask)
		=> SetMask(Unsafe.As<T, uint>(ref mask));

	public int Count => _storage.Count;

	public bool this[T index]
	{
		get => Get(index);
		set => Set(index, value);
	}

	public bool Get(T index) => _storage.Get(Unsafe.As<T, int>(ref index));

	public void Set(T index, bool value)
	{
		int indexInt = Unsafe.As<T, int>(ref index);

		if (!s_defined.Get(indexInt))
			s_defined.Set(indexInt, true);

		_storage.Set(indexInt, value);
	}

	public void SetAll(bool value)
	{
		// Set all defined bits.
		_storage.Or(s_defined);

		// If the caller wants everything off, flip everything. This will leave undefined bits set, though.
		if (!value)
			_storage.Not();

		// Mask off undefined bits.
		_storage.And(s_defined);
	}

	public IEnumerator<T> GetEnumerator()
	{
		for (int i=0; i < _storage.Length; i++)
			if (s_defined.Get(i) && _storage.Get(i))
				yield return Unsafe.As<int, T>(ref i);
	}

	public bool HasAllSet()
	{
		foreach (int index in s_defined)
			if (!_storage.Get(index))
				return false;

		return true;
	}

	public bool HasAnySet() => _storage.HasAnySet();

	public bool IsReadOnly => false;
	public bool IsSynchronized => false;

	void System.Collections.ICollection.CopyTo(Array array, int index) => _storage.CopyTo(array, index);
	object System.Collections.ICollection.SyncRoot => _storage.SyncRoot;
	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

	static BitArray()
	{
		if (typeof(T).GetEnumUnderlyingType() != typeof(int))
		{
			// There will be an error, but it would be better if it weren't inside the static initializer.
			s_defined = new System.Collections.BitArray(1);
			return;
		}

		s_defined = new System.Collections.BitArray(
			Enum.GetValues<T>().Select(t => Convert.ToInt32(t)).Max() + 1);

		foreach (var i in Enum.GetValues<T>())
		{
			var t = i;

			s_defined[Unsafe.As<T, int>(ref t)] = true;
		}
	}

	public BitArray()
	{
		if (typeof(T).GetEnumUnderlyingType() != typeof(int))
			throw new Exception("BitArray<T>'s type must be an Enum type that derives from Int32");

		_storage = new System.Collections.BitArray(
			Enum.GetValues<T>().Select(t => Convert.ToInt32(t)).Max() + 1);
	}
}