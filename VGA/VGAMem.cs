using System;
using System.IO;
using System.Text;

namespace ChasmTracker.VGA;

using ChasmTracker.Configurations;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public static class VGAMem
{
	public const byte SampleDataColour = 13; /* Sample data */
	public const byte SampleLoopColour = 3; /* Sample loop marks */
	public const byte SampleMarkColour = 6; /* Play mark colour */
	public const byte SampleBackgroundMarkColour = 7; /* Play mark colour after note fade / NNA */

	public const byte DefaultForeground = 3;

	public static VGAMemCharacter[] Memory = new VGAMemCharacter[4000];
	public static VGAMemCharacter[] MemoryRead = new VGAMemCharacter[4000];

	public static byte[] Overlay = new byte[Constants.NativeScreenWidth * Constants.NativeScreenHeight];

	static Palette _currentPalette = Palettes.UserDefined;

	public static Palette CurrentPalette
	{
		get => _currentPalette;
		set
		{
			_currentPalette = value;
			Configuration.General.Palette = _currentPalette.Index;
			Configuration.General.CurrentPalette = _currentPalette.ToString();
			Configuration.Save();
		}
	}

	static VGAMem()
	{
		Configuration.RegisterConfigurable(new GeneralConfigurationThunk());
	}

	class GeneralConfigurationThunk : IConfigurable<GeneralConfiguration>
	{
		public void SaveConfiguration(GeneralConfiguration config) => VGAMem.SaveConfiguration(config);
		public void LoadConfiguration(GeneralConfiguration config) => VGAMem.LoadConfiguration(config);
	}

	static void LoadConfiguration(GeneralConfiguration config)
	{
		if (config.CurrentPalette.Length >= 48)
			CurrentPalette.SetFromString(config.CurrentPalette);

		if ((config.Palette >= 0) && (config.Palette < Palettes.Presets.Length))
			CurrentPalette = Palettes.Presets[config.Palette];
	}

	static void SaveConfiguration(GeneralConfiguration config)
	{
		config.Palette = CurrentPalette.Index;
		config.CurrentPalette = CurrentPalette.ToString();
	}

	static void CheckInvert(ref byte tl, ref byte br)
	{
		if (Status.Flags.HasAllFlags(StatusFlags.InvertedPalette))
			(tl, br) = (br, tl);
	}

	public static void Flip()
	{
		Array.Copy(Memory, MemoryRead, MemoryRead.Length);
	}

	public static void Clear()
	{
		Array.Clear(Memory);
	}

	public static VGAMemOverlay AllocateOverlay(Point topLeft, Point bottomRight)
		=> new VGAMemOverlay(topLeft, bottomRight);

	public static void ApplyOverlay(VGAMemOverlay n)
	{
		for (int y = n.TopLeft.Y; y <= n.BottomRight.Y; y++)
			for (int x = n.TopLeft.X; x <= n.BottomRight.X; x++)
				Memory[x + y * 80].Font = FontTypes.Overlay;
	}

	/* scanner variants
	 *
	 * I've tried to make this code as small and predictable
	 * as possible in an effort to make it fast as I can.
	 *
	 * Older versions prioritized memory efficiency over speed,
	 * as in every character was packed into a 32-bit integer.
	 * Arguably this is a bad choice, especially considering
	 * that this is the most taxing function to call in the
	 * whole program (the audio crap doesn't even come close)
	 * In a normal session, this function will probably amount
	 * for ~80% of all processing that Schism does. */
	public static unsafe void Scan8(int ry, byte *@out, ref ChannelData tc, int[] mouseLine, int[] mouseLineMask)
	{
		/* constants */
		int y = (ry >> 3), yl = (ry & 7);
		int q = ry * Constants.NativeScreenWidth;

		int bp = y * 80;

		for (int x = 0; x < 80; x++, bp++, q += 8)
		{
			ref var ch = ref MemoryRead[bp];

			byte fg, bg, fg2, bg2, dg;

			switch (ch.Font)
			{
				case FontTypes.ImpulseTracker:
					/* regular character */
					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = fg;
					bg2 = bg;
					dg = Font.Data[yl + ch.C << 3];
					break;
				case FontTypes.BIOS:
					/* VGA BIOS character */
					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = fg;
					bg2 = bg;
					dg = Font.Alt[yl + ch.C << 3];
					break;
				case FontTypes.HalfWidth:
				{
					/* halfwidth (used for patterns) */
					byte dg1 = Font.HalfData[yl + ch.C << 2];
					byte dg2 = Font.HalfData[yl + ch.C2 << 2];

					dg = unchecked((byte)(((ry & 1) == 0)
						? ((dg1 & 0xF0) | dg2 >> 4)
						: (dg1 << 4 | (dg2 & 0xF))));

					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = ch.Colours2.FG;
					bg2 = ch.Colours2.BG;

					break;
				}
				case FontTypes.Overlay:
					/* raw pixel data, needs special code ;) */
					*@out++ = unchecked((byte)(tc[(Overlay[q + 0] | (((mouseLine[x] & 0x80) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((byte)(tc[(Overlay[q + 1] | (((mouseLine[x] & 0x40) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((byte)(tc[(Overlay[q + 2] | (((mouseLine[x] & 0x20) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((byte)(tc[(Overlay[q + 3] | (((mouseLine[x] & 0x10) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((byte)(tc[(Overlay[q + 4] | (((mouseLine[x] & 0x08) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((byte)(tc[(Overlay[q + 5] | (((mouseLine[x] & 0x04) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((byte)(tc[(Overlay[q + 6] | (((mouseLine[x] & 0x02) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((byte)(tc[(Overlay[q + 7] | (((mouseLine[x] & 0x1) != 0) ? 15 : 0)) & 0xFF]));
					continue;
				case FontTypes.Unicode:
				{
					/* Any unicode character. */
					uint c = ch.CUnicode;

					/* These are ordered by how often they will probably appear
					 * for an average user of Chasm (i.e., English speakers). */
					if (c >= 0x20 && c <= 0x7F) /* ASCII */
						dg = Font.Data[yl + c << 3];
					else if (c >= 0xA0 && c <= 0xFF) /* extended latin */
						dg = DefaultFonts.ExtendedLatin[yl + (c - 0xA0) << 3];
					else if (c >= 0x390 && c <= 0x3C9) /* greek */
						dg = DefaultFonts.Greek[yl + (c - 0x390) << 3];
					else if (c >= 0x3040 && c <= 0x309F) /* japanese hiragana */
						dg = DefaultFonts.Hiragana[yl + (c - 0x3040) << 3];
					else /* will display a ? if no cp437 equivalent found */
						dg = Font.Data[yl + ((char)c).ToCP437() << 3];

					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = fg;
					bg2 = bg;

					break;
				}

				default:
					continue;
			}

			dg = unchecked((byte)(dg | mouseLine[x]));
			dg = unchecked((byte)(dg & ~(mouseLineMask[x] ^ mouseLine[x])));

			*@out++ = unchecked((byte)(tc[((dg & 0x80) != 0) ? fg : bg]));
			*@out++ = unchecked((byte)(tc[((dg & 0x40) != 0) ? fg : bg]));
			*@out++ = unchecked((byte)(tc[((dg & 0x20) != 0) ? fg : bg]));
			*@out++ = unchecked((byte)(tc[((dg & 0x10) != 0) ? fg : bg]));
			*@out++ = unchecked((byte)(tc[((dg & 0x8) != 0) ? fg2 : bg2]));
			*@out++ = unchecked((byte)(tc[((dg & 0x4) != 0) ? fg2 : bg2]));
			*@out++ = unchecked((byte)(tc[((dg & 0x2) != 0) ? fg2 : bg2]));
			*@out++ = unchecked((byte)(tc[((dg & 0x1) != 0) ? fg2 : bg2]));
		}
	}

	public static unsafe void Scan16(int ry, ushort *@out, ref ChannelData tc, int[] mouseLine, int[] mouseLineMask)
	{
		/* constants */
		int y = (ry >> 3), yl = (ry & 7);
		int q = ry * Constants.NativeScreenWidth;

		int bp = y * 80;

		for (int x = 0; x < 80; x++, bp++, q += 8)
		{
			ref var ch = ref MemoryRead[bp];

			byte fg, bg, fg2, bg2, dg;

			switch (ch.Font)
			{
				case FontTypes.ImpulseTracker:
					/* regular character */
					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = fg;
					bg2 = bg;
					dg = Font.Data[yl + ch.C << 3];
					break;
				case FontTypes.BIOS:
					/* VGA BIOS character */
					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = fg;
					bg2 = bg;
					dg = Font.Alt[yl + ch.C << 3];
					break;
				case FontTypes.HalfWidth:
				{
					/* halfwidth (used for patterns) */
					byte dg1 = Font.HalfData[yl + ch.C << 2];
					byte dg2 = Font.HalfData[yl + ch.C2 << 2];

					dg = unchecked((byte)(((ry & 1) == 0)
						? ((dg1 & 0xF0) | dg2 >> 4)
						: (dg1 << 4 | (dg2 & 0xF))));

					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = ch.Colours2.FG;
					bg2 = ch.Colours2.BG;

					break;
				}
				case FontTypes.Overlay:
					/* raw pixel data, needs special code ;) */
					*@out++ = unchecked((ushort)(tc[(Overlay[q + 0] | (((mouseLine[x] & 0x80) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((ushort)(tc[(Overlay[q + 1] | (((mouseLine[x] & 0x40) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((ushort)(tc[(Overlay[q + 2] | (((mouseLine[x] & 0x20) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((ushort)(tc[(Overlay[q + 3] | (((mouseLine[x] & 0x10) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((ushort)(tc[(Overlay[q + 4] | (((mouseLine[x] & 0x08) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((ushort)(tc[(Overlay[q + 5] | (((mouseLine[x] & 0x04) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((ushort)(tc[(Overlay[q + 6] | (((mouseLine[x] & 0x02) != 0) ? 15 : 0)) & 0xFF]));
					*@out++ = unchecked((ushort)(tc[(Overlay[q + 7] | (((mouseLine[x] & 0x1) != 0) ? 15 : 0)) & 0xFF]));
					continue;
				case FontTypes.Unicode:
				{
					/* Any unicode character. */
					uint c = ch.CUnicode;

					/* These are ordered by how often they will probably appear
					 * for an average user of Chasm (i.e., English speakers). */
					if (c >= 0x20 && c <= 0x7F) /* ASCII */
						dg = Font.Data[yl + c << 3];
					else if (c >= 0xA0 && c <= 0xFF) /* extended latin */
						dg = DefaultFonts.ExtendedLatin[yl + (c - 0xA0) << 3];
					else if (c >= 0x390 && c <= 0x3C9) /* greek */
						dg = DefaultFonts.Greek[yl + (c - 0x390) << 3];
					else if (c >= 0x3040 && c <= 0x309F) /* japanese hiragana */
						dg = DefaultFonts.Hiragana[yl + (c - 0x3040) << 3];
					else /* will display a ? if no cp437 equivalent found */
						dg = Font.Data[yl + ((char)c).ToCP437() << 3];

					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = fg;
					bg2 = bg;

					break;
				}

				default:
					continue;
			}

			dg = unchecked((byte)(dg | mouseLine[x]));
			dg = unchecked((byte)(dg & ~(mouseLineMask[x] ^ mouseLine[x])));

			*@out++ = unchecked((ushort)(tc[((dg & 0x80) != 0) ? fg : bg]));
			*@out++ = unchecked((ushort)(tc[((dg & 0x40) != 0) ? fg : bg]));
			*@out++ = unchecked((ushort)(tc[((dg & 0x20) != 0) ? fg : bg]));
			*@out++ = unchecked((ushort)(tc[((dg & 0x10) != 0) ? fg : bg]));
			*@out++ = unchecked((ushort)(tc[((dg & 0x8) != 0) ? fg2 : bg2]));
			*@out++ = unchecked((ushort)(tc[((dg & 0x4) != 0) ? fg2 : bg2]));
			*@out++ = unchecked((ushort)(tc[((dg & 0x2) != 0) ? fg2 : bg2]));
			*@out++ = unchecked((ushort)(tc[((dg & 0x1) != 0) ? fg2 : bg2]));
		}
	}

	public static unsafe void Scan32(int ry, uint *@out, ref ChannelData tc, int[] mouseLine, int[] mouseLineMask)
	{
		/* constants */
		int y = (ry >> 3), yl = (ry & 7);
		int q = ry * Constants.NativeScreenWidth;

		int bp = y * 80;

		for (int x = 0; x < 80; x++, bp++, q += 8)
		{
			ref var ch = ref MemoryRead[bp];

			byte fg, bg, fg2, bg2, dg;

			switch (ch.Font)
			{
				case FontTypes.ImpulseTracker:
					/* regular character */
					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = fg;
					bg2 = bg;
					dg = Font.Data[yl + (ch.C << 3)];
					break;
				case FontTypes.BIOS:
					/* VGA BIOS character */
					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = fg;
					bg2 = bg;
					dg = Font.Alt[yl + (ch.C << 3)];
					break;
				case FontTypes.HalfWidth:
				{
					/* halfwidth (used for patterns) */
					byte dg1 = Font.HalfData[yl + (ch.C << 2)];
					byte dg2 = Font.HalfData[yl + (ch.C2 << 2)];

					dg = unchecked((byte)(((ry & 1) == 0)
						? ((dg1 & 0xF0) | dg2 >> 4)
						: (dg1 << 4 | (dg2 & 0xF))));

					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = ch.Colours2.FG;
					bg2 = ch.Colours2.BG;

					break;
				}
				case FontTypes.Overlay:
					/* raw pixel data, needs special code ;) */
					*@out++ = tc[(Overlay[q + 0] | (((mouseLine[x] & 0x80) != 0) ? 15 : 0)) & 0xFF];
					*@out++ = tc[(Overlay[q + 1] | (((mouseLine[x] & 0x40) != 0) ? 15 : 0)) & 0xFF];
					*@out++ = tc[(Overlay[q + 2] | (((mouseLine[x] & 0x20) != 0) ? 15 : 0)) & 0xFF];
					*@out++ = tc[(Overlay[q + 3] | (((mouseLine[x] & 0x10) != 0) ? 15 : 0)) & 0xFF];
					*@out++ = tc[(Overlay[q + 4] | (((mouseLine[x] & 0x08) != 0) ? 15 : 0)) & 0xFF];
					*@out++ = tc[(Overlay[q + 5] | (((mouseLine[x] & 0x04) != 0) ? 15 : 0)) & 0xFF];
					*@out++ = tc[(Overlay[q + 6] | (((mouseLine[x] & 0x02) != 0) ? 15 : 0)) & 0xFF];
					*@out++ = tc[(Overlay[q + 7] | (((mouseLine[x] & 0x1) != 0) ? 15 : 0)) & 0xFF];
					continue;
				case FontTypes.Unicode:
				{
					/* Any unicode character. */
					uint c = ch.CUnicode;

					/* These are ordered by how often they will probably appear
					 * for an average user of Chasm (i.e., English speakers). */
					if (c >= 0x20 && c <= 0x7F) /* ASCII */
						dg = Font.Data[yl + (c << 3)];
					else if (c >= 0xA0 && c <= 0xFF) /* extended latin */
						dg = DefaultFonts.ExtendedLatin[yl + ((c - 0xA0) << 3)];
					else if (c >= 0x390 && c <= 0x3C9) /* greek */
						dg = DefaultFonts.Greek[yl + ((c - 0x390) << 3)];
					else if (c >= 0x3040 && c <= 0x309F) /* japanese hiragana */
						dg = DefaultFonts.Hiragana[yl + ((c - 0x3040) << 3)];
					else /* will display a ? if no cp437 equivalent found */
						dg = Font.Data[yl + (((char)c).ToCP437() << 3)];

					fg = ch.Colours.FG;
					bg = ch.Colours.BG;
					fg2 = fg;
					bg2 = bg;

					break;
				}

				default:
					continue;
			}

			dg = unchecked((byte)(dg | mouseLine[x]));
			dg = unchecked((byte)(dg & ~(mouseLineMask[x] ^ mouseLine[x])));

			*@out++ = tc[((dg & 0x80) != 0) ? fg : bg];
			*@out++ = tc[((dg & 0x40) != 0) ? fg : bg];
			*@out++ = tc[((dg & 0x20) != 0) ? fg : bg];
			*@out++ = tc[((dg & 0x10) != 0) ? fg : bg];
			*@out++ = tc[((dg & 0x8) != 0) ? fg2 : bg2];
			*@out++ = tc[((dg & 0x4) != 0) ? fg2 : bg2];
			*@out++ = tc[((dg & 0x2) != 0) ? fg2 : bg2];
			*@out++ = tc[((dg & 0x1) != 0) ? fg2 : bg2];
		}
	}

	public static void DrawCharacterUnicode(uint c, Point position, VGAMemColours colours)
	{
		Assert.IsTrue(
			position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50,
			"position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50",
			"Coordinates should always be in bounds");

		ref var ch = ref Memory[position.X + (position.Y * 80)];

		ch.Font = FontTypes.Unicode;
		ch.CUnicode = c;
		ch.Colours = colours;
	}

	public static void DrawCharacter(char c, Point position, VGAMemColours colours)
	{
		Assert.IsTrue(
			position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50,
			"position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50",
			"Coordinates should always be in bounds");

		ref var ch = ref Memory[position.X + (position.Y * 80)];

		ch.Font = FontTypes.ImpulseTracker;
		ch.C = (byte)c;
		ch.Colours = colours;
	}

	public static void DrawCharacter(byte c, Point position, VGAMemColours colours)
	{
		Assert.IsTrue(
			position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50,
			"position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50",
			"Coordinates should always be in bounds");

		ref var ch = ref Memory[position.X + (position.Y * 80)];

		ch.Font = FontTypes.ImpulseTracker;
		ch.C = c;
		ch.Colours = colours;
	}

	public static void DrawCharacterBIOS(char c, Point position, VGAMemColours colours)
	{
		Assert.IsTrue(
			position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50,
			"position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50",
			"Coordinates should always be in bounds");

		ref var ch = ref Memory[position.X + (position.Y * 80)];

		ch.Font = FontTypes.BIOS;
		ch.C = (byte)c;
		ch.Colours = colours;
	}

	public static void DrawCharacterBIOS(byte c, Point position, VGAMemColours colours)
	{
		Assert.IsTrue(
			position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50,
			"position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50",
			"Coordinates should always be in bounds");

		ref var ch = ref Memory[position.X + (position.Y * 80)];

		ch.Font = FontTypes.BIOS;
		ch.C = c;
		ch.Colours = colours;
	}

	public static int DrawText(string text, Point position, VGAMemColours colours)
	{
		int rightEdge = position.X + text.Length - 1;

		if (rightEdge > 80)
			rightEdge = 80;

		for (int n = 0, x = position.X; x <= rightEdge; n++, x++)
			DrawCharacter(text[n], position.Advance(n), colours);

		return text.Length;
	}

	public static int DrawText(string text, int offset, Point position, VGAMemColours colours)
	{
		for (int n = offset; n < text.Length; n++)
			DrawCharacter(text[n], position.Advance(n - offset), colours);

		return text.Length - offset;
	}

	public static int DrawTextBIOS(string text, Point position, VGAMemColours colours)
	{
		for (int n = 0; n < text.Length; n++)
			DrawCharacterBIOS(text[n], position.Advance(n), colours);

		return text.Length;
	}

	public static int DrawTextBIOS(byte[] text, Point position, VGAMemColours colours)
	{
		for (int n = 0; n < text.Length; n++)
			DrawCharacterBIOS(text[n], position.Advance(n), colours);

		return text.Length;
	}

	public static int DrawTextUTF8(byte[] textBytes, Point position, VGAMemColours colours)
	{
		string composed;

		try
		{
			composed = Encoding.UTF8.GetString(textBytes);
		}
		catch
		{
			return DrawTextBIOS(textBytes, position, colours);
		}

		for (int n = 0; n < composed.Length; n++)
			DrawCharacterUnicode(composed[n], position.Advance(n), colours);

		return composed.Length;
	}

	public static void DrawFillCharacters(Rect bounds, VGAMemColours colours)
		=> DrawFillCharacters(bounds.TopLeft, bounds.BottomRight, colours);

	public static void DrawFillCharacters(Point s, Point e, VGAMemColours colours)
	{
		for (int y = s.Y; y <= e.Y; y++)
			for (int x = s.X; x <= e.X; x++)
			{
				ref var mm = ref Memory[(y * 80) + x];

				mm.Font = FontTypes.ImpulseTracker;
				mm.C = 0;
				mm.Colours = colours;
			}
	}

	public static int DrawTextLen(string text, int len, Point position, VGAMemColours colours)
	{
		return DrawTextLen(text, 0, len, position, colours);
	}

	public static int DrawTextLen(string text, int offset, int len, Point position, VGAMemColours colours)
	{
		int n, o;

		for (n = offset, o = 0; (n < text.Length) && (o < len); n++, o++)
			DrawCharacter(text[n], position.Advance(o), colours);

		DrawFillCharacters(position.Advance(n), position.Advance(len - 1), colours);

		return o;
	}

	public static int DrawTextLen(StringBuilder text, int offset, int len, Point position, VGAMemColours colours)
	{
		int o = 0;

		for (int i = 0; i < len; i++)
		{
			o = offset + i;

			if (o >= text.Length)
				break;

			DrawCharacter(text[o], position.Advance(i), colours);
		}

		DrawFillCharacters(position.Advance(o), position.Advance(len - 1), colours);

		return o;
	}

	public static int DrawTextBIOSLen(string text, int len, Point position, VGAMemColours colours)
	{
		return DrawTextBIOSLen(text, 0, len, position, colours);
	}

	public static int DrawTextBIOSLen(string text, int offset, int len, Point position, VGAMemColours colours)
	{
		int n, o;

		for (n = offset, o = 0; (n < text.Length) && (o < len); n++, o++)
			DrawCharacterBIOS(text[n], position.Advance(o), colours);

		DrawFillCharacters(position.Advance(o), position.Advance(len - 1), colours);

		return o;
	}

	public static int DrawTextBIOSLen(StringBuilder text, int offset, int len, Point position, VGAMemColours colours)
	{
		int o = 0;

		int i;

		for (i = 0; i < len; i++)
		{
			o = offset + i;

			if (o >= text.Length)
				break;

			DrawCharacterBIOS(text[o], position.Advance(i), colours);
		}

		DrawFillCharacters(position.Advance(i), position.Advance(len - 1), colours);

		return o;
	}

	public static int DrawTextUTF8Len(byte[] text, int len, Point position, VGAMemColours colours)
	{
		return DrawTextUTF8Len(text, 0, len, position, colours);
	}

	public static int DrawTextUTF8Len(byte[] textBytes, int offset, int len, Point position, VGAMemColours colours)
	{
		string composed;

		try
		{
			composed = Encoding.UTF8.GetString(textBytes);
		}
		catch
		{
			DrawFillCharacters(position, position.Advance(len - 1), colours);

			return DrawTextBIOS(textBytes, position, colours);
		}

		int n, o;

		for (n = offset, o = 0; (n < composed.Length) && (o < len); n++, o++)
			DrawCharacterUnicode(composed[n], position.Advance(o), colours);

		DrawFillCharacters(position.Advance(o), position.Advance(len - 1), colours);

		return o;
	}

	/* --------------------------------------------------------------------- */

	public static void DrawHalfWidthCharacters(char c1, char c2, Point position, VGAMemColours colours, VGAMemColours colours2)
	{
		Assert.IsTrue(
			position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50,
			"position.X >= 0 && position.Y >= 0 && position.X < 80 && position.Y < 50",
			"Coordinates should always be in bounds");

		ref var ch = ref Memory[position.X + (position.Y * 80)];

		ch.Font = FontTypes.HalfWidth;
		ch.C = (byte)c1;
		ch.C2 = (byte)c2;
		ch.Colours = colours;
		ch.Colours2 = colours2;
	}

	/* --------------------------------------------------------------------- */
	/* boxes */

	static readonly byte[,] BoxColours = new byte[5, 2] { { 3, 1 }, { 1, 3 }, { 3, 3 }, { 1, 1 }, { 0, 0 } };

	enum BoxShapes
	{
		ThinInner = 0,
		ThinOuter = 1,
		CornerOuter = 2,
		ThickInner = 3,
		ThickOuter = 4,
	}

	static readonly byte[,] Boxes =
		new byte[5, 8]
		{
			/*[BoxShapes.BoxThinInner]   = */{139, 138, 137, 136, 134, 129, 132, 131},
			/*[BoxShapes.BoxThinOuter]   = */{128, 130, 133, 135, 129, 134, 131, 132},
			/*[BoxShapes.BoxCornerOuter] = */{128, 141, 140, 135, 129, 134, 131, 132},
			/*[BoxShapes.BoxThickInner]  = */{153, 152, 151, 150, 148, 143, 146, 145},
			/*[BoxShapes.BoxThickOuter]  = */{142, 144, 147, 149, 143, 148, 145, 146},
		};

	public static void DrawBox(Point s, Point e, BoxTypes flags)
	{
		byte tl = BoxColours[(int)(flags & BoxTypes.ShadeMask), 0];
		byte br = BoxColours[(int)(flags & BoxTypes.ShadeMask), 1];

		byte trbl;

		BoxShapes box;

		CheckInvert(ref tl, ref br);

		switch (flags & (BoxTypes.TypeMask | BoxTypes.ThicknessMask))
		{
			case BoxTypes.Thin | BoxTypes.Inner:
				trbl = br;
				box = BoxShapes.ThinInner;
				break;
			case BoxTypes.Thick | BoxTypes.Inner:
				trbl = tl;
				box = BoxShapes.ThickInner;
				break;
			case BoxTypes.Thin | BoxTypes.Outer:
				trbl = br = tl;
				box = BoxShapes.ThinOuter;
				break;
			case BoxTypes.Thick | BoxTypes.Outer:
				trbl = br = tl;
				box = BoxShapes.ThickOuter;
				break;
			case BoxTypes.Thin | BoxTypes.Corner:
			case BoxTypes.Thick | BoxTypes.Corner:
				trbl = 1;
				box = BoxShapes.CornerOuter;
				break;
			default:
				return; // ?
		}

		/* now, the actual magic :) */
		DrawCharacter(Boxes[(int)box, 0], new Point(s.X, s.Y), (tl, 2));       /* TL corner */
		DrawCharacter(Boxes[(int)box, 1], new Point(e.X, s.Y), (trbl, 2));     /* TR corner */
		DrawCharacter(Boxes[(int)box, 2], new Point(s.X, e.Y), (trbl, 2));     /* BL corner */
		DrawCharacter(Boxes[(int)box, 3], new Point(e.X, e.Y), (br, 2));       /* BR corner */

		for (int n = s.X + 1; n < e.X; n++)
		{
			DrawCharacter(Boxes[(int)box, 4], new Point(n, s.Y), (tl, 2));        /* top */
			DrawCharacter(Boxes[(int)box, 5], new Point(n, e.Y), (br, 2));        /* bottom */
		}

		for (int n = s.Y + 1; n < e.Y; n++)
		{
			DrawCharacter(Boxes[(int)box, 6], new Point(s.X, n), (tl, 2));        /* left */
			DrawCharacter(Boxes[(int)box, 7], new Point(e.X, n), (br, 2));        /* right */
		}
	}

	/* ----------------------------------------------------------------- */

	static readonly byte[,] ThumbChars =
		new byte[2, 8]
		{
			{ 155, 156, 157, 158, 159, 160, 161, 162 },
			{ 0, 0, 0, 163, 164, 165, 166, 167 },
		};

	static void DrawThumbBarInternal(int width, Point position, int val, byte fg)
	{
		val++;

		int n = val >> 3;

		val %= 8;

		DrawFillCharacters(position, position.Advance(n - 1), (DefaultForeground, 0));
		DrawCharacter(ThumbChars[0, val], position.Advance(n), (fg, 0));
		if (++n < width)
			DrawCharacter(ThumbChars[1, val], position.Advance(n), (fg, 0));
		if (++n < width)
			DrawFillCharacters(position.Advance(n), position.Advance(width - 1), (DefaultForeground, 0));
	}

	public static void DrawThumbBar(Point position, int width, int minimum, int maximum, int value, bool isSelected)
	{
		/* this wouldn't happen in a perfect world :P */
		if (value < minimum || value > maximum)
		{
			DrawFillCharacters(position, position.Advance(width - 1),
				(DefaultForeground, Status.Flags.HasAllFlags(StatusFlags.ClassicMode) ? (byte)2 : (byte)0));
			return;
		}

		/* fix the range so that it's 0->n */
		value -= minimum;
		maximum -= minimum;

		/* draw the bar */
		if (maximum == 0)
			DrawThumbBarInternal(width, position, 0, isSelected ? (byte)3 : (byte)2);
		else
			DrawThumbBarInternal(width, position,
				value * (width / 1) * 8 / maximum,
				isSelected ? (byte)3 : (byte)2);
	}

	/* --------------------------------------------------------------------- */
	/* VU meters */
	static readonly byte[,] VUMeterEndText =
		new byte[8, 3]
		{
			{ 174, 0, 0}, { 175, 0, 0}, { 176, 0, 0}, { 176, 177, 0},
			{ 176, 178, 0}, { 176, 179, 180}, { 176, 179, 181},
			{ 176, 179, 182},
		};

	public static void DrawVUMeter(Point position, int width, int val, byte color, byte peak)
	{
		int chunks = (width / 3);
		int maxval = width * 8 / 3;

		/* reduced from (val * maxval / 64) */
		val = (val * width / 24).Clamp(0, (maxval - 1));
		if (val == 0)
			return;

		int leftover = val & 7;
		val >>= 3;
		if ((val < chunks - 1) || Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
			peak = color;

		DrawCharacter(VUMeterEndText[leftover, 0], position.Advance(3 * val + 0), (peak, 0));
		DrawCharacter(VUMeterEndText[leftover, 1], position.Advance(3 * val + 1), (peak, 0));
		DrawCharacter(VUMeterEndText[leftover, 2], position.Advance(3 * val + 2), (peak, 0));

		while (val-- >= 0)
		{
			DrawCharacter(176, position.Advance(3 * val + 0), (color, 0));
			DrawCharacter(179, position.Advance(3 * val + 1), (color, 0));
			DrawCharacter(182, position.Advance(3 * val + 2), (color, 0));
		}
	}

	/* --------------------------------------------------------------------- */
	/* sample drawing
	 *
	 * output channels = number of oscis
	 * input channels = number of channels in data
	*/

	/* somewhat heavily based on CViewSample::DrawSampleData2 in modplug */

	static void DrawSampleData8(VGAMemOverlay r,
		Span<sbyte> data, int inputChans, int outputChans)
	{
		if (data.Length == 0)
			return;

		int nh = r.Size.Height / outputChans;
		int np = r.Size.Height - nh / 2;

		int length = data.Length / inputChans;

		int step = (int)(((long)length << 32) / r.Size.Width);

		for (int cc = 0; cc < outputChans; cc++)
		{
			int posHi = 0;
			long posLo = 0;

			for (int x = 0; x < r.Size.Width; x++)
			{
				sbyte min = sbyte.MaxValue, max = sbyte.MinValue;

				posLo += step;

				int scanLength = unchecked((int)((posLo + 0xFFFFFFFF) >> 32));

				if (posHi >= length) posHi = length - 1;
				if (posHi + scanLength > length) scanLength = length - posHi;

				if (scanLength < 1)
					scanLength = 1;

				for (int i = 0; i < scanLength; i++)
				{
					int co = 0;

					do
					{
						sbyte s = data[(posHi + i) * inputChans + cc + co];
						if (s < min) min = s;
						if (s > max) max = s;
					} while (co++ < inputChans - outputChans);
				}

				/* XXX is doing this with integers faster than say, floating point?
				 * I mean, it sure is a bit more ~accurate~ at least, and it'll work the same everywhere. */
				min = (sbyte)((min * (long)nh) >> 8);
				max = (sbyte)((max * (long)nh) >> 8);

				r.DrawLine(new Point(x, np - 1 - max), new Point(x, np - 1 - min), SampleDataColour);

				posHi += unchecked((int)(posLo >> 32));
				posLo &= 0xFFFFFFFF;
			}

			np -= nh;
		}
	}

	static void DrawSampleData16(VGAMemOverlay r,
		Span<short> data, int inputChans, int outputChans)
	{
		if (data.Length == 0)
			return;

		int nh = r.Size.Height / outputChans;
		int np = r.Size.Height - nh / 2;

		int length = data.Length / inputChans;

		int step = (int)(((long)length << 32) / r.Size.Width);

		for (int cc = 0; cc < outputChans; cc++)
		{
			int posHi = 0;
			long posLo = 0;

			for (int x = 0; x < r.Size.Width; x++)
			{
				short min = short.MaxValue, max = short.MinValue;

				posLo += step;

				int scanLength = unchecked((int)((posLo + 0xFFFFFFFF) >> 32));

				if (posHi >= length) posHi = length - 1;
				if (posHi + scanLength > length) scanLength = length - posHi;

				if (scanLength < 1)
					scanLength = 1;

				for (int i = 0; i < scanLength; i++)
				{
					int co = 0;

					do
					{
						short s = data[(posHi + i) * inputChans + cc + co];
						if (s < min) min = s;
						if (s > max) max = s;
					} while (co++ < inputChans - outputChans);
				}

				/* XXX is doing this with integers faster than say, floating point?
				 * I mean, it sure is a bit more ~accurate~ at least, and it'll work the same everywhere. */
				min = (short)((min * (long)nh) >> 8);
				max = (short)((max * (long)nh) >> 8);

				r.DrawLine(new Point(x, np - 1 - max), new Point(x, np - 1 - min), SampleDataColour);

				posHi += unchecked((int)(posLo >> 32));
				posLo &= 0xFFFFFFFF;
			}

			np -= nh;
		}
	}

	static void DrawSampleData32(VGAMemOverlay r,
		Span<int> data, int inputChans, int outputChans)
	{
		if (data.Length == 0)
			return;

		int nh = r.Size.Height / outputChans;
		int np = r.Size.Height - nh / 2;

		int length = data.Length / inputChans;

		int step = (int)(((long)length << 32) / r.Size.Width);

		for (int cc = 0; cc < outputChans; cc++)
		{
			long posHi = 0, posLo = 0;

			for (int x = 0; x < r.Size.Width; x++)
			{
				int min = int.MaxValue, max = int.MinValue;

				posLo += step;

				int scanLength = unchecked((int)((posLo + 0xFFFFFFFF) >> 32));

				if (posHi >= length) posHi = length - 1;
				if (posHi + scanLength > length) scanLength = (int)(length - posHi);

				if (scanLength < 1)
					scanLength = 1;

				for (int i = 0; i < scanLength; i++)
				{
					int co = 0;

					do
					{
						int s = data[(int)((posHi + i) * inputChans + cc + co)];
						if (s < min) min = s;
						if (s > max) max = s;
					} while (co++ < inputChans - outputChans);
				}

				/* XXX is doing this with integers faster than say, floating point?
				 * I mean, it sure is a bit more ~accurate~ at least, and it'll work the same everywhere. */
				min = (int)((min * (long)nh) >> 8);
				max = (int)((max * (long)nh) >> 8);

				r.DrawLine(new Point(x, np - 1 - max), new Point(x, np - 1 - min), SampleDataColour);

				posHi += (posLo >> 32);
				posLo &= 0xFFFFFFFF;
			}

			np -= nh;
		}
	}

	/* --------------------------------------------------------------------- */
	/* these functions assume the screen is locked! */

	/* loop drawing */
	static void DrawSampleLoop(VGAMemOverlay r, SongSample sample)
	{
		byte c = Status.Flags.HasAllFlags(StatusFlags.ClassicMode) ? SampleDataColour : SampleLoopColour;

		if (!sample.Flags.HasAllFlags(SampleFlags.Loop))
			return;

		int loopStart = sample.LoopStart * (r.Size.Width - 1) / sample.Length;
		int loopEnd = sample.LoopEnd * (r.Size.Width - 1) / sample.Length;

		int y = 0;
		do
		{
			r[loopStart, y] = 0; r[loopEnd, y] = 0; y++;
			r[loopStart, y] = c; r[loopEnd, y] = c; y++;
			r[loopStart, y] = c; r[loopEnd, y] = c; y++;
			r[loopStart, y] = 0; r[loopEnd, y] = 0; y++;
		} while (y < r.Size.Height);
	}

	static void DrawSampleSuspendLoop(VGAMemOverlay r, SongSample sample)
	{
		byte c = Status.Flags.HasAllFlags(StatusFlags.ClassicMode) ? SampleDataColour : SampleLoopColour;

		if (!sample.Flags.HasAllFlags(SampleFlags.SustainLoop))
			return;

		int loopStart = sample.SustainStart * (r.Size.Width - 1) / sample.Length;
		int loopEnd = sample.SustainEnd * (r.Size.Width - 1) / sample.Length;

		int y = 0;

		do
		{
			r[loopStart, y] = c; r[loopEnd, y] = c; y++;
			r[loopStart, y] = 0; r[loopEnd, y] = 0; y++;
			r[loopStart, y] = c; r[loopEnd, y] = c; y++;
			r[loopStart, y] = 0; r[loopEnd, y] = 0; y++;
		} while (y < r.Size.Height);
	}

	/* this does the lines for playing samples */
	static void DrawSamplePlayMarks(VGAMemOverlay r, SongSample sample)
	{
		if (AudioPlayback.Mode == AudioPlaybackMode.Stopped)
			return;

		lock (AudioPlayback.LockScope())
		{
			var channelList = Song.CurrentSong.GetMixState(out int numActiveVoices);

			for (int n = numActiveVoices - 1; n >= 0; n--)
			{
				ref var channel = ref Song.CurrentSong.Voices[channelList[n]];

				if (channel.CurrentSampleData.RawData != sample.RawData)
					continue;

				if (channel.FinalVolume == 0)
					continue;

				byte c = channel.Flags.HasAllFlags(ChannelFlags.KeyOff | ChannelFlags.NoteFade) ? SampleBackgroundMarkColour : SampleMarkColour;

				int x = channel.Position * (r.Size.Width - 1) / sample.Length;

				if (x >= r.Size.Width)
				{
					/* this does, in fact, happen :( */
					continue;
				}

				int y = 0;

				do
				{
					/* unrolled 8 times */
					r[x, y++] = c;
					r[x, y++] = c;
					r[x, y++] = c;
					r[x, y++] = c;
					r[x, y++] = c;
					r[x, y++] = c;
					r[x, y++] = c;
					r[x, y++] = c;
				} while (y < r.Size.Height);
			}
		}
	}

	/* --------------------------------------------------------------------- */
	/* meat! */

	public static void DrawSampleData(VGAMemOverlay r, SongSample sample)
	{
		r.Clear(0);

		if (sample.Flags.HasAllFlags(SampleFlags.AdLib))
		{
			r.Clear(2);

			ApplyOverlay(r);

			int y1 = r.TopLeft.Y, y2 = y1 + 3;

			DrawBox(new Point(59, y1), new Point(77, y2), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset); // data
			DrawBox(new Point(54, y1), new Point(58, y2), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Outset); // button
			DrawTextLen("Mod", 3, new Point(55, y1 + 1), (0, 2));
			DrawTextLen("Car", 3, new Point(55, y1 + 2), (0, 2));

			if (sample.AdLibBytes != null)
			{
				string buf1 = string.Format("{0:X2} {1:X2} {2X2} {3:X2} {4:X2} {5:X2}",
					sample.AdLibBytes[0],
					sample.AdLibBytes[2],
					sample.AdLibBytes[4],
					sample.AdLibBytes[6],
					sample.AdLibBytes[8],
					sample.AdLibBytes[10]);

				string buf2 = string.Format("{0:X2} {1:X2} {2:X2} {3:X2} {4:X2}",
					sample.AdLibBytes[1],
					sample.AdLibBytes[3],
					sample.AdLibBytes[5],
					sample.AdLibBytes[7],
					sample.AdLibBytes[9]);

				DrawTextLen(buf1, 17, new Point(60, y1 + 1), (2, 0));
				DrawTextLen(buf2, 17, new Point(60, y1 + 2), (2, 0));
			}
		}

		if ((sample.Length == 0) || sample.Data.IsEmpty)
		{
			ApplyOverlay(r);
			return;
		}

		/* do the actual drawing */
		int chans = sample.Flags.HasAllFlags(SampleFlags.Stereo) ? 2 : 1;

		if (sample.Flags.HasAllFlags(SampleFlags._16Bit))
			DrawSampleData16(r, sample.Data16.Slice(0, sample.Length * chans),
					chans, chans);
		else
			DrawSampleData8(r, sample.Data8.Slice(0, sample.Length * chans),
					chans, chans);

		if (Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
			DrawSamplePlayMarks(r, sample);

		DrawSampleLoop(r, sample);
		DrawSampleSuspendLoop(r, sample);

		ApplyOverlay(r);
	}

	public static void DrawSampleDataRect32(VGAMemOverlay r, Span<int> data,
		int inputChans, int outputChans)
	{
		r.Clear(0);
		DrawSampleData32(r, data, inputChans, outputChans);
		ApplyOverlay(r);
	}

	public static void DrawSampleDataRect16(VGAMemOverlay r, Span<short> data,
		int inputChans, int outputChans)
	{
		r.Clear(0);
		DrawSampleData16(r, data, inputChans, outputChans);
		ApplyOverlay(r);
	}

	public static void DrawSampleDataRect8(VGAMemOverlay r, Span<sbyte> data,
		int inputChans, int outputChans)
	{
		r.Clear(0);
		DrawSampleData8(r, data, inputChans, outputChans);
		ApplyOverlay(r);
	}
}
