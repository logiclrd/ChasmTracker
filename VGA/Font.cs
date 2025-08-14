using System;
using System.IO;

namespace ChasmTracker.VGA;

using ChasmTracker.Configurations;
using ChasmTracker.Utility;

public class Font
{
	/* int font_width = 8, font_height = 8; */

	public static byte[] Normal = Array.Empty<byte>();
	public static byte[] HalfData = Array.Empty<byte>();
	public static byte[] Alt = Array.Empty<byte>();

	public static byte[] Data = Normal;

	/* this needs to be called before any char drawing.
	 * it's pretty much the same as doing...
	 *         if (!font.Load("font.cfg"))
	 *                 font.Reset();
	 * ... the main difference being font.Initialize() is easier to deal with :) */
	public static void Initialize()
	{
		HalfData = DefaultFonts.HalfWidth.MakeCopy();

		if ((Configuration.General.Font == null)
		 || !Load(Configuration.General.Font))
			Reset();

		Data = Normal;

		Alt = new byte[2048];

		DefaultFonts.Lower.CopyTo(Alt.AsSpan());
		DefaultFonts.UpperAlt.CopyTo(Alt.Slice(1024));
	}

	/* --------------------------------------------------------------------- */
	/* ITF loader */

	static void MakeHalfWidthMidDot()
	{
		/* this copies the left half of char 184 in the normal font (two
		* half-width dots) to char 173 of the half-width font (the
		* middot), and the right half to char 184. thus, putting
		* together chars 173 and 184 of the half-width font will
		* produce the equivalent of 184 of the full-width font. */

		HalfData[173 * 4 + 0] = (byte)(
			(Normal[184 * 8 + 0] & 0xf0) |
			(Normal[184 * 8 + 1] & 0xf0) >> 4);
		HalfData[173 * 4 + 1] = (byte)(
			(Normal[184 * 8 + 2] & 0xf0) |
			(Normal[184 * 8 + 3] & 0xf0) >> 4);
		HalfData[173 * 4 + 2] = (byte)(
			(Normal[184 * 8 + 4] & 0xf0) |
			(Normal[184 * 8 + 5] & 0xf0) >> 4);
		HalfData[173 * 4 + 3] = (byte)(
			(Normal[184 * 8 + 6] & 0xf0) |
			(Normal[184 * 8 + 7] & 0xf0) >> 4);

		HalfData[184 * 4 + 0] = (byte)(
			(Normal[184 * 8 + 0] & 0xf) << 4 |
			(Normal[184 * 8 + 1] & 0xf));
		HalfData[184 * 4 + 1] = (byte)(
			(Normal[184 * 8 + 2] & 0xf) << 4 |
			(Normal[184 * 8 + 3] & 0xf));
		HalfData[184 * 4 + 2] = (byte)(
			(Normal[184 * 8 + 4] & 0xf) << 4 |
			(Normal[184 * 8 + 5] & 0xf));
		HalfData[184 * 4 + 3] = (byte)(
			(Normal[184 * 8 + 6] & 0xf) << 4 |
			(Normal[184 * 8 + 7] & 0xf));
	}

	/* just the non-itf chars */
	public static void ResetLower()
	{
		if (Normal.Length != 2048)
			Normal = new byte[2048];

		DefaultFonts.Lower.CopyTo(Normal.AsSpan());
	}

	/* just the itf chars */
	public static void ResetUpper()
	{
		if (Normal.Length != 2048)
			Normal = new byte[2048];

		DefaultFonts.UpperITF.CopyTo(Normal.Slice(1024));

		MakeHalfWidthMidDot();
	}

	/* all together now! */
	public static void Reset()
	{
		ResetLower();
		ResetUpper();
	}

	/* or kill the upper chars as well */
	public static void ResetBIOS()
	{
		ResetLower();

		DefaultFonts.UpperAlt.CopyTo(Normal.Slice(1024));

		MakeHalfWidthMidDot();
	}

	/* ... or just one character */
	public static void ResetCharacter(int ch)
	{
		byte[] @base;

		ch <<= 3;

		int cx = ch;

		if (ch >= 1024)
			@base = DefaultFonts.UpperITF;
		else
			@base = DefaultFonts.Lower;

		/* update them both... */
		Array.Copy(@base, cx, Normal, ch, 8);

		/* update */
		MakeHalfWidthMidDot();
	}

	public static bool Squeeze8x16Font(Stream fp)
	{
		byte[] data8x16 = new byte[4096];

		try
		{
			fp.ReadExactly(data8x16);
		}
		catch
		{
			return false;
		}

		for (int n = 0; n < 2048; n++)
			Normal[n] = unchecked((byte)(data8x16[2 * n] | data8x16[2 * n + 1]));

		return true;
	}

	/* Hmm. I could've done better with this one. */
	public static bool Load(string filename)
	{
		filename = Path.Combine(Configuration.Directories.DotSchism, "fonts", filename);

		try
		{
			using (var fp = File.OpenRead(filename))
			{
				int len = (int)fp.Length;

				switch (len)
				{
					case 2048: /* raw font data */
						break;
					case 2050: /* *probably* an ITF */
					{
						byte[] data = new byte[2];

						fp.Position = 2048;
						fp.ReadExactly(data);

						if (data[1] != 0x2 || (data[0] != 0x12 && data[0] != 9))
							return false;

						fp.Position = 0;

						break;
					}
					case 4096: /* raw font data, 8x16 */
						if (Squeeze8x16Font(fp))
						{
							MakeHalfWidthMidDot();
							return true;
						}

						return false;
					default:
						return false;
				}

				fp.ReadExactly(Normal);

				MakeHalfWidthMidDot();

				return true;
			}
		}
		catch
		{
			return false;
		}
	}

	public static bool Save(string filename)
	{
		filename = Path.Combine(Configuration.Directories.DotSchism, "fonts", filename);

		try
		{
			using (var fp = File.OpenWrite(filename))
			{
				fp.Write(Normal);

				// ver
				fp.WriteByte(0x12);
				fp.WriteByte(0x2);
			}

			return true;
		}
		catch
		{
			return false;
		}
	}
}
