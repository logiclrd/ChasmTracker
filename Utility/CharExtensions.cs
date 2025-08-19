namespace ChasmTracker.Utility;

public static class CharExtensions
{
	public static int ToCP437(this char c)
	{
		/* https://www.unicode.org/Public/MAPPINGS/VENDORS/MICSFT/PC/CP437.TXT */
		if (c <= 127) return (int)c;

		switch (c)
		{
			case '\u00c7': return 0x80; /* LATIN CAPITAL LETTER C WITH CEDILLA */
			case '\u00fc': return 0x81; /* LATIN SMALL LETTER U WITH DIAERESIS */
			case '\u00e9': return 0x82; /* LATIN SMALL LETTER E WITH ACUTE */
			case '\u00e2': return 0x83; /* LATIN SMALL LETTER A WITH CIRCUMFLEX */
			case '\u00e4': return 0x84; /* LATIN SMALL LETTER A WITH DIAERESIS */
			case '\u00e0': return 0x85; /* LATIN SMALL LETTER A WITH GRAVE */
			case '\u00e5': return 0x86; /* LATIN SMALL LETTER A WITH RING ABOVE */
			case '\u00e7': return 0x87; /* LATIN SMALL LETTER C WITH CEDILLA */
			case '\u00ea': return 0x88; /* LATIN SMALL LETTER E WITH CIRCUMFLEX */
			case '\u00eb': return 0x89; /* LATIN SMALL LETTER E WITH DIAERESIS */
			case '\u00e8': return 0x8a; /* LATIN SMALL LETTER E WITH GRAVE */
			case '\u00ef': return 0x8b; /* LATIN SMALL LETTER I WITH DIAERESIS */
			case '\u00ee': return 0x8c; /* LATIN SMALL LETTER I WITH CIRCUMFLEX */
			case '\u00ec': return 0x8d; /* LATIN SMALL LETTER I WITH GRAVE */
			case '\u00c4': return 0x8e; /* LATIN CAPITAL LETTER A WITH DIAERESIS */
			case '\u00c5': return 0x8f; /* LATIN CAPITAL LETTER A WITH RING ABOVE */
			case '\u00c9': return 0x90; /* LATIN CAPITAL LETTER E WITH ACUTE */
			case '\u00e6': return 0x91; /* LATIN SMALL LIGATURE AE */
			case '\u00c6': return 0x92; /* LATIN CAPITAL LIGATURE AE */
			case '\u00f4': return 0x93; /* LATIN SMALL LETTER O WITH CIRCUMFLEX */
			case '\u00f6': return 0x94; /* LATIN SMALL LETTER O WITH DIAERESIS */
			case '\u00f2': return 0x95; /* LATIN SMALL LETTER O WITH GRAVE */
			case '\u00fb': return 0x96; /* LATIN SMALL LETTER U WITH CIRCUMFLEX */
			case '\u00f9': return 0x97; /* LATIN SMALL LETTER U WITH GRAVE */
			case '\u00ff': return 0x98; /* LATIN SMALL LETTER Y WITH DIAERESIS */
			case '\u00d6': return 0x99; /* LATIN CAPITAL LETTER O WITH DIAERESIS */
			case '\u00dc': return 0x9a; /* LATIN CAPITAL LETTER U WITH DIAERESIS */
			case '\u00a2': return 0x9b; /* CENT SIGN */
			case '\u00a3': return 0x9c; /* POUND SIGN */
			case '\u00a5': return 0x9d; /* YEN SIGN */
			case '\u20a7': return 0x9e; /* PESETA SIGN */
			case '\u0192': return 0x9f; /* LATIN SMALL LETTER F WITH HOOK */
			case '\u00e1': return 0xa0; /* LATIN SMALL LETTER A WITH ACUTE */
			case '\u00ed': return 0xa1; /* LATIN SMALL LETTER I WITH ACUTE */
			case '\u00f3': return 0xa2; /* LATIN SMALL LETTER O WITH ACUTE */
			case '\u00fa': return 0xa3; /* LATIN SMALL LETTER U WITH ACUTE */
			case '\u00f1': return 0xa4; /* LATIN SMALL LETTER N WITH TILDE */
			case '\u00d1': return 0xa5; /* LATIN CAPITAL LETTER N WITH TILDE */
			case '\u00aa': return 0xa6; /* FEMININE ORDINAL INDICATOR */
			case '\u00ba': return 0xa7; /* MASCULINE ORDINAL INDICATOR */
			case '\u00bf': return 0xa8; /* INVERTED QUESTION MARK */
			case '\u2310': return 0xa9; /* REVERSED NOT SIGN */
			case '\u00ac': return 0xaa; /* NOT SIGN */
			case '\u00bd': return 0xab; /* VULGAR FRACTION ONE HALF */
			case '\u00bc': return 0xac; /* VULGAR FRACTION ONE QUARTER */
			case '\u00a1': return 0xad; /* INVERTED EXCLAMATION MARK */
			case '\u00ab': return 0xae; /* LEFT-POINTING DOUBLE ANGLE QUOTATION MARK */
			case '\u00bb': return 0xaf; /* RIGHT-POINTING DOUBLE ANGLE QUOTATION MARK */
			case '\u2591': return 0xb0; /* LIGHT SHADE */
			case '\u2592': return 0xb1; /* MEDIUM SHADE */
			case '\u2593': return 0xb2; /* DARK SHADE */
			case '\u2502': return 0xb3; /* BOX DRAWINGS LIGHT VERTICAL */
			case '\u2524': return 0xb4; /* BOX DRAWINGS LIGHT VERTICAL AND LEFT */
			case '\u2561': return 0xb5; /* BOX DRAWINGS VERTICAL SINGLE AND LEFT DOUBLE */
			case '\u2562': return 0xb6; /* BOX DRAWINGS VERTICAL DOUBLE AND LEFT SINGLE */
			case '\u2556': return 0xb7; /* BOX DRAWINGS DOWN DOUBLE AND LEFT SINGLE */
			case '\u2555': return 0xb8; /* BOX DRAWINGS DOWN SINGLE AND LEFT DOUBLE */
			case '\u2563': return 0xb9; /* BOX DRAWINGS DOUBLE VERTICAL AND LEFT */
			case '\u2551': return 0xba; /* BOX DRAWINGS DOUBLE VERTICAL */
			case '\u2557': return 0xbb; /* BOX DRAWINGS DOUBLE DOWN AND LEFT */
			case '\u255d': return 0xbc; /* BOX DRAWINGS DOUBLE UP AND LEFT */
			case '\u255c': return 0xbd; /* BOX DRAWINGS UP DOUBLE AND LEFT SINGLE */
			case '\u255b': return 0xbe; /* BOX DRAWINGS UP SINGLE AND LEFT DOUBLE */
			case '\u2510': return 0xbf; /* BOX DRAWINGS LIGHT DOWN AND LEFT */
			case '\u2514': return 0xc0; /* BOX DRAWINGS LIGHT UP AND RIGHT */
			case '\u2534': return 0xc1; /* BOX DRAWINGS LIGHT UP AND HORIZONTAL */
			case '\u252c': return 0xc2; /* BOX DRAWINGS LIGHT DOWN AND HORIZONTAL */
			case '\u251c': return 0xc3; /* BOX DRAWINGS LIGHT VERTICAL AND RIGHT */
			case '\u2500': return 0xc4; /* BOX DRAWINGS LIGHT HORIZONTAL */
			case '\u253c': return 0xc5; /* BOX DRAWINGS LIGHT VERTICAL AND HORIZONTAL */
			case '\u255e': return 0xc6; /* BOX DRAWINGS VERTICAL SINGLE AND RIGHT DOUBLE */
			case '\u255f': return 0xc7; /* BOX DRAWINGS VERTICAL DOUBLE AND RIGHT SINGLE */
			case '\u255a': return 0xc8; /* BOX DRAWINGS DOUBLE UP AND RIGHT */
			case '\u2554': return 0xc9; /* BOX DRAWINGS DOUBLE DOWN AND RIGHT */
			case '\u2569': return 0xca; /* BOX DRAWINGS DOUBLE UP AND HORIZONTAL */
			case '\u2566': return 0xcb; /* BOX DRAWINGS DOUBLE DOWN AND HORIZONTAL */
			case '\u2560': return 0xcc; /* BOX DRAWINGS DOUBLE VERTICAL AND RIGHT */
			case '\u2550': return 0xcd; /* BOX DRAWINGS DOUBLE HORIZONTAL */
			case '\u256c': return 0xce; /* BOX DRAWINGS DOUBLE VERTICAL AND HORIZONTAL */
			case '\u2567': return 0xcf; /* BOX DRAWINGS UP SINGLE AND HORIZONTAL DOUBLE */
			case '\u2568': return 0xd0; /* BOX DRAWINGS UP DOUBLE AND HORIZONTAL SINGLE */
			case '\u2564': return 0xd1; /* BOX DRAWINGS DOWN SINGLE AND HORIZONTAL DOUBLE */
			case '\u2565': return 0xd2; /* BOX DRAWINGS DOWN DOUBLE AND HORIZONTAL SINGLE */
			case '\u2559': return 0xd3; /* BOX DRAWINGS UP DOUBLE AND RIGHT SINGLE */
			case '\u2558': return 0xd4; /* BOX DRAWINGS UP SINGLE AND RIGHT DOUBLE */
			case '\u2552': return 0xd5; /* BOX DRAWINGS DOWN SINGLE AND RIGHT DOUBLE */
			case '\u2553': return 0xd6; /* BOX DRAWINGS DOWN DOUBLE AND RIGHT SINGLE */
			case '\u256b': return 0xd7; /* BOX DRAWINGS VERTICAL DOUBLE AND HORIZONTAL SINGLE */
			case '\u256a': return 0xd8; /* BOX DRAWINGS VERTICAL SINGLE AND HORIZONTAL DOUBLE */
			case '\u2518': return 0xd9; /* BOX DRAWINGS LIGHT UP AND LEFT */
			case '\u250c': return 0xda; /* BOX DRAWINGS LIGHT DOWN AND RIGHT */
			case '\u2588': return 0xdb; /* FULL BLOCK */
			case '\u2584': return 0xdc; /* LOWER HALF BLOCK */
			case '\u258c': return 0xdd; /* LEFT HALF BLOCK */
			case '\u2590': return 0xde; /* RIGHT HALF BLOCK */
			case '\u2580': return 0xdf; /* UPPER HALF BLOCK */
			case '\u03b1': return 0xe0; /* GREEK SMALL LETTER ALPHA */
			case '\u00df': return 0xe1; /* LATIN SMALL LETTER SHARP S */
			case '\u0393': return 0xe2; /* GREEK CAPITAL LETTER GAMMA */
			case '\u03c0': return 0xe3; /* GREEK SMALL LETTER PI */
			case '\u03a3': return 0xe4; /* GREEK CAPITAL LETTER SIGMA */
			case '\u03c3': return 0xe5; /* GREEK SMALL LETTER SIGMA */
			case '\u00b5': return 0xe6; /* MICRO SIGN */
			case '\u03c4': return 0xe7; /* GREEK SMALL LETTER TAU */
			case '\u03a6': return 0xe8; /* GREEK CAPITAL LETTER PHI */
			case '\u0398': return 0xe9; /* GREEK CAPITAL LETTER THETA */
			case '\u03a9': return 0xea; /* GREEK CAPITAL LETTER OMEGA */
			case '\u03b4': return 0xeb; /* GREEK SMALL LETTER DELTA */
			case '\u221e': return 0xec; /* INFINITY */
			case '\u03c6': return 0xed; /* GREEK SMALL LETTER PHI */
			case '\u03b5': return 0xee; /* GREEK SMALL LETTER EPSILON */
			case '\u2229': return 0xef; /* INTERSECTION */
			case '\u2261': return 0xf0; /* IDENTICAL TO */
			case '\u00b1': return 0xf1; /* PLUS-MINUS SIGN */
			case '\u2265': return 0xf2; /* GREATER-THAN OR EQUAL TO */
			case '\u2264': return 0xf3; /* LESS-THAN OR EQUAL TO */
			case '\u2320': return 0xf4; /* TOP HALF INTEGRAL */
			case '\u2321': return 0xf5; /* BOTTOM HALF INTEGRAL */
			case '\u00f7': return 0xf6; /* DIVISION SIGN */
			case '\u2248': return 0xf7; /* ALMOST EQUAL TO */
			case '\u00b0': return 0xf8; /* DEGREE SIGN */
			case '\u2219': return 0xf9; /* BULLET OPERATOR */
			case '\u00b7': return 0xfa; /* MIDDLE DOT */
			case '\u221a': return 0xfb; /* SQUARE ROOT */
			case '\u207f': return 0xfc; /* SUPERSCRIPT LATIN SMALL LETTER N */
			case '\u00b2': return 0xfd; /* SUPERSCRIPT TWO */
			case '\u25a0': return 0xfe; /* BLACK SQUARE */
			case '\u00a0': return 0xff; /* NO-BREAK SPACE */

			/* -- CUSTOM CASES */
			case '\u2019': return 39; // fancy apostrophe
			case '\u00B3': return 51; // superscript three
		}

		return -1;
	}

