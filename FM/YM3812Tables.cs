using System;
using ChasmTracker.Utility;

namespace ChasmTracker.FM;

public static class YM3812Tables
{
	/* mapping of register number (offset) to slot number used by the emulator */
	public static readonly int[] slot_array =
		{
			0, 2, 4, 1, 3, 5,-1,-1,
			6, 8,10, 7, 9,11,-1,-1,
			12,14,16,13,15,17,-1,-1,
			-1,-1,-1,-1,-1,-1,-1,-1
		};

	/* key scale level */
	/* table is 3dB/octave , DV converts this into 6dB/octave */
	/* 0.1875 is bit 0 weight of the envelope counter (volume) expressed in the 'decibel' scale */
	static uint KSC(double x) => (uint)(x / (0.1875 / 2.0));

	public static readonly uint[] ksl_tab =
		{
			/* OCT 0 */
			KSC(0.000), KSC(0.000), KSC(0.000), KSC(0.000),
			KSC(0.000), KSC(0.000), KSC(0.000), KSC(0.000),
			KSC(0.000), KSC(0.000), KSC(0.000), KSC(0.000),
			KSC(0.000), KSC(0.000), KSC(0.000), KSC(0.000),
			/* OCT 1 */
			KSC(0.000), KSC(0.000), KSC(0.000), KSC(0.000),
			KSC(0.000), KSC(0.000), KSC(0.000), KSC(0.000),
			KSC(0.000), KSC(0.750), KSC(1.125), KSC(1.500),
			KSC(1.875), KSC(2.250), KSC(2.625), KSC(3.000),
			/* OCT 2 */
			KSC(0.000), KSC(0.000), KSC(0.000), KSC(0.000),
			KSC(0.000), KSC(1.125), KSC(1.875), KSC(2.625),
			KSC(3.000), KSC(3.750), KSC(4.125), KSC(4.500),
			KSC(4.875), KSC(5.250), KSC(5.625), KSC(6.000),
			/* OCT 3 */
			KSC(0.000), KSC(0.000), KSC(0.000), KSC(1.875),
			KSC(3.000), KSC(4.125), KSC(4.875), KSC(5.625),
			KSC(6.000), KSC(6.750), KSC(7.125), KSC(7.500),
			KSC(7.875), KSC(8.250), KSC(8.625), KSC(9.000),
			/* OCT 4 */
			KSC(0.000), KSC(0.000), KSC(3.000), KSC(4.875),
			KSC(6.000), KSC(7.125), KSC(7.875), KSC(8.625),
			KSC(9.000), KSC(9.750), KSC(10.125),KSC(10.500),
			KSC(10.875),KSC(11.250),KSC(11.625),KSC(12.000),
			/* OCT 5 */
			KSC(0.000), KSC(3.000), KSC(6.000), KSC(7.875),
			KSC(9.000), KSC(10.125),KSC(10.875),KSC(11.625),
			KSC(12.000),KSC(12.750),KSC(13.125),KSC(13.500),
			KSC(13.875),KSC(14.250),KSC(14.625),KSC(15.000),
			/* OCT 6 */
			KSC(0.000), KSC(6.000), KSC(9.000), KSC(10.875),
			KSC(12.000),KSC(13.125),KSC(13.875),KSC(14.625),
			KSC(15.000),KSC(15.750),KSC(16.125),KSC(16.500),
			KSC(16.875),KSC(17.250),KSC(17.625),KSC(18.000),
			/* OCT 7 */
			KSC(0.000), KSC(9.000), KSC(12.000),KSC(13.875),
			KSC(15.000),KSC(16.125),KSC(16.875),KSC(17.625),
			KSC(18.000),KSC(18.750),KSC(19.125),KSC(19.500),
			KSC(19.875),KSC(20.250),KSC(20.625),KSC(21.000)
		};

	/* 0 / 3.0 / 1.5 / 6.0 dB/OCT */
	public static readonly uint[] ksl_shift = { 31, 1, 2, 0 };

