using System;
using ChasmTracker.Utility;

namespace ChasmTracker;

public class FFT
{
	/* variables :) */
	public static bool Mono = false;
	//gain, in dBs.
	//static int gain = 0;
	public static int NoiseFloor = 72;

	/* more stupid visual stuff:
	 * I've reverted these to the values that they were at before the "Visuals"
	 * patch from JosepMa, since the newer ones seem to cause weird holes within
	 * the graph. Maybe this should be investigated further ;) */
	public const int BufferSizeLog = 10;
	public const int BufferSize = 1 << BufferSizeLog;

	public const int OutputSize = BufferSize / 2;

	public const int BandsSize = 1024; // this is enough to fill the screen and more

	/* This value is used internally to scale the power output of the FFT to decibels. */
	const double InvertBufferSize = 1.0 / (BufferSize >> 2);
	/* Scaling for FFT. Input is expected to be int16_t. */
	const double InvertSampleRange = 1.0 / 32768.0;

	static short[,] s_currentFFTData = new short[2, OutputSize];

	/*Table to change the scale from linear to log.*/
	static int[] s_fftLog = new int[BandsSize];

	/* tables */
	static int[] s_bitReverse = new int[BufferSize];
	static double[] s_window = new double[BufferSize];
	static double[] s_precos = new double[OutputSize];
	static double[] s_presin = new double[OutputSize];

	/* fft state */
	static double[] s_stateReal = new double[BufferSize];
	static double[] s_stateImag = new double[BufferSize];

	static int ReverseBits(int @in)
	{
		int r = 0;

		for (int n = 0; n < BufferSizeLog; n++)
		{
			r <<= 1;
			r |= (@in & 1);
			@in >>= 1;
		}

		return r;
	}

	public static void Initialize()
	{
		for (int n = 0; n < s_bitReverse.Length; n++)
		{
			s_bitReverse[n] = ReverseBits(n);

#if false
			/*Rectangular/none*/
			s_window[n] = 1;
			/*Cosine/sine window*/
			s_window[n] = Math.Sin(Math.PI * n / (BufferSize - 1));
			/*Hann Window*/
			s_window[n] = 0.50 - 0.50 * Math.Cos(2.0*Math.PI * n / (BufferSize - 1));
			/*Hamming Window*/
			s_window[n] = 0.54 - 0.46 * Math.Cos(2.0*Math.PI * n / (BufferSize - 1));
			/*Gaussian*/
			s_window[n] = Math.Pow(Math.E, -0.5 * Math.Pow((n-(BufferSize-1)/2.0)/(0.4*(BufferSize-1)/2.0),2.0));
			/*Blackmann*/
			s_window[n] = 0.42659 - 0.49656 * Math.Cos(2.0*Math.PI * n/ (BufferSize-1)) + 0.076849 * Math.Cos(4.0*Math.PI * n /(BufferSize-1));
			/*Blackman-Harris*/
			s_window[n] = 0.35875 - 0.48829 * Math.Cos(2.0*Math.PI * n/ (BufferSize-1)) + 0.14128 * Math.Cos(4.0*Math.PI * n /(BufferSize-1)) - 0.01168 * Math.Cos(6.0*M_PI * n /(BufferSize-1));
#endif
			/*Hann Window*/
			s_window[n] = 0.50 - 0.50 * Math.Cos(2.0 * Math.PI * n / (BufferSize - 1));
		}

		for (int n = 0; n < OutputSize; n++)
		{
			double j = 2.0 * Math.PI * n / BufferSize;

			s_precos[n] = Math.Cos(j);
			s_presin[n] = Math.Sin(j);
		}

#if false
		/* linear */
		double factor = OutputSize / (double)BandsSize;
		for (int n = 0; n < FFT.BandsSize; n++)
			s_fftLog[n] = (int)(n * factor);
#elif true
		/* exponential */
		double factor = OutputSize / (double)BandsSize / BandsSize;
		for (int n = 0; n < BandsSize; n++)
			s_fftLog[n] = (int)(n * n * factor);
#else
		/* constant note scale */
		double factor = 8.0 / (double)BandsSize;
		double factor2 = OutputSize / 256.0;

		for (int n = 0; n < BandsSize; n++)
			s_fftLog[n] = (int)(factor2 * (Math.Pow(2.0, n * factor) - 1.0));
#endif
	}

