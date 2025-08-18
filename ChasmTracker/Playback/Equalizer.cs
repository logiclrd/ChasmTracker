using System;

namespace ChasmTracker.Playback;

public static class Equalizer
{
	public const float EQBandWidth = 2.0f;
	public const double EQZero = 0.000001;

	//double f2ic = 1 << 28;
	//double i2fc = 1.0 / (1 << 28);

	static EQBandState[] s_eq =
		new EQBandState[Constants.MaxEQBands * 2]
		{
			// Default: Flat EQ
			new EQBandState(gain: 1, centreFrequency:   120, isEnabled: false),
			new EQBandState(gain: 1, centreFrequency:   600, isEnabled: false),
			new EQBandState(gain: 1, centreFrequency:  1200, isEnabled: false),
			new EQBandState(gain: 1, centreFrequency:  3000, isEnabled: false),
			new EQBandState(gain: 1, centreFrequency:  6000, isEnabled: false),
			new EQBandState(gain: 1, centreFrequency: 10000, isEnabled: false),
			new EQBandState(gain: 1, centreFrequency:   120, isEnabled: false),
			new EQBandState(gain: 1, centreFrequency:   600, isEnabled: false),
			new EQBandState(gain: 1, centreFrequency:  1200, isEnabled: false),
			new EQBandState(gain: 1, centreFrequency:  3000, isEnabled: false),
			new EQBandState(gain: 1, centreFrequency:  6000, isEnabled: false),
			new EQBandState(gain: 1, centreFrequency: 10000, isEnabled: false),
		};

	static void Filter(EQBandState pbs, Span<int> buffer)
	{
		int amt = (AudioSettings.Channels == 1) ? 1 : 2; // if 1, amt is 1, else 2

		for (int i = 0; i < buffer.Length; i+=amt)
		{
			float x = buffer[i];
			float y = pbs.A1 * pbs.X1 +
					pbs.A2 * pbs.X2 +
					pbs.A0 * x +
					pbs.B1 * pbs.Y1 +
					pbs.B2 * pbs.Y2;

			pbs.X2 = pbs.X1;
			pbs.Y2 = pbs.Y1;
			pbs.X1 = x;
			buffer[i] = unchecked((int)y);
			pbs.Y1 = y;
		}
	}

	public static void NormalizeMono(Span<int> buffer)
	{
		for (int b = 0; b < buffer.Length; b++)
			buffer[b] = buffer[b] * (AudioSettings.Master.Left + AudioSettings.Master.Right) /  62;
	}

	public static void NormalizeStereo(Span<int> buffer)
	{
		for (int b = 0; b < buffer.Length; b += 2)
		{
			buffer[b + 0] = buffer[b + 0] * AudioSettings.Master.Left /  31;
			buffer[b + 1] = buffer[b + 1] * AudioSettings.Master.Right /  31;
		}
	}

	public static void EqualizeMono(Span<int> buffer)
	{
		for (int b = 0; b < Constants.MaxEQBands; b++)
			if (s_eq[b].IsEnabled && s_eq[b].Gain != 1.0f)
				Filter(s_eq[b], buffer);
	}

	// XXX: I rolled the two loops into one. Make sure this works.
	public static void EqualizeStereo(Span<int> buffer)
	{
		for (int bl = 0; bl < Constants.MaxEQBands; bl++)
		{
			int br = bl + Constants.MaxEQBands;

			// Left band
			if (s_eq[bl].IsEnabled && s_eq[bl].Gain != 1.0f)
				Filter(s_eq[bl], buffer);

			// Right band
			if (s_eq[br].IsEnabled && s_eq[br].Gain != 1.0f)
				Filter(s_eq[br], buffer.Slice(1));
		}
	}

	public static void Initialize(bool reset, float freq)
	{
		//float fMixingFreq = AudioPlayback.MixFrequency;

		// Gain = 0.5 (-6dB) .. 2 (+6dB)
		for (int band = 0; band < Constants.MaxEQBands * 2; band++)
		{
			float k, k2, r, f;
			float v0, v1;

			bool b = reset;

			if (!s_eq[band].IsEnabled)
			{
				s_eq[band].A0 = 0;
				s_eq[band].A1 = 0;
				s_eq[band].A2 = 0;
				s_eq[band].B1 = 0;
				s_eq[band].B2 = 0;
				s_eq[band].X1 = 0;
				s_eq[band].X2 = 0;
				s_eq[band].Y1 = 0;
				s_eq[band].Y2 = 0;
				continue;
			}

			f = s_eq[band].CentreFrequency / freq;

			if (f > 0.45f)
				s_eq[band].Gain = 1;

			//if (f > 0.25)
			//	f = 0.25;

			//k = Math.Tan(PI * f);

			k = f * (float)Math.PI;
			k = k + k * f;

			//if (k > 0.707f)
			//	k = 0.707f;

			k2 = k*k;
			v0 = s_eq[band].Gain;
			v1 = 1;

			if (s_eq[band].Gain < 1.0)
			{
				v0 *= 0.5f / EQBandWidth;
				v1 *= 0.5f / EQBandWidth;
			}
			else
			{
				v0 *= 1.0f / EQBandWidth;
				v1 *= 1.0f / EQBandWidth;
			}

			r = (1 + v0 * k + k2) / (1 + v1 * k + k2);

			if (r != s_eq[band].A0) {
				s_eq[band].A0 = r;
				b = true;
			}

			r = 2 * (k2 - 1) / (1 + v1 * k + k2);

			if (r != s_eq[band].A1) {
				s_eq[band].A1 = r;
				b = true;
			}

			r = (1 - v0 * k + k2) / (1 + v1 * k + k2);

			if (r != s_eq[band].A2) {
				s_eq[band].A2 = r;
				b = true;
			}

			r = -2 * (k2 - 1) / (1 + v1 * k + k2);

			if (r != s_eq[band].B1) {
				s_eq[band].B1 = r;
				b = true;
			}

			r = -(1 - v1 * k + k2) / (1 + v1 * k + k2);

			if (r != s_eq[band].B2) {
				s_eq[band].B2 = r;
				b = true;
			}

			if (b)
			{
				s_eq[band].X1 = 0;
				s_eq[band].X2 = 0;
				s_eq[band].Y1 = 0;
				s_eq[band].Y2 = 0;
			}
		}
	}

	public static void SetGains(int[] gains, int[] freqs, bool reset, int mixFrequency)
	{
		for (int i = 0; i < Constants.MaxEQBands; i++)
		{
			float g, f = 0;

			if (i < gains.Length)
			{
				int n = gains[i];

				//if (n > 32)
				//        n = 32;

				g = 1.0f + n / 64.0f;

				if (freqs != null)
					f = freqs[i];
			}
			else
				g = 1;

			int il = i, ir = i + Constants.MaxEQBands;

			s_eq[il].Gain = s_eq[ir].Gain = g;
			s_eq[il].CentreFrequency = s_eq[ir].CentreFrequency = f;

			/* don't enable bands outside... */
			if (f > 20.0f && i < gains.Length)
				s_eq[il].IsEnabled = s_eq[ir].IsEnabled = true;
			else
				s_eq[il].IsEnabled = s_eq[ir].IsEnabled = false;
		}

		Initialize(reset, mixFrequency);
	}
}
