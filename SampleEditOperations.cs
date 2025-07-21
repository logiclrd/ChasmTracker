using System;
using System.Data;
using System.Diagnostics;
using ChasmTracker.MIDI.Drivers;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using Mono.Unix.Native;

namespace ChasmTracker;

public static class SampleEditOperations
{
	static (int Minimum, int Maximum) MinMax8(sbyte[] data)
	{
		sbyte min = sbyte.MaxValue;
		sbyte max = sbyte.MinValue;

		for (int i = 0; i < data.Length; i++)
		{
			sbyte value = data[i];

			if (value < min)
				min = value;
			if (value > max)
				max = value;
		}

		return (min, max);
	}

	static (int Minimum, int Maximum) MinMax16(short[] data)
	{
		short min = short.MaxValue;
		short max = short.MinValue;

		for (int i = 0; i < data.Length; i++)
		{
			short value = data[i];

			if (value < min)
				min = value;
			if (value > max)
				max = value;
		}

		return (min, max);
	}

	/* --------------------------------------------------------------------- */
	/* sign convert (a.k.a. amiga flip) */
	static void SignConvert8(sbyte[]? data)
	{
		if (data == null)
			return;

		for (int i = 0; i < data.Length; i++)
			data[i] ^= sbyte.MinValue;
	}

	static void SignConvert16(short[]? data)
	{
		if (data == null)
			return;

		for (int i = 0; i < data.Length; i++)
			data[i] ^= short.MinValue;
	}

	public static void SignConvert(SongSample sample)
	{
		using (AudioPlayback.LockScope())
		{
			Status.Flags |= StatusFlags.SongNeedsSave;

			if (sample.Flags.HasFlag(SampleFlags._16Bit))
				SignConvert16(sample.RawData16);
			else
				SignConvert8(sample.RawData8);

			sample.AdjustLoop();
		}
	}

	/* --------------------------------------------------------------------- */
	/* from the back to the front */
	static void ReverseMono<TSample>(TSample[]? data)
	{
		if (data == null)
			return;

		int lpos = 0, rpos = data.Length - 1;

		while (lpos < rpos)
		{
			(data[lpos], data[rpos]) = (data[rpos], data[lpos]);

			lpos++;
			rpos--;
		}
	}

	static void ReverseStereo<TSample>(TSample[]? data)
	{
		if (data == null)
			return;

		int lpos = 0, rpos = data.Length - 2;

		rpos -= (rpos & 1); // should be unnecessary, but you never know :-)

		while (lpos < rpos)
		{
			(data[lpos], data[lpos + 1], data[rpos], data[rpos + 1]) =
				(data[rpos], data[rpos + 1], data[lpos], data[lpos + 1]);

			lpos += 2;
			rpos -= 2;
		}
	}

	public static void Reverse(SongSample sample)
	{
		using (AudioPlayback.LockScope())
		{
			Status.Flags |= StatusFlags.SongNeedsSave;

			if (sample.Flags.HasFlag(SampleFlags.Stereo))
			{
				if (sample.Flags.HasFlag(SampleFlags._16Bit)) // FIXME This is UB!
					ReverseStereo<short>(sample.RawData16);
				else
					ReverseMono<sbyte>(sample.RawData8);
			}
			else
			{
				if (sample.Flags.HasFlag(SampleFlags._16Bit))
					ReverseStereo<short>(sample.RawData16);
				else
					ReverseMono<sbyte>(sample.RawData8);
			}

			(sample.LoopStart, sample.LoopEnd) = (sample.LoopEnd, sample.LoopStart);
			(sample.SustainStart, sample.SustainEnd) = (sample.SustainEnd, sample.SustainStart);

			sample.AdjustLoop();
		}
	}

	/* --------------------------------------------------------------------- */

	/* if convert_data is nonzero, the sample data is modified (so it sounds
	* the same); otherwise, the sample length is changed and the data is
	* left untouched. */

	static void QualityConvert8to16(sbyte[] idata, short[] odata)
	{
		for (int i = 0; i < idata.Length; i++)
			odata[i] = unchecked((short)(((ushort)idata[i]) << 8));
	}

	static void QualityConvert16to8(short[] idata, sbyte[] odata)
	{
		for (int i = 0; i < idata.Length; i++)
			odata[i] = unchecked((sbyte)(((ushort)idata[i]) >> 8));
	}

