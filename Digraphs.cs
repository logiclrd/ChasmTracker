using System.Collections.Generic;

namespace ChasmTracker;

public class Digraphs
{
	static readonly Dictionary<(char, char), char> s_digraphs =
		new Dictionary<(char, char), char>()
		{
			{ ('N', 'B'), '#' },
			{ ('D', 'O'), '$' },
			{ ('A', 't'), '@' },
			{ ('<', '('), '[' },
			{ ('/', '/'), '\\' },
			{ (')', '>'), ']' },
			{ ('\'', '>'), '^' },
			{ ('\'', '!'), '`' },
			{ ('(', '!'), '{' },
			{ ('!', '!'), '|' },
			{ ('!', ')'), '{' },
			{ ('\'', '?'), '~' },
			{ ('C', ','), (char)128 }, // LATIN CAPITAL LETTER C WITH CEDILLA
			{ ('u', ':'), (char)129 }, // LATIN SMALL LETTER U WITH DIAERESIS
			{ ('e', '\''), (char)130 }, // LATIN SMALL LETTER E WITH ACUTE
			{ ('a', '>'), (char)131 }, // LATIN SMALL LETTER A WITH CIRCUMFLEX
			{ ('a', ':'), (char)132 }, // LATIN SMALL LETTER A WITH DIAERESIS
			{ ('a', '!'), (char)133 }, // LATIN SMALL LETTER A WITH GRAVE
			{ ('a', 'a'), (char)134 }, // LATIN SMALL LETTER A WITH RING ABOVE
			{ ('c', ','), (char)135 }, // LATIN SMALL LETTER C WITH CEDILLA
			{ ('e', '>'), (char)136 }, // LATIN SMALL LETTER E WITH CIRCUMFLEX
			{ ('e', ':'), (char)137 }, // LATIN SMALL LETTER E WITH DIAERESIS
			{ ('e', '!'), (char)138 }, // LATIN SMALL LETTER E WITH GRAVE
			{ ('i', ':'), (char)139 }, // LATIN SMALL LETTER I WITH DIAERESIS
			{ ('i', '>'), (char)140 }, // LATIN SMALL LETTER I WITH CIRCUMFLEX
			{ ('i', '!'), (char)141 }, // LATIN SMALL LETTER I WITH GRAVE
			{ ('A', ':'), (char)142 }, // LATIN CAPITAL LETTER A WITH DIAERESIS
			{ ('A', 'A'), (char)143 }, // LATIN CAPITAL LETTER A WITH RING ABOVE
			{ ('E', '\''), (char)144 }, // LATIN CAPITAL LETTER E WITH ACUTE
			{ ('a', 'e'), (char)145 }, // LATIN SMALL LETTER AE
			{ ('A', 'E'), (char)146 }, // LATIN CAPITAL LETTER AE
			{ ('o', '>'), (char)147 }, // LATIN SMALL LETTER O WITH CIRCUMFLEX
			{ ('o', ':'), (char)148 }, // LATIN SMALL LETTER O WITH DIAERESIS
			{ ('o', '!'), (char)149 }, // LATIN SMALL LETTER O WITH GRAVE
			{ ('u', '>'), (char)150 }, // LATIN SMALL LETTER U WITH CIRCUMFLEX
			{ ('u', '!'), (char)151 }, // LATIN SMALL LETTER U WITH GRAVE
			{ ('y', ':'), (char)152 }, // LATIN SMALL LETTER Y WITH DIAERESIS
			{ ('O', ':'), (char)153 }, // LATIN CAPITAL LETTER O WITH DIAERESIS
			{ ('U', ':'), (char)154 }, // LATIN CAPITAL LETTER U WITH DIAERESIS
			{ ('C', 't'), (char)155 }, // CENT SIGN
			{ ('P', 'd'), (char)156 }, // POUND SIGN
			{ ('Y', 'e'), (char)157 }, // YEN SIGN
			{ ('P', 't'), (char)158 },
			{ ('f', 'f'), (char)159 },
			{ ('a', '\''), (char)160 }, // LATIN SMALL LETTER A WITH ACUTE
			{ ('i', '\''), (char)161 }, // LATIN SMALL LETTER I WITH ACUTE
			{ ('o', '\''), (char)162 }, // LATIN SMALL LETTER O WITH ACUTE
			{ ('u', '\''), (char)163 }, // LATIN SMALL LETTER U WITH ACUTE
			{ ('n', '?'), (char)164 }, // LATIN SMALL LETTER N WITH TILDE
			{ ('N', '?'), (char)165 }, // LATIN CAPITAL LETTER N WITH TILDE
			{ ('-', 'a'), (char)166 }, // FEMININE ORDINAL INDICATOR
			{ ('-', 'o'), (char)167 }, // MASCULINE ORDINAL INDICATOR
			{ ('?', 'I'), (char)168 }, // INVERTED QUESTION MARK

			{ ('N', 'O'), (char)170 }, // NOT SIGN
			{ ('1', '2'), (char)171 }, // VULGAR FRACTION ONE HALF
			{ ('1', '4'), (char)174 }, // VULGAR FRACTION ONE QUARTER
			{ ('!', 'I'), (char)175 }, // INVERTED EXCLAMATION MARK
			{ ('<', '<'), (char)176 }, // LEFT-POINTING DOUBLE ANGLE QUOTATION MARK
			{ ('>', '>'), (char)177 }, // RIGHT-POINTING DOUBLE ANGLE QUOTATION MARK

			{ ('s', 's'), (char)225 }, // LATIN SMALL LETTER SHARP S
			{ ('p', 'i'), (char)227 }, // PI... mmm... pie...
			{ ('M', 'y'), (char)230 }, // MICRO SIGN
			{ ('o', '/'), (char)237 }, // LATIN SMALL LETTER O WITH STROKE
			{ ('O', '/'), (char)237 }, // LATIN SMALL LETTER O WITH STROKE
			{ ('+', '-'), (char)241 }, // PLUS-MINUS SIGN
			{ ('-', ':'), (char)246 }, // DIVISION SIGN
			{ ('D', 'G'), (char)248 }, // DEGREE SIGN
			{ ('.', 'M'), (char)249 }, // MIDDLE DOT
			{ ('2', 'S'), (char)253 }, // SUPERSCRIPT TWO
			{ ('n', 'S'), (char)252 },

			{ ('P', 'I'), (char)20 },  // PILCROW SIGN
			{ ('S', 'E'), (char)21 },  // SECTION SIGN
		};

	public static char Digraph(char k1, char k2)
	{
		if (s_digraphs.TryGetValue((k1, k2), out var d))
			return d;

		return '\0';
	}
}
