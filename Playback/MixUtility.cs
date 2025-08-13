using System;

namespace ChasmTracker.Playback;

using ChasmTracker.Songs;

public static class MixUtility
{
	public const int OfsDecayShift = 8;
	public const int OfsDecayMask = 0xFF;

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
}