	public static void ToggleQuality(SongSample sample, bool convertData)
	{
		using (AudioPlayback.LockScope())
		{
			// Stop playing the sample because we'll be reallocating and/or changing lengths
			Song.CurrentSong.StopSample(sample);

			sample.Flags ^= SampleFlags._16Bit;

			Status.Flags |= StatusFlags.SongNeedsSave;

			if (convertData)
			{
				if (sample.Flags.HasFlag(SampleFlags._16Bit))
				{
					sample.RawData16 = new short[sample.RawData8!.Length];
					QualityConvert8to16(sample.RawData8, sample.RawData16);
					sample.RawData8 = null;
				}
				else
				{
					sample.RawData8 = new sbyte[sample.RawData16!.Length];
					QualityConvert16to8(sample.RawData16, sample.RawData8);
					sample.RawData16 = null;
				}
			}
			else
			{
				if (sample.Flags.HasFlag(SampleFlags._16Bit))
				{
					sample.Length >>= 1;
					sample.LoopStart >>= 1;
					sample.LoopEnd >>= 1;
					sample.SustainStart >>= 1;
					sample.SustainEnd >>= 1;
				}
				else
				{
					sample.Length <<= 1;
					sample.LoopStart <<= 1;
					sample.LoopEnd <<= 1;
					sample.SustainStart <<= 1;
					sample.SustainEnd <<= 1;
				}
			}

			sample.AdjustLoop();
		}
	}

	/* --------------------------------------------------------------------- */
	/* centralise (correct dc offset) */
	static void Centralise8(sbyte[] data)
	{
		var (min, max) = MinMax8(data);

		int offset = (max + min + 1) >> 1;

		if (offset == 0)
			return;

		for (int i = 0; i < data.Length; i++)
			data[i] = unchecked((sbyte)(data[i] - offset));
	}

	static void Centralise16(short[] data)
	{
		var (min, max) = MinMax16(data);

		int offset = (max + min + 1) >> 1;

		if (offset == 0)
			return;

		for (int i = 0; i < data.Length; i++)
			data[i] = unchecked((short)(data[i] - offset));
	}

	public static void Centralise(SongSample sample)
	{
		lock (AudioPlayback.LockScope())
		{
			Status.Flags |= StatusFlags.SongNeedsSave;

			if (sample.Flags.HasFlag(SampleFlags._16Bit))
				Centralise16(sample.RawData16!);
			else
				Centralise8(sample.RawData8!);

			sample.AdjustLoop();
		}
	}

	/* --------------------------------------------------------------------- */
	/* downmix stereo to mono */
	static sbyte[] Downmix8(sbyte[] data)
	{
		var odata = new sbyte[data.Length / 2];

		for (int i = 0, j = 0; i + 1 < data.Length; j++, i += 2)
			data[j] = unchecked((sbyte)((data[i] + data[i + 1]) / 2));

		return odata;
	}

	static short[] Downmix16(short[] data)
	{
		var odata = new short[data.Length / 2];

		for (int i = 0, j = 0; i + 1 < data.Length; j++, i += 2)
			data[j] = unchecked((short)((data[i] + data[i + 1]) / 2));

		return odata;
	}

	public static void Downmix(SongSample sample)
	{
		if (!sample.Flags.HasFlag(SampleFlags.Stereo))
			return; /* what are we doing here with a mono sample? */

		lock (AudioPlayback.LockScope())
		{
			// Stop playing the sample because we'll be reallocating and/or changing lengths
			Song.CurrentSong.StopSample(sample);

			Status.Flags |= StatusFlags.SongNeedsSave;

			if (sample.Flags.HasFlag(SampleFlags._16Bit))
				sample.RawData16 = Downmix16(sample.RawData16!);
			else
				sample.RawData8 = Downmix8(sample.RawData8!);

			sample.Flags |= ~SampleFlags.Stereo;

			sample.AdjustLoop();
		}
	}

	/* --------------------------------------------------------------------- */
	/* amplify (or attenuate) */
	static void Amplify8(sbyte[] data, int percent)
	{
		for (int i = 0; i < data.Length; i++)
			data[i] = unchecked((sbyte)(data[i] * percent / 100).Clamp(sbyte.MinValue, sbyte.MaxValue));
	}

