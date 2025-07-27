using System;
using System.Linq;

namespace ChasmTracker;

public class Tables
{
	// <eightbitbubsy> better than having a table.
	public static int ShortPanning(int i)
	{
		return ((((i) << 4) | (i)) + 2) >> 2;
	}

	public static readonly byte[] VolumeColumnPortamentoTable =
		new byte[]
		{
			0x00, 0x01, 0x04, 0x08, 0x10, 0x20, 0x40, 0x60,
			0x80, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
		};

	public static readonly short[] PeriodTable;

	public static readonly short[] AmigaPeriodTable =
		new short[]
		{
			0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,   0,    0,    0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,   0,    0,    0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,   0,    0,    0,
			1712, 1616, 1524, 1440, 1356, 1280, 1208, 1140, 1076, 1016, 960, 906,
			856,  808,  762,  720,  678,  640,  604,  570,  538,  508,  480, 453,
			428,  404,  381,  360,  339,  320,  302,  285,  269,  254,  240, 226,
			214,  202,  190,  180,  170,  160,  151,  143,  135,  127,  120, 113,
			107,  101,  95,   90,   85,   80,   75,   71,   67,   63,   60,  56,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0,    0,    0,    0,    0,    0,    0,    0,    0,   0,
			0,    0,    0
		};

	public static readonly short[] FineTuneTable;

	// Tables from ITTECH.TXT
	public static readonly sbyte[] SineTable;

	public static readonly sbyte[] RampDownTable;

	public static readonly sbyte[] SquareTable;

	// volume fade tables for Retrig Note:
	public static readonly sbyte[] RetriggerTable1 =
		new sbyte[] { 0, 0, 0, 0, 0, 0, 10, 8, 0, 0, 0, 0, 0, 0, 24, 32 };
	public static readonly sbyte[] RetriggerTable2 =
		new sbyte[] { 0, -1, -2, -4, -8, -16, 0, 0, 0, 1, 2, 4, 8, 16, 0, 0 };

	// round(65536 * 2**(n/768))
	// 768 = 64 extra-fine finetune steps for 12 notes
	// Table content is in 16.16 format
	public static readonly int[] FineLinearSlideUpTable;

	// round(65536 * 2**(-n/768))
	// 768 = 64 extra-fine finetune steps for 12 notes
	// Table content is in 16.16 format
	public static readonly int[] FineLinearSlideDownTable;

	// floor(65536 * 2**(n/192))
	// 192 = 16 finetune steps for 12 notes
	// Table content is in 16.16 format
	public static readonly int[] LinearSlideUpTable;

	// floor(65536 * 2**(-n/192))
	// 192 = 16 finetune steps for 12 notes
	// Table content is in 16.16 format
	public static readonly int[] LinearSlideDownTable;

	static Tables()
	{
		// The hardcoded table differs from this in some seemingly-arbitrary ways.
		PeriodTable =
			Enumerable.Range(0, 12)
			.Select(n => 12 - n)
			.Select(n => (short)(856.0 * Math.Pow(2.0, n / 12.0)))
			.ToArray();

		FineTuneTable =
			Enumerable.Range(0, 16)
			.Select(i => (short)(8363.0 * Math.Pow(2.0, (i - 8) / (12 * 8))))
			.ToArray();

		SineTable =
			Enumerable.Range(0, 256)
			.Select(n => (sbyte)(63 * Math.Sin(n * 2 * Math.PI / 256)))
			.ToArray();

		RampDownTable =
			Enumerable.Range(0, 256)
			.Select(n => (sbyte)((128 - n) / 2))
			.ToArray();

		SquareTable =
			Enumerable.Range(0, 256)
			.Select(n => (sbyte)((n < 128) ? 64 : 0))
			.ToArray();

		FineLinearSlideUpTable =
			Enumerable.Range(0, 16)
			.Select(n => (int)Math.Round(65536.0 * Math.Pow(2.0, n / 768.0)))
			.ToArray();

		FineLinearSlideDownTable =
			Enumerable.Range(0, 16)
			.Select(n => (int)Math.Round(65536.0 * Math.Pow(2.0, -n / 768.0)))
			.ToArray();

		LinearSlideUpTable =
			Enumerable.Range(0, 256)
			.Select(n => (int)Math.Floor(65536.0 * Math.Pow(2.0, n / 192.0)))
			.ToArray();

		LinearSlideDownTable =
			Enumerable.Range(0, 256)
			.Select(n => (int)Math.Floor(65536.0 * Math.Pow(2.0, -n / 192.0)))
			.ToArray();
	}

