using System;
using System.Buffers;
using System.Runtime.InteropServices;
using ChasmTracker.Utility;

namespace ChasmTracker.Songs;

public class SongSample
{
	public int Length;
	public int LoopStart;
	public int LoopEnd;
	public int SustainStart;
	public int SustainEnd;
	public Span<sbyte> Data8 => (Data.Length > 0) ? MemoryMarshal.Cast<byte, sbyte>(Data.AsSpan()) : Span<sbyte>.Empty;
	public Span<short> Data16 => (Data.Length > 0) ? MemoryMarshal.Cast<byte, short>(Data.AsSpan()) : Span<short>.Empty;
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

	public byte[]? AdLibBytes;

	public byte[]? RawData;

	public bool HasData => RawData != null;

	public SampleWindow Data = SampleWindow.Empty;

	public const int AllocatePrepend = Constants.MaxSamplingPointSize * Constants.MaxInterpolationLookaheadBufferSize;
	public const int AllocateAppend = (1 + 4 + 4) * Constants.MaxInterpolationLookaheadBufferSize * 4;

	public void AllocateData()
	{
		bool _16bit = Flags.HasAllFlags(SampleFlags._16Bit);

		int bps = _16bit ? 2 : 1;

		RawData = new byte[(Length + AllocatePrepend + AllocateAppend) * bps];
		Data = new SampleWindow(RawData, bps);
	}

	public void TakeSubset(int firstSample, int sampleCount)
	{
		Length = Math.Min(Length - firstSample, sampleCount);

		if (firstSample > 0)
		{
			bool stereo = Flags.HasAllFlags(SampleFlags.Stereo);

			if (stereo)
			{
				firstSample <<= 1;
				sampleCount <<= 1;
			}

			bool _16bit = Flags.HasAllFlags(SampleFlags._16Bit);

			int bps = _16bit ? 2 : 1;

			Buffer.BlockCopy(RawData!, bps * (AllocatePrepend + firstSample), RawData!, bps * AllocatePrepend, bps * Length);
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

		if (Flags.HasAllFlags(SampleFlags._16Bit))
			PrecomputeLoops16();
		else
			PrecomputeLoops8();
	}

	void PrecomputeLoopCopyLoopImpl8(int targetOffset, int loopStartOffset, int loopEnd, int channels, bool bidi, bool forwardDirection)
	{
		if (RawData == null)
			return;

		int samples = 2 * Constants.MaxInterpolationLookaheadBufferSize + (forwardDirection ? 1 : 0);
		int destOffset = channels * (2 * Constants.MaxInterpolationLookaheadBufferSize - 1);
		int position = loopEnd - 1;
		int writeIncrement = forwardDirection ? 1 : -1;
		int readIncrement = writeIncrement;

		for (int i = 0; i < samples; i++)
		{
			for (int c = 0; c < channels; c++)
				RawData[targetOffset + destOffset + c] = RawData[AllocatePrepend + position * channels + c];

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
		if (RawData == null)
			return;

		int channels = Flags.HasAllFlags(SampleFlags.Stereo) ? 2 : 1;
		int copySamples = channels * Constants.MaxInterpolationLookaheadBufferSize;

		int smpStartOffset = AllocatePrepend;
		int afterSmpStartOffset = AllocatePrepend + Data.Length;
		int loopLookaheadStartOffset = afterSmpStartOffset + copySamples;
		int sustainLookaheadStartOffset = loopLookaheadStartOffset + 4 * copySamples;

		/* Hold sample on the same level as the last sampling point at the end to prevent extra pops with interpolation.
		 * Do the same at the sample start, too. */
		for (int i = 0; i < Constants.MaxInterpolationLookaheadBufferSize; i++)
		{
			for (int c = 0; c < channels; c++)
			{
				RawData[afterSmpStartOffset + i * channels + c] = RawData[afterSmpStartOffset - channels + c];
				RawData[smpStartOffset - (i + 1) * channels + c] = RawData[c];
			}
		}

		if (Flags.HasAllFlags(SampleFlags.Loop))
		{
			PrecomputeLoopImpl8(
				loopLookaheadStartOffset,
				LoopStart * channels,
				LoopEnd - LoopStart,
				channels,
				Flags.HasAllFlags(SampleFlags.PingPongLoop));
		}

		if (Flags.HasAllFlags(SampleFlags.SustainLoop))
		{
			PrecomputeLoopImpl8(
				sustainLookaheadStartOffset,
				SustainStart * channels,
				SustainEnd - SustainStart,
				channels,
				Flags.HasAllFlags(SampleFlags.PingPongSustain));
		}
	}


	void PrecomputeLoopCopyLoopImpl16(int targetOffset, int loopStartOffset, int loopEnd, int channels, bool bidi, bool forwardDirection)
	{
		if (RawData == null)
			return;

		var RawData16 = MemoryMarshal.Cast<byte, short>(RawData);
		var Data16 = RawData16.Slice(AllocatePrepend, Data.Length / 2);

		int samples = 2 * Constants.MaxInterpolationLookaheadBufferSize + (forwardDirection ? 1 : 0);
		int destOffset = channels * (2 * Constants.MaxInterpolationLookaheadBufferSize - 1);
		int position = loopEnd - 1;
		int writeIncrement = forwardDirection ? 1 : -1;
		int readIncrement = writeIncrement;

		for (int i = 0; i < samples; i++)
		{
			for (int c = 0; c < channels; c++)
				RawData16[targetOffset + destOffset + c] = Data16[position * channels + c];

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
		if (RawData == null)
			return;

		var RawData16 = MemoryMarshal.Cast<byte, short>(RawData);
		var Data16 = RawData16.Slice(AllocatePrepend, Data.Length / 2);

		int channels = Flags.HasAllFlags(SampleFlags.Stereo) ? 2 : 1;
		int copySamples = channels * Constants.MaxInterpolationLookaheadBufferSize;

		int smpStartOffset = AllocatePrepend;
		int afterSmpStartOffset = smpStartOffset + Data16.Length;
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

		if (Flags.HasAllFlags(SampleFlags.Loop))
		{
			PrecomputeLoopImpl16(
				loopLookaheadStartOffset,
				LoopStart * channels,
				LoopEnd - LoopStart,
				channels,
				Flags.HasAllFlags(SampleFlags.PingPongLoop));
		}

		if (Flags.HasAllFlags(SampleFlags.SustainLoop))
		{
			PrecomputeLoopImpl16(
				sustainLookaheadStartOffset,
				SustainStart * channels,
				SustainEnd - SustainStart,
				channels,
				Flags.HasAllFlags(SampleFlags.PingPongSustain));
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

		if (AdLibBytes != null)
			ret.AdLibBytes = (byte[])AdLibBytes.Clone();

		if (RawData != null)
		{
			ret.RawData = (byte[])RawData.Clone();
			ret.Data = new SampleWindow(ret.RawData, Data.BytesPerSample);
		}

		return ret;
	}
}