	static void Amplify16(short[] data, int percent)
	{
		for (int i = 0; i < data.Length; i++)
			data[i] = unchecked((short)(data[i] * percent / 100).Clamp(short.MinValue, short.MaxValue));
	}

	public static void Amplify(SongSample sample, int percent)
	{
		lock (AudioPlayback.LockScope())
		{
			Status.Flags |= StatusFlags.SongNeedsSave;

			if (sample.Flags.HasFlag(SampleFlags._16Bit))
				Amplify16(sample.RawData16!, percent);
			else
				Amplify8(sample.RawData8!, percent);

			sample.AdjustLoop();
		}
	}

	static int GetAmplify8(sbyte[] data)
	{
		var (min, max) = MinMax8(data);

		max = Math.Max(max, -min);

		return (max != 0) ? (sbyte.MaxValue * 100 / max) : 100;
	}

	static int GetAmplify16(short[] data)
	{
		var (min, max) = MinMax16(data);

		max = Math.Max(max, -min);

		return (max != 0) ? (short.MaxValue * 100 / max) : 100;
	}

	public static int GetAmplifyAmount(SongSample sample)
	{
		int percent;

		if (sample.Flags.HasAnyFlag(SampleFlags._16Bit))
			percent = GetAmplify16(sample.RawData16!);
		else
			percent = GetAmplify8(sample.RawData8!);

		if (percent < 100)
			percent = 100;

		return percent;
	}

	/* --------------------------------------------------------------------- */
	/* useful for importing delta-encoded raw data */

	static void DeltaDecode8(sbyte[] data)
	{
		for (int pos = 1; pos < data.Length; pos++)
			data[pos] = unchecked((sbyte)(data[pos] + data[pos - 1]));
	}

	static void DeltaDecode16(short[] data)
	{
		for (int pos = 1; pos < data.Length; pos++)
			data[pos] = unchecked((short)(data[pos] + data[pos - 1]));
	}

	public static void DeltaDecode(SongSample sample)
	{
		lock (AudioPlayback.LockScope())
		{
			if (sample.Flags.HasFlag(SampleFlags._16Bit))
				DeltaDecode16(sample.RawData16!);
			else
				DeltaDecode8(sample.RawData8!);

			sample.AdjustLoop();
		}
	}

	/* --------------------------------------------------------------------- */
	/* surround flipping (probably useless with the S91 effect, but why not) */

	static void Invert8(sbyte[] data)
	{
		for (int i = 0; i < data.Length; i++)
			data[i] = unchecked((sbyte)(~data[i]));
	}

	static void Invert16(short[] data)
	{
		for (int i = 0; i < data.Length; i++)
			data[i] = unchecked((short)(~data[i]));
	}

	public static void Invert(SongSample sample)
	{
		lock (AudioPlayback.LockScope())
		{
			if (sample.Flags.HasFlag(SampleFlags._16Bit))
				Invert16(sample.RawData16!);
			else
				Invert8(sample.RawData8!);

			sample.AdjustLoop();
		}
	}

	/* --------------------------------------------------------------------- */
	/* resize */

	static sbyte[] Resize8(sbyte[] src, int newLen, bool isStereo)
	{
		// newLen is number of samples, but if isStereo is true then each sample is 2 channels
		sbyte[] dst = new sbyte[isStereo ? 2 * newLen : newLen];

		double factor = (double)src.Length / (double)newLen;

		if (isStereo)
		{
			for (int i = 0; i < newLen; i++)
			{
				int pos = 2 * (int)(i * factor);
				dst[2 * i] = src[pos];
				dst[2 * i + 1] = src[pos + 1];
			}
		}
		else
		{
			for (int i = 0; i < newLen; i++)
				dst[i] = src[(int)(i * factor)];
		}

		return dst;
	}

	static short[] Resize16(short[] src, int newLen, bool isStereo)
	{
		// newLen is number of samples, but if isStereo is true then each sample is 2 channels
		short[] dst = new short[isStereo ? 2 * newLen : newLen];

		double factor = (double)src.Length / (double)newLen;

		if (isStereo)
		{
			for (int i = 0; i < newLen; i++)
			{
				int pos = 2 * (int)(i * factor);
				dst[2 * i] = src[pos];
				dst[2 * i + 1] = src[pos + 1];
			}
		}
		else
		{
			for (int i = 0; i < newLen; i++)
				dst[i] = src[(int)(i * factor)];
		}

		return dst;
	}

