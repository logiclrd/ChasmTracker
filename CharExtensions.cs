namespace ChasmTracker;

public static class CharExtensions
{
	/* this is useful elsewhere. */
	public static extern byte ToCP437(this char ch)
	{
		/* not really correct, but whatever */
		if (ch < 0x80)
			return unchecked((byte)ch);

		switch (ch)
		{
			case 0x2019: return 39; // fancy apostrophe
			case 0x00B3: return 51; // superscript three
			case 0x266F: return (byte)'#';// MUSIC SHARP SIGN
			case 0x00A6: return 124;
			case 0x0394:
			case 0x2302: return 127;// HOUSE
			// DIACRITICS
			case 0x00C7: return 128;
			case 0x00FC: return 129;
			case 0x00E9: return 130;
			case 0x00E2: return 131;
			case 0x00E4: return 132;
			case 0x00E0: return 133;
			case 0x00E5: return 134;
			case 0x00E7: return 135;
			case 0x00EA: return 136;
			case 0x00EB: return 137;
			case 0x00E8: return 138;
			case 0x00EF: return 139;
			case 0x00EE: return 140;
			case 0x00EC: return 141;
			case 0x00C4: return 142;
			case 0x00C5: return 143;
			case 0x00C9: return 144;
			case 0x00E6: return 145;
			case 0x00C6: return 146;
			case 0x00F4: return 147;
			case 0x00F6: return 148;
			case 0x00F2: return 149;
			case 0x00FB: return 150;
			case 0x00F9: return 151;
			case 0x00FF: return 152;
			case 0x00D6: return 153;
			case 0x00DC: return 154;
			case 0x20B5:
			case 0x20B2:
			case 0x00A2: return 155;// CENT SIGN
			case 0x00A3: return 156;// POUND SIGN
			case 0x00A5: return 157;// YEN SIGN
			case 0x20A7: return 158;
			case 0x0192: return 159;
			case 0x00E1: return 160;
			case 0x00ED: return 161;
			case 0x00F3: return 162;
			case 0x00FA: return 163;
			case 0x00F1: return 164;
			case 0x00D1: return 165;
			case 0x00AA: return 166;
			case 0x00BA: return 167;
			case 0x00BF: return 168;
			case 0x2310: return 169;// REVERSED NOT SIGN
			case 0x00AC: return 170;// NOT SIGN
			case 0x00BD: return 171;// 1/2
			case 0x00BC: return 172;// 1/4
			case 0x00A1: return 173;// INVERTED EXCLAMATION MARK
			case 0x00AB: return 174;// <<
			case 0x00BB: return 175;// >>
			case 0x2591: return 176;// LIGHT SHADE
			case 0x2592: return 177;// MEDIUM SHADE
			case 0x2593: return 178;// DARK SHADE
			// BOX DRAWING
			case 0x2502: return 179;
			case 0x2524: return 180;
			case 0x2561: return 181;
			case 0x2562: return 182;
			case 0x2556: return 183;
			case 0x2555: return 184;
			case 0x2563: return 185;
			case 0x2551: return 186;
			case 0x2557: return 187;
			case 0x255D: return 188;
			case 0x255C: return 189;
			case 0x255B: return 190;
			case 0x2510: return 191;
			case 0x2514: return 192;
			case 0x2534: return 193;
			case 0x252C: return 194;
			case 0x251C: return 195;
			case 0x2500: return 196;
			case 0x253C: return 197;
			case 0x255E: return 198;
			case 0x255F: return 199;
			case 0x255A: return 200;
			case 0x2554: return 201;
			case 0x2569: return 202;
			case 0x2566: return 203;
			case 0x2560: return 204;
			case 0x2550: return 205;
			case 0x256C: return 206;
			case 0x2567: return 207;
			case 0x2568: return 208;
			case 0x2564: return 209;
			case 0x2565: return 210;
			case 0x2559: return 211;
			case 0x2558: return 212;
			case 0x2552: return 213;
			case 0x2553: return 214;
			case 0x256B: return 215;
			case 0x256A: return 216;
			case 0x2518: return 217;
			case 0x250C: return 218;
			case 0x25A0: return 219;// BLACK SQUARE
			case 0x2584: return 220;// LOWER HALF BLOCK
			case 0x258C: return 221;// LEFT HALF BLOCK
			case 0x2590: return 222;// RIGHT HALF BLOCK
			case 0x2580: return 223;// UPPER HALF BLOCK
			case 0x03B1: return 224;// GREEK SMALL LETTER ALPHA
			case 0x03B2: return 225;// GREEK SMALL LETTER BETA
			case 0x0393: return 226;// GREEK CAPITAL LETTER GAMMA
			case 0x03C0: return 227;// mmm... pie...
			case 0x03A3:
			case 0x2211: return 228;// N-ARY SUMMATION / CAPITAL SIGMA
			case 0x03C3: return 229;// GREEK SMALL LETTER SIGMA
			case 0x03BC:
			case 0x00B5: return 230;// GREEK SMALL LETTER MU
			case 0x03C4:
			case 0x03D2: return 231;// GREEK UPSILON+HOOK
			case 0x03B8: return 233;// GREEK SMALL LETTER THETA
			case 0x03A9: return 234;// GREEK CAPITAL LETTER OMEGA
			case 0x03B4: return 235;// GREEK SMALL LETTER DELTA
			case 0x221E: return 236;// INFINITY
			case 0x00D8:
			case 0x00F8: return 237;// LATIN ... LETTER O WITH STROKE
			case 0x03F5: return 238;// GREEK LUNATE EPSILON SYMBOL
			case 0x2229:
			case 0x03A0: return 239;// GREEK CAPITAL LETTER PI
			case 0x039E: return 240;// GREEK CAPITAL LETTER XI
			case 0x00B1: return 241;// PLUS-MINUS SIGN
			case 0x2265: return 242;// GREATER-THAN OR EQUAL TO
			case 0x2264: return 243;// LESS-THAN OR EQUAL TO
			case 0x2320: return 244;// TOP HALF INTEGRAL
			case 0x2321: return 245;// BOTTOM HALF INTEGRAL
			case 0x00F7: return 246;// DIVISION SIGN
			case 0x2248: return 247;// ALMOST EQUAL TO
			case 0x00B0: return 248;// DEGREE SIGN
			case 0x00B7: return 249;// MIDDLE DOT
			case 0x2219:
			case 0x0387: return 250;// GREEK ANO TELEIA
			case 0x221A: return 251;// SQUARE ROOT
			case 0x207F: return 252;// SUPERSCRIPT SMALL LETTER N
			case 0x00B2: return 253;// SUPERSCRIPT TWO
			case 0x220E: return 254;// QED
			case 0x00A0: return 255;
			default:
				return (byte)'?';
		}
	}
}
