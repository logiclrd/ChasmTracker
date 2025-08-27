using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace ChasmTracker.Playback;

using ChasmTracker.MIDI;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public static class Mixer
{
	// For pingpong loops that work like most of Impulse Tracker's drivers
	// (including SB16, SBPro, and the disk writer) -- as well as XMPlay, use 1
	// To make them sound like the GUS driver, use 0.
	// It's really only noticeable for very small loops... (e.g. chip samples)
	// (thanks Saga_Musix for this)
	const int PingPongOffset = 1;

	/* The following lut settings are PRECOMPUTED.
	*
	* If you plan on changing these settings, you
	* MUST also regenerate the arrays. */

	/*
	 *  cubic spline interpolation doc,
	 *    (derived from "digital image warping", g. wolberg)
	 *
	 *    interpolation polynomial: f(x) = A3*(x-floor(x))**3 + A2*(x-floor(x))**2 + A1*(x-floor(x)) + A0
	 *
	 *    with Y = equispaced data points (dist=1), YD = first derivates of data points and IP = floor(x)
	 *    the A[0..3] can be found by solving
	 *      A0  = Y[IP]
	 *      A1  = YD[IP]
	 *      A2  = 3*(Y[IP+1]-Y[IP])-2.0*YD[IP]-YD[IP+1]
	 *      A3  = -2.0 * (Y[IP+1]-Y[IP]) + YD[IP] - YD[IP+1]
	 *
	 *    with the first derivates as
	 *      YD[IP]    = 0.5 * (Y[IP+1] - Y[IP-1]);
	 *      YD[IP+1]  = 0.5 * (Y[IP+2] - Y[IP])
	 *
	 *    the coefs becomes
	 *      A0  = Y[IP]
	 *      A1  = YD[IP]
	 *          =  0.5 * (Y[IP+1] - Y[IP-1]);
	 *      A2  =  3.0 * (Y[IP+1]-Y[IP])-2.0*YD[IP]-YD[IP+1]
	 *          =  3.0 * (Y[IP+1] - Y[IP]) - 0.5 * 2.0 * (Y[IP+1] - Y[IP-1]) - 0.5 * (Y[IP+2] - Y[IP])
	 *          =  3.0 * Y[IP+1] - 3.0 * Y[IP] - Y[IP+1] + Y[IP-1] - 0.5 * Y[IP+2] + 0.5 * Y[IP]
	 *          = -0.5 * Y[IP+2] + 2.0 * Y[IP+1] - 2.5 * Y[IP] + Y[IP-1]
	 *          = Y[IP-1] + 2 * Y[IP+1] - 0.5 * (5.0 * Y[IP] + Y[IP+2])
	 *      A3  = -2.0 * (Y[IP+1]-Y[IP]) + YD[IP] + YD[IP+1]
	 *          = -2.0 * Y[IP+1] + 2.0 * Y[IP] + 0.5 * (Y[IP+1] - Y[IP-1]) + 0.5 * (Y[IP+2] - Y[IP])
	 *          = -2.0 * Y[IP+1] + 2.0 * Y[IP] + 0.5 * Y[IP+1] - 0.5 * Y[IP-1] + 0.5 * Y[IP+2] - 0.5 * Y[IP]
	 *          =  0.5 * Y[IP+2] - 1.5 * Y[IP+1] + 1.5 * Y[IP] - 0.5 * Y[IP-1]
	 *          =  0.5 * (3.0 * (Y[IP] - Y[IP+1]) - Y[IP-1] + YP[IP+2])
	 *
	 *    then interpolated data value is (horner rule)
	 *      out = (((A3*x)+A2)*x+A1)*x+A0
	 *
	 *    this gives parts of data points Y[IP-1] to Y[IP+2] of
	 *      part       x**3    x**2    x**1    x**0
	 *      Y[IP-1]    -0.5     1      -0.5    0
	 *      Y[IP]       1.5    -2.5     0      1
	 *      Y[IP+1]    -1.5     2       0.5    0
	 *      Y[IP+2]     0.5    -0.5     0      0
	 */

	// number of bits used to scale spline coefs
	const int SplineQuantBits  = 14;
	const short SplineQuantScale = (1 << SplineQuantBits);
	const int Spline8Shift     = (SplineQuantBits - 8);
	const int Spline16Shift    = (SplineQuantBits);

	// forces coefsset to unity gain
	const bool SplineClampForUnity = true;

	// log2(number) of precalculated splines (range is [4..14])
	const int SplineFracBits = 10;
	const int SplineLUTLength = (1 << SplineFracBits);

	static short[] cubic_spline_lut = new short[4 * SplineLUTLength];

	static void InitializeCubicSplineLUT()
	{
		int len = SplineLUTLength;
		float flen = 1.0f / (float)SplineLUTLength;
		float scale = (float)SplineQuantScale;

		for (int i = 0; i < len; i++)
		{
			float LCm1, LC0, LC1, LC2;
			float LX = ((float) i) * flen;
			int indx = i << 2;

			LCm1 = (float) Math.Floor(0.5 + scale * (-0.5 * LX * LX * LX + 1.0 * LX * LX - 0.5 * LX       ));
			LC0  = (float) Math.Floor(0.5 + scale * ( 1.5 * LX * LX * LX - 2.5 * LX * LX             + 1.0));
			LC1  = (float) Math.Floor(0.5 + scale * (-1.5 * LX * LX * LX + 2.0 * LX * LX + 0.5 * LX       ));
			LC2  = (float) Math.Floor(0.5 + scale * ( 0.5 * LX * LX * LX - 0.5 * LX * LX                  ));

			cubic_spline_lut[indx + 0] = unchecked((short) ((LCm1 < -scale) ? -scale : ((LCm1 > scale) ? scale : LCm1)));
			cubic_spline_lut[indx + 1] = unchecked((short) ((LC0  < -scale) ? -scale : ((LC0  > scale) ? scale : LC0 )));
			cubic_spline_lut[indx + 2] = unchecked((short) ((LC1  < -scale) ? -scale : ((LC1  > scale) ? scale : LC1 )));
			cubic_spline_lut[indx + 3] = unchecked((short) ((LC2  < -scale) ? -scale : ((LC2  > scale) ? scale : LC2 )));

			if (SplineClampForUnity)
			{
				int sum =
					cubic_spline_lut[indx + 0] +
					cubic_spline_lut[indx + 1] +
					cubic_spline_lut[indx + 2] +
					cubic_spline_lut[indx + 3];

				if (sum != SplineQuantScale)
				{
					int max = indx;

					if (cubic_spline_lut[indx + 1] > cubic_spline_lut[max]) max = indx + 1;
					if (cubic_spline_lut[indx + 2] > cubic_spline_lut[max]) max = indx + 2;
					if (cubic_spline_lut[indx + 3] > cubic_spline_lut[max]) max = indx + 3;

					cubic_spline_lut[max] += (short)(SplineQuantScale - sum);
				}
			}
		}
	}

	/* ------------------------------------------------------------------------ */
	/* FIR interpolation */

	/* fir interpolation doc,
	 *  (derived from "an engineer's guide to fir digital filters", n.j. loy)
	 *
	 *  calculate coefficients for ideal lowpass filter (with cutoff = fc in 0..1 (mapped to 0..nyquist))
	 *    c[-N..N] = (i==0) ? fc : sin(fc*pi*i)/(pi*i)
	 *
	 *  then apply selected window to coefficients
	 *    c[-N..N] *= w(0..N)
	 *  with n in 2*N and w(n) being a window function (see loy)
	 *
	 *  then calculate gain and scale filter coefs to have unity gain.
	 */

	// quantizer scale of window coefs
	const int WindowedFIRQuantBits  = 15;
	const int WindowedFIRQuantScale = (1 << WindowedFIRQuantBits);
	const int WindowedFIR8Shift     = (WindowedFIRQuantBits - 8);
	const int WindowedFIR16Shift    = (WindowedFIRQuantBits);

	// log2(number)-1 of precalculated taps range is [4..12]
	const int WindowedFIRFracBits  = 10;
	const int WindowedFIRLUTLength = ((1 << (WindowedFIRFracBits + 1)) + 1);

	// number of samples in window
	const int WindowedFIRLog2Width      = 3;
	const int WindowedFIRWidth          = (1 << WindowedFIRLog2Width);
	const int WindowedFIRSamplesPerWing = ((WindowedFIRWidth - 1) >> 1);

	// cutoff (1.0 == pi/2)
	const float WindowedFIRCutoff = 0.90f;

	// wfir type
	enum WindowedFIRType
	{
		Hann          = 0,
		Hamming       = 1,
		BlackmanExact = 2,
		Blackman3T61  = 3,
		Blackman3T67  = 4,
		Blackman4T92  = 5,
		Blackman4T74  = 6,
		Kaiser4T      = 7,
	}

	const WindowedFIRType DefaultWindowedFIRType = WindowedFIRType.BlackmanExact;

	// wfir help
	const double M_zEPS       = 1e-8;
	const double M_zBESSELEPS = 1e-21;

	const int SplineFracShift = ((16 - SplineFracBits) - 2);
	const int SplineFracMask  = (((1 << (16 - SplineFracShift)) - 1) & ~3);

	const int WindowedFIRFracShift = (16 - (WindowedFIRFracBits + 1 + WindowedFIRLog2Width));
	const int WindowedFIRFracMask  = ((((1 << (17 - WindowedFIRFracShift)) - 1) & ~((1 << WindowedFIRLog2Width) - 1)));
	const int WindowedFIRFracHalve = (1 << (16 - (WindowedFIRFracBits + 2)));

	static float coef(int pc_nr, float p_ofs, float p_cut, int p_width, WindowedFIRType p_type)
	{
		double width_m1      = p_width - 1;
		double width_m1_half = 0.5 * width_m1;
		double pos_u         = (double) pc_nr - p_ofs;
		double pos           = pos_u - width_m1_half;
		double idl           = 2.0 * Math.PI / width_m1;
		double wc, si;

		if (Math.Abs(pos) < M_zEPS)
		{
			wc    = 1.0;
			si    = p_cut;
			return p_cut;
		}

		switch (p_type)
		{
			case WindowedFIRType.Hann:
				wc = 0.50 - 0.50 * Math.Cos(idl * pos_u);
				break;

			case WindowedFIRType.Hamming:
				wc = 0.54 - 0.46 * Math.Cos(idl * pos_u);
				break;

			case WindowedFIRType.BlackmanExact:
				wc = 0.42 - 0.50 * Math.Cos(idl * pos_u) + 0.08 * Math.Cos(2.0 * idl * pos_u);
				break;

			case WindowedFIRType.Blackman3T61:
				wc = 0.44959 - 0.49364 * Math.Cos(idl * pos_u) + 0.05677 * Math.Cos(2.0 * idl * pos_u);
				break;

			case WindowedFIRType.Blackman3T67:
				wc = 0.42323 - 0.49755 * Math.Cos(idl * pos_u) + 0.07922 * Math.Cos(2.0 * idl * pos_u);
				break;

			case WindowedFIRType.Blackman4T92:
				wc = 0.35875 - 0.48829 * Math.Cos(idl * pos_u) + 0.14128 * Math.Cos(2.0 * idl * pos_u) -
					0.01168 * Math.Cos(3.0 * idl * pos_u);
				break;

			case WindowedFIRType.Blackman4T74:
				wc = 0.40217 - 0.49703 * Math.Cos(idl * pos_u) + 0.09392 * Math.Cos(2.0 * idl * pos_u) -
				0.00183 * Math.Cos(3.0*idl*pos_u);
				break;

			case WindowedFIRType.Kaiser4T:
				wc = 0.40243 - 0.49804 * Math.Cos(idl * pos_u) + 0.09831 * Math.Cos(2.0 * idl * pos_u) -
				0.00122 * Math.Cos(3.0 * idl * pos_u);
				break;

			default:
				wc = 1.0;
				break;
		}

		pos *= Math.PI;
		si   = Math.Sin(p_cut * pos) / pos;

		return (float)(wc * si);
	}

	static short[] windowed_fir_lut = new short[WindowedFIRLUTLength*WindowedFIRWidth];

	static void InitializeWindowedFIR()
	{
		int pcl;
		// number of precalculated lines for 0..1 (-1..0)
		float pcllen = (float)(1L << WindowedFIRFracBits);
		float norm  = 1.0f / (float)(2.0f * pcllen);
		float cut   = WindowedFIRCutoff;
		float scale = (float) WindowedFIRQuantScale;

		float[] coefs = new float[WindowedFIRWidth];

		for (pcl = 0; pcl < WindowedFIRLUTLength; pcl++)
		{
			float gain;
			float ofs = ((float) pcl - pcllen) * norm;
			int cc, indx = pcl << WindowedFIRLog2Width;

			for (cc = 0, gain = 0.0f; cc < WindowedFIRWidth; cc++)
			{
				coefs[cc] = coef(cc, ofs, cut, WindowedFIRWidth, DefaultWindowedFIRType);
				gain += coefs[cc];
			}

			gain = 1.0f / gain;

			for (cc = 0; cc < WindowedFIRWidth; cc++) {
				float coef = (float)Math.Floor( 0.5 + scale * coefs[cc] * gain);
				windowed_fir_lut[indx + cc] =
					(short)((coef < -scale)
						? (-scale)
						: ((coef > scale)
							? scale
							: coef));
			}
		}
	}

	static Mixer()
	{
		InitializeCubicSplineLUT();
		InitializeWindowedFIR();
	}

	/* ------------------------------------------------------------------------ */

	// ----------------------------------------------------------------------------
	// MIXING MACROS
	// ----------------------------------------------------------------------------

	abstract class Bits
	{
		public abstract int Get();
	}

	class Bits_8 : Bits
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override int Get() => 8;
	}

	class Bits_16 : Bits
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override int Get() => 16;
	}

	abstract class Shift
	{
		public abstract int Get();
	}

	class Shift_0 : Shift
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override int Get() => 0;
	}

	class Shift_Spline_8 : Shift
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override int Get() => Spline8Shift;
	}

	class Shift_Spline_16 : Shift
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override int Get() => Spline16Shift;
	}

	class Shift_FIR_8 : Shift
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override int Get() => WindowedFIR8Shift;
	}

	class Shift_FIR_16 : Shift
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override int Get() => WindowedFIR16Shift;
	}

	abstract class GetVolume<TSample>
	{
		public abstract void Do(Span<TSample> p, SamplePosition position, out int left, out int right);
	}

	// No interpolation
	class GetVolume_Mono_NoIDO<TSample, TBits, TShift> : GetVolume<TSample>
		where TSample : IConvertible
		where TBits : Bits, new()
	{
		static TBits s_bits = new TBits();

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(Span<TSample> p, SamplePosition position, out int left, out int right)
		{
			left = right = (p[position.Whole].ToInt32(null) << (16 - s_bits.Get()));
		}
	}

	// Linear Interpolation
	class GetVolume_Mono_Linear<TSample, TBits, TShift> : GetVolume<TSample>
		where TSample : IConvertible
		where TBits : Bits, new()
	{
		static TBits s_bits = new TBits();

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(Span<TSample> p, SamplePosition position, out int left, out int right)
		{
			int poshi  = position.Whole;
			int poslo   = unchecked((int)(position.Fraction >> 24));
			int srcvol  = p[poshi].ToInt32(null);
			int destvol = p[poshi + 1].ToInt32(null);

			left = right = (srcvol << (16 - s_bits.Get())) + ((poslo * (destvol - srcvol)) >> (s_bits.Get() - 8));
		}
	}

	// spline interpolation (2 guard bits should be enough???)
	class GetVolume_Mono_Spline<TSample, TBits, TShift> : GetVolume<TSample>
		where TSample : IConvertible
		where TBits : Bits, new()
		where TShift : Shift, new()
	{
		static TBits s_bits = new TBits();
		static TShift s_shift = new TShift();

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(Span<TSample> p, SamplePosition position, out int left, out int right)
		{
			int poshi = position.Whole;
			/* FIXME this is stupid */
			int poslo = unchecked((int)((((long)position >> 16) >> SplineFracShift) & SplineFracMask));
			int vol   =
				cubic_spline_lut[poslo + 0] * p[poshi - 1].ToInt32(null) +
				cubic_spline_lut[poslo + 1] * p[poshi + 0].ToInt32(null) +
				cubic_spline_lut[poslo + 2] * p[poshi + 1].ToInt32(null) +
				cubic_spline_lut[poslo + 3] * p[poshi + 2].ToInt32(null);

			vol >>= s_shift.Get();

			left = right = vol;
		}
	}

	// fir interpolation
	class GetVolume_Mono_FIR<TSample, TBits, TShift> : GetVolume<TSample>
		where TSample : IConvertible
		where TBits : Bits, new()
		where TShift : Shift, new()
	{
		static TBits s_bits = new TBits();
		static TShift s_shift = new TShift();

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(Span<TSample> p, SamplePosition position, out int left, out int right)
		{
			int poshi  = position.Whole;
			int poslo  = unchecked((int)(position.Fraction >> 16));
			int firidx = ((poslo + WindowedFIRFracHalve) >> WindowedFIRFracShift) & WindowedFIRFracMask;
			int vol = (
				((
					(windowed_fir_lut[firidx + 0] * p[poshi + 1 - 4].ToInt32(null)) +
					(windowed_fir_lut[firidx + 1] * p[poshi + 2 - 4].ToInt32(null)) +
					(windowed_fir_lut[firidx + 2] * p[poshi + 3 - 4].ToInt32(null)) +
					(windowed_fir_lut[firidx + 3] * p[poshi + 4 - 4].ToInt32(null))
				) >> 1) +
				((
					(windowed_fir_lut[firidx + 4] * p[poshi + 5 - 4].ToInt32(null)) +
					(windowed_fir_lut[firidx + 5] * p[poshi + 6 - 4].ToInt32(null)) +
					(windowed_fir_lut[firidx + 6] * p[poshi + 7 - 4].ToInt32(null)) +
					(windowed_fir_lut[firidx + 7] * p[poshi + 8 - 4].ToInt32(null))
				) >> 1))
				>> (s_shift.Get() - 1);

			left = right = vol;
		}
	}

	/////////////////////////////////////////////////////////////////////////////
	// Stereo

	class GetVolume_Stereo_NoIDO<TSample, TBits, TShift> : GetVolume<TSample>
		where TSample : IConvertible
		where TBits : Bits, new()
	{
		static TBits s_bits = new TBits();

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(Span<TSample> p, SamplePosition position, out int left, out int right)
		{
			left = p[position.Whole * 2 + 0].ToInt32(null) << (16 - s_bits.Get());
			right = p[position.Whole * 2 + 1].ToInt32(null) << (16 - s_bits.Get());
		}
	}

	class GetVolume_Stereo_Linear<TSample, TBits, TShift> : GetVolume<TSample>
		where TSample : IConvertible
		where TBits : Bits, new()
	{
		static TBits s_bits = new TBits();

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(Span<TSample> p, SamplePosition position, out int left, out int right)
		{
			int poshi  = position.Whole;
			int poslo   = unchecked((int)(position.Fraction >> 24));
			int srcvol_l  = p[poshi * 2 + 0].ToInt32(null);
			int srcvol_r  = p[poshi * 2 + 1].ToInt32(null);
			int destvol_l = p[poshi * 2 + 2].ToInt32(null);
			int destvol_r = p[poshi * 2 + 3].ToInt32(null);

			left = (srcvol_l << (16 - s_bits.Get())) + ((poslo * (destvol_l - srcvol_l)) >> (s_bits.Get() - 8));
			right = (srcvol_l << (16 - s_bits.Get())) + ((poslo * (destvol_r - srcvol_r)) >> (s_bits.Get() - 8));
		}
	}

	// Spline Interpolation
	class GetVolume_Stereo_Spline<TSample, TBits, TShift> : GetVolume<TSample>
		where TSample : IConvertible
		where TBits : Bits, new()
		where TShift : Shift, new()
	{
		static TBits s_bits = new TBits();
		static TShift s_shift = new TShift();

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(Span<TSample> p, SamplePosition position, out int left, out int right)
		{
			int poshi = position.Whole;
			/* FIXME this is stupid */
			int poslo = unchecked((int)((((long)position >> 16) >> SplineFracShift) & SplineFracMask));
			int volL  =
				cubic_spline_lut[poslo + 0] * p[(poshi - 1) * 2].ToInt32(null) +
				cubic_spline_lut[poslo + 1] * p[(poshi + 0) * 2].ToInt32(null) +
				cubic_spline_lut[poslo + 2] * p[(poshi + 1) * 2].ToInt32(null) +
				cubic_spline_lut[poslo + 3] * p[(poshi + 2) * 2].ToInt32(null);
			int volR  =
				cubic_spline_lut[poslo + 0] * p[(poshi - 1) * 2 + 1].ToInt32(null) +
				cubic_spline_lut[poslo + 1] * p[(poshi + 0) * 2 + 1].ToInt32(null) +
				cubic_spline_lut[poslo + 2] * p[(poshi + 1) * 2 + 1].ToInt32(null) +
				cubic_spline_lut[poslo + 3] * p[(poshi + 2) * 2 + 1].ToInt32(null);

			left = volL >> s_shift.Get();
			right = volR >> s_shift.Get();
		}
	}

	// fir interpolation
	class GetVolume_Stereo_FIR<TSample, TBits, TShift> : GetVolume<TSample>
		where TSample : IConvertible
		where TBits : Bits, new()
		where TShift : Shift, new()
	{
		static TBits s_bits = new TBits();
		static TShift s_shift = new TShift();

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(Span<TSample> p, SamplePosition position, out int left, out int right)
		{
			int poshi  = position.Whole;
			int poslo  = unchecked((int)(position.Fraction >> 16));
			int firidx = ((poslo + WindowedFIRFracHalve) >> WindowedFIRFracShift) & WindowedFIRFracMask;

			left = (
				((
					(windowed_fir_lut[firidx + 0] * p[(poshi + 1 - 4) * 2].ToInt32(null)) +
					(windowed_fir_lut[firidx + 1] * p[(poshi + 2 - 4) * 2].ToInt32(null)) +
					(windowed_fir_lut[firidx + 2] * p[(poshi + 3 - 4) * 2].ToInt32(null)) +
					(windowed_fir_lut[firidx + 3] * p[(poshi + 4 - 4) * 2].ToInt32(null))
				) >> 1) +
				((
					(windowed_fir_lut[firidx + 4] * p[(poshi + 5 - 4) * 2].ToInt32(null)) +
					(windowed_fir_lut[firidx + 5] * p[(poshi + 6 - 4) * 2].ToInt32(null)) +
					(windowed_fir_lut[firidx + 6] * p[(poshi + 7 - 4) * 2].ToInt32(null)) +
					(windowed_fir_lut[firidx + 7] * p[(poshi + 8 - 4) * 2].ToInt32(null))
				) >> 1))
				>> (s_shift.Get() - 1);

			right = (
				((
					(windowed_fir_lut[firidx + 0] * p[(poshi + 1 - 4) * 2 + 1].ToInt32(null)) +
					(windowed_fir_lut[firidx + 1] * p[(poshi + 2 - 4) * 2 + 1].ToInt32(null)) +
					(windowed_fir_lut[firidx + 2] * p[(poshi + 3 - 4) * 2 + 1].ToInt32(null)) +
					(windowed_fir_lut[firidx + 3] * p[(poshi + 4 - 4) * 2 + 1].ToInt32(null))
				) >> 1) +
				((
					(windowed_fir_lut[firidx + 4] * p[(poshi + 5 - 4) * 2 + 1].ToInt32(null)) +
					(windowed_fir_lut[firidx + 5] * p[(poshi + 6 - 4) * 2 + 1].ToInt32(null)) +
					(windowed_fir_lut[firidx + 6] * p[(poshi + 7 - 4) * 2 + 1].ToInt32(null)) +
					(windowed_fir_lut[firidx + 7] * p[(poshi + 8 - 4) * 2 + 1].ToInt32(null))
				) >> 1))
				>> (s_shift.Get() - 1);
		}
	}

	abstract class PutVolume<TSample>
	{
		public abstract void Do(TSample left, TSample right, ref Span<TSample> p);
	}

	class PutVolume_Mono<TSample> : PutVolume<TSample>
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(TSample left, TSample right, ref Span<TSample> p)
		{
			p[0] = left;
			p = p.Slice(1);
		}
	}

	class PutVolume_Stereo<TSample> : PutVolume<TSample>
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(TSample left, TSample right, ref Span<TSample> p)
		{
			p[0] = left;
			p[1] = right;
			p = p.Slice(2);
		}
	}

	abstract class Store
	{
		[ThreadStatic]
		static int s_max;

		// Fast average of two unsigned 32-bit integers.
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		static uint avg_u32(uint a, uint b)
		{
			return (a >> 1) + (b >> 1) + (a & b & 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		static int avg_u32(int a, int b)
		{
			return unchecked((int)avg_u32((uint)a, (uint)b));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void SetVUMeter(int max)
		{
			s_max = max;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public int GetVUMeter()
		{
			return s_max;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		protected void StoreVUMeter(int volLX, int volLY)
		{
			int volAvg = avg_u32(volLX, volLY);
			if (s_max < volAvg) s_max = volAvg;
		}

		public abstract void Do(int volL, int volR, ref int leftRampVolume, ref int rightRampVolume, ref SongVoice chan, Span<int> pVol);
	}

	// FIXME why are these backwards? what?
	class StoreVolume : Store
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(int volL, int volR, ref int leftRampVolume, ref int rightRampVolume, ref SongVoice chan, Span<int> pVol)
		{
			int volLX = volL * chan.RightVolume;
			int volRX = volR * chan.LeftVolume;

			StoreVUMeter(volLX, volRX);

			pVol[0] += volLX;
			pVol[1] += volRX;
		}
	}

	// Volume Ramps
	class StoreRampVolume : Store
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public override void Do(int volL, int volR, ref int leftRampVolume, ref int rightRampVolume, ref SongVoice chan, Span<int> pVol)
		{
			leftRampVolume += chan.LeftRamp;
			rightRampVolume += chan.RightRamp;

			int volLX = volL * (rightRampVolume >> Constants.VolumeRampPrecision);
			int volRX = volR * (leftRampVolume >> Constants.VolumeRampPrecision);

			StoreVUMeter(volLX, volRX);

			pVol[0] += volLX;
			pVol[1] += volRX;
		}
	}

	///////////////////////////////////////////////////
	// Resonant Filters

	static int FilterClip(int i) => i.Clamp(-65536, 65534);

	interface IFilter
	{
		void DoBegin(ref SongVoice channel);
		void Do(ref SongVoice channel, ref int volL, ref int volR);
		void DoEnd(ref SongVoice channel);
	}

	class FilterNone : IFilter
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void DoBegin(ref SongVoice channel)
		{
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void Do(ref SongVoice channel, ref int volL, ref int volR)
		{
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void DoEnd(ref SongVoice channel)
		{
		}
	}

	class FilterMono : IFilter
	{
		int fy0, fy1;

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void DoBegin(ref SongVoice channel)
		{
			fy0 = channel.FilterY00;
			fy1 = channel.FilterY01;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void Do(ref SongVoice channel, ref int volL, ref int volR)
		{
			int t = unchecked((int)(
				(((long)volL * channel.FilterA0)
					+ ((long)FilterClip(fy0) * channel.FilterB0)
					+ ((long)FilterClip(fy1) * channel.FilterB1)
					+ (1 << (Constants.FilterPrecision - 1)))
				>> Constants.FilterPrecision));

			fy1 = fy0; fy0 = t; volL = volR = t;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void DoEnd(ref SongVoice channel)
		{
			channel.FilterY00 = fy0;
			channel.FilterY01 = fy1;
		}
	}

	class FilterStereo : IFilter
	{
		int fy00, fy01;
		int fy10, fy11;

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void DoBegin(ref SongVoice channel)
		{
			fy00 = channel.FilterY00;
			fy01 = channel.FilterY01;
			fy10 = channel.FilterY10;
			fy11 = channel.FilterY11;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void Do(ref SongVoice channel, ref int volL, ref int volR)
		{
			int t;

			t = unchecked((int)(
				(((long)volL * channel.FilterA0)
					+ ((long)FilterClip(fy00) * channel.FilterB0)
					+ ((long)FilterClip(fy01) * channel.FilterB1)
					+ (1 << (Constants.FilterPrecision - 1)))
				>> Constants.FilterPrecision));

			fy01 = fy00; fy00 = t; volL = t;

			t = unchecked((int)(
				(((long)volR * channel.FilterA0)
					+ ((long)FilterClip(fy10) * channel.FilterB0)
					+ ((long)FilterClip(fy11) * channel.FilterB1)
					+ (1 << (Constants.FilterPrecision - 1)))
				>> Constants.FilterPrecision));

			fy11 = fy10; fy10 = t; volR = t;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void DoEnd(ref SongVoice channel)
		{
			channel.FilterY00 = fy00;
			channel.FilterY01 = fy01;
			channel.FilterY10 = fy10;
			channel.FilterY11 = fy11;
		}
	}

	class MixerKernel<TSample, TBits, TGetVolume, TFilter, TStore> : IMixerKernel
		where TSample : struct
		where TBits : Bits, new()
		where TGetVolume : GetVolume<TSample>, new()
		where TFilter : IFilter, new()
		where TStore : Store, new()
	{
		static TGetVolume GetVolume = new TGetVolume();
		static TFilter Filter = new TFilter();
		static TStore Store = new TStore();

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void Mix(ref SongVoice chan, Span<int> pVol)
		{
			var position = chan.Position;
			Span<TSample> p = MemoryMarshal.Cast<byte, TSample>(chan.CurrentSampleData.AsExtendedSpan());
			Store.SetVUMeter(chan.VUMeter);

			int leftRampVolume = chan.LeftRampVolume;
			int rightRampVolume = chan.RightRampVolume;

			Filter.DoBegin(ref chan);

			do
			{
				int l, r;

				GetVolume.Do(p, position, out l, out r);
				Filter.Do(ref chan, ref l, ref r);
				Store.Do(l, r, ref leftRampVolume, ref rightRampVolume, ref chan, pVol);

				pVol = pVol.Slice(2);
				position += chan.Increment;
			} while (pVol.Length > 1);

			Filter.DoEnd(ref chan);

			chan.RightRampVolume = rightRampVolume;
			chan.RightVolume     = (rightRampVolume >> Constants.VolumeRampPrecision);
			chan.LeftRampVolume  = leftRampVolume;
			chan.LeftVolume      = (leftRampVolume >> Constants.VolumeRampPrecision);

			chan.VUMeter = Store.GetVUMeter();
			chan.Position = position;
		}
	}

	/* --------------------------------------------------------------------------- */
	/* generate processing functions */

	static IMixerKernel GetMixerKernel(int bits, int channels, ResamplingType resampling, bool filter, bool ramp)
	{
		Type kernelTypeDefinition = typeof(MixerKernel<,,,,>);

		Type TSample =
			bits switch
			{
				8 => typeof(sbyte),
				16 => typeof(short),
				_ => throw new Exception("Invalid bits: " + bits)
			};

		Type TBits =
			bits switch
			{
				8 => typeof(Bits_8),
				16 => typeof(Bits_16),
				_ => throw new Exception("Invalid bits: " + bits)
			};

		Type TShift =
			resampling switch
			{
				ResamplingType.Spline => (bits == 8 ? typeof(Shift_Spline_8) : typeof(Shift_Spline_16)),
				ResamplingType.FIRFilter => (bits == 8 ? typeof(Shift_FIR_8) : typeof(Shift_FIR_16)),
				_ => typeof(Shift_0)
			};

		Type TGetVolumeDefinition = (channels == 1)
			? resampling switch
				{
					ResamplingType.None => typeof(GetVolume_Mono_NoIDO<,,>),
					ResamplingType.Linear => typeof(GetVolume_Mono_Linear<,,>),
					ResamplingType.Spline => typeof(GetVolume_Mono_Spline<,,>),
					ResamplingType.FIRFilter => typeof(GetVolume_Mono_FIR<,,>),
					_ => throw new Exception("Invalid resampling: " + resampling)
				}
			: resampling switch
				{
					ResamplingType.None => typeof(GetVolume_Stereo_NoIDO<,,>),
					ResamplingType.Linear => typeof(GetVolume_Stereo_Linear<,,>),
					ResamplingType.Spline => typeof(GetVolume_Stereo_Spline<,,>),
					ResamplingType.FIRFilter => typeof(GetVolume_Stereo_FIR<,,>),
					_ => throw new Exception("Invalid resampling: " + resampling)
				};

		Type TGetVolume = TGetVolumeDefinition.MakeGenericType(TSample, TBits, TShift);

		Type TFilter = (!filter || (resampling == ResamplingType.None))
			? typeof(FilterNone)
			: (channels == 1) ? typeof(FilterMono) : typeof(FilterStereo);

		Type TStore = ramp ? typeof(StoreRampVolume) : typeof(StoreVolume);

		Type kernelType = kernelTypeDefinition.MakeGenericType(TSample, TBits, TGetVolume, TFilter, TStore);

		return (IMixerKernel)Activator.CreateInstance(kernelType)!;
	}

	class Resampler8<TGetVolume, TShift, TPutVolume> : IResamplerKernel<sbyte>
		where TGetVolume : GetVolume<sbyte>, new()
		where TShift : Shift, new()
		where TPutVolume : PutVolume<sbyte>, new()
	{
		static TGetVolume GetVolume = new TGetVolume();
		static TShift Shift = new TShift();
		static TPutVolume PutVolume = new TPutVolume();

		public void Resample(Span<sbyte> oldBuf, Span<sbyte> newBuf)
		{
			var position = SamplePosition.Zero;

			Span<sbyte> p = oldBuf;
			Span<sbyte> pVol = newBuf;

			var increment = new SamplePosition(oldBuf.Length, 0) / newBuf.Length;

			do
			{
				GetVolume.Do(p, position, out int volL, out int volR);

				/* This is used to compensate, since the code assumes that it always outputs to 16bits */
				volL >>= Shift.Get();
				volR >>= Shift.Get();

				PutVolume.Do(unchecked((sbyte)volL), unchecked((sbyte)volR), ref pVol);
				position += increment;
			} while (pVol.Length > 0);
		}
	}

	class Resampler16<TGetVolume, TShift, TPutVolume> : IResamplerKernel<short>
		where TGetVolume : GetVolume<short>, new()
		where TShift : Shift, new()
		where TPutVolume : PutVolume<short>, new()
	{
		static TGetVolume GetVolume = new TGetVolume();
		static TShift Shift = new TShift();
		static TPutVolume PutVolume = new TPutVolume();

		public void Resample(Span<short> oldBuf, Span<short> newBuf)
		{
			var position = SamplePosition.Zero;

			Span<short> p = oldBuf;
			Span<short> pVol = newBuf;

			var increment = new SamplePosition(oldBuf.Length, 0) / newBuf.Length;

			do
			{
				GetVolume.Do(p, position, out int volL, out int volR);

				/* This is used to compensate, since the code assumes that it always outputs to 16bits */
				volL >>= Shift.Get();
				volR >>= Shift.Get();

				PutVolume.Do(unchecked((short)volL), unchecked((short)volR), ref pVol);
				position += increment;
			} while (pVol.Length > 0);
		}
	}

	public static IResamplerKernel<TSample> GetResampler<TSample>(bool stereo, ResamplingType resampling)
		where TSample : struct
	{
		int bits;

		if (typeof(TSample) == typeof(sbyte))
			bits = 8;
		else if (typeof(TSample) == typeof(short))
			bits = 16;
		else
			throw new Exception("Invalid sample type: " + typeof(TSample));

		Type resamplerTypeDefinition = (bits == 8)
			? typeof(Resampler8<,,>)
			: typeof(Resampler16<,,>);

		Type TBits =
			bits switch
			{
				8 => typeof(Bits_8),
				16 => typeof(Bits_16),
				_ => throw new Exception("Invalid bits: " + bits)
			};

		Type TShift =
			resampling switch
			{
				ResamplingType.Spline => (bits == 8 ? typeof(Shift_Spline_8) : typeof(Shift_Spline_16)),
				ResamplingType.FIRFilter => (bits == 8 ? typeof(Shift_FIR_8) : typeof(Shift_FIR_16)),
				_ => typeof(Shift_0)
			};

		Type TGetVolumeDefinition =
			stereo
			? resampling switch
				{
					ResamplingType.None => typeof(GetVolume_Stereo_NoIDO<,,>),
					ResamplingType.Linear => typeof(GetVolume_Stereo_Linear<,,>),
					ResamplingType.Spline => typeof(GetVolume_Stereo_Spline<,,>),
					ResamplingType.FIRFilter => typeof(GetVolume_Stereo_FIR<,,>),
					_ => throw new Exception("Invalid resampling: " + resampling)
				}
			: resampling switch
				{
					ResamplingType.None => typeof(GetVolume_Mono_NoIDO<,,>),
					ResamplingType.Linear => typeof(GetVolume_Mono_Linear<,,>),
					ResamplingType.Spline => typeof(GetVolume_Mono_Spline<,,>),
					ResamplingType.FIRFilter => typeof(GetVolume_Mono_FIR<,,>),
					_ => throw new Exception("Invalid resampling: " + resampling)
				};

		Type TGetVolume = TGetVolumeDefinition.MakeGenericType(typeof(TSample), TBits, TShift);
		Type TPutVolume = typeof(PutVolume<TSample>);

		Type resamplerType = resamplerTypeDefinition.MakeGenericType(TGetVolume, TShift, TPutVolume);

		return (IResamplerKernel<TSample>)Activator.CreateInstance(resamplerType)!;
	}

	/////////////////////////////////////////////////////////////////////////////////////
	//
	// Mix function tables
	//
	//
	// Index is as follows:
	//      [b1-b0] format (8-bit-mono, 16-bit-mono, 8-bit-stereo, 16-bit-stereo)
	//      [b2]    ramp
	//      [b3]    filter
	//      [b5-b4] src type
	const int MixIndex_16Bit = 0x01;
	const int MixIndex_Stereo = 0x02;
	const int MixIndex_Ramp = 0x04;
	const int MixIndex_Filter = 0x08;
	const int MixIndex_LinearSource = 0x10;
	const int MixIndex_SplineSource = 0x20;
	const int MixIndex_FIRSource = 0x30;

	// oof dude
	static readonly IMixerKernel[] s_mixKernels =
		{
			GetMixerKernel(8, 1, ResamplingType.None, filter: false, ramp: false),
			GetMixerKernel(16, 1, ResamplingType.None, filter: false, ramp: false),
			GetMixerKernel(8, 1, ResamplingType.None, filter: false, ramp: false),
			GetMixerKernel(16, 2, ResamplingType.None, filter: false, ramp: false),

			GetMixerKernel(8, 1, ResamplingType.None, filter: false, ramp: true),
			GetMixerKernel(16, 1, ResamplingType.None, filter: false, ramp: true),
			GetMixerKernel(8, 1, ResamplingType.None, filter: false, ramp: true),
			GetMixerKernel(16, 2, ResamplingType.None, filter: false, ramp: true),

			GetMixerKernel(8, 1, ResamplingType.None, filter: true, ramp: false),
			GetMixerKernel(16, 1, ResamplingType.None, filter: true, ramp: false),
			GetMixerKernel(8, 1, ResamplingType.None, filter: true, ramp: false),
			GetMixerKernel(16, 2, ResamplingType.None, filter: true, ramp: false),

			GetMixerKernel(8, 1, ResamplingType.None, filter: true, ramp: true),
			GetMixerKernel(16, 1, ResamplingType.None, filter: true, ramp: true),
			GetMixerKernel(8, 1, ResamplingType.None, filter: true, ramp: true),
			GetMixerKernel(16, 2, ResamplingType.None, filter: true, ramp: true),

			GetMixerKernel(8, 1, ResamplingType.Linear, filter: false, ramp: false),
			GetMixerKernel(16, 1, ResamplingType.Linear, filter: false, ramp: false),
			GetMixerKernel(8, 1, ResamplingType.Linear, filter: false, ramp: false),
			GetMixerKernel(16, 2, ResamplingType.Linear, filter: false, ramp: false),

			GetMixerKernel(8, 1, ResamplingType.Linear, filter: false, ramp: true),
			GetMixerKernel(16, 1, ResamplingType.Linear, filter: false, ramp: true),
			GetMixerKernel(8, 1, ResamplingType.Linear, filter: false, ramp: true),
			GetMixerKernel(16, 2, ResamplingType.Linear, filter: false, ramp: true),

			GetMixerKernel(8, 1, ResamplingType.Linear, filter: true, ramp: false),
			GetMixerKernel(16, 1, ResamplingType.Linear, filter: true, ramp: false),
			GetMixerKernel(8, 1, ResamplingType.Linear, filter: true, ramp: false),
			GetMixerKernel(16, 2, ResamplingType.Linear, filter: true, ramp: false),

			GetMixerKernel(8, 1, ResamplingType.Linear, filter: true, ramp: true),
			GetMixerKernel(16, 1, ResamplingType.Linear, filter: true, ramp: true),
			GetMixerKernel(8, 1, ResamplingType.Linear, filter: true, ramp: true),
			GetMixerKernel(16, 2, ResamplingType.Linear, filter: true, ramp: true),

			GetMixerKernel(8, 1, ResamplingType.Spline, filter: false, ramp: false),
			GetMixerKernel(16, 1, ResamplingType.Spline, filter: false, ramp: false),
			GetMixerKernel(8, 1, ResamplingType.Spline, filter: false, ramp: false),
			GetMixerKernel(16, 2, ResamplingType.Spline, filter: false, ramp: false),

			GetMixerKernel(8, 1, ResamplingType.Spline, filter: false, ramp: true),
			GetMixerKernel(16, 1, ResamplingType.Spline, filter: false, ramp: true),
			GetMixerKernel(8, 1, ResamplingType.Spline, filter: false, ramp: true),
			GetMixerKernel(16, 2, ResamplingType.Spline, filter: false, ramp: true),

			GetMixerKernel(8, 1, ResamplingType.Spline, filter: true, ramp: false),
			GetMixerKernel(16, 1, ResamplingType.Spline, filter: true, ramp: false),
			GetMixerKernel(8, 1, ResamplingType.Spline, filter: true, ramp: false),
			GetMixerKernel(16, 2, ResamplingType.Spline, filter: true, ramp: false),

			GetMixerKernel(8, 1, ResamplingType.Spline, filter: true, ramp: true),
			GetMixerKernel(16, 1, ResamplingType.Spline, filter: true, ramp: true),
			GetMixerKernel(8, 1, ResamplingType.Spline, filter: true, ramp: true),
			GetMixerKernel(16, 2, ResamplingType.Spline, filter: true, ramp: true),

			GetMixerKernel(8, 1, ResamplingType.FIRFilter, filter: false, ramp: false),
			GetMixerKernel(16, 1, ResamplingType.FIRFilter, filter: false, ramp: false),
			GetMixerKernel(8, 1, ResamplingType.FIRFilter, filter: false, ramp: false),
			GetMixerKernel(16, 2, ResamplingType.FIRFilter, filter: false, ramp: false),

			GetMixerKernel(8, 1, ResamplingType.FIRFilter, filter: false, ramp: true),
			GetMixerKernel(16, 1, ResamplingType.FIRFilter, filter: false, ramp: true),
			GetMixerKernel(8, 1, ResamplingType.FIRFilter, filter: false, ramp: true),
			GetMixerKernel(16, 2, ResamplingType.FIRFilter, filter: false, ramp: true),

			GetMixerKernel(8, 1, ResamplingType.FIRFilter, filter: true, ramp: false),
			GetMixerKernel(16, 1, ResamplingType.FIRFilter, filter: true, ramp: false),
			GetMixerKernel(8, 1, ResamplingType.FIRFilter, filter: true, ramp: false),
			GetMixerKernel(16, 2, ResamplingType.FIRFilter, filter: true, ramp: false),

			GetMixerKernel(8, 1, ResamplingType.FIRFilter, filter: true, ramp: true),
			GetMixerKernel(16, 1, ResamplingType.FIRFilter, filter: true, ramp: true),
			GetMixerKernel(8, 1, ResamplingType.FIRFilter, filter: true, ramp: true),
			GetMixerKernel(16, 2, ResamplingType.FIRFilter, filter: true, ramp: true),
		};

	/* yap */
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	static int DistanceToBufferLength(SamplePosition from, SamplePosition to, SamplePosition increment)
		=> unchecked((int)((((to - from) - new SamplePosition(1, 0)) / increment) + 1));

	struct MixLoopState
	{
		public SampleWindow SamplePointer;
		public SampleWindow LookaheadPointer;
		public int LookaheadStart;
		public int MaxSamples;

		public MixLoopState(ref SongVoice chan)
		{
			if (chan.CurrentSampleData.IsEmpty)
				return;

			UpdateLookaheadPointers(ref chan);

			var inv = chan.Increment.Abs();

			MaxSamples = 16384 / (inv.Whole + 1);
			MaxSamples = Math.Max(MaxSamples, 2);
		}

		public void UpdateLookaheadPointers(ref SongVoice channel)
		{
			// Our loop lookahead buffer is basically the exact same as OpenMPT's.
			// (in essence, it is mostly just a backport)
			//
			// This means that it has the same bugs that are notated in OpenMPT's
			// `soundlib/Fastmix.cpp' file, which are the following:
			//
			// - Playing samples backwards should reverse interpolation LUTs for interpolation modes
			//   with more than two taps since they're not symmetric. We might need separate LUTs
			//   because otherwise we will add tons of branches.
			// - Loop wraparound works pretty well in general, but not at the start of bidi samples.
			// - The loop lookahead stuff might still fail for sampes with backward loops.
			SamplePointer = channel.Sample?.Data ?? SampleWindow.Empty;
			LookaheadPointer = SampleWindow.Empty;
			LookaheadStart = (channel.LoopEnd < Constants.MaxInterpolationLookaheadBufferSize)
				? channel.LoopStart
				: Math.Max(channel.LoopStart, channel.LoopEnd - Constants.MaxInterpolationLookaheadBufferSize);

			// This shouldn't be necessary with interpolation disabled but with that conditional
			// it causes weird precision loss within the sample, hence why I've removed it. This
			// shouldn't be that heavy anyway :p
			if ((channel.Sample != null) && channel.Flags.HasAllFlags(ChannelFlags.Loop))
			{
				var pIns = channel.Sample;

				var lookaheadOffset = ((channel.Flags.HasFlag(ChannelFlags.SustainLoop) ? 7 : 3) * Constants.MaxInterpolationLookaheadBufferSize)
					+ (pIns.Length - channel.LoopEnd);

				LookaheadPointer = SamplePointer.Shift(lookaheadOffset);
			}
		}

		public int GetSampleCount(ref SongVoice chan, int samples)
		{
			var loopStart = chan.Flags.HasAllFlags(ChannelFlags.Loop) ? new SamplePosition(chan.LoopStart, 0) : SamplePosition.Zero;
			var increment = chan.Increment;

			if (samples <= 0 || (increment == 0) || (chan.Length == 0))
				return 0;

			/* reset this */
			chan.CurrentSampleData = SamplePointer;

			// Under zero ?
			if (chan.Position < loopStart)
			{
				if (increment < 0)
				{
					// Invert loop for bidi loops
					var delta = loopStart - chan.Position;
					chan.Position = loopStart + delta;

					if (chan.Position < loopStart
					|| chan.Position >= (loopStart + chan.Length) / 2)
						chan.Position = loopStart;

					increment = -increment;
					chan.Increment = increment;
					// go forward
					chan.Flags &= ~ChannelFlags.PingPongFlag;

					if ((!chan.Flags.HasAllFlags(ChannelFlags.Loop))
					 || (chan.Position >= chan.Length))
					{
						chan.Position = new SamplePosition(chan.Length, 0);
						return 0;
					}
				}
				else
				{
					// We probably didn't hit the loop end yet (first loop), so we do nothing
					if (chan.Position < 0)
						chan.Position = SamplePosition.Zero;
				}
			}
			// Past the end
			else if (chan.Position >= chan.Length)
			{
				// not looping -> stop this channel
				if (!chan.Flags.HasAllFlags(ChannelFlags.Loop))
					return 0;

				if (chan.Flags.HasAllFlags(ChannelFlags.PingPongLoop))
				{
					// Invert loop
					if (increment > 0)
					{
						increment = -increment;
						chan.Increment = increment;
					}

					chan.Flags |= ChannelFlags.PingPongFlag;

					// adjust loop position
					var overshoot = chan.Position - chan.Length;
					var loopLength = new SamplePosition(chan.LoopEnd - chan.LoopStart - PingPongOffset, 0);

					if (overshoot < loopLength)
						chan.Position = new SamplePosition(chan.Length - PingPongOffset, 0) - overshoot;
					else
					{
						/* not 100% accurate, but only matters for extremely small loops played at extremely high frequencies */
						chan.Position = new SamplePosition(chan.LoopStart, 0);
					}
				}
				else
				{
					// This is a bug
					if (increment < 0)
					{
						increment = -increment;
						chan.Increment = increment;
					}

					// Restart at loop start
					chan.Position += loopStart - chan.Length;

					if (chan.Position < loopStart)
						chan.Position = new SamplePosition(chan.LoopStart, 0);

					chan.Flags |= ChannelFlags.LoopWrapped;
				}
			}

			var position = chan.Position.Whole;

			// too big increment, and/or too small loop length
			if (position < loopStart.Whole)
			{
				if (position < 0 || increment < 0)
					return 0;
			}

			if (position < 0 || position >= chan.Length)
				return 0;

			int sampleCount = samples;

			var inv = increment.Abs();

			sampleCount = Math.Min(sampleCount, MaxSamples);

			var incSamples = increment * (sampleCount - 1);
			int posDest = (chan.Position + incSamples).Whole;

			bool atLoopStart = (chan.Position >= chan.LoopStart)
				&& (chan.Position < chan.LoopStart + Constants.MaxInterpolationLookaheadBufferSize);

			if (!atLoopStart)
				chan.Flags &= ~ChannelFlags.LoopWrapped;

			bool checkDest = true;

			// Loop wrap-around magic. (yummers)
			if (!LookaheadPointer.IsEmpty)
			{
				if (chan.Position >= LookaheadStart)
				{
					if (chan.Increment < 0)
					{
						// going backwards and we're in the loop. We have to set the sample count
						// from the position from lookahead buffer start...
						sampleCount = DistanceToBufferLength(new SamplePosition(LookaheadStart, 0), chan.Position, inv);
						chan.CurrentSampleData = LookaheadPointer;
					}
					else if (chan.Position <= chan.LoopEnd)
					{
						// going forwards, and we're in the loop
						sampleCount = DistanceToBufferLength(chan.Position, new SamplePosition(chan.LoopEnd, 0), inv);
						chan.CurrentSampleData = LookaheadPointer;
					}
					else
					{
						// loop has ended, fix the position and keep going
						sampleCount = DistanceToBufferLength(chan.Position, new SamplePosition(chan.Length, 0), inv);
					}

					checkDest = false;
				}
				else if (chan.Flags.HasAllFlags(ChannelFlags.LoopWrapped) && atLoopStart)
				{
					// Interpolate properly after looping
					sampleCount = DistanceToBufferLength(chan.Position, loopStart + Constants.MaxInterpolationLookaheadBufferSize, inv);
					chan.CurrentSampleData = LookaheadPointer.Shift(chan.Length - loopStart.Whole);
					checkDest = false;
				}
				else if ((chan.Increment > 0) && (posDest >= LookaheadStart) && (sampleCount > 1))
				{
					// Don't go past the loop start!
					sampleCount = DistanceToBufferLength(chan.Position, new SamplePosition(LookaheadStart, 0), inv);
					checkDest = false;
				}
			}

			if (checkDest)
			{
				if (increment < 0)
				{
					if (posDest < loopStart)
						sampleCount = DistanceToBufferLength(loopStart, chan.Position, inv);
				}
				else
				{
					if (posDest >= chan.Length)
						sampleCount = DistanceToBufferLength(chan.Position, new SamplePosition(chan.Length, 0), inv);
				}
			}

			sampleCount = sampleCount.Clamp(1, samples);

			return sampleCount;
		}
	}

	public static int CreateStereoMix(Song csf, int count)
	{
		if (count == 0)
			return 0;

		int numChannelsUsed = 0;
		int numChannelsMixed = 0;

		// yuck
		if (csf.MultiWrite != null)
			for (int nchan = 0; nchan < Constants.MaxChannels; nchan++)
				Array.Clear(csf.MultiWrite[nchan].Buffer);

		for (int nChan = 0; nChan < csf.NumVoices; nChan++)
		{
			ref SongVoice channel = ref csf.Voices[csf.VoiceMix[nChan]];

			if ((channel.CurrentSampleData.IsEmpty || (channel.Sample == null) /* HAX */)
				&& (channel.LOfs == 0)
				&& (channel.ROfs == 0))
				continue;

			var channelSample = channel.Sample;

			if (channelSample == null)
				continue;

			int flags = 0;

			if (channel.Flags.HasAllFlags(ChannelFlags._16Bit))
				flags |= MixIndex_16Bit;

			if (channel.Flags.HasAllFlags(ChannelFlags.Stereo))
				flags |= MixIndex_Stereo;

			if (channel.Flags.HasAllFlags(ChannelFlags.Filter))
				flags |= MixIndex_Filter;

			if (!channel.Flags.HasAllFlags(ChannelFlags.NoIDO))
			{
				switch (AudioPlayback.MixInterpolation)
				{
					case SourceMode.Nearest:   flags |= 0;                     break;
					case SourceMode.Linear:    flags |= MixIndex_LinearSource; break;
					case SourceMode.Spline:    flags |= MixIndex_SplineSource; break;
					case SourceMode.Polyphase: flags |= MixIndex_FIRSource;    break;
				}
			}

			int numSamples = count;

			Span<int> pBuffer;

			if (csf.MultiWrite != null)
			{
				int master = (csf.VoiceMix[nChan] < Constants.MaxChannels)
					? csf.VoiceMix[nChan]
					: (channel.MasterChannel - 1);
				pBuffer = csf.MultiWrite[master].Buffer;
				csf.MultiWrite[master].IsUsed = true;
			}
			else
				pBuffer = csf.MixBuffer;

			numChannelsUsed++;

			////////////////////////////////////////////////////
			int nAddMix = 0;

			var mls = new MixLoopState(ref channel);

			channel.VUMeter <<= 16;

			do
			{
				int nrampsamples = numSamples;

				if (channel.RampLength > 0)
				{
					if (nrampsamples > channel.RampLength)
						nrampsamples = channel.RampLength;
				}

				int sampleCount = 1;

				/* Figure out the number of remaining samples,
				* unless we're in AdLib or MIDI mode (to prevent
				* artificial KeyOffs)
				*/
				if (!channel.Flags.HasAllFlags(ChannelFlags.AdLib))
					sampleCount = mls.GetSampleCount(ref channel, nrampsamples);

				if (sampleCount <= 0)
				{
					// Stopping the channel
					channel.CurrentSampleData = SampleWindow.Empty;
					channel.Length = 0;
					channel.Position = SamplePosition.Zero;
					channel.RampLength = 0;
					MixUtility.EndChannelOfs(ref channel, pBuffer, numSamples);
					AudioPlayback.DryROfsVol += channel.ROfs;
					AudioPlayback.DryLOfsVol += channel.LOfs;
					channel.ROfs = channel.LOfs = 0;
					channel.Flags &= ~ChannelFlags.PingPongFlag;
					break;
				}

				// Should we mix this channel ?

				if ((numChannelsMixed >= AudioPlayback.MaxVoices && !AudioPlayback.MixFlags.HasAllFlags(MixFlags.DirectToDisk))
					|| ((channel.RampLength == 0) && ((channel.LeftVolume | channel.RightVolume) == 0)))
				{
					channel.Position += channel.Increment * sampleCount;
					channel.ROfs = channel.LOfs = 0;

					pBuffer = pBuffer.Slice(sampleCount * 2);
				}
				else if (!channel.Flags.HasAllFlags(ChannelFlags.AdLib))
				{
					// Mix the stream, unless we're in AdLib mode

					// Choose function for mixing
					var mixKernel = (channel.RampLength != 0)
						? s_mixKernels[flags | MixIndex_Ramp]
						: s_mixKernels[flags];

					var pbufferRange = pBuffer.Slice(0, sampleCount * 2);

					var lastSample = pBuffer.Slice(sampleCount * 2 - 2);

					channel.ROfs = -lastSample[0];
					channel.LOfs = -lastSample[1];

					mixKernel.Mix(ref channel, pbufferRange);

					channel.ROfs += lastSample[0];
					channel.LOfs += lastSample[1];

					pBuffer = pBuffer.Slice(sampleCount * 2);
					nAddMix = 1;
				}

				numSamples -= sampleCount;

				if (channel.RampLength != 0)
				{
					if (channel.RampLength <= sampleCount)
					{
						// Ramping is done
						channel.RampLength = 0;
						channel.RightVolume = channel.RightVolumeNew;
						channel.LeftVolume = channel.LeftVolumeNew;
						channel.RightRamp = channel.LeftRamp = 0;

						if (channel.Flags.HasAllFlags(ChannelFlags.NoteFade)
						 && (channel.FadeOutVolume == 0))
						{
							channel.Length = 0;
							channel.CurrentSampleData = SampleWindow.Empty;
						}
					}
					else
						channel.RampLength -= sampleCount;
				}
			} while (numSamples > 0);

			/* Restore sample pointer in case it got changed through loop wrap-around */
			channel.CurrentSampleData = mls.SamplePointer;

			channel.VUMeter >>= 16;
			if (channel.VUMeter > 0xFF)
				channel.VUMeter = 0xFF;

			numChannelsMixed += nAddMix;
		}

		GeneralMIDI.IncrementSongCounter(csf, count);

		csf.OPL?.Mix(csf, count);

		return numChannelsUsed;
	}
}