	/* sustain level table (3dB per step) */
	/* 0 - 15: 0, 3, 6, 9,12,15,18,21,24,27,30,33,36,39,42,93 (dB)*/
	static uint SC(double db) => (uint)(db * (2.0 / YM3812FMDriver.EnvelopeStep));

	public static readonly uint[] sl_tab =
		{
			SC( 0),SC( 1),SC( 2),SC(3 ),SC(4 ),SC(5 ),SC(6 ),SC( 7),
			SC( 8),SC( 9),SC(10),SC(11),SC(12),SC(13),SC(14),SC(31)
		};

	public static readonly byte[] eg_inc =
		{
			/*cycle:0 1  2 3  4 5  6 7*/

			/* 0 */ 0,1, 0,1, 0,1, 0,1, /* rates 00..12 0 (increment by 0 or 1) */
			/* 1 */ 0,1, 0,1, 1,1, 0,1, /* rates 00..12 1 */
			/* 2 */ 0,1, 1,1, 0,1, 1,1, /* rates 00..12 2 */
			/* 3 */ 0,1, 1,1, 1,1, 1,1, /* rates 00..12 3 */

			/* 4 */ 1,1, 1,1, 1,1, 1,1, /* rate 13 0 (increment by 1) */
			/* 5 */ 1,1, 1,2, 1,1, 1,2, /* rate 13 1 */
			/* 6 */ 1,2, 1,2, 1,2, 1,2, /* rate 13 2 */
			/* 7 */ 1,2, 2,2, 1,2, 2,2, /* rate 13 3 */

			/* 8 */ 2,2, 2,2, 2,2, 2,2, /* rate 14 0 (increment by 2) */
			/* 9 */ 2,2, 2,4, 2,2, 2,4, /* rate 14 1 */
			/*10 */ 2,4, 2,4, 2,4, 2,4, /* rate 14 2 */
			/*11 */ 2,4, 4,4, 2,4, 4,4, /* rate 14 3 */

			/*12 */ 4,4, 4,4, 4,4, 4,4, /* rates 15 0, 15 1, 15 2, 15 3 (increment by 4) */
			/*13 */ 8,8, 8,8, 8,8, 8,8, /* rates 15 2, 15 3 for attack */
			/*14 */ 0,0, 0,0, 0,0, 0,0, /* infinity rates for attack and decay(s) */
		};

	static byte O(int a) => unchecked((byte)(a * YM3812FMDriver.RateSteps));

	/*note that there is no O(13) in this table - it's directly in the code */
	/* Envelope Generator rates (16 + 64 rates + 16 RKS) */
	public static readonly byte[] eg_rate_select =
		{
			/* 16 infinite time rates */
			O(14),O(14),O(14),O(14),O(14),O(14),O(14),O(14),
			O(14),O(14),O(14),O(14),O(14),O(14),O(14),O(14),

			/* rates 00-12 */
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),
			O( 0),O( 1),O( 2),O( 3),

			/* rate 13 */
			O( 4),O( 5),O( 6),O( 7),

			/* rate 14 */
			O( 8),O( 9),O(10),O(11),

			/* rate 15 */
			O(12),O(12),O(12),O(12),

