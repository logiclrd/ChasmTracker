using System;
using System.Collections.Generic;
using System.Linq;

namespace ChasmTracker.FileTypes;

public abstract class FileConverter
{
	public abstract string Label { get; }
	public abstract string Description { get; }
	public abstract string Extension { get; }

	public virtual bool IsEnabled => true;

	public virtual int SortOrder => 0;

	protected static IEnumerable<T> EnumerateImplementationsOfType<T>(bool sort = true)
		where T : FileConverter
	{
		var enumeration = typeof(T).Assembly
			.GetTypes()
			.Where(type => typeof(T).IsAssignableFrom(type) && !type.IsAbstract)
			.Select(type => (T)Activator.CreateInstance(type)!);

		if (sort)
			enumeration = enumeration.OrderBy(converter => converter.SortOrder);

		return enumeration;
	}
}