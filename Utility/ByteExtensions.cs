using System;

namespace ChasmTracker.Utility;

public static class ByteExtensions
{
	public static byte Clamp(this byte value, byte min, byte max)
		=> Math.Max(min, Math.Min(max, value));

	public static byte Clamp(this byte value, int min, int max)
		=> Math.Max((byte)min, Math.Min((byte)max, value));

	public static bool HasBitSet(this byte value, byte flag)
	{
		return (value & flag) == flag;
	}

	public static bool HasBitSet(this byte value, int flag)
	{
		return (value & flag) == flag;
	}

	public static bool HasAnyBitSet(this byte value, byte flag)
	{
		return (value & flag) != 0;
	}

	public static char FromCP437(this byte value)
	{
		/* https://www.unicode.org/Public/MAPPINGS/VENDORS/MICSFT/PC/CP437.TXT */
		if (value <= 127) return (char)value;

		switch (value)
		{
			case 0x80: return '\u00c7'; /* LATIN CAPITAL LETTER C WITH CEDILLA */
			case 0x81: return '\u00fc'; /* LATIN SMALL LETTER U WITH DIAERESIS */
			case 0x82: return '\u00e9'; /* LATIN SMALL LETTER E WITH ACUTE */
			case 0x83: return '\u00e2'; /* LATIN SMALL LETTER A WITH CIRCUMFLEX */
			case 0x84: return '\u00e4'; /* LATIN SMALL LETTER A WITH DIAERESIS */
			case 0x85: return '\u00e0'; /* LATIN SMALL LETTER A WITH GRAVE */
			case 0x86: return '\u00e5'; /* LATIN SMALL LETTER A WITH RING ABOVE */
			case 0x87: return '\u00e7'; /* LATIN SMALL LETTER C WITH CEDILLA */
			case 0x88: return '\u00ea'; /* LATIN SMALL LETTER E WITH CIRCUMFLEX */
			case 0x89: return '\u00eb'; /* LATIN SMALL LETTER E WITH DIAERESIS */
			case 0x8a: return '\u00e8'; /* LATIN SMALL LETTER E WITH GRAVE */
			case 0x8b: return '\u00ef'; /* LATIN SMALL LETTER I WITH DIAERESIS */
			case 0x8c: return '\u00ee'; /* LATIN SMALL LETTER I WITH CIRCUMFLEX */
			case 0x8d: return '\u00ec'; /* LATIN SMALL LETTER I WITH GRAVE */
			case 0x8e: return '\u00c4'; /* LATIN CAPITAL LETTER A WITH DIAERESIS */
			case 0x8f: return '\u00c5'; /* LATIN CAPITAL LETTER A WITH RING ABOVE */
			case 0x90: return '\u00c9'; /* LATIN CAPITAL LETTER E WITH ACUTE */
			case 0x91: return '\u00e6'; /* LATIN SMALL LIGATURE AE */
			case 0x92: return '\u00c6'; /* LATIN CAPITAL LIGATURE AE */
			case 0x93: return '\u00f4'; /* LATIN SMALL LETTER O WITH CIRCUMFLEX */
			case 0x94: return '\u00f6'; /* LATIN SMALL LETTER O WITH DIAERESIS */
			case 0x95: return '\u00f2'; /* LATIN SMALL LETTER O WITH GRAVE */
			case 0x96: return '\u00fb'; /* LATIN SMALL LETTER U WITH CIRCUMFLEX */
			case 0x97: return '\u00f9'; /* LATIN SMALL LETTER U WITH GRAVE */
			case 0x98: return '\u00ff'; /* LATIN SMALL LETTER Y WITH DIAERESIS */
			case 0x99: return '\u00d6'; /* LATIN CAPITAL LETTER O WITH DIAERESIS */
			case 0x9a: return '\u00dc'; /* LATIN CAPITAL LETTER U WITH DIAERESIS */
			case 0x9b: return '\u00a2'; /* CENT SIGN */
			case 0x9c: return '\u00a3'; /* POUND SIGN */
			case 0x9d: return '\u00a5'; /* YEN SIGN */
			case 0x9e: return '\u20a7'; /* PESETA SIGN */
			case 0x9f: return '\u0192'; /* LATIN SMALL LETTER F WITH HOOK */
			case 0xa0: return '\u00e1'; /* LATIN SMALL LETTER A WITH ACUTE */
			case 0xa1: return '\u00ed'; /* LATIN SMALL LETTER I WITH ACUTE */
			case 0xa2: return '\u00f3'; /* LATIN SMALL LETTER O WITH ACUTE */
			case 0xa3: return '\u00fa'; /* LATIN SMALL LETTER U WITH ACUTE */
			case 0xa4: return '\u00f1'; /* LATIN SMALL LETTER N WITH TILDE */
			case 0xa5: return '\u00d1'; /* LATIN CAPITAL LETTER N WITH TILDE */
			case 0xa6: return '\u00aa'; /* FEMININE ORDINAL INDICATOR */
			case 0xa7: return '\u00ba'; /* MASCULINE ORDINAL INDICATOR */
			case 0xa8: return '\u00bf'; /* INVERTED QUESTION MARK */
			case 0xa9: return '\u2310'; /* REVERSED NOT SIGN */
			case 0xaa: return '\u00ac'; /* NOT SIGN */
			case 0xab: return '\u00bd'; /* VULGAR FRACTION ONE HALF */
			case 0xac: return '\u00bc'; /* VULGAR FRACTION ONE QUARTER */
			case 0xad: return '\u00a1'; /* INVERTED EXCLAMATION MARK */
			case 0xae: return '\u00ab'; /* LEFT-POINTING DOUBLE ANGLE QUOTATION MARK */
			case 0xaf: return '\u00bb'; /* RIGHT-POINTING DOUBLE ANGLE QUOTATION MARK */
			case 0xb0: return '\u2591'; /* LIGHT SHADE */
			case 0xb1: return '\u2592'; /* MEDIUM SHADE */
			case 0xb2: return '\u2593'; /* DARK SHADE */
			case 0xb3: return '\u2502'; /* BOX DRAWINGS LIGHT VERTICAL */
			case 0xb4: return '\u2524'; /* BOX DRAWINGS LIGHT VERTICAL AND LEFT */
			case 0xb5: return '\u2561'; /* BOX DRAWINGS VERTICAL SINGLE AND LEFT DOUBLE */
			case 0xb6: return '\u2562'; /* BOX DRAWINGS VERTICAL DOUBLE AND LEFT SINGLE */
			case 0xb7: return '\u2556'; /* BOX DRAWINGS DOWN DOUBLE AND LEFT SINGLE */
			case 0xb8: return '\u2555'; /* BOX DRAWINGS DOWN SINGLE AND LEFT DOUBLE */
			case 0xb9: return '\u2563'; /* BOX DRAWINGS DOUBLE VERTICAL AND LEFT */
			case 0xba: return '\u2551'; /* BOX DRAWINGS DOUBLE VERTICAL */
			case 0xbb: return '\u2557'; /* BOX DRAWINGS DOUBLE DOWN AND LEFT */
			case 0xbc: return '\u255d'; /* BOX DRAWINGS DOUBLE UP AND LEFT */
			case 0xbd: return '\u255c'; /* BOX DRAWINGS UP DOUBLE AND LEFT SINGLE */
			case 0xbe: return '\u255b'; /* BOX DRAWINGS UP SINGLE AND LEFT DOUBLE */
			case 0xbf: return '\u2510'; /* BOX DRAWINGS LIGHT DOWN AND LEFT */
			case 0xc0: return '\u2514'; /* BOX DRAWINGS LIGHT UP AND RIGHT */
			case 0xc1: return '\u2534'; /* BOX DRAWINGS LIGHT UP AND HORIZONTAL */
			case 0xc2: return '\u252c'; /* BOX DRAWINGS LIGHT DOWN AND HORIZONTAL */
			case 0xc3: return '\u251c'; /* BOX DRAWINGS LIGHT VERTICAL AND RIGHT */
			case 0xc4: return '\u2500'; /* BOX DRAWINGS LIGHT HORIZONTAL */
			case 0xc5: return '\u253c'; /* BOX DRAWINGS LIGHT VERTICAL AND HORIZONTAL */
			case 0xc6: return '\u255e'; /* BOX DRAWINGS VERTICAL SINGLE AND RIGHT DOUBLE */
			case 0xc7: return '\u255f'; /* BOX DRAWINGS VERTICAL DOUBLE AND RIGHT SINGLE */
			case 0xc8: return '\u255a'; /* BOX DRAWINGS DOUBLE UP AND RIGHT */
			case 0xc9: return '\u2554'; /* BOX DRAWINGS DOUBLE DOWN AND RIGHT */
			case 0xca: return '\u2569'; /* BOX DRAWINGS DOUBLE UP AND HORIZONTAL */
			case 0xcb: return '\u2566'; /* BOX DRAWINGS DOUBLE DOWN AND HORIZONTAL */
			case 0xcc: return '\u2560'; /* BOX DRAWINGS DOUBLE VERTICAL AND RIGHT */
			case 0xcd: return '\u2550'; /* BOX DRAWINGS DOUBLE HORIZONTAL */
			case 0xce: return '\u256c'; /* BOX DRAWINGS DOUBLE VERTICAL AND HORIZONTAL */
			case 0xcf: return '\u2567'; /* BOX DRAWINGS UP SINGLE AND HORIZONTAL DOUBLE */
			case 0xd0: return '\u2568'; /* BOX DRAWINGS UP DOUBLE AND HORIZONTAL SINGLE */
			case 0xd1: return '\u2564'; /* BOX DRAWINGS DOWN SINGLE AND HORIZONTAL DOUBLE */
			case 0xd2: return '\u2565'; /* BOX DRAWINGS DOWN DOUBLE AND HORIZONTAL SINGLE */
			case 0xd3: return '\u2559'; /* BOX DRAWINGS UP DOUBLE AND RIGHT SINGLE */
			case 0xd4: return '\u2558'; /* BOX DRAWINGS UP SINGLE AND RIGHT DOUBLE */
			case 0xd5: return '\u2552'; /* BOX DRAWINGS DOWN SINGLE AND RIGHT DOUBLE */
			case 0xd6: return '\u2553'; /* BOX DRAWINGS DOWN DOUBLE AND RIGHT SINGLE */
			case 0xd7: return '\u256b'; /* BOX DRAWINGS VERTICAL DOUBLE AND HORIZONTAL SINGLE */
			case 0xd8: return '\u256a'; /* BOX DRAWINGS VERTICAL SINGLE AND HORIZONTAL DOUBLE */
			case 0xd9: return '\u2518'; /* BOX DRAWINGS LIGHT UP AND LEFT */
			case 0xda: return '\u250c'; /* BOX DRAWINGS LIGHT DOWN AND RIGHT */
			case 0xdb: return '\u2588'; /* FULL BLOCK */
			case 0xdc: return '\u2584'; /* LOWER HALF BLOCK */
			case 0xdd: return '\u258c'; /* LEFT HALF BLOCK */
			case 0xde: return '\u2590'; /* RIGHT HALF BLOCK */
			case 0xdf: return '\u2580'; /* UPPER HALF BLOCK */
			case 0xe0: return '\u03b1'; /* GREEK SMALL LETTER ALPHA */
			case 0xe1: return '\u00df'; /* LATIN SMALL LETTER SHARP S */
			case 0xe2: return '\u0393'; /* GREEK CAPITAL LETTER GAMMA */
			case 0xe3: return '\u03c0'; /* GREEK SMALL LETTER PI */
			case 0xe4: return '\u03a3'; /* GREEK CAPITAL LETTER SIGMA */
			case 0xe5: return '\u03c3'; /* GREEK SMALL LETTER SIGMA */
			case 0xe6: return '\u00b5'; /* MICRO SIGN */
			case 0xe7: return '\u03c4'; /* GREEK SMALL LETTER TAU */
			case 0xe8: return '\u03a6'; /* GREEK CAPITAL LETTER PHI */
			case 0xe9: return '\u0398'; /* GREEK CAPITAL LETTER THETA */
			case 0xea: return '\u03a9'; /* GREEK CAPITAL LETTER OMEGA */
			case 0xeb: return '\u03b4'; /* GREEK SMALL LETTER DELTA */
			case 0xec: return '\u221e'; /* INFINITY */
			case 0xed: return '\u03c6'; /* GREEK SMALL LETTER PHI */
			case 0xee: return '\u03b5'; /* GREEK SMALL LETTER EPSILON */
			case 0xef: return '\u2229'; /* INTERSECTION */
			case 0xf0: return '\u2261'; /* IDENTICAL TO */
			case 0xf1: return '\u00b1'; /* PLUS-MINUS SIGN */
			case 0xf2: return '\u2265'; /* GREATER-THAN OR EQUAL TO */
			case 0xf3: return '\u2264'; /* LESS-THAN OR EQUAL TO */
			case 0xf4: return '\u2320'; /* TOP HALF INTEGRAL */
			case 0xf5: return '\u2321'; /* BOTTOM HALF INTEGRAL */
			case 0xf6: return '\u00f7'; /* DIVISION SIGN */
			case 0xf7: return '\u2248'; /* ALMOST EQUAL TO */
			case 0xf8: return '\u00b0'; /* DEGREE SIGN */
			case 0xf9: return '\u2219'; /* BULLET OPERATOR */
			case 0xfa: return '\u00b7'; /* MIDDLE DOT */
			case 0xfb: return '\u221a'; /* SQUARE ROOT */
			case 0xfc: return '\u207f'; /* SUPERSCRIPT LATIN SMALL LETTER N */
			case 0xfd: return '\u00b2'; /* SUPERSCRIPT TWO */
			case 0xfe: return '\u25a0'; /* BLACK SQUARE */
			case 0xff: return '\u00a0'; /* NO-BREAK SPACE */
		}

		// should never happen -- the if and the switch should cover all possible byte values
		return '?';
	}
}