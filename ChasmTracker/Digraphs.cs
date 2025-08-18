using System.Collections.Generic;
using ChasmTracker.Utility;

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
			{ ('C', ','), ((byte)128).FromCP437() }, // LATIN CAPITAL LETTER C WITH CEDILLA
			{ ('u', ':'), ((byte)129).FromCP437() }, // LATIN SMALL LETTER U WITH DIAERESIS
			{ ('e', '\''), ((byte)130).FromCP437() }, // LATIN SMALL LETTER E WITH ACUTE
			{ ('a', '>'), ((byte)131).FromCP437() }, // LATIN SMALL LETTER A WITH CIRCUMFLEX
			{ ('a', ':'), ((byte)132).FromCP437() }, // LATIN SMALL LETTER A WITH DIAERESIS
			{ ('a', '!'), ((byte)133).FromCP437() }, // LATIN SMALL LETTER A WITH GRAVE
			{ ('a', 'a'), ((byte)134).FromCP437() }, // LATIN SMALL LETTER A WITH RING ABOVE
			{ ('c', ','), ((byte)135).FromCP437() }, // LATIN SMALL LETTER C WITH CEDILLA
			{ ('e', '>'), ((byte)136).FromCP437() }, // LATIN SMALL LETTER E WITH CIRCUMFLEX
			{ ('e', ':'), ((byte)137).FromCP437() }, // LATIN SMALL LETTER E WITH DIAERESIS
			{ ('e', '!'), ((byte)138).FromCP437() }, // LATIN SMALL LETTER E WITH GRAVE
			{ ('i', ':'), ((byte)139).FromCP437() }, // LATIN SMALL LETTER I WITH DIAERESIS
			{ ('i', '>'), ((byte)140).FromCP437() }, // LATIN SMALL LETTER I WITH CIRCUMFLEX
			{ ('i', '!'), ((byte)141).FromCP437() }, // LATIN SMALL LETTER I WITH GRAVE
			{ ('A', ':'), ((byte)142).FromCP437() }, // LATIN CAPITAL LETTER A WITH DIAERESIS
			{ ('A', 'A'), ((byte)143).FromCP437() }, // LATIN CAPITAL LETTER A WITH RING ABOVE
			{ ('E', '\''), ((byte)144).FromCP437() }, // LATIN CAPITAL LETTER E WITH ACUTE
			{ ('a', 'e'), ((byte)145).FromCP437() }, // LATIN SMALL LETTER AE
			{ ('A', 'E'), ((byte)146).FromCP437() }, // LATIN CAPITAL LETTER AE
			{ ('o', '>'), ((byte)147).FromCP437() }, // LATIN SMALL LETTER O WITH CIRCUMFLEX
			{ ('o', ':'), ((byte)148).FromCP437() }, // LATIN SMALL LETTER O WITH DIAERESIS
			{ ('o', '!'), ((byte)149).FromCP437() }, // LATIN SMALL LETTER O WITH GRAVE
			{ ('u', '>'), ((byte)150).FromCP437() }, // LATIN SMALL LETTER U WITH CIRCUMFLEX
			{ ('u', '!'), ((byte)151).FromCP437() }, // LATIN SMALL LETTER U WITH GRAVE
			{ ('y', ':'), ((byte)152).FromCP437() }, // LATIN SMALL LETTER Y WITH DIAERESIS
			{ ('O', ':'), ((byte)153).FromCP437() }, // LATIN CAPITAL LETTER O WITH DIAERESIS
			{ ('U', ':'), ((byte)154).FromCP437() }, // LATIN CAPITAL LETTER U WITH DIAERESIS
			{ ('C', 't'), ((byte)155).FromCP437() }, // CENT SIGN
			{ ('P', 'd'), ((byte)156).FromCP437() }, // POUND SIGN
			{ ('Y', 'e'), ((byte)157).FromCP437() }, // YEN SIGN
			{ ('P', 't'), ((byte)158).FromCP437() },
			{ ('f', 'f'), ((byte)159).FromCP437() },
			{ ('a', '\''), ((byte)160).FromCP437() }, // LATIN SMALL LETTER A WITH ACUTE
			{ ('i', '\''), ((byte)161).FromCP437() }, // LATIN SMALL LETTER I WITH ACUTE
			{ ('o', '\''), ((byte)162).FromCP437() }, // LATIN SMALL LETTER O WITH ACUTE
			{ ('u', '\''), ((byte)163).FromCP437() }, // LATIN SMALL LETTER U WITH ACUTE
			{ ('n', '?'), ((byte)164).FromCP437() }, // LATIN SMALL LETTER N WITH TILDE
			{ ('N', '?'), ((byte)165).FromCP437() }, // LATIN CAPITAL LETTER N WITH TILDE
			{ ('-', 'a'), ((byte)166).FromCP437() }, // FEMININE ORDINAL INDICATOR
			{ ('-', 'o'), ((byte)167).FromCP437() }, // MASCULINE ORDINAL INDICATOR
			{ ('?', 'I'), ((byte)168).FromCP437() }, // INVERTED QUESTION MARK

			{ ('N', 'O'), ((byte)170).FromCP437() }, // NOT SIGN
			{ ('1', '2'), ((byte)171).FromCP437() }, // VULGAR FRACTION ONE HALF
			{ ('1', '4'), ((byte)174).FromCP437() }, // VULGAR FRACTION ONE QUARTER
			{ ('!', 'I'), ((byte)175).FromCP437() }, // INVERTED EXCLAMATION MARK
			{ ('<', '<'), ((byte)176).FromCP437() }, // LEFT-POINTING DOUBLE ANGLE QUOTATION MARK
			{ ('>', '>'), ((byte)177).FromCP437() }, // RIGHT-POINTING DOUBLE ANGLE QUOTATION MARK

			{ ('s', 's'), ((byte)225).FromCP437() }, // LATIN SMALL LETTER SHARP S
			{ ('p', 'i'), ((byte)227).FromCP437() }, // PI... mmm... pie...
			{ ('M', 'y'), ((byte)230).FromCP437() }, // MICRO SIGN
			{ ('o', '/'), ((byte)237).FromCP437() }, // LATIN SMALL LETTER O WITH STROKE
			{ ('O', '/'), ((byte)237).FromCP437() }, // LATIN SMALL LETTER O WITH STROKE
			{ ('+', '-'), ((byte)241).FromCP437() }, // PLUS-MINUS SIGN
			{ ('-', ':'), ((byte)246).FromCP437() }, // DIVISION SIGN
			{ ('D', 'G'), ((byte)248).FromCP437() }, // DEGREE SIGN
			{ ('.', 'M'), ((byte)249).FromCP437() }, // MIDDLE DOT
			{ ('2', 'S'), ((byte)253).FromCP437() }, // SUPERSCRIPT TWO
			{ ('n', 'S'), ((byte)252).FromCP437() },

			{ ('P', 'I'), ((byte)20).FromCP437() },  // PILCROW SIGN
			{ ('S', 'E'), ((byte)21).FromCP437() },  // SECTION SIGN
		};

	public static char Digraph(char k1, char k2)
	{
		if (s_digraphs.TryGetValue((k1, k2), out var d))
			return d;

		return '\0';
	}
}
