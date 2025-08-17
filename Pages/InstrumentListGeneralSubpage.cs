using System;

namespace ChasmTracker.Pages;

using ChasmTracker.Input;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class InstrumentListGeneralSubpage : InstrumentListPage
{
	OtherWidget otherNoteTranslationTable;
	ToggleButtonWidget toggleButtonNewNoteActionNoteCut;
	ToggleButtonWidget toggleButtonNewNoteActionContinue;
	ToggleButtonWidget toggleButtonNewNoteActionNoteOff;
	ToggleButtonWidget toggleButtonNewNoteActionNoteFade;
	ToggleButtonWidget toggleButtonDuplicateCheckTypeDisabled;
	ToggleButtonWidget toggleButtonDuplicateCheckTypeNote;
	ToggleButtonWidget toggleButtonDuplicateCheckTypeSample;
	ToggleButtonWidget toggleButtonDuplicateCheckTypeInstrument;
	ToggleButtonWidget toggleButtonDuplicateCheckActionNoteCut;
	ToggleButtonWidget toggleButtonDuplicateCheckActionNoteOff;
	ToggleButtonWidget toggleButtonDuplicateCheckActionNoteFade;
	TextEntryWidget textEntryFileName;

	int _noteTransTopLine = 0;
	int _noteTransSelectedLine = 0;

	int _noteTransCursorPos = 0;

	/* toggled when pressing "," on the note table's sample field
	 * more of a boolean than a bit mask  -delt.
	 */
	static bool _noteSampleMask = true;

	public InstrumentListGeneralSubpage()
		: base(PageNumbers.InstrumentListGeneral)
	{
		otherNoteTranslationTable = new OtherWidget(new Point(32, 16), new Size(9, 31));
		otherNoteTranslationTable.OtherHandleKey += otherNoteTranslationTable_HandleKey;
		otherNoteTranslationTable.OtherRedraw += otherNoteTranslationTable_Redraw;

		/* special case stuff */
		toggleButtonSubpageGeneral.InitializeState(true);

		/* 6-9 = nna toggles */
		toggleButtonNewNoteActionNoteCut = new ToggleButtonWidget(new Point(46, 19), 29, "Note Cut", 2, NNAGroup);
		toggleButtonNewNoteActionContinue = new ToggleButtonWidget(new Point(46, 22), 29, "Continue", 2, NNAGroup);
		toggleButtonNewNoteActionNoteOff = new ToggleButtonWidget(new Point(46, 25), 29, "Note Off", 2, NNAGroup);
		toggleButtonNewNoteActionNoteFade = new ToggleButtonWidget(new Point(46, 28), 29, "Note Fade", 2, NNAGroup);

		toggleButtonDuplicateCheckTypeDisabled = new ToggleButtonWidget(new Point(46, 34), 12, "Disabled", 2, DCTGroup);
		toggleButtonDuplicateCheckTypeNote = new ToggleButtonWidget(new Point(46, 37), 12, "Note", 2, DCTGroup);
		toggleButtonDuplicateCheckTypeSample = new ToggleButtonWidget(new Point(46, 40), 12, "Sample", 2, DCTGroup);
		toggleButtonDuplicateCheckTypeInstrument = new ToggleButtonWidget(new Point(46, 43), 12, "Instrument", 2, DCTGroup);

		toggleButtonDuplicateCheckActionNoteCut = new ToggleButtonWidget(new Point(62, 34), 13, "Note Cut", 2, DCAGroup);
		toggleButtonDuplicateCheckActionNoteOff = new ToggleButtonWidget(new Point(62, 37), 13, "Note Off", 2, DCAGroup);
		toggleButtonDuplicateCheckActionNoteFade = new ToggleButtonWidget(new Point(62, 40), 13, "Note Fade", 2, DCAGroup);

		/* impulse tracker has a 17-char-wide box for the filename for
		* some reason, though it still limits the actual text to 12
		* characters. go figure... */
		textEntryFileName = new TextEntryWidget(new Point(56, 47), 13, "", 12);

		toggleButtonNewNoteActionNoteCut.Changed += UpdateValues;
		toggleButtonNewNoteActionContinue.Changed += UpdateValues;
		toggleButtonNewNoteActionNoteOff.Changed += UpdateValues;
		toggleButtonNewNoteActionNoteFade.Changed += UpdateValues;
		toggleButtonDuplicateCheckTypeDisabled.Changed += UpdateValues;
		toggleButtonDuplicateCheckTypeNote.Changed += UpdateValues;
		toggleButtonDuplicateCheckTypeSample.Changed += UpdateValues;
		toggleButtonDuplicateCheckTypeInstrument.Changed += UpdateValues;
		toggleButtonDuplicateCheckActionNoteCut.Changed += UpdateValues;
		toggleButtonDuplicateCheckActionNoteOff.Changed += UpdateValues;
		toggleButtonDuplicateCheckActionNoteFade.Changed += UpdateValues;
		textEntryFileName.Changed += UpdateFileName;

		AddWidget(otherNoteTranslationTable);
		AddWidget(toggleButtonNewNoteActionNoteCut);
		AddWidget(toggleButtonNewNoteActionContinue);
		AddWidget(toggleButtonNewNoteActionNoteOff);
		AddWidget(toggleButtonNewNoteActionNoteFade);
		AddWidget(toggleButtonDuplicateCheckTypeDisabled);
		AddWidget(toggleButtonDuplicateCheckTypeNote);
		AddWidget(toggleButtonDuplicateCheckTypeSample);
		AddWidget(toggleButtonDuplicateCheckTypeInstrument);
		AddWidget(toggleButtonDuplicateCheckActionNoteCut);
		AddWidget(toggleButtonDuplicateCheckActionNoteOff);
		AddWidget(toggleButtonDuplicateCheckActionNoteFade);
		AddWidget(textEntryFileName);
	}

	public override void SetPage()
	{
		base.SetPage();

		WidgetNext.Initialize(Widgets);
	}

	void NoteTransReposition()
	{
		if (_noteTransSelectedLine < _noteTransTopLine)
			_noteTransTopLine = _noteTransSelectedLine;
		else if (_noteTransSelectedLine > _noteTransTopLine + 31)
			_noteTransTopLine = _noteTransSelectedLine - 31;
	}

	void otherNoteTranslationTable_Redraw()
	{
		bool isSelected = SelectedActiveWidget == otherNoteTranslationTable;
		int selBg = isSelected ? 14 : 0;

		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		for (int pos = 0, n = _noteTransTopLine; pos < 32; pos++, n++)
		{
			int bg = (n == _noteTransSelectedLine) ? selBg : 0;

			/* invalid notes are translated to themselves (and yes, this edits the actual instrument) */
			if (ins.NoteMap[n] < 1 || ins.NoteMap[n] > 120)
				ins.NoteMap[n] = (byte)(n + 1);

			string buf = SongNote.GetNoteString(ins.NoteMap[n]);

			VGAMem.DrawText(SongNote.GetNoteString(n + 1), new Point(32, 16 + pos), (2, bg));
			VGAMem.DrawCharacter(168, new Point(35, 16 + pos), (2, bg));
			VGAMem.DrawText(buf, new Point(36, 16 + pos), (2, bg));
			if (isSelected && n == _noteTransSelectedLine)
			{
				if (_noteTransCursorPos == 0)
					VGAMem.DrawCharacter(buf[0], new Point(36, 16 + pos), (0, 3));
				else if (_noteTransCursorPos == 1)
					VGAMem.DrawCharacter(buf[2], new Point(38, 16 + pos), (0, 3));
			}
			VGAMem.DrawCharacter(0, new Point(39, 16 + pos), (2, bg));

			if (ins.SampleMap[n] != 0)
				buf = ins.SampleMap[n].ToString99();
			else
				buf = "\xAD\xAD";

			VGAMem.DrawText(buf, new Point(40, 16 + pos), (2, bg));
			if (isSelected && n == _noteTransSelectedLine)
			{
				if (_noteTransCursorPos == 2)
					VGAMem.DrawCharacter(buf[0], new Point(40, 16 + pos), (0, 3));
				else if (_noteTransCursorPos == 3)
					VGAMem.DrawCharacter(buf[1], new Point(41, 16 + pos), (0, 3));
			}
		}

		/* draw the little mask thingy at the bottom. Could optimize this....  -delt.
			Sure can! This could share the same track-view functions that the
			pattern editor ought to be using. -Storlek */
		if (isSelected && !Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
		{
			switch (_noteTransCursorPos)
			{
				case 0:
					VGAMem.DrawCharacter(171, new Point(36, 48), (3, 2));
					VGAMem.DrawCharacter(171, new Point(37, 48), (3, 2));
					VGAMem.DrawCharacter(169, new Point(38, 48), (3, 2));

					if (_noteSampleMask)
					{
						VGAMem.DrawCharacter(169, new Point(40, 48), (3, 2));
						VGAMem.DrawCharacter(169, new Point(41, 48), (3, 2));
					}
					break;
				case 1:
					VGAMem.DrawCharacter(169, new Point(38, 48), (3, 2));

					if (_noteSampleMask)
					{
						VGAMem.DrawCharacter(170, new Point(40, 48), (3, 2));
						VGAMem.DrawCharacter(170, new Point(41, 48), (3, 2));
					}
					break;
				case 2:
				case 3:
					VGAMem.DrawCharacter(_noteSampleMask ? (byte)171 : (byte)169, new Point(40, 48), (3, 2));
					VGAMem.DrawCharacter(_noteSampleMask ? (byte)171 : (byte)169, new Point(41, 48), (3, 2));
					break;
			}
		}
	}

	void InstrumentNoteTransTranspose(SongInstrument ins, int dir)
	{
		for (int i = 0; i < 120; i++)
			ins.NoteMap[i] = (byte)(ins.NoteMap[i] + dir).Clamp(1, 120);
	}

	void InstrumentNoteTransInsert(SongInstrument ins, int pos)
	{
		for (int i = 119; i > pos; i--)
		{
			ins.NoteMap[i] = ins.NoteMap[i - 1];
			ins.SampleMap[i] = ins.SampleMap[i - 1];
		}

		if (pos > 0)
			ins.NoteMap[pos] = (byte)(ins.NoteMap[pos - 1] + 1); // Range fixed in _Redraw
		else
			ins.NoteMap[0] = 1;
	}

	void InstrumentNoteTransDelete(SongInstrument ins, int pos)
	{
		for (int i = pos; i < 120; i++)
		{
			ins.NoteMap[i] = ins.NoteMap[i + 1];
			ins.SampleMap[i] = ins.SampleMap[i + 1];
		}

		ins.NoteMap[119] = (byte)(ins.NoteMap[118] + 1);
	}

	bool otherNoteTranslationTable_HandleKey(KeyEvent k)
	{
		int prevLine = _noteTransSelectedLine;
		int newLine = prevLine;
		int prevPos = _noteTransCursorPos;
		int newPos = prevPos;
		SongInstrument ins = Song.CurrentSong.GetInstrument(CurrentInstrument);
		int c, n;

		if (k.Mouse == MouseState.Click && k.MouseButton == MouseButton.Middle)
		{
			if (k.State == KeyState.Release)
				Status.Flags |= StatusFlags.ClippyPasteSelection;
			return true;
		}
		else if (k.Mouse == MouseState.ScrollUp || k.Mouse == MouseState.ScrollDown)
		{
			if (k.State == KeyState.Press)
			{
				_noteTransTopLine += (k.Mouse == MouseState.ScrollUp) ? -3 : 3;
				_noteTransTopLine = _noteTransTopLine.Clamp(0, 119 - 31);
				Status.Flags |= StatusFlags.NeedUpdate;
			}
			return true;
		}
		else if (k.Mouse != MouseState.None)
		{
			if (otherNoteTranslationTable.ContainsPoint(k.MousePosition))
			{
				newLine = _noteTransTopLine + k.MousePosition.Y - 16;
				if (newLine == prevLine)
				{
					switch (k.MousePosition.X - 36)
					{
						case 2:
							newPos = 1;
							break;
						case 4:
							newPos = 2;
							break;
						case 5:
							newPos = 3;
							break;
						default:
							newPos = 0;
							break;
					}
					;
				}
			}
		}
		else if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
		{
			if (k.State == KeyState.Release)
				return false;
			switch (k.Sym)
			{
				case KeySym.Up:
					InstrumentNoteTransTranspose(ins, 1);
					break;
				case KeySym.Down:
					InstrumentNoteTransTranspose(ins, -1);
					break;
				case KeySym.Insert:
					InstrumentNoteTransInsert(ins, _noteTransSelectedLine);
					break;
				case KeySym.Delete:
					InstrumentNoteTransDelete(ins, _noteTransSelectedLine);
					break;
				case KeySym.n:
					n = _noteTransSelectedLine - 1; // the line to copy *from*
					if (n < 0 || ins.NoteMap[n] == SpecialNotes.Last)
						break;
					ins.NoteMap[_noteTransSelectedLine] = (byte)(ins.NoteMap[n] + 1);
					ins.SampleMap[_noteTransSelectedLine] = ins.SampleMap[n];
					newLine++;
					break;
				case KeySym.p:
					n = _noteTransSelectedLine + 1; // the line to copy *from*
					if (n > (SpecialNotes.Last - SpecialNotes.First) || ins.NoteMap[n] == SpecialNotes.First)
						break;
					ins.NoteMap[_noteTransSelectedLine] = (byte)(ins.NoteMap[n] - 1);
					ins.SampleMap[_noteTransSelectedLine] = ins.SampleMap[n];
					newLine--;
					break;
				case KeySym.a:
					c = AllPages.SampleList.CurrentSample;
					for (n = 0; n < (SpecialNotes.Last - SpecialNotes.First + 1); n++)
						ins.SampleMap[n] = (byte)c;
					if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					{
						// Copy the name too.
						ins.Name = Song.CurrentSong.Samples[c]?.Name ?? "";
					}
					break;
				default:
					return false;
			}
		}
		else
		{
			switch (k.Sym)
			{
				case KeySym.Up:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Control))
						AllPages.SampleList.CurrentSample--;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					if (--newLine < 0)
					{
						ChangeFocusTo(1);
						return true;
					}
					break;
				case KeySym.Down:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Control))
						AllPages.SampleList.CurrentSample++;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					newLine++;
					break;
				case KeySym.PageUp:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Control))
					{
						CurrentInstrument--;
						return true;
					}
					newLine -= 16;
					break;
				case KeySym.PageDown:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.Control))
					{
						CurrentInstrument++;
						return true;
					}
					newLine += 16;
					break;
				case KeySym.Home:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					newLine = 0;
					break;
				case KeySym.End:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					newLine = 119;
					break;
				case KeySym.Left:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					newPos--;
					break;
				case KeySym.Right:
					if (k.State == KeyState.Release)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					newPos++;
					break;
				case KeySym.Return:
					if (k.State == KeyState.Press)
						return false;
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						return false;
					AllPages.SampleList.CurrentSample = ins.SampleMap[_noteTransSelectedLine];
					otherInstrumentList.OtherAcceptsText = !(s_instrumentCursorPos == 25);
					return true;
				case KeySym.Less:
				case KeySym.Semicolon:
				case KeySym.Colon:
					if (k.State == KeyState.Release)
						return false;
					AllPages.SampleList.CurrentSample--;
					return true;
				case KeySym.Greater:
				case KeySym.Quote:
				case KeySym.QuoteDbl:
					if (k.State == KeyState.Release)
						return false;
					AllPages.SampleList.CurrentSample++;
					return true;

				default:
					if (k.State == KeyState.Release)
						return false;
					switch (_noteTransCursorPos)
					{
						case 0:        /* note */
							n = k.NoteValue;
							if (!SongNote.IsNote(n))
								return false;
							ins.NoteMap[_noteTransSelectedLine] = (byte)n;
							if (_noteSampleMask || Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
								ins.SampleMap[_noteTransSelectedLine] = (byte)AllPages.SampleList.CurrentSample;
							newLine++;
							break;
						case 1:        /* octave */
							c = k.HexValue;
							if (c < 0 || c > 9) return false;
							n = ins.NoteMap[_noteTransSelectedLine];
							n = ((n - 1) % 12) + (12 * c) + 1;
							ins.NoteMap[_noteTransSelectedLine] = (byte)n;
							newLine++;
							break;

						/* Made it possible to enter H to R letters
						on 1st digit for expanded sample slots.  -delt. */

						case 2:        /* instrument, first digit */
						case 3:        /* instrument, second digit */
							if (k.Sym == KeySym.Space)
							{
								ins.SampleMap[_noteTransSelectedLine] =
									(byte)AllPages.SampleList.CurrentSample;
								newLine++;
								break;
							}

							if ((k.Sym == KeySym.Period && !k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift)) || k.Sym == KeySym.Delete)
							{
								ins.SampleMap[_noteTransSelectedLine] = 0;
								newLine += (k.Sym == KeySym.Period) ? 1 : 0;
								break;
							}
							if (k.Sym == KeySym.Comma && !k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
							{
								_noteSampleMask = !_noteSampleMask;
								break;
							}

							n = ins.SampleMap[_noteTransSelectedLine];
							if (_noteTransCursorPos == 2)
							{
								c = k.Character99Value;
								if (c < 0) return false;
								n = (c * 10) + (n % 10);
								newPos++;
							}
							else
							{
								c = k.HexValue;
								if (c < 0 || c > 9) return false;
								n = ((n / 10) * 10) + c;
								newPos--;
								newLine++;
							}
							n = Math.Min(n, Song.CurrentSong.Samples.Count - 1);
							ins.SampleMap[_noteTransSelectedLine] = (byte)n;
							AllPages.SampleList.CurrentSample = n;
							break;
					}
					break;
			}
		}

		newLine = newLine.Clamp(0, 119);
		_noteTransCursorPos = newPos.Clamp(0, 3);

		if (newLine != prevLine)
		{
			_noteTransSelectedLine = newLine;
			NoteTransReposition();
		}

		/* this causes unneeded redraws in some cases... oh well :P */
		Status.Flags |= StatusFlags.NeedUpdate;
		return true;
	}

	public override void PredrawHook()
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		switch (ins.NewNoteAction)
		{
			case NewNoteActions.NoteCut: toggleButtonNewNoteActionNoteCut.SetState(true); break;
			case NewNoteActions.Continue: toggleButtonNewNoteActionContinue.SetState(true); break;
			case NewNoteActions.NoteOff: toggleButtonNewNoteActionNoteOff.SetState(true); break;
			case NewNoteActions.NoteFade: toggleButtonNewNoteActionNoteFade.SetState(true); break;
		}

		switch (ins.DuplicateCheckType)
		{
			case DuplicateCheckTypes.None: toggleButtonDuplicateCheckTypeDisabled.SetState(true); break;
			case DuplicateCheckTypes.Note: toggleButtonDuplicateCheckTypeNote.SetState(true); break;
			case DuplicateCheckTypes.Sample: toggleButtonDuplicateCheckTypeSample.SetState(true); break;
			case DuplicateCheckTypes.Instrument: toggleButtonDuplicateCheckTypeInstrument.SetState(true); break;
		}

		switch (ins.DuplicateCheckAction)
		{
			case DuplicateCheckActions.NoteCut: toggleButtonDuplicateCheckActionNoteCut.SetState(true); break;
			case DuplicateCheckActions.NoteOff: toggleButtonDuplicateCheckActionNoteOff.SetState(true); break;
			case DuplicateCheckActions.NoteFade: toggleButtonDuplicateCheckActionNoteFade.SetState(true); break;
		}

		textEntryFileName.Text = ins.FileName ?? "";
	}

	public override void DrawConst()
	{
		base.DrawConst();

		VGAMem.DrawBox(new Point(31, 15), new Point(42, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		/* Kind of a hack, and not really useful, but... :) */
		if (Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
		{
			VGAMem.DrawBox(new Point(55, 46), new Point(73, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
			VGAMem.DrawText("    ", new Point(69, 47), (1, 0));
		}
		else
			VGAMem.DrawBox(new Point(55, 46), new Point(69, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawText("New Note Action", new Point(54, 17), (0, 2));
		VGAMem.DrawText("Duplicate Check Type & Action", new Point(47, 32), (0, 2));
		VGAMem.DrawText("Filename", new Point(47, 47), (0, 2));

		for (int n = 0; n < 35; n++)
		{
			VGAMem.DrawCharacter(134, new Point(44 + n, 15), (0, 2));
			VGAMem.DrawCharacter(134, new Point(44 + n, 30), (0, 2));
			VGAMem.DrawCharacter(154, new Point(44 + n, 45), (0, 2));
		}
	}

	void UpdateValues()
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		Status.Flags |= StatusFlags.SongNeedsSave;

		if (toggleButtonNewNoteActionNoteCut.State)
			ins.NewNoteAction = NewNoteActions.NoteCut;
		else if (toggleButtonNewNoteActionContinue.State)
			ins.NewNoteAction = NewNoteActions.Continue;
		else if (toggleButtonNewNoteActionNoteOff.State)
			ins.NewNoteAction = NewNoteActions.NoteOff;
		else if (toggleButtonNewNoteActionNoteFade.State)
			ins.NewNoteAction = NewNoteActions.NoteFade;

		if (toggleButtonDuplicateCheckTypeDisabled.State)
			ins.DuplicateCheckType = DuplicateCheckTypes.None;
		else if (toggleButtonDuplicateCheckTypeNote.State)
			ins.DuplicateCheckType = DuplicateCheckTypes.Note;
		else if (toggleButtonDuplicateCheckTypeSample.State)
			ins.DuplicateCheckType = DuplicateCheckTypes.Sample;
		else if (toggleButtonDuplicateCheckTypeInstrument.State)
			ins.DuplicateCheckType = DuplicateCheckTypes.Instrument;

		if (toggleButtonDuplicateCheckActionNoteCut.State)
			ins.DuplicateCheckAction = DuplicateCheckActions.NoteCut;
		else if (toggleButtonDuplicateCheckActionNoteOff.State)
			ins.DuplicateCheckAction = DuplicateCheckActions.NoteOff;
		else if (toggleButtonDuplicateCheckActionNoteFade.State)
			ins.DuplicateCheckAction = DuplicateCheckActions.NoteFade;
	}

	void UpdateFileName()
	{
		Status.Flags |= StatusFlags.SongNeedsSave;
	}
}
