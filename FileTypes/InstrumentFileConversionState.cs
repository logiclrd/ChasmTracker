using System.Collections.Generic;

namespace ChasmTracker.FileTypes;

using ChasmTracker.Songs;

public class InstrumentFileConversionState
{
	public SongInstrument? Instrument;
	public Dictionary<int, int> SampleMap = new Dictionary<int, int>();
	public int BaseX;
	public int Slot;
	public int ExpectSamples;

	public void Abort()
	{
		if (Slot >= 0)
		{
			Song.CurrentSong.WipeInstrument(Slot);

			for (int n = 0; n < Song.CurrentSong.Samples.Count; n++)
				if (SampleMap.TryGetValue(n, out var m))
					Song.CurrentSong.ClearSample(m - 1);

			SampleMap.Clear();
		}
	}

	public int EnsureSample(int slot)
	{
		if (slot == 0)
			return 0;

		if (SampleMap.TryGetValue(slot, out var sampleNumber))
			return sampleNumber;

		for (int x = BaseX; x < Song.CurrentSong.Samples.Count; x++)
		{
			var cur = Song.CurrentSong.GetSample(x);

			if ((cur != null) && cur.HasData)
				continue;

			if (cur == null)
			{
				cur = new SongSample();

				Song.CurrentSong.Samples[x] = cur;
			}

			ExpectSamples++;
			SampleMap[slot] = x;
			BaseX = x + 1;

			return x;
		}

		Status.FlashText("Too many samples");
		return 0;
	}
}
