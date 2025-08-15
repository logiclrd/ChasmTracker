using System;

namespace ChasmTracker.Pages;

using System.Reflection.Metadata;
using System.Text;
using ChasmTracker.Configurations;
using ChasmTracker.Input;
using ChasmTracker.MIDI;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class MIDIPage : Page
{
	OtherWidget otherPortList;
	ToggleWidget toggleTickQuantize;
	ToggleWidget toggleBaseProgram1;
	ToggleWidget toggleRecordNoteOff;
	ToggleWidget toggleRecordVelocity;
	ToggleWidget toggleRecordAftertouch;
	ToggleWidget toggleCutNoteOff;
	ThumbBarWidget thumbBarAmplification;
	ThumbBarWidget thumbBarC5NoteValue;
	ToggleWidget toggleOutputMIDIPitch;
	ThumbBarWidget thumbBarPitchWheelDepth;
	ToggleWidget toggleEmbedMIDIConfig;
	ThumbBarWidget thumbBarIPMIDIPorts;
	ButtonWidget buttonMIDIOutputConfiguration;
	ButtonWidget buttonSave;

	int _topMIDIPort = 0;
	int _currentPort = 0;
	DateTime _lastMIDIPoll = default;

	public MIDIPage()
		: base(PageNumbers.MIDI, "MIDI Screen (Shift-F1)", HelpTexts.Global)
	{
		otherPortList = new OtherWidget(new Point(2, 14), new Size(75, 15));
		otherPortList.OtherHandleKey += otherPortList_HandleKey;
		otherPortList.OtherRedraw += otherPortList_Redraw;

		toggleTickQuantize = new ToggleWidget(new Point(20, 30));
		toggleBaseProgram1 = new ToggleWidget(new Point(20, 31));
		toggleRecordNoteOff = new ToggleWidget(new Point(20, 32));
		toggleRecordVelocity = new ToggleWidget(new Point(20, 33));
		toggleRecordAftertouch = new ToggleWidget(new Point(20, 34));
		toggleCutNoteOff = new ToggleWidget(new Point(20, 35));
		thumbBarAmplification = new ThumbBarWidget(new Point(53, 30), 20, 0, 200);
		thumbBarC5NoteValue = new ThumbBarWidget(new Point(53, 31), 20, 0, 127);
		toggleOutputMIDIPitch = new ToggleWidget(new Point(53, 34));
		thumbBarPitchWheelDepth = new ThumbBarWidget(new Point(53, 35), 20, 0, 48);
		toggleEmbedMIDIConfig = new ToggleWidget(new Point(53, 38));
		thumbBarIPMIDIPorts = new ThumbBarWidget(new Point(53, 41), 20, 0, 128);
		buttonMIDIOutputConfiguration = new ButtonWidget(new Point(2, 41), 27,
			"MIDI Output Configuration", 2);
		buttonSave = new ButtonWidget(new Point(2, 44), 27,
			"Save Output Configuration", 2);

		toggleTickQuantize.Changed += UpdateMIDIValues;
		toggleBaseProgram1.Changed += UpdateMIDIValues;
		toggleRecordNoteOff.Changed += UpdateMIDIValues;
		toggleRecordVelocity.Changed += UpdateMIDIValues;
		toggleRecordAftertouch.Changed += UpdateMIDIValues;
		toggleCutNoteOff.Changed += UpdateMIDIValues;
		thumbBarAmplification.Changed += UpdateMIDIValues;
		thumbBarC5NoteValue.Changed += UpdateMIDIValues;
		toggleOutputMIDIPitch.Changed += UpdateMIDIValues;
		thumbBarPitchWheelDepth.Changed += UpdateMIDIValues;
		toggleEmbedMIDIConfig.Changed += UpdateMIDIValues;

		thumbBarIPMIDIPorts.Changed += UpdateIPPorts;

		buttonMIDIOutputConfiguration.Clicked += ShowMIDIOutputConfig;
		buttonSave.Clicked += () => Configuration.Save();
	}

	public override void DrawConst()
	{
		VGAMem.DrawText(       "Tick quantize", new Point(6, 30), (0, 2));
		VGAMem.DrawText(      "Base Program 1", new Point(5, 31), (0, 2));
		VGAMem.DrawText(     "Record Note-Off", new Point(4, 32), (0, 2));
		VGAMem.DrawText(     "Record Velocity", new Point(4, 33), (0, 2));
		VGAMem.DrawText(   "Record Aftertouch", new Point(2, 34), (0, 2));
		VGAMem.DrawText(        "Cut note off", new Point(7, 35), (0, 2));

		VGAMem.DrawFillCharacters(new Point(23, 30), new Point(24, 35), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(19, 29), new Point(25, 36), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawBox(new Point(52, 29), new Point(73, 32), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawFillCharacters(new Point(56, 34), new Point(72, 34), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(52, 33), new Point(73, 36), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawFillCharacters(new Point(56, 38), new Point(72, 38), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(52, 37), new Point(73, 39), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawText(    "Amplification", new Point(39, 30), (0, 2));
		VGAMem.DrawText(   "C-5 Note-value", new Point(38, 31), (0, 2));
		VGAMem.DrawText("Output MIDI pitch", new Point(35, 34), (0, 2));
		VGAMem.DrawText("Pitch wheel depth", new Point(35, 35), (0, 2));
		VGAMem.DrawText(  "Embed MIDI data", new Point(37, 38), (0, 2));

		VGAMem.DrawText(    "IP MIDI ports", new Point(39, 41), (0, 2));
		VGAMem.DrawBox(new Point(52, 40), new Point(73, 42), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
	}

	public override void SetPage()
	{
		GetMIDIConfiguration();
	}

	bool otherPortList_HandleKey(KeyEvent k)
	{
		int newPort = _currentPort;

		if (k.Mouse == MouseState.ScrollUp)
			newPort -= Constants.MouseScrollLines;
		else if (k.Mouse == MouseState.ScrollDown)
			newPort += Constants.MouseScrollLines;
		else if (k.Mouse != MouseState.None)
		{
			if (k.MousePosition.X >= 3 && k.MousePosition.X <= 11 && k.MousePosition.Y >= 15 && k.MousePosition.Y <= 27)
			{
				if (k.Mouse == MouseState.DoubleClick)
				{
					if (k.State == KeyState.Press)
						return false;
					TogglePort();
					return true;
				}
				newPort = _topMIDIPort + (k.MousePosition.Y - 15);
			}
			else
				return false;
		}

		switch (k.Sym)
		{
			case KeySym.Space:
				if (k.State == KeyState.Press)
					return true;
				TogglePort();
				return true;
			case KeySym.PageUp:
				newPort -= 13;
				break;
			case KeySym.PageDown:
				newPort += 13;
				break;
			case KeySym.Home:
				newPort = 0;
				break;
			case KeySym.End:
				newPort = MIDIEngine.GetPortCount() - 1;
				break;
			case KeySym.Up:
				newPort--;
				break;
			case KeySym.Down:
				newPort++;
				break;
			case KeySym.Tab:
				if (k.State == KeyState.Release)
					return true;
				ChangeFocusTo(toggleTickQuantize);
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			default:
				if (k.Mouse == MouseState.None) return false;
				break;
		}
		if (k.State == KeyState.Release)
			return false;

		if (newPort != _currentPort) {
			int sz = MIDIEngine.GetPortCount() - 1;
			newPort = newPort.Clamp(0, sz);

			_currentPort = newPort;
			if (_currentPort < _topMIDIPort)
				_topMIDIPort = _currentPort;

			int pos = _currentPort - _topMIDIPort;
			if (pos > 12) _topMIDIPort = _currentPort - 12;
			if (_topMIDIPort < 0) _topMIDIPort = 0;

			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return true;
	}

	void otherPortList_Redraw()
	{
		/* XXX this can become outdated with the midi code; it can
		* and will overflow */
		var now = DateTime.UtcNow;

		VGAMem.DrawFillCharacters(new Point(3, 15), new Point(76, 28), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawText("MIDI ports:", new Point(2, 13), (0, 2));
		VGAMem.DrawBox(new Point(2, 14), new Point(77, 28), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		if ((now - _lastMIDIPoll).TotalSeconds > 10.0)
		{
			_lastMIDIPoll = DateTime.UtcNow;
			MIDIEngine.PollPorts();
		}

		var ct = MIDIEngine.GetPortCount();

		/* make sure this stuff doesn't overflow! */
		if (ct > 13 && _topMIDIPort + 13 >= ct)
			_topMIDIPort = ct - 13;

		_currentPort = Math.Min(_currentPort, ct - 1);

		for (int i = 0; i < 13; i++) {
			VGAMem.DrawCharacter(168, new Point(12, i + 15), (2, 0));

			if (_topMIDIPort + i >= ct)
				continue; /* err */

			var p = MIDIEngine.GetPort(_topMIDIPort + i);
			int fg, bg;

			if (_currentPort == _topMIDIPort + i
					&& (SelectedActiveWidget is OtherWidget))
			{
				fg = 0;
				bg = 3;
			}
			else
			{
				fg = 5;
				bg = 0;
			}

			VGAMem.DrawTextLen(p.Name ?? "", 64, new Point(13, 15+i), (5, 0));

			if (Status.Flags.HasAllFlags(StatusFlags.MIDIEventChanged)
					&& (now - Status.LastMIDITick) < TimeSpan.FromMilliseconds(3000)
					&& (((Status.LastMIDIPort == null) && p.IO.HasAllFlags(MIDIIO.Output))
					|| p == Status.LastMIDIPort))
			{
				var buffer = new StringBuilder();

				/* 21 is approx 64/3 */
				for (int j = 0; j < 21 && j < Status.LastMIDILength; j++)
					buffer.Append(Status.LastMIDIEvent[j].ToString("X2")).Append(' ');

				VGAMem.DrawText(buffer.ToString(), new Point(77 - buffer.Length, 15+i),
					(Status.LastMIDIPort != null) ? (4, 0) : (10, 0));
			}

			string state;

			switch (p.IO) {
				case MIDIIO.None:                   state = "Disabled "; break;
				case MIDIIO.Input:                  state = "   Input "; break;
				case MIDIIO.Output:                 state = "  Output "; break;
				case MIDIIO.Input | MIDIIO.Output:  state = "  Duplex "; break;
				default:                            state = " Enabled?"; break;
			}
			VGAMem.DrawText(state, new Point(3, 15 + i), (fg, bg));
		}
	}

	/* --------------------------------------------------------------------- */

	void ShowMIDIOutputConfig()
	{
		SetPage(PageNumbers.MIDIOutput);
	}

	void UpdateIPPorts()
	{
		if ((thumbBarIPMIDIPorts.Value > 0) && Status.Flags.HasAllFlags(StatusFlags.NoNetwork))
		{
			Status.FlashText("Networking is disabled");
			thumbBarIPMIDIPorts.Value = 0;
		}
		else
			MIDIEngine.SetIPPortCount(thumbBarIPMIDIPorts.Value);

		_lastMIDIPoll = default;
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void UpdateMIDIValues()
	{
		MIDIEngine.Flags = default(MIDIFlags)
		| (toggleTickQuantize.State ? MIDIFlags.TickQuantize : default)
		| (toggleBaseProgram1.State ? MIDIFlags.BaseProgram1 : default)
		| (toggleRecordNoteOff.State ? MIDIFlags.RecordNoteOff : default)
		| (toggleRecordVelocity.State ? MIDIFlags.RecordVelocity : default)
		| (toggleRecordAftertouch.State ? MIDIFlags.RecordAftertouch : default)
		| (toggleCutNoteOff.State ? MIDIFlags.CutNoteOff : default)
		| (toggleOutputMIDIPitch.State ? MIDIFlags.PitchBend : default)
		;

		if (toggleEmbedMIDIConfig.State)
			Song.CurrentSong.Flags |= SongFlags.EmbedMIDIConfig;
		else
			Song.CurrentSong.Flags &= ~SongFlags.EmbedMIDIConfig;

		MIDIEngine.Amplification = thumbBarAmplification.Value;
		MIDIEngine.C5Note = thumbBarC5NoteValue.Value;
		MIDIEngine.PitchWheelDepth = thumbBarPitchWheelDepth.Value;
	}

	void GetMIDIConfiguration()
	{
		toggleTickQuantize.State = MIDIEngine.Flags.HasAllFlags(MIDIFlags.TickQuantize);
		toggleBaseProgram1.State = MIDIEngine.Flags.HasAllFlags(MIDIFlags.BaseProgram1);
		toggleRecordNoteOff.State = MIDIEngine.Flags.HasAllFlags(MIDIFlags.RecordNoteOff);
		toggleRecordVelocity.State = MIDIEngine.Flags.HasAllFlags(MIDIFlags.RecordVelocity);
		toggleRecordAftertouch.State = MIDIEngine.Flags.HasAllFlags(MIDIFlags.RecordAftertouch);
		toggleCutNoteOff.State = MIDIEngine.Flags.HasAllFlags(MIDIFlags.CutNoteOff);
		toggleOutputMIDIPitch.State = MIDIEngine.Flags.HasAllFlags(MIDIFlags.PitchBend);
		toggleEmbedMIDIConfig.State = Song.CurrentSong.Flags.HasAllFlags(SongFlags.EmbedMIDIConfig);

		thumbBarAmplification.Value = MIDIEngine.Amplification;
		thumbBarC5NoteValue.Value = MIDIEngine.C5Note;
		thumbBarPitchWheelDepth.Value = MIDIEngine.PitchWheelDepth;
		thumbBarIPMIDIPorts.Value = MIDIEngine.GetIPPortCount();
	}

	void TogglePort()
	{
		var p = MIDIEngine.GetPort(_currentPort);

		if (p != null)
		{
			Status.Flags |= StatusFlags.NeedUpdate;

			if (p.CanDisable && p.Disable())
				return;

			switch (p.IO)
			{
				case MIDIIO.None:
					if (p.IOCap.HasAllFlags(MIDIIO.Input)) p.IO = MIDIIO.Input;
					else if (p.IOCap.HasAllFlags(MIDIIO.Output)) p.IO = MIDIIO.Output;
					break;
				case MIDIIO.Input:
					if (p.IOCap.HasAllFlags(MIDIIO.Output)) p.IO = MIDIIO.Output;
					else p.IO = MIDIIO.None;
					break;
				case MIDIIO.Output:
					if (p.IOCap.HasAllFlags(MIDIIO.Input)) p.IO |= MIDIIO.Input;
					else p.IO = MIDIIO.None;
					break;
				case MIDIIO.Input | MIDIIO.Output:
					p.IO = MIDIIO.None;
					break;
			}

			p.Enable();
		}
	}
}