	/*
	* Understanding In and Out:
	* input is the samples (so, it is amplitude). The scale is expected to be signed 16bits.
	*    The window function calculated in "window" will automatically be applied.
	* output is a value between 0 and 128 representing 0 = noisefloor variable
	*    and 128 = 0dBFS (deciBell, FullScale) for each band.
	*/
	public static void DataWork(short[] output, short[] input)
	{
		int ex = 1;
		int ff = OutputSize;

		for (int n = 0; n < BufferSize; n++)
		{
			int nr = s_bitReverse[n];

			s_stateReal[n] = input[nr] * InvertSampleRange * s_window[nr];
			s_stateImag[n] = 0;
		}

		for (int n = BufferSizeLog; n != 0; n--)
		{
			for (int k = 0; k != ex; k++)
			{
				double fr = s_precos[k * ff];
				double fi = s_presin[k * ff];

				for (int y = k; y < BufferSize; y += ex << 1)
				{
					int yp = y + ex;

					double tr = fr * s_stateReal[yp] - fi * s_stateImag[yp];
					double ti = fr * s_stateImag[yp] + fi * s_stateReal[yp];

					s_stateReal[yp] = s_stateReal[y] - tr;
					s_stateImag[yp] = s_stateImag[y] - ti;
					s_stateReal[y] += tr;
					s_stateImag[y] += ti;
				}
			}

			ex <<= 1;
			ff >>= 1;
		}

		/* collect fft */
		/* XXX I changed the behavior here since the states were getting overflowed (originally
		* was 'n + 1' changed to just 'n'. Hopefully nothing breaks... */
		double dBInvBufSize = Decibel.dB(InvertBufferSize);
		for (int n = 0; n < OutputSize; n++)
		{
			/* "out" is the total power for each band.
			* To get amplitude from "output", use sqrt(out[N])/(sizeBuf>>2)
			* To get dB from "output", use powerdB(out[N])+db(1/(sizeBuf>>2)).
			* powerdB is = 10 * log10(in)
			* dB is = 20 * log10(in) */
			var @out = s_stateReal[n] * s_stateReal[n] + s_stateImag[n] * s_stateImag[n];
			/* +0.0000000001f is -100dB of power. Used to prevent evaluating powerdB(0.0) */
			output[n] = Decibel.pdB_s(NoiseFloor, @out + 0.0000000001, dBInvBufSize);
		}
	}

	public static void GetColumns(byte[] @out, int chan)
	{
		int width = @out.Length;

		for (int i = 0, a = 0; i < @out.Length && a < OutputSize; i++)
		{
			int fftLogI = i * BandsSize / width;

			int ax = s_fftLog[fftLogI];

			if (ax >= OutputSize)
				break; // NOW JUST WHO SAY THEY AINT GOT MANY BLOOD?

			/* mmm... this got ugly */
			int j;

			if ((fftLogI + 1 >= BandsSize) || (ax + 1 > s_fftLog[fftLogI]))
			{
				a = ax;
				j = GetFFTValue(chan, a);
			}
			else
			{
				j = GetFFTValue(chan, a);

				while (a <= ax)
				{
					a++;
					j = Math.Max(j, GetFFTValue(chan, a));
				}
			}

			/* FIXME if the FTT data is 16-bits, why are we cutting off the top bits */
			@out[i] = (byte)j;
		}
	}

	static int GetFFTValue(int chan, int offset)
	{
		switch (chan)
		{
			case 1:
			case 2:
				return s_currentFFTData[chan - 1, offset];
		}

		int x1 = s_currentFFTData[0, offset];
		int x2 = s_currentFFTData[1, offset];

		return (x1 >> 1) + (x2 >> 1) + (x1 & x2 & 1);
	}
}
