using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChasmTracker.FileTypes;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;

public abstract class InstrumentFileConverter : FileConverter, IFileInfoReader
{
	public virtual bool CanSave => false;

	public abstract bool FillExtendedData(Stream stream, FileReference file);
	public abstract bool LoadInstrument(Stream file, int slot);
	public virtual SaveResult SaveInstrument(Song song, SongInstrument instrument, Stream file) => throw new NotSupportedException();

	static Type[] s_converterTypes =
		typeof(InstrumentFileConverter).Assembly.GetTypes()
		.Where(t => typeof(InstrumentFileConverter).IsAssignableFrom(t) && !t.IsAbstract)
		.Select(t => (Type: t, Instance: (InstrumentFileConverter)Activator.CreateInstance(t)!))
		.OrderBy(ti => ti.Instance.SortOrder)
		.Select(ti => ti.Type)
		.ToArray();

	public static bool TryLoadInstrumentWithAllConverters(string path, int slot)
	{
		using (var stream = File.OpenRead(path))
		{
			foreach (var type in s_converterTypes)
			{
				var converter = (InstrumentFileConverter)Activator.CreateInstance(type)!;

				try
				{
					stream.Position = 0;
					return converter.LoadInstrument(stream, slot);
				}
				catch { }
			}
		}

		return false;
	}

	public static IEnumerable<InstrumentFileConverter> EnumerateImplementations()
		=> EnumerateImplementationsOfType<InstrumentFileConverter>();
	public static InstrumentFileConverter? FindImplementation(string label)
		=> EnumerateImplementationsOfType<InstrumentFileConverter>(false).FirstOrDefault(t => t.Label == label);
}