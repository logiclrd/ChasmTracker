using System;

using ChasmTracker.Utility;

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

	public void TakeSubset(int firstSample, int sampleCount)
	{
		Length = Math.Min(Length - firstSample, sampleCount);

		if (firstSample > 0)
		{
			bool stereo = Flags.HasFlag(SampleFlags.Stereo);

			if (stereo)
			{
				firstSample <<= 1;
				sampleCount <<= 1;
			}

			bool _16bit = Flags.HasFlag(SampleFlags._16Bit);

			if (_16bit)
				Buffer.BlockCopy(RawData16!, 2 * (AllocatePrepend + firstSample), RawData16!, 2 * AllocatePrepend, 2 * Length);
			else
				Buffer.BlockCopy(RawData8!, AllocatePrepend + firstSample, RawData8!, AllocatePrepend, Length);
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

	public static bool IsNullOrEmpty(SongSample? smp)
	{
		if (smp == null)
			return true;

		return smp.IsEmpty;
	}

	public bool IsEmpty
	{
		get
		{
			return
				!HasData &&
				string.IsNullOrWhiteSpace(Name) &&
				string.IsNullOrEmpty(FileName) &&
				C5Speed == 8363 &&
				Volume == 64 * 4 && //mphack
				GlobalVolume == 64 &&
				Panning == 0 &&
				!Flags.HasAnyFlag(SampleFlags.Loop | SampleFlags.SustainLoop | SampleFlags.Panning) &&
				Length == 0 &&
				LoopStart == 0 &&
				LoopEnd == 0 &&
				SustainStart == 0 &&
				SustainEnd == 0 &&
				VibratoType == default &&
				VibratoRate == 0 &&
				VibratoDepth == 0 &&
				VibratoSpeed == 0;
		}
	}

	public SongSample Clone()
	{
		var ret = new SongSample();

		ret.Length = Length;
		ret.LoopStart = LoopStart;
		ret.LoopEnd = LoopEnd;
		ret.SustainStart = SustainStart;
		ret.SustainEnd = SustainEnd;
		ret.C5Speed = C5Speed;
		ret.Panning = Panning;
		ret.Volume = Volume;
		ret.GlobalVolume = GlobalVolume;
		ret.Flags = Flags;
		ret.VibratoType = VibratoType;
		ret.VibratoRate = VibratoRate;
		ret.VibratoDepth = VibratoDepth;
		ret.VibratoSpeed = VibratoSpeed;
		ret.Name = Name;
		ret.FileName = FileName;

		ret.DiskWriterBoundPattern = null;
		ret.IsPlayed = false;

		ret.AllocateData();

		if (AdlibBytes != null)
			ret.AdlibBytes = (byte[])AdlibBytes.Clone();

		if (RawData8 != null)
			Array.Copy(RawData8, ret.RawData8!, RawData8.Length);
		if (RawData16 != null)
			Array.Copy(RawData16, ret.RawData16!, RawData16.Length);

		return ret;
	}
}
