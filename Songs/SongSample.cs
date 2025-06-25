using System;

namespace ChasmTracker.Songs;

public class SongSample
{
	public int Length;
	public int LoopStart;
	public int LoopEnd;
	public int SustainStart;
	public int SustainEnd;
	public ArraySegment<sbyte> Data8 = ArraySegment<sbyte>.Empty;
	public ArraySegment<short> Data16 = ArraySegment<short>.Empty;
	public int C5Speed = 8363;
	public int Panning;
	public int Volume = 64 * 4;
	public int GlobalVolume = 64;
	public SampleFlags Flags;
	public VibratoType VibratoType;
	public int VibratoRate;
	public int VibratoDepth;
	public int VibratoSpeed;
	public string Name = "";
	public string FileName = "";
	public int? DiskWriterBoundPattern;

	public bool IsPlayed; // for note playback dots
	public int SavedGlobalVolume; // for muting individual samples

	public byte[]? AdlibBytes;

	public sbyte[]? RawData8;
	public short[]? RawData16;

	public bool HasData => Flags.HasFlag(SampleFlags._16Bit) ? (RawData16 != null) : (RawData8 != null);
	public Array? Data => Flags.HasFlag(SampleFlags._16Bit) ? RawData16 : RawData8;

	public const int AllocatePrepend = Constants.MaxSamplingPointSize * Constants.MaxInterpolationLookaheadBufferSize;
	public const int AllocateAppend = (1 + 4 + 4) * Constants.MaxInterpolationLookaheadBufferSize * 4;

	public void AllocateData()
	{
		RawData8 = null;
		RawData16 = null;

		Data8 = ArraySegment<sbyte>.Empty;
		Data16 = ArraySegment<short>.Empty;

		bool _16bit = Flags.HasFlag(SampleFlags._16Bit);

		if (_16bit)
		{
			RawData16 = new short[Length + AllocatePrepend + AllocateAppend];
			Data16 = new ArraySegment<short>(RawData16, AllocatePrepend, Length);
		}
		else
		{
			RawData8 = new sbyte[Length + AllocatePrepend + AllocateAppend];
			Data8 = new ArraySegment<sbyte>(RawData8, AllocatePrepend, Length);
		}
	}

	public void AdjustLoop()
	{
		if (!HasData || (Length < 1))
			return;

		// sanitize the loop points
		SustainEnd = Math.Min(SustainEnd, Length);
		LoopEnd = Math.Min(LoopEnd, Length);

		if (SustainStart >= SustainEnd)
		{
			SustainStart = SustainEnd = 0;
			Flags &= ~(SampleFlags.SustainLoop | SampleFlags.PingPongSustain);
		}

		if (LoopStart >= LoopEnd)
		{
			LoopStart = LoopEnd = 0;
			Flags &= ~(SampleFlags.Loop | SampleFlags.PingPongLoop);
		}

		if (Flags.HasFlag(SampleFlags._16Bit))
			PrecomputeLoops16();
		else
			PrecomputeLoops8();
	}

	void PrecomputeLoopCopyLoopImpl8(int targetOffset, int loopStartOffset, int loopEnd, int channels, bool bidi, bool forwardDirection)
	{
		if (RawData8 == null)
			return;

		int samples = 2 * Constants.MaxInterpolationLookaheadBufferSize + (forwardDirection ? 1 : 0);
		int destOffset = channels * (2 * Constants.MaxInterpolationLookaheadBufferSize - 1);
		int position = loopEnd - 1;
		int writeIncrement = forwardDirection ? 1 : -1;
		int readIncrement = writeIncrement;

		for (int i = 0; i < samples; i++)
		{
			for (int c = 0; c < channels; c++)
				RawData8[targetOffset + destOffset + c] = RawData8[Data8.Offset + position * channels + c];

			destOffset += writeIncrement * channels;

			if (position == loopEnd - 1 && readIncrement > 0)
			{
				if (bidi)
				{
					readIncrement = -1;
					if (position > 0)
						position--;
				}
				else
					position = 0;
			}
			else if (position == 0 && readIncrement < 0)
			{
				if (bidi)
					readIncrement = 1;
				else
					position = loopEnd - 1;
			}
			else
				position += readIncrement;
		}
	}

	void PrecomputeLoopImpl8(int targetOffset, int loopStartOffset, int loopEnd, int channels, bool bidi)
	{
		if (loopEnd <= 0)
			return;

		PrecomputeLoopCopyLoopImpl8(targetOffset, loopStartOffset, loopEnd, channels, bidi, true);
		PrecomputeLoopCopyLoopImpl8(targetOffset, loopStartOffset, loopEnd, channels, bidi, false);
	}

