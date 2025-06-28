using System;
using System.Linq;

namespace ChasmTracker;

public class Tables
{
	public static readonly byte[] VolumeColumnPortamentoTable =
		new byte[]
		{
			0x00, 0x01, 0x04, 0x08, 0x10, 0x20, 0x40, 0x60,
			0x80, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
		};

	public static readonly short[] PeriodTable;

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
}
