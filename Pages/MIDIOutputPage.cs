using System.Linq;

namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.Input;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class MIDIOutputPage : Page
{
	TextEntryWidget textEntryMIDIStart;
	TextEntryWidget textEntryMIDIStop;
	TextEntryWidget textEntryMIDITick;
	TextEntryWidget textEntryNoteOn;
	TextEntryWidget textEntryNoteOff;
	TextEntryWidget textEntrySetVolume;
	TextEntryWidget textEntrySetPanning;
	TextEntryWidget textEntrySetBank;
	TextEntryWidget textEntrySetProgram;

	TextEntryWidget[] textEntryMacroSFx;
	TextEntryWidget[] textEntryMacroZxx;

	const int ZxxVisibleLines = 8;

	MIDIConfiguration _editConfig = MIDIConfiguration.GetDefault();
	int _zxxTop;

	public MIDIOutputPage()
		: base(PageNumbers.MIDIOutput, "MIDI Output Configuration", HelpTexts.MIDIOutput)
	{
		textEntryMIDIStart = new TextEntryWidget(new Point(17, 13), 43, _editConfig.Start ?? "", Constants.MaxMIDIMacro - 1);
		textEntryMIDIStop = new TextEntryWidget(new Point(17, 14), 43, _editConfig.Stop ?? "", Constants.MaxMIDIMacro - 1);
		textEntryMIDITick = new TextEntryWidget(new Point(17, 15), 43, _editConfig.Tick ?? "", Constants.MaxMIDIMacro - 1);
		textEntryNoteOn = new TextEntryWidget(new Point(17, 16), 43, _editConfig.NoteOn ?? "", Constants.MaxMIDIMacro - 1);
		textEntryNoteOff = new TextEntryWidget(new Point(17, 17), 43, _editConfig.NoteOff ?? "", Constants.MaxMIDIMacro - 1);
		textEntrySetVolume = new TextEntryWidget(new Point(17, 18), 43, _editConfig.SetVolume ?? "", Constants.MaxMIDIMacro - 1);
		textEntrySetPanning = new TextEntryWidget(new Point(17, 19), 43, _editConfig.SetPanning ?? "", Constants.MaxMIDIMacro - 1);
		textEntrySetBank = new TextEntryWidget(new Point(17, 20), 43, _editConfig.SetBank ?? "", Constants.MaxMIDIMacro - 1);
		textEntrySetProgram = new TextEntryWidget(new Point(17, 21), 43, _editConfig.SetProgram ?? "", Constants.MaxMIDIMacro - 1);

		textEntryMacroSFx = new TextEntryWidget[_editConfig.SFx.Length];

		for (int i = 0; i < _editConfig.SFx.Length; i++)
			textEntryMacroSFx[i] = new TextEntryWidget(new Point(17, 24 + i), 43, _editConfig.SFx[i] ?? "", Constants.MaxMIDIMacro - 1);

		textEntryMacroZxx = new TextEntryWidget[ZxxVisibleLines];

		for (int i = 0; i < ZxxVisibleLines; i++)
			textEntryMacroZxx[i] = new TextEntryWidget(new Point(17, 42 + i), 43, _editConfig.Zxx[i] ?? "", Constants.MaxMIDIMacro - 1);

		Widgets.Add(textEntryMIDIStart);
		Widgets.Add(textEntryMIDIStop);
		Widgets.Add(textEntryMIDITick);
		Widgets.Add(textEntryNoteOn);
		Widgets.Add(textEntryNoteOff);
		Widgets.Add(textEntrySetVolume);
		Widgets.Add(textEntrySetPanning);
		Widgets.Add(textEntrySetBank);
		Widgets.Add(textEntrySetProgram);
		Widgets.AddRange(textEntryMacroSFx);
		Widgets.AddRange(textEntryMacroZxx);

		Widgets.ForEach(w => w.Changed += CopyOut);
	}

	static readonly string[] SFx = { "SF0", "SF1", "SF2", "SF3", "SF4", "SF5", "SF6", "SF7", "SF8", "SF9", "SFA", "SFB", "SFC", "SFD", "SFE", "SFF" };
	static readonly string[] Z8x = { "Z80", "Z81", "Z82", "Z83", "Z84", "Z85", "Z86", "Z87" };

	public override void DrawConst()
	{
		VGAMem.DrawText("MIDI Start", new Point(6, 13), (0, 2));
		VGAMem.DrawText("MIDI Stop", new Point(7, 14), (0, 2));
		VGAMem.DrawText("MIDI Tick", new Point(7, 15), (0, 2));
		VGAMem.DrawText("Note On", new Point(9, 16), (0, 2));
		VGAMem.DrawText("Note Off", new Point(8, 17), (0, 2));
		VGAMem.DrawText("Change Volume", new Point(3, 18), (0, 2));
		VGAMem.DrawText("Change Pan", new Point(6, 19), (0, 2));
		VGAMem.DrawText("Bank Select", new Point(5, 20), (0, 2));
		VGAMem.DrawText("Program Change", new Point(2, 21), (0, 2));

		VGAMem.DrawText("Macro   SF0", new Point(5, 24), (0, 2));
		VGAMem.DrawText("Setup   SF1", new Point(5, 25), (0, 2));

		for (int i = 2; i < 16; i++)
			VGAMem.DrawText(SFx[i], new Point(13, i + 24), (0, 2));

		VGAMem.DrawBox(new Point(16, 12), new Point(60, 22), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(16, 23), new Point(60, 40), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(16, 41), new Point(60, 49), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		for (int i = 0; i < 7; i++)
			VGAMem.DrawText(Z8x[i], new Point(13, i + 42), (0, 2));
	}

	void CopyOut()
	{
		lock (AudioPlayback.LockScope())
			Song.CurrentSong.MIDIConfig = _editConfig.Clone();
	}

	void CopyIn()
	{
		lock (AudioPlayback.LockScope())
			_editConfig = Song.CurrentSong.MIDIConfig.Clone();
	}

	void ZxxSetPosition(int pos)
	{
		/* 128 items, scrolled on 7 lines */
		pos = pos.Clamp(0, 128 - 7);

		if (_zxxTop == pos)
			return;

		_zxxTop = pos;

		for (int i = 0; i < 7; i++)
			textEntryMacroZxx[i].Text = _editConfig.Zxx[_zxxTop + i] ?? "";

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public override bool? PreHandleKey(KeyEvent k)
	{
		if ((SelectedWidget == textEntryMacroZxx.First()) && k.Sym == KeySym.Up)
		{
			/* scroll up */
			if (k.State == KeyState.Release)
				return true;
			if (_zxxTop == 0)
				return false; /* let the normal key handler catch it and change focus */
			ZxxSetPosition(_zxxTop - 1);
			return true;
		}

		if ((SelectedWidget == textEntryMacroZxx.Last()) && k.Sym == KeySym.Down)
		{
			/* scroll down */
			if (k.State == KeyState.Release)
				return true;
			ZxxSetPosition(_zxxTop + 1);
			return true;
		}

		if (textEntryMacroZxx.Contains(SelectedWidget))
		{
			switch (k.Sym)
			{
				case KeySym.PageUp:
					if (k.State == KeyState.Release)
						return true;
					ZxxSetPosition(_zxxTop - 7);
					return true;
				case KeySym.PageDown:
					if (k.State == KeyState.Release)
						return true;
					ZxxSetPosition(_zxxTop + 7);
					return true;
				default:
					break;
			}
		}

		return false;
	}

	public override void SetPage()
	{
		CopyIn();
	}
}