	static sbyte[] Resize8AntiAlias(sbyte[] src, int newLen, bool isStereo)
	{
		// newLen is number of samples, but if isStereo is true then each sample is 2 channels

		// TODO: Resample{Mono|Stereo}8BitFirFilter
		throw new NotImplementedException();
	}

	static short[] Resize16AntiAlias(short[] src, int newLen, bool isStereo)
	{
		// newLen is number of samples, but if isStereo is true then each sample is 2 channels

		// TODO: Resample{Mono|Stereo}16BitFirFilter
		throw new NotImplementedException();
	}

	public static void Resize(SongSample sample, int newLen, bool antialias)
	{
		if (newLen <= 0)
			return;

		if (!sample.HasData || (sample.Length <= 0))
			return;

		using (AudioPlayback.LockScope())
		{
			/* resizing samples while they're playing keeps crashing things.
			so here's my "fix": stop the song. --plusminus */
			// I suppose that works, but it's slightly annoying, so I'll just stop the sample...
			// hopefully this won't (re)introduce crashes. --Storlek
			Song.CurrentSong.StopSample(sample);

			int bps = (sample.Flags.HasFlag(SampleFlags.Stereo) ? 2 : 1)
				* (sample.Flags.HasFlag(SampleFlags._16Bit) ? 2 : 1);

			Status.Flags |= StatusFlags.SongNeedsSave;

			sample.C5Speed = (int)(sample.C5Speed * (double)newLen / sample.Length);

			// scale loop points
			sample.LoopStart = (int)(sample.LoopStart * (double)newLen / sample.Length);
			sample.LoopEnd = (int)(sample.LoopEnd * (double)newLen / sample.Length);
			sample.SustainStart = (int)(sample.SustainStart * (double)newLen / sample.Length);
			sample.SustainEnd = (int)(sample.SustainEnd * (double)newLen / sample.Length);

			int oldLen = sample.Length;
			sample.Length = newLen;

			if (sample.Flags.HasFlag(SampleFlags._16Bit))
			{
				if (antialias)
					sample.RawData16 = Resize16AntiAlias(sample.RawData16!, newLen, sample.Flags.HasFlag(SampleFlags.Stereo));
				else
					sample.RawData16 = Resize16(sample.RawData16!, newLen, sample.Flags.HasFlag(SampleFlags.Stereo));
			}
			else
			{
				if (antialias)
					sample.RawData8 = Resize8AntiAlias(sample.RawData8!, newLen, sample.Flags.HasFlag(SampleFlags.Stereo));
				else
					sample.RawData8 = Resize8(sample.RawData8!, newLen, sample.Flags.HasFlag(SampleFlags.Stereo));
			}

			sample.AdjustLoop();
		}
	}

	static sbyte[] MonoLR8(sbyte[] data, bool takeLeftChannel)
	{
		var odata = new sbyte[data.Length / 2];

		for (int i = takeLeftChannel ? 0 : 1, j = 0; i + 1 < data.Length; j++, i += 2)
			odata[j] = data[i];

		return odata;
	}

	static short[] MonoLR16(short[] data, bool takeLeftChannel)
	{
		var odata = new short[data.Length / 2];

		for (int i = takeLeftChannel ? 0 : 1, j = 0; i + 1 < data.Length; j++, i += 2)
			odata[j] = data[i];

		return odata;
	}

	public static void MonoLeft(SongSample sample)
	{
		lock (AudioPlayback.LockScope())
		{
			Status.Flags |= StatusFlags.SongNeedsSave;

			if (sample.Flags.HasFlag(SampleFlags.Stereo))
			{
				if (sample.Flags.HasFlag(SampleFlags._16Bit))
					sample.RawData16 = MonoLR16(sample.RawData16!, takeLeftChannel: true);
				else
					sample.RawData8 = MonoLR8(sample.RawData8!, takeLeftChannel: true);

				sample.Flags &= SampleFlags.Stereo;

				sample.AdjustLoop();
			}
		}
	}

