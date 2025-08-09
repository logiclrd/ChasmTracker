using System;
using System.Reflection;
using ChasmTracker.MIDI;

namespace ChasmTracker.Configurations;

public class MIDIConfiguration : ConfigurationSection
{
	public MIDIFlags Flags = MIDIFlags.TickQuantize | MIDIFlags.RecordNoteOff | MIDIFlags.RecordVelocity | MIDIFlags.RecordAftertouch | MIDIFlags.PitchBend;
	[ConfigurationKey("pitch_depth")]
	public int PitchWheelDepth = 12;
	public int Amplification = 100;
	public int C5Note = 60;

	public string? Name;
	public string? Start;
	public string? Stop;
	public string? Tick;
	public string? NoteOn;
	public string? NoteOff;
	public string? SetVolume;
	public string? SetPanning;
	public string? SetBank;
	public string? SetProgram;
	[ArrayMemberNaming(Prefix = "SF", IndexFormat = "X", Length = 16)]
	public string?[] SFx = new string?[16];
	[ArrayMemberNaming(Prefix = "Z", IndexFormat = "X2", Length = 128)]
	public string?[] Zxx = new string?[128];

	public static int SerializedSize => (10 + 16 + 128) * Constants.MaxMIDIMacro;

	public static MIDIConfiguration GetDefault()
	{
		var ret = new MIDIConfiguration();

		ret.Start = "FF";
		ret.Stop = "FC";
		ret.Tick = "";
		ret.NoteOn = "9c n v";
		ret.NoteOff = "9c n 0";
		ret.SetVolume = "";
		ret.SetPanning = "";
		ret.SetBank = "";
		ret.SetProgram = "Cc p";

		ret.SFx[0] = "F0F000z";
		for (int i = 1; i < ret.SFx.Length; i++)
			ret.SFx[i] = "";

		for (int i = 0; i < ret.Zxx.Length; i++)
		{
			if (i < 16)
				ret.Zxx[i] = "F0F001" + (i * 8).ToString("x2");
			else
				ret.Zxx[i] = "";
		}

		return ret;
	}

	public MIDIConfiguration Clone()
	{
		var ret = new MIDIConfiguration();

		foreach (var field in typeof(MIDIConfiguration).GetFields(BindingFlags.Public | BindingFlags.Instance))
		{
			var value = field.GetValue(this);

			if (value is Array array)
				value = array.Clone();

			field.SetValue(ret, value);
		}

		return ret;
	}

	public void CopyFrom(MIDIConfiguration other)
	{
		foreach (var field in typeof(MIDIConfiguration).GetFields(BindingFlags.Public | BindingFlags.Instance))
		{
			var value = field.GetValue(other);

			if (value is Array array)
				value = array.Clone();

			field.SetValue(this, value);
		}
	}
}
