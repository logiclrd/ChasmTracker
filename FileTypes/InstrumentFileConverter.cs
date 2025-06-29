using System;
using System.IO;
using System.Linq;

namespace ChasmTracker.FileTypes;

using ChasmTracker.FileTypes;
using ChasmTracker.Songs;

public abstract class InstrumentFileConverter
{
	protected InstrumentFileConversionState Init(int slot)
	{
		var ii = new InstrumentFileConversionState();

		ii.ExpectSamples = 0;
		ii.Instrument = Song.CurrentSong.GetInstrument(slot);
		ii.Slot = slot;
		ii.BaseX = 1;

		return ii;
	}

	public abstract bool LoadInstrument(Stream file, int slot);

	static Type[] s_converterTypes =
		typeof(InstrumentFileConverter).Assembly.GetTypes()
		.Where(t => typeof(InstrumentFileConverter).IsAssignableFrom(t) && !t.IsAbstract)
		.ToArray(); // TODO: priority

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
}