	/* --------------------------------------------------------------------------------------------------------- */

	public static readonly string[] MIDIGroupNames =
		new string[]
		{
			"Piano",
			"Chromatic Percussion",
			"Organ",
			"Guitar",
			"Bass",
			"Strings",
			"Ensemble",
			"Brass",
			"Reed",
			"Pipe",
			"Synth Lead",
			"Synth Pad",
			"Synth Effects",
			"Ethnic",
			"Percussive",
			"Sound Effects",
			"Percussions",
		};

	public static readonly string[] MIDIProgramNames =
		new string[]
		{
			// 1-8: Piano
			"Acoustic Grand Piano",
			"Bright Acoustic Piano",
			"Electric Grand Piano",
			"Honky-tonk Piano",
			"Electric Piano 1",
			"Electric Piano 2",
			"Harpsichord",
			"Clavi",
			// 9-16: Chromatic Percussion
			"Celesta",
			"Glockenspiel",
			"Music Box",
			"Vibraphone",
			"Marimba",
			"Xylophone",
			"Tubular Bells",
			"Dulcimer",
			// 17-24: Organ
			"Drawbar Organ",
			"Percussive Organ",
			"Rock Organ",
			"Church Organ",
			"Reed Organ",
			"Accordion",
			"Harmonica",
			"Tango Accordion",
			// 25-32: Guitar
			"Acoustic Guitar (nylon)",
			"Acoustic Guitar (steel)",
			"Electric Guitar (jazz)",
			"Electric Guitar (clean)",
			"Electric Guitar (muted)",
			"Overdriven Guitar",
			"Distortion Guitar",
			"Guitar harmonics",
			// 33-40   Bass
			"Acoustic Bass",
			"Electric Bass (finger)",
			"Electric Bass (pick)",
			"Fretless Bass",
			"Slap Bass 1",
			"Slap Bass 2",
			"Synth Bass 1",
			"Synth Bass 2",
			// 41-48   Strings
			"Violin",
			"Viola",
			"Cello",
			"Contrabass",
			"Tremolo Strings",
			"Pizzicato Strings",
			"Orchestral Harp",
			"Timpani",
			// 49-56   Ensemble
			"String Ensemble 1",
			"String Ensemble 2",
			"SynthStrings 1",
			"SynthStrings 2",
			"Choir Aahs",
			"Voice Oohs",
			"Synth Voice",
			"Orchestra Hit",
			// 57-64   Brass
			"Trumpet",
			"Trombone",
			"Tuba",
			"Muted Trumpet",
			"French Horn",
			"Brass Section",
			"SynthBrass 1",
			"SynthBrass 2",
			// 65-72   Reed
			"Soprano Sax",
			"Alto Sax",
			"Tenor Sax",
			"Baritone Sax",
			"Oboe",
			"English Horn",
			"Bassoon",
			"Clarinet",
			// 73-80   Pipe
			"Piccolo",
			"Flute",
			"Recorder",
			"Pan Flute",
			"Blown Bottle",
			"Shakuhachi",
			"Whistle",
			"Ocarina",
			// 81-88   Synth Lead
			"Lead 1 (square)",
			"Lead 2 (sawtooth)",
			"Lead 3 (calliope)",
			"Lead 4 (chiff)",
			"Lead 5 (charang)",
			"Lead 6 (voice)",
			"Lead 7 (fifths)",
			"Lead 8 (bass + lead)",
			// 89-96   Synth Pad
			"Pad 1 (new age)",
			"Pad 2 (warm)",
			"Pad 3 (polysynth)",
			"Pad 4 (choir)",
			"Pad 5 (bowed)",
			"Pad 6 (metallic)",
			"Pad 7 (halo)",
			"Pad 8 (sweep)",
			// 97-104  Synth Effects
			"FX 1 (rain)",
			"FX 2 (soundtrack)",
			"FX 3 (crystal)",
			"FX 4 (atmosphere)",
			"FX 5 (brightness)",
			"FX 6 (goblins)",
			"FX 7 (echoes)",
			"FX 8 (sci-fi)",
			// 105-112 Ethnic
			"Sitar",
			"Banjo",
			"Shamisen",
			"Koto",
			"Kalimba",
			"Bag pipe",
			"Fiddle",
			"Shanai",
			// 113-120 Percussive
			"Tinkle Bell",
			"Agogo",
			"Steel Drums",
			"Woodblock",
			"Taiko Drum",
			"Melodic Tom",
			"Synth Drum",
			"Reverse Cymbal",
			// 121-128 Sound Effects
			"Guitar Fret Noise",
			"Breath Noise",
			"Seashore",
			"Bird Tweet",
			"Telephone Ring",
			"Helicopter",
			"Applause",
			"Gunshot",
		};

