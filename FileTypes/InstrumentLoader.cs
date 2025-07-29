using System;
using System.Threading;
using ChasmTracker.Songs;

namespace ChasmTracker.FileTypes;

public class InstrumentLoader
{
	public Song Song;
	public SongInstrument Instrument;
	public int[] SampleMap = new int[Constants.MaxSamples];
	public int BaseX, Slot;
	public int ExpectSamples;
	public bool IsAborted = false;

	public InstrumentLoader(Song song, int slot)
	{
		Song = song;
		ExpectSamples = 0;
		Instrument = song.GetInstrument(slot);
		Slot = slot;
		BaseX = 1;

		Array.Clear(Instrument.SampleMap);
	}

	public void Abort()
	{
		if (!IsAborted)
		{
			IsAborted = true;

			Song.WipeInstrument(Slot);

			foreach (int sampleNumber in SampleMap)
				if (sampleNumber > 0)
					Song.ClearSample(sampleNumber - 1);

			Array.Clear(SampleMap);
		}
	}

	public int GetNewSampleNumber(int slot)
	{
		if (slot == 0)
			return 0;

		if (SampleMap[slot] != 0)
			return SampleMap[slot];

		for (int x = BaseX; x < Song.Samples.Count; x++)
		{
			var cur = Song.Samples[x];

			if ((cur != null) && cur.HasData)
				continue;

			if (cur == null)
			{
				cur = new SongSample();

				Song.Samples[x] = cur;
			}

			ExpectSamples++;
			SampleMap[slot] = x;
			BaseX = x + 1;

			return SampleMap[slot];
		}

		Status.FlashText("Too many samples");

		return 0;
	}
}
