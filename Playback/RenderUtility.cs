using System;
using System.Runtime.InteropServices;

namespace ChasmTracker.Playback;

using ChasmTracker.Songs;
using ChasmTracker.Utility;

public static class RenderUtility
{
	const int OfsDecayShift = 8;
	const int OfsDecayMask = 0xFF;

	public static void StereoFill(Span<int> buffer, int samples, ref int pROfs, ref int pLOfs)
	{
		int rOfs = pROfs;
		int lOfs = pLOfs;

		if ((rOfs == 0) && (lOfs == 0))
			buffer.Slice(0, samples * 2).Clear();

		for (int i = 0; i < samples; i++)
		{
			int x_r = (rOfs + (((-rOfs) >> 31) & OfsDecayMask)) >> OfsDecayShift;
			int x_l = (lOfs + (((-lOfs) >> 31) & OfsDecayMask)) >> OfsDecayShift;

			rOfs -= x_r;
			lOfs -= x_l;
			buffer[i * 2 ]    = x_r;
			buffer[i * 2 + 1] = x_l;
		}

		pROfs = rOfs;
		pLOfs = lOfs;
	}

	public static void EndChannelOfs(ref SongVoice channel, Span<int> buffer, int samples)
	{
		int rOfs = channel.ROfs;
		int lOfs = channel.LOfs;

		if ((rOfs == 0) && (lOfs == 0))
			return;

		for (int i = 0; i < samples; i++)
		{
			int x_r = (rOfs + (((-rOfs) >> 31) & OfsDecayMask)) >> OfsDecayShift;
			int x_l = (lOfs + (((-lOfs) >> 31) & OfsDecayMask)) >> OfsDecayShift;

			rOfs -= x_r;
			lOfs -= x_l;
			buffer[i * 2]     += x_r;
			buffer[i * 2 + 1] += x_l;
		}

		channel.ROfs = rOfs;
		channel.LOfs = lOfs;
	}

	public static void MonoFromStereo(Span<int> mixBuf, int samples)
	{
		for (int i = 0; i < samples; i++)
		{
			int j = i << 1;

			mixBuf[i] = (mixBuf[j] + mixBuf[j + 1]) >> 1;
		}
	}

	// ----------------------------------------------------------------------------
	// Clip and convert functions
	// ----------------------------------------------------------------------------
	// XXX mins/max were int[2]
	//
	// The original C version was written by Rani Assaf <rani@magic.metawire.com>


	// Clip and convert to 8 bit. mins and maxs returned in 27bits: [MIXING_CLIPMIN..MIXING_CLIPMAX]. mins[0] left, mins[1] right.
	public static int Clip32To8(Span<byte> ptr, Span<int> buffer, int samples, int[] mins, int[] maxs)
	{
		for (int i = 0; i < samples; i++)
		{
			int n = buffer[i].Clamp(Constants.MixingClipMin, Constants.MixingClipMax);

			if (n < mins[i & 1])
					mins[i & 1] = n;
			else if (n > maxs[i & 1])
					maxs[i & 1] = n;

			// 8-bit unsigned
			ptr[i] = unchecked((byte)((n >> (24 - Constants.MixingAttenuation)) ^ 0x80));
		}

		return samples;
	}


	// Clip and convert to 16 bit. mins and maxs returned in 27bits: [MIXING_CLIPMIN..MIXING_CLIPMAX]. mins[0] left, mins[1] right.
	public static int Clip32To16(Span<byte> ptr, Span<int> buffer, int samples, int[] mins, int[] maxs)
	{
		var p = MemoryMarshal.Cast<byte, short>(ptr);

		for (int i = 0; i < samples; i++)
		{
			int n = buffer[i].Clamp(Constants.MixingClipMin, Constants.MixingClipMax);

			if (n < mins[i & 1])
					mins[i & 1] = n;
			else if (n > maxs[i & 1])
					maxs[i & 1] = n;

			// 16-bit signed
			p[i] = unchecked((short)(n >> (16 - Constants.MixingAttenuation)));
		}

		return samples * 2;
	}


	// Clip and convert to 24 bit. mins and maxs returned in 27bits: [MIXING_CLIPMIN..MIXING_CLIPMAX]. mins[0] left, mins[1] right.
	// Note, this is 24bit, not 24-in-32bits. The former is used in .wav. The latter is used in audio IO
	public static int Clip32To24(Span<byte> ptr, Span<int> buffer, int samples, int[] mins, int[] maxs)
	{
		/* the inventor of 24bit anything should be shot */
		int[] conv = new int[1];

		var conv24 = MemoryMarshal.Cast<int, byte>(conv).Slice(0, 3);

		for (int i = 0; i < samples; i++)
		{
			int n = buffer[i].Clamp(Constants.MixingClipMin, Constants.MixingClipMax);

			if (n < mins[i & 1])
					mins[i & 1] = n;
			else if (n > maxs[i & 1])
					maxs[i & 1] = n;

			// 24-bit signed
			conv[0] = n >> (8 - Constants.MixingAttenuation);

			/* err, assume same endian */
			conv24.CopyTo(ptr);

			ptr = ptr.Slice(3);
		}

		return samples * 3;
	}


	// Clip and convert to 32 bit(int). mins and maxs returned in 27bits: [MIXING_CLIPMIN..MIXING_CLIPMAX]. mins[0] left, mins[1] right.
	public static int Clip32To32(Span<byte> ptr, Span<int> buffer, int samples, int[] mins, int[] maxs)
	{
		var p = MemoryMarshal.Cast<byte, int>(ptr);

		for (int i = 0; i < samples; i++)
		{
			int n = buffer[i].Clamp(Constants.MixingClipMin, Constants.MixingClipMax);

			if (n < mins[i & 1])
					mins[i & 1] = n;
			else if (n > maxs[i & 1])
					maxs[i & 1] = n;

			// 32-bit signed
			p[i] = n << Constants.MixingAttenuation;
		}

		return samples * 4;
	}
}