	// Notes 25-85
	public static readonly string[] MIDIPercussionNames =
		new string[]
		{
			"Seq Click",
			"Brush Tap",
			"Brush Swirl",
			"Brush Slap",
			"Brush Swirl W/Attack",
			"Snare Roll",
			"Castanet",
			"Snare Lo",
			"Sticks",
			"Bass Drum Lo",
			"Open Rim Shot",
			"Acoustic Bass Drum",
			"Bass Drum 1",
			"Side Stick",
			"Acoustic Snare",
			"Hand Clap",
			"Electric Snare",
			"Low Floor Tom",
			"Closed Hi Hat",
			"High Floor Tom",
			"Pedal Hi-Hat",
			"Low Tom",
			"Open Hi-Hat",
			"Low-Mid Tom",
			"Hi Mid Tom",
			"Crash Cymbal 1",
			"High Tom",
			"Ride Cymbal 1",
			"Chinese Cymbal",
			"Ride Bell",
			"Tambourine",
			"Splash Cymbal",
			"Cowbell",
			"Crash Cymbal 2",
			"Vibraslap",
			"Ride Cymbal 2",
			"Hi Bongo",
			"Low Bongo",
			"Mute Hi Conga",
			"Open Hi Conga",
			"Low Conga",
			"High Timbale",
			"Low Timbale",
			"High Agogo",
			"Low Agogo",
			"Cabasa",
			"Maracas",
			"Short Whistle",
			"Long Whistle",
			"Short Guiro",
			"Long Guiro",
			"Claves",
			"Hi Wood Block",
			"Low Wood Block",
			"Mute Cuica",
			"Open Cuica",
			"Mute Triangle",
			"Open Triangle",
			"Shaker",
			"Jingle Bell",
			"Bell Tree",
		};

	/*
	 * LUT for 2 * damping factor
	 *
	 * Formula for the table:
	 *
	 *    resonance_table[i] = pow(10.0, -((24.0 / 128.0) * i) / 20.0);
	 * or
	 *    resonance_table[i] = pow(10.0, -3.0 * i / 320.0);
	 *
	 */
	public static readonly double[] ResonanceTable =
		Enumerable.Range(0, 128).Select(i => Math.Pow(10.0, -3.0 * i / 320)).ToArray();

	public static int ProTrackerPanning(int n)
		=> (((n + 1) >> 1) & 1) * 256;

	public static readonly uint[] GUSFrequencyScaleTable =
		{
	/*C-0..B-*/
	/* Octave 0 */  16351, 17323, 18354, 19445, 20601, 21826,
			23124, 24499, 25956, 27500, 29135, 30867,
	/* Octave 1 */  32703, 34647, 36708, 38890, 41203, 43653,
			46249, 48999, 51913, 54999, 58270, 61735,
	/* Octave 2 */  65406, 69295, 73416, 77781, 82406, 87306,
			92498, 97998, 103826, 109999, 116540, 123470,
	/* Octave 3 */  130812, 138591, 146832, 155563, 164813, 174614,
			184997, 195997, 207652, 219999, 233081, 246941,
	/* Octave 4 */  261625, 277182, 293664, 311126, 329627, 349228,
			369994, 391995, 415304, 440000, 466163, 493883,
	/* Octave 5 */  523251, 554365, 587329, 622254, 659255, 698456,
			739989, 783991, 830609, 880000, 932328, 987767,
	/* Octave 6 */  1046503, 1108731, 1174660, 1244509, 1318511, 1396914,
			1479979, 1567983, 1661220, 1760002, 1864657, 1975536,
	/* Octave 7 */  2093007, 2217464, 2349321, 2489019, 2637024, 2793830,
			2959960, 3135968, 3322443, 3520006, 3729316, 3951073,
	/* Octave 8 */  4186073, 4434930, 4698645, 4978041, 5274051, 5587663,
			5919922, 6271939, 6644889, 7040015, 7458636, 7902150,
			uint.MaxValue,
		};

	public static int GUSFrequency(uint freq)
	{
		for (int no = 0; GUSFrequencyScaleTable[no] != 0xFFFFFFFF; no++)
			if (GUSFrequencyScaleTable[no] <= freq
			 && GUSFrequencyScaleTable[no + 1] >= freq)
				return no - 12;

		return 4 * 12;
	}

	public static int MODFineTune(int b)
		=> FineTuneTable[(b & 0xF) ^ 8]; ;
}
