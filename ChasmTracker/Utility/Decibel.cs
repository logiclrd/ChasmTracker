using System;

namespace ChasmTracker.Utility;

public class Decibel
{
	/*Conversion*/
	/* linear -> deciBell*/
	/* amplitude normalized to 1.0.*/
	public static double dB(double amplitude)
	{
		return 20.0f * Math.Log10(amplitude);
	}

	/// deciBell -> linear*/
	public static double dB2_amp(double db)
	{
		return Math.Pow(10.0, db / 20.0);
	}

	/* linear -> deciBell*/
	/* power normalized to 1.0.*/
	public static double pdB(double power)
	{
		return 10.0 * Math.Log10(power);
	}

	/* deciBell -> linear*/
	public static double dB2_power(double db)
	{
		return Math.Pow(10.0, db / 10.0);
	}

	/* linear -> deciBell*/
	/* amplitude normalized to 1.0f.*/
	/* Output scaled (and clipped) to 128 lines with noisefloor range.*/
	/* ([0..128] = [-noisefloor..0dB])*/
	/* correction_dBs corrects the dB after converted, but before scaling.*/
	public static short dB_s(int noisefloor, double amplitude, double correction_dBs)
	{
		double db = dB(amplitude) + correction_dBs;
		int x = (int)(128.0f * (db + noisefloor)) / noisefloor;
		return (short)x.Clamp(0, 127);
	}

	/* deciBell -> linear*/
	/* Input scaled to 128 lines with noisefloor range.*/
	/* ([0..128] = [-noisefloor..0dB])*/
	/* amplitude normalized to 1.0f.*/
	/* correction_dBs corrects the dB after converted, but before scaling.*/
	public static short dB2_amp_s(int noisefloor, int db, double correction_dBs)
	{
		return (short)dB2_amp((db * noisefloor / 128.0) - noisefloor - correction_dBs);
	}

	/* linear -> deciBell*/
	/* power normalized to 1.0f.*/
	/* Output scaled (and clipped) to 128 lines with noisefloor range.*/
	/* ([0..128] = [-noisefloor..0dB])*/
	/* correction_dBs corrects the dB after converted, but before scaling.*/
	public static short pdB_s(int noisefloor, double power, double correction_dBs)
	{
		double db = pdB(power) + correction_dBs;
		int x = (int)(128.0 * (db + noisefloor)) / noisefloor;
		return (short)x.Clamp(0, 127);
	}

	/* deciBell -> linear*/
	/* Input scaled to 128 lines with noisefloor range.*/
	/* ([0..128] = [-noisefloor..0dB])*/
	/* power normalized to 1.0f.*/
	/* correction_dBs corrects the dB after converted, but before scaling.*/
	public static short dB2_power_s(int noisefloor, int db, double correction_dBs)
	{
		return (short)dB2_power((db * noisefloor / 128.0) - noisefloor - correction_dBs);
	}
}