	public static int ToITF(this char c)
	{
		if ((c == 0) || (c >= 32 && c <= 127)) return c;

		switch (c)
		{
			case '\u263A': return 1;  // WHITE SMILING FACE
			case '\u263B': return 2;  // BLACK SMILING FACE
			case '\u2661':
			case '\u2665': return 3;  // BLACK HEART
			case '\u2662':
			case '\u25C6':
			case '\u2666': return 4;  // BLACK DIAMOND
			case '\u2667':
			case '\u2663': return 5;  // BLACK CLUBS
			case '\u2664':
			case '\u2660': return 6;  // BLACK SPADE
			case '\u25CF': return 7;  // BLACK CIRCLE
			case '\u25D8': return 8;  // INVERSE BULLET
			case '\u25CB':
			case '\u25E6':
			case '\u25EF': return 9;  // LARGE CIRCLE
			case '\u25D9': return 10; // INVERSE WHITE CIRCLE
			case '\u2642': return 11; // MALE / MARS
			case '\u2640': return 12; // FEMALE / VENUS
			case '\u266A': return 13; // EIGHTH NOTE
			case '\u266B': return 14; // BEAMED EIGHTH NOTES

			case '\u2195': return 18; // UP DOWN ARROW
			case '\u203C': return 19; // DOUBLE EXCLAMATION MARK
			case '\u00B6': return 20; // PILCROW SIGN
			case '\u00A7': return 21; // SECTION SIGN

			case '\u21A8': return 23; // UP DOWN ARROW WITH BASE
			case '\u2191': return 24; // UPWARD ARROW
			case '\u2193': return 25; // DOWNWARD ARROW
			case '\u2192': return 26; // RIGHTWARD ARROW
			case '\u2190': return 27; // LEFTWARD ARROW

			case '\u2194': return 29; // LEFT RIGHT ARROW

			case '\u266F': return '#';// MUSIC SHARP SIGN
			case '\u00A6': return 124;
			case '\u0394':
			case '\u2302': return 127;// HOUSE

			case '\u203E': return 129;// UNDERLINE (???)

			case '\u20B5':
			case '\u20B2':
			case '\u00A2': return 155;// CENT SIGN
			case '\u00A3': return 156;// POUND SIGN
			case '\u00A5': return 157;// YEN SIGN

			case '\u2310': return 169;// REVERSED NOT SIGN
			case '\u00AC': return 170;// NOT SIGN
			case '\u00BD': return 171;// 1/2
			case '\u00BC': return 172;// 1/4
			case '\u00A1': return 173;// INVERTED EXCLAMATION MARK
			case '\u00AB': return 174;// <<
			case '\u00BB': return 175;// >>

			case '\u2591': return 176;// LIGHT SHADE
			case '\u2592': return 177;// MEDIUM SHADE
			case '\u2593': return 178;// DARK SHADE

			// BOX DRAWING
			case '\u2502': return 179;
			case '\u2524': return 180;
			case '\u2561': return 181;
			case '\u2562': return 182;
			case '\u2556': return 183;
			case '\u2555': return 184;
			case '\u2563': return 185;
			case '\u2551': return 186;
			case '\u2557': return 187;
			case '\u255D': return 188;
			case '\u255C': return 189;
			case '\u255B': return 190;
			case '\u2510': return 191;
			case '\u2514': return 192;
			case '\u2534': return 193;
			case '\u252C': return 194;
			case '\u251C': return 195;
			case '\u2500': return 196;
			case '\u253C': return 197;
			case '\u255E': return 198;
			case '\u255F': return 199;
			case '\u255A': return 200;
			case '\u2554': return 201;
			case '\u2569': return 202;
			case '\u2566': return 203;
			case '\u2560': return 204;
			case '\u2550': return 205;
			case '\u256C': return 206;
			case '\u2567': return 207;
			case '\u2568': return 208;
			case '\u2564': return 209;
			case '\u2565': return 210;
			case '\u2559': return 211;
			case '\u2558': return 212;
			case '\u2552': return 213;
			case '\u2553': return 214;
			case '\u256B': return 215;
			case '\u256A': return 216;
			case '\u2518': return 217;
			case '\u250C': return 218;
			case '\u25A0': return 219;// BLACK SQUARE
			case '\u2584': return 220;// LOWER HALF BLOCK
			case '\u258C': return 221;// LEFT HALF BLOCK
			case '\u2590': return 222;// RIGHT HALF BLOCK
			case '\u2580': return 223;// UPPER HALF BLOCK

			case '\u03B1': return 224;// GREEK SMALL LETTER ALPHA
			case '\u03B2': return 225;// GREEK SMALL LETTER BETA
			case '\u0393': return 226;// GREEK CAPITAL LETTER GAMMA
			case '\u03C0': return 227;// mmm... pie...
			case '\u03A3':
			case '\u2211': return 228;// N-ARY SUMMATION / CAPITAL SIGMA
			case '\u03C3': return 229;// GREEK SMALL LETTER SIGMA
			case '\u03BC':
			case '\u00b5': return 230;// GREEK SMALL LETTER MU
			case '\u03C4':
			case '\u03D2': return 231;// GREEK UPSILON+HOOK

			case '\u03B8': return 233;// GREEK SMALL LETTER THETA
			case '\u03A9': return 234;// GREEK CAPITAL LETTER OMEGA
			case '\u03B4': return 235;// GREEK SMALL LETTER DELTA

			case '\u221E': return 236;// INFINITY
			case '\u00D8':
			case '\u00F8': return 237;// LATIN ... LETTER O WITH STROKE
			case '\u03F5': return 238;// GREEK LUNATE EPSILON SYMBOL
			case '\u2229':
			case '\u03A0': return 239;// GREEK CAPITAL LETTER PI
			case '\u039E': return 240;// GREEK CAPITAL LETTER XI
			case '\u00b1': return 241;// PLUS-MINUS SIGN
			case '\u2265': return 242;// GREATER-THAN OR EQUAL TO
			case '\u2264': return 243;// LESS-THAN OR EQUAL TO
			case '\u2320': return 244;// TOP HALF INTEGRAL
			case '\u2321': return 245;// BOTTOM HALF INTEGRAL
			case '\u00F7': return 246;// DIVISION SIGN
			case '\u2248': return 247;// ALMOST EQUAL TO
			case '\u00B0': return 248;// DEGREE SIGN
			case '\u00B7': return 249;// MIDDLE DOT
			case '\u2219':
			case '\u0387': return 250;// GREEK ANO TELEIA
			case '\u221A': return 251;// SQUARE ROOT
			// NO UNICODE ALLOCATION?
			case '\u00B2': return 253;// SUPERSCRIPT TWO
			case '\u220E': return 254;// QED

			// No idea if this is right ;P
			case '\u00A0': return 255;
		}

		/* nothing */
		return -1;
	}
}