			/* 16 dummy rates (same as 15 3) */
			O(12),O(12),O(12),O(12),O(12),O(12),O(12),O(12),
			O(12),O(12),O(12),O(12),O(12),O(12),O(12),O(12),
		};

	/*rate  0,    1,    2,    3,   4,   5,   6,  7,  8,  9,  10, 11, 12, 13, 14, 15 */
	/*shift 12,   11,   10,   9,   8,   7,   6,  5,  4,  3,  2,  1,  0,  0,  0,  0  */
	/*mask  4095, 2047, 1023, 511, 255, 127, 63, 31, 15, 7,  3,  1,  0,  0,  0,  0  */

	static byte P(int a) => unchecked((byte)(a * 1));

	/* Envelope Generator counter shifts (16 + 64 rates + 16 RKS) */
	public static readonly byte[] eg_rate_shift =
		{
			/* 16 infinite time rates */
			P(0),P(0),P(0),P(0),P(0),P(0),P(0),P(0),
			P(0),P(0),P(0),P(0),P(0),P(0),P(0),P(0),

			/* rates 00-12 */
			P(12),P(12),P(12),P(12),
			P(11),P(11),P(11),P(11),
			P(10),P(10),P(10),P(10),
			P( 9),P( 9),P( 9),P( 9),
			P( 8),P( 8),P( 8),P( 8),
			P( 7),P( 7),P( 7),P( 7),
			P( 6),P( 6),P( 6),P( 6),
			P( 5),P( 5),P( 5),P( 5),
			P( 4),P( 4),P( 4),P( 4),
			P( 3),P( 3),P( 3),P( 3),
			P( 2),P( 2),P( 2),P( 2),
			P( 1),P( 1),P( 1),P( 1),
			P( 0),P( 0),P( 0),P( 0),

			/* rate 13 */
			P( 0),P( 0),P( 0),P( 0),

			/* rate 14 */
			P( 0),P( 0),P( 0),P( 0),

			/* rate 15 */
			P( 0),P( 0),P( 0),P( 0),

			/* 16 dummy rates (same as 15 3) */
			P( 0),P( 0),P( 0),P( 0),P( 0),P( 0),P( 0),P( 0),
			P( 0),P( 0),P( 0),P( 0),P( 0),P( 0),P( 0),P( 0),
		};

	/* multiple table */
	const int ML = 2;

	public static readonly byte[] mul_tab =
		{
		/* 1/2, 1, 2, 3, 4, 5, 6, 7, 8, 9,10,10,12,12,15,15 */
			ML/2, 1*ML, 2*ML, 3*ML, 4*ML, 5*ML, 6*ML, 7*ML,
			8*ML, 9*ML,10*ML,10*ML,12*ML,12*ML,15*ML,15*ML
		};

	/* sinwave entries */
	public const int SineBits      = 10;
	public const int SineLength    = 1 << SineBits;
	public const int SineMask      = SineLength - 1;

	/* sin waveform table in 'decibel' scale */
	/* four waveforms on OPL2 type chips */
	public static readonly uint[] sin_tab = new uint[SineLength * 4];

	public const int TLResolutionLength = 256; /* 8 bits addressing (real chip) */
	/*  TLTableLength is calculated as:
	*   12 - sinus amplitude bits     (Y axis)
	*   2  - sinus sign bit           (Y axis)
	*   TLResolutionLength - sinus resolution (X axis)
	*/
	public const int TLTableLength = 12 * 2 * TLResolutionLength;

	public static int[] tl_tab = new int[TLTableLength];

	/* LFO Amplitude Modulation table (verified on real YM3812)
		27 output levels (triangle waveform); 1 level takes one of: 192, 256 or 448 samples

		Length: 210 elements.

			Each of the elements has to be repeated
			exactly 64 times (on 64 consecutive samples).
			The whole table takes: 64 * 210 = 13440 samples.

			When AM = 1 data is used directly
			When AM = 0 data is divided by 4 before being used (losing precision is important)
	*/

	public static readonly byte[] lfo_am_table =
		{
			0,0,0,0,0,0,0,
			1,1,1,1,
			2,2,2,2,
			3,3,3,3,
			4,4,4,4,
			5,5,5,5,
			6,6,6,6,
			7,7,7,7,
			8,8,8,8,
			9,9,9,9,
			10,10,10,10,
			11,11,11,11,
			12,12,12,12,
			13,13,13,13,
			14,14,14,14,
			15,15,15,15,
			16,16,16,16,
			17,17,17,17,
			18,18,18,18,
			19,19,19,19,
			20,20,20,20,
			21,21,21,21,
			22,22,22,22,
			23,23,23,23,
			24,24,24,24,
			25,25,25,25,
			26,26,26,
			25,25,25,25,
			24,24,24,24,
			23,23,23,23,
			22,22,22,22,
			21,21,21,21,
			20,20,20,20,
			19,19,19,19,
			18,18,18,18,
			17,17,17,17,
			16,16,16,16,
			15,15,15,15,
			14,14,14,14,
			13,13,13,13,
			12,12,12,12,
			11,11,11,11,
			10,10,10,10,
			9,9,9,9,
			8,8,8,8,
			7,7,7,7,
			6,6,6,6,
			5,5,5,5,
			4,4,4,4,
			3,3,3,3,
			2,2,2,2,
			1,1,1,1
		};

	/* LFO Phase Modulation table (verified on real YM3812) */
	public static readonly sbyte[] lfo_pm_table =
		{
			/* FNUM2/FNUM = 00 0xxxxxxx (0x0000) */
			0, 0, 0, 0, 0, 0, 0, 0, /*LFO PM depth = 0*/
			0, 0, 0, 0, 0, 0, 0, 0, /*LFO PM depth = 1*/

			/* FNUM2/FNUM = 00 1xxxxxxx (0x0080) */
			0, 0, 0, 0, 0, 0, 0, 0, /*LFO PM depth = 0*/
			1, 0, 0, 0,-1, 0, 0, 0, /*LFO PM depth = 1*/

			/* FNUM2/FNUM = 01 0xxxxxxx (0x0100) */
			1, 0, 0, 0,-1, 0, 0, 0, /*LFO PM depth = 0*/
			2, 1, 0,-1,-2,-1, 0, 1, /*LFO PM depth = 1*/

			/* FNUM2/FNUM = 01 1xxxxxxx (0x0180) */
			1, 0, 0, 0,-1, 0, 0, 0, /*LFO PM depth = 0*/
			3, 1, 0,-1,-3,-1, 0, 1, /*LFO PM depth = 1*/

			/* FNUM2/FNUM = 10 0xxxxxxx (0x0200) */
			2, 1, 0,-1,-2,-1, 0, 1, /*LFO PM depth = 0*/
			4, 2, 0,-2,-4,-2, 0, 2, /*LFO PM depth = 1*/

			/* FNUM2/FNUM = 10 1xxxxxxx (0x0280) */
			2, 1, 0,-1,-2,-1, 0, 1, /*LFO PM depth = 0*/
			5, 2, 0,-2,-5,-2, 0, 2, /*LFO PM depth = 1*/

			/* FNUM2/FNUM = 11 0xxxxxxx (0x0300) */
			3, 1, 0,-1,-3,-1, 0, 1, /*LFO PM depth = 0*/
			6, 3, 0,-3,-6,-3, 0, 3, /*LFO PM depth = 1*/

			/* FNUM2/FNUM = 11 1xxxxxxx (0x0380) */
			3, 1, 0,-1,-3,-1, 0, 1, /*LFO PM depth = 0*/
			7, 3, 0,-3,-7,-3, 0, 3  /*LFO PM depth = 1*/
		};

	/* lock level of common table */
	public static int num_lock = 0;

	/* generic table initialize */
	public static bool InitializeTables()
	{
		double o;

		for (int x = 0; x < TLResolutionLength; x++)
		{
			double m = (1<<16) / Math.Pow(2.0, (x+1) * (YM3812FMDriver.EnvelopeStep / 4.0) / 8.0);
			m = Math.Floor(m);

			/* we never reach (1<<16) here due to the (x+1) */
			/* result fits within 16 bits at maximum */

			int n = (int)m;     /* 16 bits here */
			n >>= 4;        /* 12 bits here */
			if (n.HasBitSet(1))        /* round to nearest */
				n = (n>>1)+1;
			else
				n = n>>1;
							/* 11 bits here (rounded) */
			n <<= 1;        /* 12 bits here (as in real chip) */
			tl_tab[ x*2 + 0 ] = n;
			tl_tab[ x*2 + 1 ] = -tl_tab[ x*2 + 0 ];

			for (int i=1; i<12; i++)
			{
				tl_tab[ x*2+0 + i*2*TLResolutionLength ] =  tl_tab[ x*2+0 ]>>i;
				tl_tab[ x*2+1 + i*2*TLResolutionLength ] = -tl_tab[ x*2+0 + i*2*TLResolutionLength ];
			}
#if false
			Console.Error.Write("tl {0:d4}", x*2);
			for (int i=0; i<12; i++)
				Console.Error.Write(", [{0:d2}] {1:#####}", i*2, tl_tab[ x*2 /*+1*/ + i*2*TLResolutionLength ] );
			Console.Error.WriteLine();
#endif
		}

		/*Console.Error.WriteLine("FMOPL.C: TLTableLength = {0} elements ({1} bytes)", TLTableLength, tl_tab.Length * 4);*/

		for (int i=0; i < SineLength; i++)
		{
			/* non-standard sinus */
			double m = Math.Sin( ((i*2)+1) * Math.PI / SineLength ); /* checked against the real chip */

			/* we never reach zero here due to ((i*2)+1) */

			if (m > 0.0)
				o = 8*Math.Log(1.0/m)/Math.Log(2.0);  /* convert to 'decibels' */
			else
				o = 8*Math.Log(-1.0/m)/Math.Log(2.0); /* convert to 'decibels' */

			o = o / (YM3812FMDriver.EnvelopeStep/4);

			int n = (int)(2.0*o);
			if (n.HasBitSet(1))                        /* round to nearest */
				n = (n>>1)+1;
			else
				n = n>>1;

			sin_tab[ i ] = unchecked((uint)(n*2 + (m>=0.0? 0: 1 )));

			/*Console.Error.WriteLine("FMOPL.C: sin [{0:####} (hex={1:x3})]= {2:####} (tl_tab value={3:#####})", i, i,
				sin_tab[i], tl_tab[YM3812Tables.sin_tab[i]] );*/
		}

		for (int i=0; i<SineLength; i++)
		{
			/* waveform 1:  __      __     */
			/*             /  \____/  \____*/
			/* output only first half of the sinus waveform (positive one) */

			if (i.HasBitSet(1 << (SineBits-1)))
				sin_tab[1*SineLength+i] = TLTableLength;
			else
				sin_tab[1*SineLength+i] = sin_tab[i];

			/* waveform 2:  __  __  __  __ */
			/*             /  \/  \/  \/  \*/
			/* abs(sin) */

			sin_tab[2*SineLength+i] = sin_tab[i & (SineMask>>1) ];

			/* waveform 3:  _   _   _   _  */
			/*             / |_/ |_/ |_/ |_*/
			/* abs(output only first quarter of the sinus waveform) */

			if (i.HasBitSet(1 << (SineBits-2)))
				sin_tab[3*SineLength+i] = TLTableLength;
			else
				sin_tab[3*SineLength+i] = sin_tab[i & (SineMask>>2)];

			/*Console.Error.WriteLine("FMOPL.C: sin1[{0:####}]= {1:####} (tl_tab value={2:#####})", i,
				sin_tab[1*SineLength+i], tl_tab[sin_tab[1*SineLength+i]] );
			Console.Error.WriteLine("FMOPL.C: sin2[{0:####}]= {1:####} (tl_tab value={2:#####})", i,
				sin_tab[2*SineLength+i], tl_tab[sin_tab[2*SineLength+i]] );
			Console.Error.WriteLine("FMOPL.C: sin3[{0:####}]= {1:####} (tl_tab value={2:#####})", i,
				sin_tab[3*SineLength+i], tl_tab[sin_tab[3*SineLength+i]] );*/
		}
		/*Console.Error.WriteLine("FMOPL.C: EnvelopeQuiet= {0:x8} (dec*8={1})", EnvelopeQuiet, EnvelopeQuiet*8 );*/

		return true;
	}
}