	public static void MonoRight(SongSample sample)
	{
		lock (AudioPlayback.LockScope())
		{
			Status.Flags |= StatusFlags.SongNeedsSave;

			if (sample.Flags.HasFlag(SampleFlags.Stereo))
			{
				if (sample.Flags.HasFlag(SampleFlags._16Bit))
					sample.RawData16 = MonoLR16(sample.RawData16!, takeLeftChannel: false);
				else
					sample.RawData8 = MonoLR8(sample.RawData8!, takeLeftChannel: false);

				sample.Flags &= SampleFlags.Stereo;

				sample.AdjustLoop();
			}
		}
	}

	/* ------------------------------------------------------------------------ */
	/* Crossfade sample */

	static void CrossFade8(Span<sbyte> src1, Span<sbyte> src2, Span<sbyte> dest, double e)
	{
		int fadeLength = dest.Length;

		double len = 1.0 / fadeLength;

		for (int i = 0; i < fadeLength; i++)
		{
			double factor1 = Math.Pow(i * len, e);
			double factor2 = Math.Pow((fadeLength - i) * len, e);

			int @out = (int)Math.Round(src1[i] * factor1 + src2[i] * factor2);

			dest[i] = unchecked((sbyte)@out.Clamp(sbyte.MinValue, sbyte.MaxValue));
		}
	}

	static void CrossFade16(Span<short> src1, Span<short> src2, Span<short> dest, double e)
	{
		int fadeLength = dest.Length;

		double len = 1.0 / fadeLength;

		for (int i = 0; i < fadeLength; i++)
		{
			double factor1 = Math.Pow(i * len, e);
			double factor2 = Math.Pow((fadeLength - i) * len, e);

			int @out = (int)Math.Round(src1[i] * factor1 + src2[i] * factor2);

			dest[i] = unchecked((short)@out.Clamp(short.MinValue, short.MaxValue));
		}
	}

	public static void CrossFade(SongSample smp, int fadeLength, int law, bool fadeAfterLoop, bool sustainLoop)
	{
		lock (AudioPlayback.LockScope())
		{
			Status.Flags |= StatusFlags.SongNeedsSave;

			if (!smp.HasData)
				return;

			int loopStart = sustainLoop ? smp.SustainStart : smp.LoopStart;
			int loopEnd = sustainLoop ? smp.SustainEnd : smp.LoopEnd;

			// sanity checks
			if (loopEnd <= loopStart || loopEnd > smp.Length) return;
			if (loopStart < fadeLength) return;

			int channels = smp.Flags.HasFlag(SampleFlags.Stereo) ? 2 : 1;
			int start = (loopStart - fadeLength) * channels;
			int end = (loopEnd - fadeLength) * channels;
			int afterLoopStart = loopStart * channels;
			int afterLoopEnd = loopEnd * channels;
			int afterLoopLength = Math.Min(smp.Length - loopEnd, fadeLength) * channels;

			fadeLength *= channels;

			// e=0.5: constant power crossfade (for uncorrelated samples), e=1.0: constant volume crossfade (for perfectly correlated samples)
			double e = 1.0 - law / 200.0;

			if (smp.Flags.HasFlag(SampleFlags._16Bit))
			{
				CrossFade16(
					smp.RawData16!.Slice(start, fadeLength),
					smp.RawData16!.Slice(end, fadeLength),
					smp.RawData16!.Slice(end, fadeLength),
					e);

				if (fadeAfterLoop)
				{
					CrossFade16(
						smp.RawData16!.Slice(afterLoopStart, afterLoopLength),
						smp.RawData16!.Slice(afterLoopEnd, afterLoopLength),
						smp.RawData16!.Slice(afterLoopEnd, afterLoopLength),
						e);
				}
			}
			else
			{
				CrossFade8(
					smp.RawData8!.Slice(start, fadeLength),
					smp.RawData8!.Slice(end, fadeLength),
					smp.RawData8!.Slice(end, fadeLength),
					e);

				if (fadeAfterLoop)
				{
					CrossFade8(
						smp.RawData8!.Slice(afterLoopStart, afterLoopLength),
						smp.RawData8!.Slice(afterLoopEnd, afterLoopLength),
						smp.RawData8!.Slice(afterLoopEnd, afterLoopLength),
						e);
				}
			}

			smp.AdjustLoop();
		}
	}
}