	void PrecomputeLoops8()
	{
		if (RawData8 == null)
			return;

		int channels = Flags.HasFlag(SampleFlags.Stereo) ? 2 : 1;
		int copySamples = channels * Constants.MaxInterpolationLookaheadBufferSize;

		int smpStartOffset = Data8.Offset;
		int afterSmpStartOffset = Data8.Offset + Data8.Count;
		int loopLookaheadStartOffset = afterSmpStartOffset + copySamples;
		int sustainLookaheadStartOffset = loopLookaheadStartOffset + 4 * copySamples;

		/* Hold sample on the same level as the last sampling point at the end to prevent extra pops with interpolation.
		 * Do the same at the sample start, too. */
		for (int i = 0; i < Constants.MaxInterpolationLookaheadBufferSize; i++)
		{
			for (int c = 0; c < channels; c++)
			{
				RawData8[afterSmpStartOffset + i * channels + c] = RawData8[afterSmpStartOffset - channels + c];
				RawData8[smpStartOffset - (i + 1) * channels + c] = RawData8[c];
			}
		}

		if (Flags.HasFlag(SampleFlags.Loop))
		{
			PrecomputeLoopImpl8(
				loopLookaheadStartOffset,
				LoopStart * channels,
				LoopEnd - LoopStart,
				channels,
				Flags.HasFlag(SampleFlags.PingPongLoop));
		}

		if (Flags.HasFlag(SampleFlags.SustainLoop))
		{
			PrecomputeLoopImpl8(
				sustainLookaheadStartOffset,
				SustainStart * channels,
				SustainEnd - SustainStart,
				channels,
				Flags.HasFlag(SampleFlags.PingPongSustain));
		}
	}


	void PrecomputeLoopCopyLoopImpl16(int targetOffset, int loopStartOffset, int loopEnd, int channels, bool bidi, bool forwardDirection)
	{
		if (RawData16 == null)
			return;

		int samples = 2 * Constants.MaxInterpolationLookaheadBufferSize + (forwardDirection ? 1 : 0);
		int destOffset = channels * (2 * Constants.MaxInterpolationLookaheadBufferSize - 1);
		int position = loopEnd - 1;
		int writeIncrement = forwardDirection ? 1 : -1;
		int readIncrement = writeIncrement;

		for (int i = 0; i < samples; i++)
		{
			for (int c = 0; c < channels; c++)
				RawData16[targetOffset + destOffset + c] = RawData16[Data16.Offset + position * channels + c];

			destOffset += writeIncrement * channels;

			if (position == loopEnd - 1 && readIncrement > 0)
			{
				if (bidi)
				{
					readIncrement = -1;
					if (position > 0)
						position--;
				}
				else
					position = 0;
			}
			else if (position == 0 && readIncrement < 0)
			{
				if (bidi)
					readIncrement = 1;
				else
					position = loopEnd - 1;
			}
			else
				position += readIncrement;
		}
	}

	void PrecomputeLoopImpl16(int targetOffset, int loopStartOffset, int loopEnd, int channels, bool bidi)
	{
		if (loopEnd <= 0)
			return;

		PrecomputeLoopCopyLoopImpl16(targetOffset, loopStartOffset, loopEnd, channels, bidi, true);
		PrecomputeLoopCopyLoopImpl16(targetOffset, loopStartOffset, loopEnd, channels, bidi, false);
	}

	void PrecomputeLoops16()
	{
		if (RawData16 == null)
			return;

		int channels = Flags.HasFlag(SampleFlags.Stereo) ? 2 : 1;
		int copySamples = channels * Constants.MaxInterpolationLookaheadBufferSize;

		int smpStartOffset = Data16.Offset;
		int afterSmpStartOffset = Data16.Offset + Data16.Count;
		int loopLookaheadStartOffset = afterSmpStartOffset + copySamples;
		int sustainLookaheadStartOffset = loopLookaheadStartOffset + 4 * copySamples;

		/* Hold sample on the same level as the last sampling point at the end to prevent extra pops with interpolation.
		 * Do the same at the sample start, too. */
		for (int i = 0; i < Constants.MaxInterpolationLookaheadBufferSize; i++)
		{
			for (int c = 0; c < channels; c++)
			{
				RawData16[afterSmpStartOffset + i * channels + c] = RawData16[afterSmpStartOffset - channels + c];
				RawData16[smpStartOffset - (i + 1) * channels + c] = RawData16[c];
			}
		}

		if (Flags.HasFlag(SampleFlags.Loop))
		{
			PrecomputeLoopImpl16(
				loopLookaheadStartOffset,
				LoopStart * channels,
				LoopEnd - LoopStart,
				channels,
				Flags.HasFlag(SampleFlags.PingPongLoop));
		}

		if (Flags.HasFlag(SampleFlags.SustainLoop))
		{
			PrecomputeLoopImpl16(
				sustainLookaheadStartOffset,
				SustainStart * channels,
				SustainEnd - SustainStart,
				channels,
				Flags.HasFlag(SampleFlags.PingPongSustain));
		}
	}
}
