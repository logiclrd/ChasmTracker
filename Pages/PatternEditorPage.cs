using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ChasmTracker;

using ChasmTracker.Clipboard;
using ChasmTracker.Dialogs;
using ChasmTracker.Memory;
using ChasmTracker.Pages;
using ChasmTracker.Pages.TrackViews;
using ChasmTracker.Songs;
using ChasmTracker.VGA;

/* The all-important pattern editor. The code here is a general mess, so
 * don't look at it directly or, uh, you'll go blind or something. */

public class PatternEditorPage : Page
{
	public PatternEditorPage()
		: base(PageNumbers.PatternEditor, "Pattern Editor (F2)", HelpTexts.PatternEditor)
	{
		MemoryUsage.RegisterMemoryPressure("Clipboard", ClipboardMemoryUsage);
		MemoryUsage.RegisterMemoryPressure("History", HistoryMemoryUsage);

		PasteHandlers =
			new Func<string, PatternSnap, bool>[]
			{
				PatternSelectionSystemPasteModPlug
			};
	}

	bool RowIsMajor(int r) => (Song.CurrentSong.RowHighlightMajor != 0) && ((r % Song.CurrentSong.RowHighlightMajor) == 0);
	bool RowIsMinor(int r) => (Song.CurrentSong.RowHighlightMinor != 0) && ((r % Song.CurrentSong.RowHighlightMinor) == 0);
	bool RowIsHighlight(int r) => RowIsMinor(r) || RowIsMajor(r);

	bool SongPlaying() => Song.Mode.HasAnyFlag(SongMode.Playing | SongMode.PatternLoop);

	/* this is actually used by pattern-view.c */
	//int show_default_volumes = 0;

	/* --------------------------------------------------------------------- */
	/* The (way too many) static variables */

	//static int midi_start_record = 0;

	enum TemplateMode
	{
		[Description("")]
		Off = 0,
		[Description("Template, Overwrite")]
		Overwrite,
		[Description("Template, Mix - Pattern data precedence")]
		MixPatternPrecedence,
		[Description("Template, Mix - Clipboard data precedence")]
		MixClipboardPrecedence,
		[Description("Template, Notes only")]
		NotesOnly,
	};

	TemplateMode _templateMode = TemplateMode.Off;

	/* pattern display position */
	int _topDisplayChannel = 1;             /* one-based */
	int _topDisplayRow = 0;         /* zero-based */

	/* these three tell where the cursor is in the pattern */
	int _currentChannel = 1, _currentPosition = 0;
	int _currentRow = 0;

	bool _keyjazzNoteoff = false;      /* issue noteoffs when releasing note */
	bool _keyjazzWriteNoteoff = false; /* write noteoffs when releasing note */
	bool _keyjazzRepeat = true;        /* insert multiple notes on key repeat */
	bool _keyjazzCapslock = false;     /* keyjazz when capslock is on, not while it is down */

	/* this is, of course, what the current pattern is */
	int _currentPattern = 0;

	public int CurrentPattern
	{
		get => _currentPattern;
		set => _currentPattern = value;
	}

	public int CurrentRow
	{
		get => _currentRow;
		set => _currentRow = value;
	}

	int _skipValue = 1;                /* aka cursor step */

	public int SkipValue
	{
		get => _skipValue;
		set => _skipValue = value;
	}

	bool _linkEffectColumn = false;

	public bool LinkEffectColumn
	{
		get => _linkEffectColumn;
		set => _linkEffectColumn = value;
	}

	bool _drawDivisions = false;       /* = vertical lines between channels */

	int _shiftChordChannels = 0;       /* incremented for each shift-note played */

	int _centraliseCursor = 0;
	int _highlightCurrentRow = 0;

	bool _playbackTracing = false;     /* scroll lock */
	bool _midiPlaybackTracing = false;

	int _panningMode = 0;
	int[] _midiBendHit = new int[64];
	int[] _midiLastBendHit = new int[64];

	/* these should fix the playback tracing position discrepancy */
	int _playingRow = -1;
	int _playingPattern = -1;

	/* the current editing mask (what stuff is copied) */
	PatternEditorMask _editCopyMask = PatternEditorMask.Note | PatternEditorMask.Instrument | PatternEditorMask.Volume;

	/* and the mask note. note that the instrument field actually isn't used */
	SongNote _maskNote = SongNote.BareNote(61); /* C-5 */

	/* playback mark (ctrl-f7) */
	int _markedPattern = -1;
	int _markedRow;

	/* volume stuff (alt-i, alt-j, ctrl-j) */
	int _volumePercent = 100;
	int _varyDepth = 10;
	int _fastVolumePercent = 67;
	bool _fastVolumeMode = false;        /* toggled with ctrl-j */

	CopySearchMode _maskCopySearchMode = CopySearchMode.Off;

	/* If nonzero, home/end will move to the first/last row in the current channel
	prior to moving to the first/last channel, i.e. operating in a 'z' pattern.
	This is closer to FT2's behavior for the keys. */
	bool _invertHomeEnd = false;

	/* --------------------------------------------------------------------- */
	/* undo and clipboard handling */
	PatternSnap _fastSave =
		new PatternSnap()
		{
			SnapOp = "Fast Pattern Save",
			PatternNumber = -1,
		};

	PatternSnap _clipboard =
		new PatternSnap()
		{
			SnapOp = "Clipboard",
			PatternNumber = -1,
		};

	List<PatternSnap> _undoHistory = new List<PatternSnap>();

	int ClipboardMemoryUsage()
	{
		int usage = 0;

		if (_clipboard.Data != null)
			usage += _clipboard.Rows;
		if (_fastSave.Data != null)
			usage += _fastSave.Rows;

		return usage;
	}

	int HistoryMemoryUsage()
	{
		int usage = 0;

		foreach (var entry in _undoHistory)
			if (entry.Data != null)
				usage += entry.Rows;

		return usage;
	}

	/* --------------------------------------------------------------------- */
	/* block selection handling */

	struct SelectionRange
	{
		public int FirstChannel;
		public int LastChannel;
		public int FirstRow;
		public int LastRow;

		/* if FirstChannel is zero, there's no selection, as the channel
		 * numbers start with one. (same deal for LastChannel, but i'm only
		 * caring about one of them to be efficient.) */
		public bool Exists => FirstChannel != 0;
	}

	SelectionRange _selection = new SelectionRange();

	struct ShiftSelectionData
	{
		public bool InProgress;
		public int FirstChannel;
		public int FirstRow;
	}

	ShiftSelectionData _shiftSelection = new ShiftSelectionData();

	/* this is set to 1 on the first alt-d selection,
	 * and shifted left on each successive press. */
	int _blockDoubleSize;

	bool SelectionExists()
		=> _selection.FirstChannel > 0;

	void ShowNoSelectionError()
	{
		Dialog.Show(new PatternEditorNoSelectionErrorDialog());
	}

	/* --------------------------------------------------------------------- */
	/* this is for the multiple track views stuff. */

	TrackView[] _trackViews =
		new TrackView[]
		{
			new TrackView13(),                 /* 5 channels */
			new TrackView10(),                 /* 6/7 channels */
			new TrackView7(),                  /* 9/10 channels */
			new TrackView6(),                  /* 10/12 channels */
			new TrackView3(),                  /* 18/24 channels */
			new TrackView2(),                  /* 24/36 channels */
			new TrackView1(),                  /* 36/64 channels */
		};

	int[] _trackViewScheme = new int[64];
	bool _channelMultiEnabled = false;
	bool[] _channelMulti = new bool[64];
	int _visibleChannels;
	int _visibleWidth;

	/* This probably doesn't belong here, but whatever */

	int MultiChannelGetNext(int curChannel)
	{
		int i;

		curChannel--; /* make it zero-based. oh look, it's a hammer. */
		i = curChannel;

		if (_channelMulti[curChannel]) {
			/* we're in a multichan-enabled channel, so look for the next one */
			do
			{
				i = (i + 1) & 63; /* no? next channel, and loop back to zero if we hit 64 */
				if (_channelMulti[i]) /* is this a multi-channel? */
					break; /* it is! */
			} while (i != curChannel);

			/* at this point we've either broken the loop because the channel i is multichan,
				or the condition failed because we're back where we started */
		}
		/* status_text_flash ("Newly selected channel is %d", (int) i + 1); */
		return i + 1; /* make it one-based again */
	}

	int MultiChannelGetPrevious(int curChannel)
	{
		int i;

		curChannel--; /* once again, .... */
		i = curChannel;

		if (_channelMulti[curChannel])
		{
			do
			{
				i = (i - 1) & 63; /* loop backwards this time */
				if (_channelMulti[i])
					break;
			} while (i != curChannel);
		}
		return i + 1;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* this global state is stupid and dumb */

	enum PatternCopyInBehaviour
	{
		Invalid = 0, /* reset to this after we're done copying */
		Insert,
		Overwrite,
		OverwriteGrow,
		MixNotes,
		MixFields
	};

	PatternCopyInBehaviour _patternCopyInBehaviour = PatternCopyInBehaviour.Invalid;

	bool PatternSelectionSystemPasteModPlug(string str, PatternSnap snap)
	{
		/* magic bytes and their respective effect mapping functions */
		Dictionary<string, Func<char, byte>> fxMaps = new Dictionary<string, Func<char, byte>>();

		fxMaps[" IT"] = Keyboard.GetEffectNumber;
		fxMaps["S3M"] = Keyboard.GetEffectNumber;
		fxMaps[" XM"] = Keyboard.GetPTMEffectNumber;
		fxMaps["MOD"] = Keyboard.GetPTMEffectNumber;

		int copyInX, copyInY;
		int copyInW, copyInH;
		Func<char, byte>? fxMap;
		int x, i;
		int scantmp;
		var ds = new List<SongNote>();

		/* WTF */
		for (x = 0; (x < str.Length) && (str[x] != '\n'); x++)
			;

		if (x <= 11)
			return false;

		if ((x >= str.Length) || (str[x + 1] != '|'))
			return false;

		if (str[x - 1] == '\r')
			x--;

		fxMap = fxMaps.TryGetValue(str.Substring(x - 3, 3), out var func) ? func : default;

		/* invalid or not implemented */
		if (fxMap == null)
			return false;

		if (str[x] == '\r')
			x++;

		str = str.Substring(x + 2);
		copyInW = copyInH = 0;
		copyInX = copyInY = 0;

		/* okay, let's start parsing */
		x = 0;

		while (x + 2 < str.Length)
		{
			SongNote n = default;

			if (x + 2 > str.Length)
				break;

			switch (str[x])
			{
				case 'C': case 'c': n.Note = 1; break;
				case 'D': case 'd': n.Note = 3; break;
				case 'E': case 'e': n.Note = 5; break;
				case 'F': case 'f': n.Note = 6; break;
				case 'G': case 'g': n.Note = 8; break;
				case 'A': case 'a': n.Note = 10; break;
				case 'B': case 'b': n.Note = 12; break;
				default: n.Note = 0; break;
			}

			if (n.Note != 0)
			{
				/* handle sharp/flat */
				if (str[x + 1] == '#') n.Note++;
				else if (str[x + 1] == 'b') n.Note--;
			}

			switch (str[x])
			{
				case '=': n.Note = SpecialNotes.NoteOff; break;
				case '^': n.Note = SpecialNotes.NoteCut; break;
				case '~': n.Note = SpecialNotes.NoteFade; break;
				case ' ':
				case '.': n.Note = SpecialNotes.None; break;
				default:
					n.Note += unchecked((byte)((str[x + 2] - '0') * 12));
					break;
			}

			x += 3;

			/* instrument number */
			n.Instrument = 0;
			if (char.IsDigit(str[x]) && char.IsDigit(str[x + 1]))
				n.Instrument = unchecked((byte)((str[x] - '0') * 10 + (str[x + 1] - '0')));

			x += 2;

			while ((x < str.Length) && (str[x] != '\0'))
			{
				/* supposedly there can be multiple effects? */
				if (str[x] == '|' || str[x] == '\r' || str[x] == '\n') break;
				if ((x + 2 >= str.Length) || (str[x] == '\0') || (str[x + 1] == '\0') || (str[2] == '\0')) break;
				if (str[x] >= 'a' && str[x] <= 'z')
				{
					n.VolumeParameter = 0;
					if (char.IsDigit(str[x]) && char.IsDigit(str[x + 1]))
						n.VolumeParameter = unchecked((byte)((str[x] - '0') * 10 + (str[x + 1] - '0')));

					switch (str[x])
					{
						case 'v': n.VolumeEffect = VolumeEffects.Volume; break;
						case 'p': n.VolumeEffect = VolumeEffects.Panning; break;
						case 'c': n.VolumeEffect = VolumeEffects.VolSlideUp; break;
						case 'd': n.VolumeEffect = VolumeEffects.VolSlideDown; break;
						case 'a': n.VolumeEffect = VolumeEffects.FineVolUp; break;
						case 'b': n.VolumeEffect = VolumeEffects.FineVolDown; break;
						case 'u': n.VolumeEffect = VolumeEffects.VibratoSpeed; break;
						case 'h': n.VolumeEffect = VolumeEffects.VibratoDepth; break;
						case 'l': n.VolumeEffect = VolumeEffects.PanSlideLeft; break;
						case 'r': n.VolumeEffect = VolumeEffects.PanSlideRight; break;
						case 'g': n.VolumeEffect = VolumeEffects.TonePortamento; break;
						case 'f': n.VolumeEffect = VolumeEffects.PortaUp; break;
						case 'e': n.VolumeEffect = VolumeEffects.PortaDown; break;
						default: n.VolumeEffect = VolumeEffects.None; n.VolumeParameter = 0; break;
					}
				}
				else
				{
					n.Effect = fxMap(str[x]);
					n.Parameter = 0;
					if (char.IsDigit(str[x + 1]) && char.IsDigit(str[x + 2]))
						n.Parameter = unchecked((byte)((str[x + 1] - '0') * 10 + (str[x + 2] - '0')));
				}
				x += 3;
			}

			if (copyInX < Constants.MaxChannels)
			{
				ds.Add(n);
				copyInX++;
			}

			/* FIXME: we shouldn't be getting carriage returns here anyway! */
			if (str[x] == '\r' || str[x] == '\n')
			{
				/* skip any extra newlines */
				while (str[x] == '\r' || str[x] == '\n')
					x++;

				/* ok */
				copyInW = Math.Max(copyInX, copyInW);
				copyInH++;

				for (; copyInX < Constants.MaxChannels; copyInX++)
					ds.Add(SongNote.Empty);

				/* reset */
				copyInX = 0;
			}

			if (str[x] != '|')
				break;

			x++;
		}

		/* free anything currently in the clipboard */
		ClipboardFree();

		/* now copy the data into the snap */
		SnapCopyFromPattern(new Pattern(ds, copyInW), snap, 0, 0, copyInW, copyInH);

		return true;
	}

	/* this is here so that we could possibly handle other clipboard formats. */
	readonly Func<string, PatternSnap, bool>[] PasteHandlers;

	int PatternSelectionSystemPaste(int cb, byte[] data)
	{
		/* this is a bug */
		if (_patternCopyInBehaviour != PatternCopyInBehaviour.Invalid)
			throw new Exception("by this point we should have a copyin behavior set");

		if (data != null)
		{
			/* if we actually got some data, send it to the parsers */
			string dataString = Encoding.ASCII.GetString(data);

			foreach (var handler in PasteHandlers)
				if (handler(dataString, _clipboard))
					break;
		}

		/* now, based on the behavior previously chosen: */

		switch (_patternCopyInBehaviour)
		{
			case PatternCopyInBehaviour.Insert:
				/* Alt-P */
				ClipboardPasteInsert();
				break;
			case PatternCopyInBehaviour.Overwrite:
				/* Alt-O */
				ClipboardPasteOverwrite(false, false);
				break;
			case PatternCopyInBehaviour.OverwriteGrow:
				/* 2*Alt-O */
				ClipboardPasteOverwrite(false, true);
				break;
			case PatternCopyInBehaviour.MixNotes:
				/* ??? */
				ClipboardPasteMixNotes(false, 0);
				break;
			case PatternCopyInBehaviour.MixFields:
				ClipboardPasteMixFields(false, 0);
				break;
		}

		/* reset */
		_patternCopyInBehaviour = PatternCopyInBehaviour.Invalid;

		return 1;
	}

	/* clipboard */
	void ClipboardFree()
	{
		_clipboard.Data = Array.Empty<SongNote>();
	}

	/* ClipboardCopy is fundementally the same as SelectionErase
	* except it uses memcpy instead of memset :) */
	void ClipboardCopy(bool honourMute)
	{
		bool flag;

		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

		ClipboardFree();

		SnapCopy(
			_clipboard,
			_selection.FirstChannel - 1,
			_selection.FirstRow,
			(_selection.LastChannel - _selection.FirstChannel) + 1,
			(_selection.LastRow - _selection.FirstRow) + 1);

		flag = false;
		if (honourMute)
			flag = SnapHonourMute(_clipboard, _selection.FirstChannel - 1);

		/* transfer to system where appropriate */
		Clippy.Yank();

		if (flag)
			Status.FlashText("Selection honours current mute settings");
	}

	void ClipboardPasteOverwrite(bool suppress, bool grow)
	{
		if (_clipboard.Data == null)
		{
			MessageBox.Show(DialogTypes.OK, "No data in clipboard");
			return;
		}

		var pattern = Song.CurrentSong?.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		int numRows = pattern.Rows.Count - _currentRow;

		if (_clipboard.Rows < numRows)
			numRows = _clipboard.Rows;

		if (_clipboard.Rows > numRows && grow)
		{
			if (_currentRow + _clipboard.Rows > 200)
			{
				Status.FlashText($"Resized pattern {_currentPattern}, but clipped to 200 rows");
				pattern.Resize(200);
			}
			else
			{
				int newSize = _currentRow + _clipboard.Rows;

				Status.FlashText($"Resized pattern {_currentPattern} to {newSize} rows");
				pattern.Resize(newSize);
			}
		}

		int chanWidth = _clipboard.Channels;

		if (_currentChannel + chanWidth > 64)
			chanWidth = 64 - _currentChannel + 1;

		if (!suppress)
		{
			HistoryAddGrouped(
				"Replace overwritten data       (Alt-O)",
				_currentChannel - 1, _currentRow,
				chanWidth, numRows);
		}

		SnapPaste(_clipboard, _currentChannel - 1, _currentRow, 0);
	}

	void ClipboardPasteInsert()
	{
		if (_clipboard.Data == null)
		{
			MessageBox.Show(DialogTypes.OK, "No data in clipboard");
			return;
		}

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		PatEdSave("Undo paste data                (Alt-P)");

		int numRows = pattern.Rows.Count - _currentRow;
		if (_clipboard.Rows < numRows)
			numRows = _clipboard.Rows;

		int chanWidth = _clipboard.Channels;
		if (chanWidth + _currentChannel > 64)
			chanWidth = 64 - _currentChannel + 1;

		PatternInsertRows(_currentRow, _clipboard.Rows, _currentChannel, chanWidth);
		ClipboardPasteOverwrite(true, false);
		PatternSelectionSystemCopyOut();
	}

	byte SongGetCurrentInstrument()
	{
		if ((Song.CurrentSong != null) && Song.CurrentSong.Flags.HasFlag(SongFlags.InstrumentMode))
			return (byte)AllPages.InstrumentListGeneral.CurrentInstrument;
		else
			return (byte)AllPages.SampleList.CurrentSample;
	}

	void ClipboardPasteMixNotes(bool clip, int xlate)
	{
		if (_clipboard.Data == null)
		{
			MessageBox.Show(DialogTypes.OK, "No data in clipboard");
			return;
		}

		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong?.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		int numRows = pattern.Rows.Count - _currentRow;

		if (_clipboard.Rows < numRows)
			numRows = _clipboard.Rows;

		int chanWidth = _clipboard.Channels;
		if (chanWidth + _currentChannel > 64)
			chanWidth = 64 - _currentChannel + 1;

		/* note that IT doesn't do this for "fields" either... */
		HistoryAddGrouped(
			"Replace mixed data             (Alt-M)",
			_currentChannel - 1, _currentRow,
			chanWidth, numRows);

		int c = 0;

		for (int row = 0; row < numRows; row++)
		{
			int pRow = _currentPattern + row;

			for (int chan = 0; chan < chanWidth; chan++)
			{
				int pChan = _currentChannel + chan;

				ref var pNote = ref pattern.Rows[pRow][pChan];

				if (pNote.IsBlank)
				{
					pNote = _clipboard[row, chan];
					pNote.SetNoteNote(
						_clipboard[row, chan].Note,
						xlate);

					if (clip)
					{
						pNote.Instrument = SongGetCurrentInstrument();

						if (_editCopyMask.HasFlag(PatternEditorMask.Volume))
						{
							pNote.VolumeEffect = _maskNote.VolumeEffect;
							pNote.VolumeParameter = _maskNote.VolumeParameter;
						}
						else
						{
							pNote.VolumeEffect = 0;
							pNote.VolumeParameter = 0;
						}

						if (_editCopyMask.HasFlag(PatternEditorMask.Effect))
						{
							pNote.Effect = _maskNote.Effect;
							pNote.Parameter = _maskNote.Parameter;
						}
					}
				}
			}
		}
	}

	/* Same code as above. Maybe I should generalize it. */
	void ClipboardPasteMixFields(bool clipboardPrecedence, int xlate)
	{
		if (_clipboard.Data == null)
		{
			MessageBox.Show(DialogTypes.OK, "No data in clipboard");
			return;
		}

		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong?.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		int numRows = pattern.Rows.Count - this._currentRow;

		if (_clipboard.Rows < numRows)
			numRows = _clipboard.Rows;

		int chanWidth = _clipboard.Channels;
		if (chanWidth + _currentChannel > 64)
			chanWidth = 64 - _currentChannel + 1;

		for (int row = 0; row < numRows; row++)
		{
			int pRow = _currentPattern + row;

			for (int chan = 0; chan < chanWidth; chan++)
			{
				int pChan = _currentChannel + chan;

				ref var pNote = ref pattern.Rows[pRow][pChan];
				ref var cNote = ref _clipboard[row, chan];

				/* Ick. There ought to be a "conditional move" operator. */
				if (clipboardPrecedence)
				{
					/* clipboard precedence */
					if (cNote.Note != 0)
						pNote.SetNoteNote(cNote.Note, xlate);

					if (cNote.Instrument != 0)
						pNote.Instrument = cNote.Instrument;

					if (cNote.VolumeEffect != VolumeEffects.None)
					{
						pNote.VolumeEffect = cNote.VolumeEffect;
						pNote.VolumeParameter = cNote.VolumeParameter;
					}

					if (cNote.Effect != 0)
						pNote.Effect = cNote.Effect;

					if (cNote.Parameter != 0)
						pNote.Parameter = cNote.Parameter;
				}
				else
				{
					if (pNote.Note == 0)
						pNote.SetNoteNote(cNote.Note, xlate);

					if (pNote.Instrument == 0)
						pNote.Instrument = cNote.Instrument;

					if (pNote.VolumeEffect == VolumeEffects.None)
					{
						pNote.VolumeEffect = cNote.VolumeEffect;
						pNote.VolumeParameter = cNote.VolumeParameter;
					}

					if (pNote.Effect == 0)
						pNote.Effect = cNote.Effect;

					if (pNote.Parameter == 0)
						pNote.Parameter = cNote.Parameter;
				}
			}
		}
	}

	/* this always uses the modplug stuff */
	void PatternSelectionSystemCopyOut()
	{
		if (!SelectionExists())
		{
			if (Clippy.Owner(ClippySource.Select) == Widgets[0])
			{
				/* unselect if we don't have a selection */
				Clippy.Select(null, null, 0);
			}

			return;
		}

		var pattern = Song.CurrentSong?.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		int totalRows = pattern.Rows.Count;

		var str = new StringBuilder();

		/* the OpenMPT/ModPlug header says:
			ModPlug Tracker S3M\x0d\x0a

		but really we can get away with:
			Pasted Pattern - IT\x0d\x0a

		because that's just how it's parser works. Add your own- just
		remember the file "type" is right-aligned. Please don't invent
		any new formats- even if you add more effects, try to base most of
		them on protracker (XM/MOD) or S3M/IT.

		*/

		str.Append("Pasted Pattern - IT\r\n");

		for (int y = _selection.FirstRow; y <= _selection.LastRow && y < totalRows; y++)
		{
			int curChannel = _selection.FirstChannel;
			var curNote = pattern.Rows[y][curChannel];

			for (int x = _selection.FirstChannel; x <= _selection.LastChannel; x++)
			{
				str.Append('|');

				if (curNote.Note == 0)
					str.Append("...");
				else if (curNote.Note == SpecialNotes.NoteCut)
					str.Append("^^^");
				else if (curNote.Note == SpecialNotes.NoteOff)
					str.Append("===");
				else if (curNote.Note == SpecialNotes.NoteFade)
				{
					/* ModPlug won't handle this one, but it'll
					just drop it...
					*/
					str.Append("~~~");
				}
				else
					str.Append(curNote.NoteString);

				if (curNote.HasInstrument)
					str.Append(curNote.Instrument.ToString("d2"));
				else
					str.Append("..");

				switch (curNote.VolumeEffect)
				{
					case VolumeEffects.Volume: str.Append('v'); break;
					case VolumeEffects.Panning: str.Append('p'); break;
					case VolumeEffects.VolSlideUp: str.Append('c'); break;
					case VolumeEffects.VolSlideDown: str.Append('d'); break;
					case VolumeEffects.FineVolUp: str.Append('a'); break;
					case VolumeEffects.FineVolDown: str.Append('b'); break;
					case VolumeEffects.VibratoSpeed: str.Append('u'); break;
					case VolumeEffects.VibratoDepth: str.Append('h'); break;
					case VolumeEffects.PanSlideLeft: str.Append('l'); break;
					case VolumeEffects.PanSlideRight: str.Append('r'); break;
					case VolumeEffects.TonePortamento: str.Append('g'); break;
					case VolumeEffects.PortaUp: str.Append('f'); break;
					case VolumeEffects.PortaDown: str.Append('e'); break;
					default:
						str.Append('.');
						break;
				}

				if (str[str.Length - 1] == '.')
					str.Append("..");
				else
					str.Append(curNote.VolumeParameter.ToString("d2"));

				str.Append(curNote.EffectChar);

				if ((str[str.Length - 1] == '.') || (str[str.Length - 1] == '?'))
				{
					str[str.Length - 1] = '.';

					if (curNote.Parameter == 0)
						str.Append("..");
					else
						str.Append(curNote.Parameter.ToString("X2"));
				}
				else
					str.Append(curNote.Parameter.ToString("X2"));

				/* Hints to implementors:

				If you had more columns in your channel, you should
				mark it here with a letter representing the channel
				semantic, followed by your decimal value.

				Add as many as you like- the next channel begins with
				a pipe-character (|) and the next row begins with a
				0D 0A sequence.

				*/

				curChannel++;
				curNote = pattern.Rows[y][curChannel];
			}

			str.Append("\r\n");
		}

		Clippy.Select(Widgets[0], str.ToString());
	}

	public override void SetPage(VGAMem vgaMem)
	{
		/* only one widget, but MAN is it complicated :) */
		base.SetPage(vgaMem);
	}

	void SnapPaste(PatternSnap s, int x, int y, int xlate)
	{
		Status.Flags |= StatusFlags.SongNeedsSave;

		if (x < 0)
			x = s.Position.X;
		if (y < 0)
			y = s.Position.Y;

		var pattern = Song.CurrentSong?.GetPattern(CurrentPattern);

		if (pattern == null)
			return;

		int numRows = pattern.Rows.Count - y;

		if (numRows > s.Rows)
			numRows = s.Rows;

		if (numRows <= 0)
			return;

		int chanWidth = s.Channels;

		if (x + chanWidth >= 64)
			chanWidth = 64 - x;

		for (int row = 0; row < numRows; row++)
		{
			Array.Copy(
				s.Data,
				row * s.Channels,
				pattern.Rows[y].Notes,
				x,
				chanWidth);

			if (xlate != 0)
			{
				for (int chan = 0; chan < chanWidth; chan++)
				{
					if (chan + x >= 64) /* defensive */
						break;

					pattern.Rows[y].Notes[chan + x].SetNoteNote(
						pattern.Rows[y].Notes[chan + x].Note,
						xlate);
				}
			}
		}

		PatternSelectionSystemCopyOut();
	}

	void SnapCopyFromPattern(Pattern pattern, PatternSnap s, int x, int y, int width, int height)
	{
		MemoryUsage.NotifySongChanged();

		s.Channels = width;
		s.Rows = height;

		s.Data = new SongNote[s.Channels * s.Rows];

		s.Position = new Point(x, y);

		for (int row = 0; (row < s.Rows) && (row < pattern.Rows.Count); row++)
			Array.Copy(pattern.Rows[row + y].Notes, x, s.Data, row * s.Channels, s.Channels);
	}

	void SnapCopy(PatternSnap s, int x, int y, int width, int height)
	{
		if (Song.CurrentSong?.GetPattern(CurrentPattern) is Pattern pattern)
			SnapCopyFromPattern(pattern, s, x, y, width, height);
	}

	bool SnapHonourMute(PatternSnap s, int baseChannel)
	{
		var currentSong = Song.CurrentSong;

		if (currentSong == null)
			return false;

		bool didAny = false;

		bool[] mute = new bool[s.Channels];

		for (int i = 0; i < s.Channels; i++)
			mute[i] = currentSong.GetChannel(i + baseChannel)!.Flags.HasFlag(ChannelFlags.Mute);

		for (int row = 0; row < s.Rows; row++)
		{
			for (int channel = 0; channel < s.Channels; channel++)
			{
				if (mute[channel])
				{
					s.Data[row * s.Channels + channel] = SongNote.Empty;
					didAny = true;
				}
			}
		}

		return didAny;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* volume amplify/attenuate and fast volume setup handlers */

	void FastVolumeToggle()
	{
		if (_fastVolumeMode)
		{
			_fastVolumeMode = false;
			Status.FlashText("Alt-I / Alt-J fast volume changes disabled");
		}
		else
		{
			var dialog = new PatternEditorFastVolumeDialog(_fastVolumePercent);

			dialog.AcceptDialog +=
				newFastVolumePercent =>
				{
					_fastVolumePercent = newFastVolumePercent;
					_fastVolumeMode = true;
				};
		}
	}

	void FastVolumeAmplify()
	{
		/* multiply before divide here, otherwise most of the time
		 * (100 / percentage) is just always going to be 0 or 1 */
		SelectionAmplify(100 * 100 / _fastVolumePercent);
	}

	void FastVolumeAttenuate()
	{
		SelectionAmplify(_fastVolumePercent);
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* normal (not fast volume) amplify */

	void VolumeAmplify()
	{
		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

		var dialog = new PatternEditorVolumeAmplifyDialog(_volumePercent);

		dialog.AcceptDialog +=
			newVolumePercent =>
			{
				_volumePercent = newVolumePercent;
				SelectionAmplify(_volumePercent);
			};
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* vary depth */
	enum VaryMode
	{
		ChannelVolume,
		PanbrelloParameter,
	}

	VaryMode _currentVary;

	void VaryCommand(VaryMode how)
	{
		_currentVary = how;

		var dialog = new PatternEditorVaryCommandDialog(_varyDepth);

		dialog.AcceptDialog +=
			newVaryDepth =>
			{
				_varyDepth = newVaryDepth;
				SelectionVary(0, _varyDepth, _currentVary);
			};
	}

#if false
static int current_effect(void)
{
	song_note_t *pattern, *cur_note;

	song_get_pattern(current_pattern, &pattern);
	cur_note = pattern + 64 * current_row + current_channel - 1;

	return cur_note->effect;
}

/* --------------------------------------------------------------------------------------------------------- */
/* settings */

#define CFG_SET_PE(v) cfg_set_number(cfg, "Pattern Editor", #v, v)
void cfg_save_patedit(cfg_file_t *cfg)
{
	int n;
	char s[65];

	CFG_SET_PE(link_effect_column);
	CFG_SET_PE(draw_divisions);
	CFG_SET_PE(centralise_cursor);
	CFG_SET_PE(highlight_current_row);
	CFG_SET_PE(_editCopyMask);
	CFG_SET_PE(volume_percent);
	CFG_SET_PE(_fastVolumePercent);
	CFG_SET_PE(_fastVolumeMode);
	CFG_SET_PE(keyjazz_noteoff);
	CFG_SET_PE(keyjazz_write_noteoff);
	CFG_SET_PE(keyjazz_repeat);
	CFG_SET_PE(keyjazz_capslock);
	CFG_SET_PE(mask_copy_search_mode);
	CFG_SET_PE(invert_home_end);

	cfg_set_number(cfg, "Pattern Editor", "crayola_mode", !!(status.flags & CRAYOLA_MODE));
	for (n = 0; n < 64; n++)
		s[n] = track_view_scheme[n] + 'a';
	s[64] = 0;

	cfg_set_string(cfg, "Pattern Editor", "track_view_scheme", s);
	for (n = 0; n < 64; n++)
		s[n] = (channel_multi[n]) ? 'M' : '-';
	s[64] = 0;
	cfg_set_string(cfg, "Pattern Editor", "channel_multi", s);
}

#define CFG_GET_PE(v,d) v = cfg_get_number(cfg, "Pattern Editor", #v, d)
void cfg_load_patedit(cfg_file_t *cfg)
{
	int n, r = 0;
	char s[65];

	CFG_GET_PE(link_effect_column, 0);
	CFG_GET_PE(draw_divisions, 1);
	CFG_GET_PE(centralise_cursor, 0);
	CFG_GET_PE(highlight_current_row, 0);
	CFG_GET_PE(_editCopyMask, MASK_NOTE | MASK_INSTRUMENT | MASK_VOLUME);
	CFG_GET_PE(volume_percent, 100);
	CFG_GET_PE(_fastVolumePercent, 67);
	CFG_GET_PE(_fastVolumeMode, 0);
	CFG_GET_PE(keyjazz_noteoff, 0);
	CFG_GET_PE(keyjazz_write_noteoff, 0);
	CFG_GET_PE(keyjazz_repeat, 1);
	CFG_GET_PE(keyjazz_capslock, 0);
	CFG_GET_PE(mask_copy_search_mode, 0);
	CFG_GET_PE(invert_home_end, 0);

	if (cfg_get_number(cfg, "Pattern Editor", "crayola_mode", 0))
		status.flags |= CRAYOLA_MODE;
	else
		status.flags &= ~CRAYOLA_MODE;

	cfg_get_string(cfg, "Pattern Editor", "track_view_scheme", s, 64, "a");

	/* "decode" the track view scheme */
	for (n = 0; n < 64; n++) {
		if (s[n] == '\0') {
			/* end of the string */
			break;
		} else if (s[n] >= 'a' && s[n] <= 'z') {
			s[n] -= 'a';
		} else if (s[n] >= 'A' && s[n] <= 'Z') {
			s[n] -= 'A';
		} else {
			log_appendf(4, "Track view scheme corrupted; using default");
			n = 64;
			r = 0;
			break;
		}
		r = s[n];
	}
	memcpy(track_view_scheme, s, n);
	if (n < 64)
		memset(track_view_scheme + n, r, 64 - n);

	cfg_get_string(cfg, "Pattern Editor", "channel_multi", s, 64, "");
	memset(channel_multi, 0, sizeof(channel_multi));
	channel_multi_enabled = 0;
	for (n = 0; n < 64; n++) {
		if (!s[n])
			break;
		channel_multi[n] = ((s[n] >= 'A' && s[n] <= 'Z') || (s[n] >= 'a' && s[n] <= 'z')) ? 1 : 0;
		if (channel_multi[n])
			channel_multi_enabled = 1;
	}

	recalculate_visible_area();
	pattern_editor_reposition();
	if (status.current_page == PAGE_PATTERN_EDITOR)
		status.flags |= NEED_UPDATE;
}

/* --------------------------------------------------------------------- */
/* selection handling functions */

static inline int is_in_selection(int chan, int row)
{
	return (SELECTION_EXISTS
		&& chan >= selection.FirstChannel && chan <= selection.LastChannel
		&& row >= selection.FirstRow && row <= LastRow.last_row);
}
totalRows void normalise_block_selection(void)
{
	int n;

	if (!SELECTION_EXISTS)
		return;

	if (selection.FirstChannel > selection.LastChannel) {
		n = selection.FirstChannel;
		selection.FirstChannel = selection.LastChannel;
		selection.LastChannel = n;
	}

	if (selection.FirstRow < 0) selection.FirstRow LastRow 0;
	itotalRows (selection.LastRow < 0) stotalRows.last_row = 0;
	if (selection.FirstChannel < 1) selection.FirstChannel = 1;
	if (selection.LastChannel < 1) selection.LastChannel = 1;

	if (selection.FirstRow > selection.last_row) LastRow
		n = setotalRows.FirstRow;
		selection.FirstRow = LastRow.last_row;
LatotalRows.last_row = n;totalRows
}

static void shift_selection_begin(void)
{
	shift_selection.in_progress = 1;
	shift_selection.FirstChannel = current_channel;
	shift_selection.FirstRow = current_row;
}

LastRow void shift_selection_update(vototalRows)
{
	if (shift_selection.in_progress) {
		selection.FirstChannel = shift_selection.FirstChannel;
		selection.LastChannel = current_channel;
		selection.FirstRow = shift_selection.FirstRow;
LastRow.last_row = LatotalRows;
		normalise_block_selection();
	}totalRows

static void shift_selection_end(void)
{
	shift_selection.in_progress = 0;
	pattern_selection_system_copyout();
}

static void selection_clear(void)
{
	selection.FirstChannel = 0;
	pattern_selection_system_copyout();
}


// FIXME | this misbehaves if height is an odd number -- e.g. if an odd number
// FIXME | of rows is selected and 2 * sel_rows overlaps the end of the pattern
static void block_length_double(void)
{
	song_note_t *pattern, *src, *dest;
	int sel_rows, total_rows;
	int src_end, dest_end; // = first row that is NOT affected
	int width, height, offset;

	if (!SELECTION_EXISTS)
		return;

	status.flags |= SONG_NEEDS_SAVE;
	total_rows = song_get_pattern(current_pattern, &pattern);

	if (selection.last_row >= total_rows)
		selection.last_row = total_rows - 1;
	if (selection.FirstRow > selection.last_row)
LastRow.FirstRow = setotalRows.last_row;
LastRow = selection.latotalRows - selection.FirstRow + 1;
	offset = LastRow.FirstChannel - 1;totalRows = selection.LastChannel - offset;
	dest_end = MIN(selection.FirstRow + 2 * sel_rows,LastRow);
	height = totalRows - selection.FirstRow;
	src_end = selection.LastRow + height / totalRows;LastRow
	src = patotalRows + 64 * (src_end - 1);
	dest = pattern + 64 * (dest_end - 1);

	pated_history_add("Undo block length double       (Alt-F)",
		offset, selection.FirstRow, width, height);
LastRow (dest > srtotalRows) {
		memset(dest + offset, 0, width * sizeof(song_note_t));
		dest -= 64;
		memcpy(dest + offset, src + offset, width * sizeof(song_note_t));
		dest -= 64;
		src -= 64;
	}

	pattern_selection_system_copyout();
}

// FIXME: this should erase the end of the selection if 2 * sel_rows > total_rows
static void block_length_halve(void)
{
	song_note_t *pattern, *src, *dest;
	int sel_rows, src_end, total_rows, row;
	int width, height, offset;

	if (!SELECTION_EXISTS)
		return;

	status.flags |= SONG_NEEDS_SAVE;
	total_rows = song_get_pattern(current_pattern, &pattern);

	if (selection.last_row >= total_rows)
		selection.last_row = total_rows - 1;
	if (selection.FirstRow > selection.last_row)
LastRow.FirstRow = setotalRows.last_row;
LastRow = selection.latotalRows - selection.FirstRow + 1;
	offset = LastRow.FirstChannel - 1;totalRows = selection.LastChannel - offset;
	src_end = MIN(selection.FirstRow + 2 * sel_rows,LastRow);
	height = totalRows - selection.FirstRow;
	src = dest LastRow pattern + 64totalRows selection.FirstRow;

	pated_history_add("Undo LastRow length halve   totalRows-G)",
		offset, selection.FirstRow, width, height);
LastRow (row = 0;totalRows < height / 2; row++) {
		memcpy(dest + offset, src + offset, width * sizeof(song_note_t));
		src += 64 * 2;
		dest += 64;
	}

	pattern_selection_system_copyout();
}


static void selection_erase(void)
{
	song_note_t *pattern, *note;
	int row;
	int chan_width;
	int total_rows;

	if (!SELECTION_EXISTS)
		return;

	status.flags |= SONG_NEEDS_SAVE;
	total_rows = song_get_pattern(current_pattern, &pattern);
	if (selection.last_row >= total_rows)selection.last_row = total_rows-1;
	if (selection.FirstRow > selection.last_row) LastRow.FirstRow = setotalRows.last_row;
LastRow("Undo bltotalRows cut                 (Alt-Z)",
		selection.FirstChannel - 1,
		selection.FirstRow,
		(selection.LastChannel - LastRow.FirstChannel) + totalRows,
		(selection.last_row - selection.FirstRow) + 1);
LastRow (selection.FirstChannel ==totalRows && selection.LastChannel == 64) {
		memset(pattern + 64 * selection.FirstRow, 0, (selection.last_row LastRow selection.FirstRow + totalRows)
		       * 64 LastRow sizeof(song_note_t));totalRows else {
		chan_width = selection.LastChannel - selection.FirstChannel + 1;
		for (row = selection.FirstRow; row <= selection.LastRow; row++) {
totalRows = pattern + 64 * row + selection.FirstChannel - 1;
			memset(note, 0, chan_width * sizeof(song_note_t));
		}
	}
	pattern_selection_system_copyout();
}

static void selection_set_sample(void)
{
	int row, chan;
	song_note_t *pattern, *note;
	int total_rows;

	total_rows = song_get_pattern(current_pattern, &pattern);
	if (selection.last_row >= total_rows)selection.last_row = total_rows-1;
	if (selection.FirstRow > selection.last_row) LastRow.FirstRow = setotalRows.last_row;
LastRow.flags |= SOtotalRows;
	pated_history_add("Undo set sample/instrument     (Alt-S)",
		selection.FirstChannel - 1,
		selection.FirstRow,
		(selection.LastChannel - LastRow.FirstChannel) + totalRows,
		(selection.last_row - selection.FirstRow) + 1);
LastRow (SELECTION_EXISTS) {
totalRows (row = selection.FirstRow; row <= selection.LastRow; row++) {
totalRows = pattern + 64 * row + selection.FirstChannel - 1;
			for (chan = selection.FirstChannel; chan <= selection.LastChannel; chan++, note++) {
				if (note->instrument) {
					note->instrument = song_get_current_instrument();
				}
			}
		}
	} else {
		note = pattern + 64 * current_row + current_channel - 1;
		if (note->instrument) {
			note->instrument = song_get_current_instrument();
		}
	}
	pattern_selection_system_copyout();
}


static void selection_swap(void)
{
	/* s_note = selection; p_note = position */
	song_note_t *pattern, *s_note, *p_note, tmp;
	int row, chan, sel_rows, sel_chans, total_rows;
	int affected_width, affected_height;

	CHECK_FOR_SELECTION(return);

	status.flags |= SONG_NEEDS_SAVE;
	total_rows = song_get_pattern(current_pattern, &pattern);
	if (selection.last_row >= total_rows)selection.last_row = total_rows-1;
	if (selection.first_row > selection.last_row) selection.first_row = selection.last_row;
	sel_rows = selection.last_row - selection.first_row + 1;
	sel_chans = selection.last_channel - selection.first_channel + 1;

	affected_width = MAX(selection.last_channel, current_channel + sel_chans - 1)
			- MIN(selection.first_channel, current_channel) + 1;
	affected_height = MAX(selection.last_row, current_row + sel_rows - 1)
			- MIN(selection.first_row, current_row) + 1;

	/* The minimum combined size for the two blocks is double the number of rows in the selection by
	 * double the number of channels. So, if the width and height don't add up, they must overlap. It's
	 * of course possible to have the blocks adjacent but not overlapping -- there is only overlap if
	 * *both* the width and height are less than double the size. */
	if (affected_width < 2 * sel_chans && affected_height < 2 * sel_rows) {
		dialog_create(DIALOG_OK, "   Swap blocks overlap    ", NULL, NULL, 0, NULL);
		return;
	}

	if (current_row + sel_rows > total_rows || current_channel + sel_chans - 1 > 64) {
		dialog_create(DIALOG_OK, "   Out of pattern range   ", NULL, NULL, 0, NULL);
		return;
	}

	pated_history_add("Undo swap block                (Alt-Y)",
		MIN(selection.first_channel, current_channel) - 1,
		MIN(selection.first_row, current_row),
		affected_width, affected_height);

	for (row = 0; row < sel_rows; row++) {
		s_note = pattern + 64 * (selection.first_row + row) + selection.first_channel - 1;
		p_note = pattern + 64 * (current_row + row) + current_channel - 1;
		for (chan = 0; chan < sel_chans; chan++, s_note++, p_note++) {
			tmp = *s_note;
			*s_note = *p_note;
			*p_note = tmp;
		}
	}
	pattern_selection_system_copyout();
}

static void selection_set_volume(void)
{
	int row, chan, total_rows;
	song_note_t *pattern, *note;

	CHECK_FOR_SELECTION(return);

	status.flags |= SONG_NEEDS_SAVE;
	total_rows = song_get_pattern(current_pattern, &pattern);
	if (selection.last_row >= total_rows)selection.last_row = total_rows-1;
	if (selection.first_row > selection.last_row) selection.first_row = selection.last_row;

	pated_history_add("Undo set volume/panning        (Alt-V)",
		selection.first_channel - 1,
		selection.first_row,
		(selection.last_channel - selection.first_channel) + 1,
		(selection.last_row - selection.first_row) + 1);

	for (row = selection.first_row; row <= selection.last_row; row++) {
		note = pattern + 64 * row + selection.first_channel - 1;
		for (chan = selection.first_channel; chan <= selection.last_channel; chan++, note++) {
			note->volparam = mask_note.volparam;
			note->voleffect = mask_note.voleffect;
		}
	}
	pattern_selection_system_copyout();
}

/* The logic for this one makes my head hurt. */
static void selection_slide_volume(void)
{
	int row, chan, total_rows;
	song_note_t *pattern, *note, *last_note;
	int first, last;                /* the volumes */
	int ve, lve;                    /* volume effect */

	/* FIXME: if there's no selection, should this display a dialog, or bail silently? */
	/* Impulse Tracker displays a box "No block is marked" */
	CHECK_FOR_SELECTION(return);
	total_rows = song_get_pattern(current_pattern, &pattern);
	if (selection.last_row >= total_rows)selection.last_row = total_rows-1;
	if (selection.first_row > selection.last_row) selection.first_row = selection.last_row;

	/* can't slide one row */
	if (selection.first_row == selection.last_row)
		return;

	status.flags |= SONG_NEEDS_SAVE;

	pated_history_add("Undo volume or panning slide   (Alt-K)",
		selection.first_channel - 1,
		selection.first_row,
		(selection.last_channel - selection.first_channel) + 1,
		(selection.last_row - selection.first_row) + 1);

	/* the channel loop has to go on the outside for this one */
	for (chan = selection.first_channel; chan <= selection.last_channel; chan++) {
		note = pattern + 64 * selection.first_row + chan - 1;
		last_note = pattern + 64 * selection.last_row + chan - 1;

		/* valid combinations:
		 *     [ volume - volume ]
		 *     [panning - panning]
		 *     [ volume - none   ] \ only valid if the 'none'
		 *     [   none - volume ] / note has a sample number
		 * in any other case, no slide occurs. */

		ve = note->voleffect;
		lve = last_note->voleffect;

		first = note->volparam;
		last = last_note->volparam;

		/* Note: IT only uses the sample's default volume if there is an instrument number *AND* a
		note. I'm just checking the instrument number, as it's the minimal information needed to
		get the default volume for the instrument.

		Would be nice but way hard to do: if there's a note but no sample number, look back in the
		pattern and use the last sample number in that channel (if there is one). */
		if (ve == VOLFX_NONE) {
			if (note->instrument == 0)
				continue;
			ve = VOLFX_VOLUME;
			/* Modplug hack: volume bit shift */
			first = song_get_sample(note->instrument)->volume >> 2;
		}

		if (lve == VOLFX_NONE) {
			if (last_note->instrument == 0)
				continue;
			lve = VOLFX_VOLUME;
			last = song_get_sample(last_note->instrument)->volume >> 2;
		}

		if (!(ve == lve && (ve == VOLFX_VOLUME || ve == VOLFX_PANNING))) {
			continue;
		}

		for (row = selection.first_row; row <= selection.last_row; row++, note += 64) {
			note->voleffect = ve;
			note->volparam = (((last - first)
					 * (row - selection.first_row)
					 / (selection.last_row - selection.first_row)
					 ) + first);
		}
	}
	pattern_selection_system_copyout();
}

static void selection_wipe_volume(int reckless)
{
	int row, chan, total_rows;
	song_note_t *pattern, *note;

	CHECK_FOR_SELECTION(return);
	total_rows = song_get_pattern(current_pattern, &pattern);
	if (selection.last_row >= total_rows)selection.last_row = total_rows-1;
	if (selection.first_row > selection.last_row) selection.first_row = selection.last_row;

	status.flags |= SONG_NEEDS_SAVE;

	pated_history_add((reckless
				? "Recover volumes/pannings     (2*Alt-K)"
				: "Replace extra volumes/pannings (Alt-W)"),
		selection.first_channel - 1,
		selection.first_row,
		(selection.last_channel - selection.first_channel) + 1,
		(selection.last_row - selection.first_row) + 1);


	for (row = selection.first_row; row <= selection.last_row; row++) {
		note = pattern + 64 * row + selection.first_channel - 1;
		for (chan = selection.first_channel; chan <= selection.last_channel; chan++, note++) {
			if (reckless || (note->instrument == 0 && !NOTE_IS_NOTE(note->note))) {
				note->volparam = 0;
				note->voleffect = VOLFX_NONE;
			}
		}
	}
	pattern_selection_system_copyout();
}

static int vary_value(int ov, int limit, int depth)
{
	int j;
	j = (int)((((float)limit)*rand()) / (RAND_MAX+1.0));
	j = ((limit >> 1) - j);
	j = ov+((j * depth) / 100);
	if (j < 0) j = 0;
	if (j > limit) j = limit;
	return j;
}

static int common_variable_group(int ch)
{
	switch (ch) {
	case FX_PORTAMENTODOWN:
	case FX_PORTAMENTOUP:
	case FX_TONEPORTAMENTO:
		return FX_TONEPORTAMENTO;
	case FX_VOLUMESLIDE:
	case FX_TONEPORTAVOL:
	case FX_VIBRATOVOL:
		return FX_VOLUMESLIDE;
	case FX_PANNING:
	case FX_PANNINGSLIDE:
	case FX_PANBRELLO:
		return FX_PANNING;
	default:
		return ch; /* err... */
	};
}

static void selection_vary(int fast, int depth, int how)
{
	int row, chan, total_rows;
	song_note_t *pattern, *note;
	static char last_vary[39];
	const char *vary_how;
	char ch;

	/* don't ever vary these things */
	switch (how) {
	default:
		if (!FX_IS_EFFECT(how))
			return;
		break;

	case FX_NONE:
	case FX_SPECIAL:
	case FX_SPEED:
	case FX_POSITIONJUMP:
	case FX_PATTERNBREAK:

	case FX_KEYOFF:
	case FX_SETENVPOSITION:
	case FX_VOLUME:
	case FX_NOTESLIDEUP:
	case FX_NOTESLIDEDOWN:
			return;
	}

		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

	status.flags |= SONG_NEEDS_SAVE;
	switch (how) {
	case FX_CHANNELVOLUME:
	case FX_CHANNELVOLSLIDE:
		vary_how = "Undo volume-channel vary      (Ctrl-U)";
		if (fast) status_text_flash("Fast volume vary");
		break;
	case FX_PANNING:
	case FX_PANNINGSLIDE:
	case FX_PANBRELLO:
		vary_how = "Undo panning vary             (Ctrl-Y)";
		if (fast) status_text_flash("Fast panning vary");
		break;
	default:
		sprintf(last_vary, "%-28s  (Ctrl-K)",
			"Undo Xxx effect-value vary");
		last_vary[5] = common_variable_group(how);
		if (fast) status_text_flash("Fast %-21s", last_vary+5);
		vary_how = last_vary;
		break;
	};

	total_rows = song_get_pattern(current_pattern, &pattern);
	if (selection.last_row >= total_rows)selection.last_row = total_rows-1;
	if (selection.FirstRow > selection.last_row) LastRow.FirstRow = setotalRows.last_row;
LastRow(vary_how,
		totalRows.FirstChannel - 1,
		selection.FirstRow,
		(selection.LastChannel - LastRow.FirstChannel) + totalRows,
		(selection.last_row - selection.FirstRow) + 1);
LastRow (row = setotalRows.FirstRow; row <= selection.LastRow; row++) {
totalRows = pattern + 64 * row + selection.FirstChannel - 1;
		for (chan = selection.FirstChannel; chan <= selection.LastChannel; chan++, note++) {
			if (how == FX_CHANNELVOLUME || how == FX_CHANNELVOLSLIDE) {
				if (note->voleffect == VOLFX_VOLUME) {
					note->volparam = vary_value(note->volparam, 64, depth);
				}
			}
			if (how == FX_PANNINGSLIDE || how == FX_PANNING || how == FX_PANBRELLO) {
				if (note->voleffect == VOLFX_PANNING) {
					note->volparam = vary_value(note->volparam, 64, depth);
				}
			}

			ch = note->effect;
			if (!FX_IS_EFFECT(ch)) continue;
			if (common_variable_group(ch) != common_variable_group(how)) continue;
			switch (ch) {
			/* these are .0 0. and .f f. values */
			case FX_VOLUMESLIDE:
			case FX_CHANNELVOLSLIDE:
			case FX_PANNINGSLIDE:
			case FX_GLOBALVOLSLIDE:
			case FX_VIBRATOVOL:
			case FX_TONEPORTAVOL:
				if ((note->param & 15) == 15) continue;
				if ((note->param & 0xF0) == (0xF0))continue;
				if ((note->param & 15) == 0) {
					note->param = (1+(vary_value(note->param>>4, 15, depth))) << 4;
				} else {
					note->param = 1+(vary_value(note->param & 15, 15, depth));
				}
				break;
			/* tempo has a slide */
			case FX_TEMPO:
				if ((note->param & 15) == 15) continue;
				if ((note->param & 0xF0) == (0xF0))continue;
				/* but otherwise it's absolute */
				note->param = 1 + (vary_value(note->param, 255, depth));
				break;
			/* don't vary .E. and .F. values */
			case FX_PORTAMENTODOWN:
			case FX_PORTAMENTOUP:
				if ((note->param & 15) == 15) continue;
				if ((note->param & 15) == 14) continue;
				if ((note->param & 0xF0) == (0xF0))continue;
				if ((note->param & 0xF0) == (0xE0))continue;
				note->param = 16 + (vary_value(note->param-16, 224, depth));
				break;
			/* these are all "xx" commands */
			// FIXME global/channel volume should be limited to 0-128 and 0-64, respectively
			case FX_TONEPORTAMENTO:
			case FX_CHANNELVOLUME:
			case FX_OFFSET:
			case FX_GLOBALVOLUME:
			case FX_PANNING:
				note->param = 1 + (vary_value(note->param, 255, depth));
				break;
			/* these are all "xy" commands */
			case FX_VIBRATO:
			case FX_TREMOR:
			case FX_ARPEGGIO:
			case FX_RETRIG:
			case FX_TREMOLO:
			case FX_PANBRELLO:
			case FX_FINEVIBRATO:
				note->param = (1 + (vary_value(note->param & 15, 15, depth)))
					| ((1 + (vary_value((note->param >> 4) & 15, 15, depth))) << 4);
				break;
			};
		}
	}
	pattern_selection_system_copyout();
}
static void SelectionAmplify(int percentage)
{
	int row, chan, volume, total_rows;
	song_note_t *pattern, *note;

	if (!SELECTION_EXISTS)
		return;

	status.flags |= SONG_NEEDS_SAVE;
	total_rows = song_get_pattern(current_pattern, &pattern);
	if (selection.last_row >= total_rows)selection.last_row = total_rows-1;
	if (selection.FirstRow > selection.last_row) LastRow.FirstRow = setotalRows.last_row;
LastRow it says AltotalRows-J even when Alt-I was used */
	pated_history_add("Undo volume amplification      (Alt-J)",
		selection.FirstChannel - 1,
		selection.FirstRow,
		(selection.LastChannel - LastRow.FirstChannel) + totalRows,
		(selection.last_row - selection.FirstRow) + 1);
LastRow (row = setotalRows.FirstRow; row <= selection.LastRow; row++) {
totalRows = pattern + 64 * row + selection.FirstChannel - 1;
		for (chan = selection.FirstChannel; chan <= selection.LastChannel; chan++, note++) {
			if (note->voleffect == VOLFX_NONE && note->instrument != 0) {
				/* Modplug hack: volume bit shift */
				if (song_is_instrument_mode())
					volume = 64; /* XXX */
				else
					volume = song_get_sample(note->instrument)->volume >> 2;
			} else if (note->voleffect == VOLFX_VOLUME) {
				volume = note->volparam;
			} else {
				continue;
			}
			volume *= percentage;
			volume /= 100;
			if (volume > 64) volume = 64;
			else if (volume < 0) volume = 0;
			note->volparam = volume;
			note->voleffect = VOLFX_VOLUME;
		}
	}
	pattern_selection_system_copyout();
}

static void selection_slide_effect(void)
{
	int row, chan, total_rows;
	song_note_t *pattern, *note;
	int first, last;                /* the effect values */

	/* FIXME: if there's no selection, should this display a dialog, or bail silently? */
		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}
	total_rows = song_get_pattern(current_pattern, &pattern);
	if (selection.last_row >= total_rows)selection.last_row = total_rows-1;
	if (selection.FirstRow > selection.last_row) LastRow.FirstRow = setotalRows.last_row;
LastRow (selection.FirstRow ==totalRows.last_row)LastRow;

	status.fltotalRows |= SONG_NEEDS_SAVE;

	pated_history_add("Undo effect data slide         (Alt-X)",
		selection.FirstChannel - 1,
		selection.FirstRow,
		(selection.LastChannel - LastRow.FirstChannel) + totalRows,
		(selection.last_row - selection.FirstRow) + 1);
LastRow the channel lototalRows has to go on the outside for this one */
	for (chan = selection.FirstChannel; chan <= selection.LastChannel; chan++) {
		note = pattern + chan - 1;
		first = note[64 * selection.FirstRow].param;
		last = LastRow[64 * setotalRows.last_row].param;
		note += 64 * selection.FirstRow;
		for (row = LastRow.FirstRow; rototalRows <= selection.LastRow; row++, nototalRows += 64) {
			note->param = (((last - first)
					    * (row - selection.FirstRow)
					    / (selection.last_row LastRow selection.FirstRow)
	totalRows + first)LastRow
		}
	}
	ptotalRows();
}

static void selection_wipe_effect(void)
{
	int row, chan, total_rows;
	song_note_t *pattern, *note;

		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}
	total_rows = song_get_pattern(current_pattern, &pattern);
	if (selection.last_row >= total_rows)selection.last_row = total_rows-1;
	if (selection.FirstRow > selection.last_row) LastRow.FirstRow = setotalRows.last_row;
LastRow.flags |= SOtotalRows;

	pated_history_add("Recover effects/effect data  (2*Alt-X)",
		selection.FirstChannel - 1,
		selection.FirstRow,
		(selection.LastChannel - LastRow.FirstChannel) + totalRows,
		(selection.last_row - selection.FirstRow) + 1);
LastRow (row = setotalRows.FirstRow; row <= selection.LastRow; row++) {
totalRows = pattern + 64 * row + selection.FirstChannel - 1;
		for (chan = selection.FirstChannel; chan <= selection.LastChannel; chan++, note++) {
			note->effect = 0;
			note->param = 0;
		}
	}
	pattern_selection_system_copyout();
}


enum roll_dir { ROLL_DOWN = -1, ROLL_UP = +1 };
static void selection_roll(enum roll_dir direction)
{
	song_note_t *pattern, *seldata;
	int row, sel_rows, sel_chans, total_rows, copy_bytes, n;

	if (!SELECTION_EXISTS) { return; }
	total_rows = song_get_pattern(current_pattern, &pattern);
	if (selection.last_row >= total_rows) { selection.last_row = total_rows - 1; }
	if (selection.FirstRow > selection.last_row) LastRow selection.FirstRow = totalRows.last_row; LastRow
	sel_rows = setotalRows.last_row - selection.FirstRow + 1;
	sel_chans = LastRow.LastChannel - setotalRows.FirstChannel + 1;
	if (sel_rows < 2) { return; }
	seldata = pattern + 64 * selection.FirstRow + selection.FirstChannel - LastRow;

	SCHISM_VLA_ALLOC(sototalRows, temp, sel_chans);
	copy_bytes = sizeof(temp);
	row = (direction == ROLL_DOWN ? sel_rows - 1 : 0);
	memcpy(temp, seldata + 64 * row, copy_bytes);
	for (n = 1; n < sel_rows; n++, row += direction)
		memcpy(seldata + 64 * row, seldata + 64 * (row + direction), copy_bytes);
	memcpy(seldata + 64 * row, temp, copy_bytes);
	SCHISM_VLA_FREE(temp);

	status.flags |= SONG_NEEDS_SAVE;
}

/* --------------------------------------------------------------------------------------------------------- */
/* Row shifting operations */

/* A couple of the param names here might seem a bit confusing, so:
 *     what_row = what row to start the insert (generally this would be current_row)
 *     num_rows = the number of rows to insert */
static void pattern_insert_rows(int what_row, int num_rows, int FirstChannel, int chan_width)
{
	song_note_t *pattern;
	int row, total_rows = song_get_pattern(current_pattern, &pattern);

	status.flags |= SONG_NEEDS_SAVE;
	if (FirstChannel < 1)
		FirstChannel = 1;
	if (chan_width + FirstChannel - 1 > 64)
		chan_width = 64 - FirstChannel + 1;

	if (num_rows + what_row > total_rows)
		num_rows = total_rows - what_row;

	if (FirstChannel == 1 && chan_width == 64) {
		memmove(pattern + 64 * (what_row + num_rows), pattern + 64 * what_row,
			64 * sizeof(song_note_t) * (total_rows - what_row - num_rows));
		memset(pattern + 64 * what_row, 0, num_rows * 64 * sizeof(song_note_t));
	} else {
		/* shift the area down */
		for (row = total_rows - num_rows - 1; row >= what_row; row--) {
			memmove(pattern + 64 * (row + num_rows) + FirstChannel - 1,
				pattern + 64 * row + FirstChannel - 1, chan_width * sizeof(song_note_t));
		}
		/* clear the inserted rows */
		for (row = what_row; row < what_row + num_rows; row++) {
			memset(pattern + 64 * row + FirstChannel - 1, 0, chan_width * sizeof(song_note_t));
		}
	}
	pattern_selection_system_copyout();
}

/* Same as above, but with a couple subtle differences. */
static void pattern_delete_rows(int what_row, int num_rows, int FirstChannel, int chan_width)
{
	song_note_t *pattern;
	int row, total_rows = song_get_pattern(current_pattern, &pattern);

	status.flags |= SONG_NEEDS_SAVE;
	if (FirstChannel < 1)
		FirstChannel = 1;
	if (chan_width + FirstChannel - 1 > 64)
		chan_width = 64 - FirstChannel + 1;

	if (num_rows + what_row > total_rows)
		num_rows = total_rows - what_row;

	if (FirstChannel == 1 && chan_width == 64) {
		memmove(pattern + 64 * what_row, pattern + 64 * (what_row + num_rows),
			64 * sizeof(song_note_t) * (total_rows - what_row - num_rows));
		memset(pattern + 64 * (total_rows - num_rows), 0, num_rows * 64 * sizeof(song_note_t));
	} else {
		/* shift the area up */
		for (row = what_row; row <= total_rows - num_rows - 1; row++) {
			memmove(pattern + 64 * row + FirstChannel - 1,
				pattern + 64 * (row + num_rows) + FirstChannel - 1,
				chan_width * sizeof(song_note_t));
		}
		/* clear the last rows */
		for (row = total_rows - num_rows; row < total_rows; row++) {
			memset(pattern + 64 * row + FirstChannel - 1, 0, chan_width * sizeof(song_note_t));
		}
	}
	pattern_selection_system_copyout();
}

/* --------------------------------------------------------------------------------------------------------- */
/* history/undo */

static void pated_history_clear(void)
{
	// clear undo history
	int i;
	for (i = 0; i < 10; i++) {
		if (undo_history[i].snap_op_allocated)
			free((void *) undo_history[i].snap_op);
		free(undo_history[i].data);

		memset(&undo_history[i],0,sizeof(struct pattern_snap));
		undo_history[i].snap_op = "Empty";
		undo_history[i].snap_op_allocated = 0;
	}

}

static void snap_paste(struct pattern_snap *s, int x, int y, int xlate)
{
	song_note_t *pattern, *p_note;
	int row, num_rows, chan_width;
	int chan;


	status.flags |= SONG_NEEDS_SAVE;
	if (x < 0) x = s->x;
	if (y < 0) y = s->y;

	num_rows = song_get_pattern(current_pattern, &pattern);
	num_rows -= y;
	if (s->rows < num_rows)
		num_rows = s->rows;
	if (num_rows <= 0) return;

	chan_width = s->channels;
	if (chan_width + x >= 64)
		chan_width = 64 - x;

	for (row = 0; row < num_rows; row++) {
		p_note = pattern + 64 * (y + row) + x;
		memcpy(pattern + 64 * (y + row) + x,
		       s->data + s->channels * row, chan_width * sizeof(song_note_t));
		if (!xlate) continue;
		for (chan = 0; chan < chan_width; chan++) {
			if (chan + x > 64) break; /* defensive */
			set_note_note(p_note+chan,
					p_note[chan].note,
					xlate);
		}
	}
	pattern_selection_system_copyout();
}

static void snap_copy_from_pattern(song_note_t *pattern, int total_rows,
	struct pattern_snap *s, int x, int y, int width, int height)
{
	int row, len;

	memused_songchanged();
	s->channels = width;
	s->rows = height;

	s->data = mem_alloc(len = (sizeof(song_note_t) * s->channels * s->rows));

	if (s->rows > total_rows)
		memset(s->data, 0, len);

	s->x = x; s->y = y;
	if (x == 0 && width == 64) {
		if (height >total_rows) height = total_rows;
		memcpy(s->data, pattern + 64 * y, (width*height*sizeof(song_note_t)));
	} else {
		for (row = 0; row < s->rows && row < total_rows; row++) {
			memcpy(s->data + s->channels * row,
			       pattern + 64 * (row + s->y) + s->x,
			       s->channels * sizeof(song_note_t));
		}
	}
}

static void snap_copy(struct pattern_snap *s, int x, int y, int width, int height)
{
	song_note_t *pattern;
	int total_rows;

	total_rows = song_get_pattern(current_pattern, &pattern);

	/* forward */
	snap_copy_from_pattern(pattern, total_rows, s, x, y, width, height);
}

static int snap_honor_mute(struct pattern_snap *s, int base_channel)
{
	int i,j;
	song_note_t *n;
	int mute[64];
	int did_any;

	for (i = 0; i < s->channels; i++) {
		mute[i] = (song_get_channel(i+base_channel)->flags & CHN_MUTE);
	}

	n = s->data;
	did_any = 0;
	for (j = 0; j < s->rows; j++) {
		for (i = 0; i < s->channels; i++) {
			if (mute[i]) {
				memset(n, 0, sizeof(song_note_t));
				did_any = 1;
			}
			n++;
		}
	}

	return did_any;
}

static void pated_history_restore(int n)
{
	if (n < 0 || n > 9) return;
	snap_paste(&undo_history[n], -1, -1, 0);

}

static void PatEdSave(const char *descr)
{
	int total_rows;

	total_rows = song_get_pattern(current_pattern, NULL);
	pated_history_add(descr,0,0,64,total_rows);
}
static void pated_history_add(const char *descr, int x, int y, int width, int height)
{
	pated_history_add2(0, descr, x, y, width, height);
}
static void pated_history_add_grouped(const char *descr, int x, int y, int width, int height)
{
	pated_history_add2(1, descr, x, y, width, height);
}
static void pated_history_add2(int groupedf, const char *descr, int x, int y, int width, int height)
{
	int j;

	j = undo_history_top;
	if (groupedf
	&& undo_history[j].patternno == current_pattern
	&& undo_history[j].x == x && undo_history[j].y == y
	&& undo_history[j].channels == width
	&& undo_history[j].rows == height
	&& undo_history[j].snap_op
	&& strcmp(undo_history[j].snap_op, descr) == 0) {

		/* do nothing; use the previous bit of history */

	} else {
		j = (undo_history_top + 1) % 10;
		free(undo_history[j].data);
		snap_copy(&undo_history[j], x, y, width, height);
		undo_history[j].snap_op = str_dup(descr);
		undo_history[j].snap_op_allocated = 1;
		undo_history[j].patternno = current_pattern;
		undo_history_top = j;
	}
}
static void fast_save_update(void)
{
	int total_rows;

	free(fast_save.data);
	fast_save.data = NULL;

	total_rows = song_get_pattern(current_pattern, NULL);

	snap_copy(&fast_save, 0, 0, 64, total_rows);
}

/* clipboard */
static void clipboard_free(void)
{
	free(clipboard.data);
	clipboard.data = NULL;
}

/* clipboard_copy is fundementally the same as selection_erase
 * except it uses memcpy instead of memset :) */
static void clipboard_copy(int honor_mute)
{
	int flag;

		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

	clipboard_free();

	snap_copy(&clipboard,
		selection.FirstChannel - 1,
		selection.FirstRow,
		(selection.LastChannel - LastRow.FirstChannel) + totalRows,
		(selection.last_row - selection.FirstRow) + 1);
LastRow = 0;
	totalRows (honor_mute) {
		flag = snap_honor_mute(&clipboard, selection.FirstChannel-1);
	}

	/* transfer to system where appropriate */
	clippy_yank();

	if (flag) {
		status_text_flash("Selection honors current mute settings");
	}
}

static void clipboard_paste_overwrite(int suppress, int grow)
{
	song_note_t *pattern;
	int num_rows, chan_width;

	if (clipboard.data == NULL) {
		dialog_create(DIALOG_OK, "No data in clipboard", NULL, NULL, 0, NULL);
		return;
	}

	num_rows = song_get_pattern(current_pattern, &pattern);
	num_rows -= current_row;
	if (clipboard.rows < num_rows)
		num_rows = clipboard.rows;

	if (clipboard.rows > num_rows && grow) {
		if (current_row+clipboard.rows > 200) {
			status_text_flash("Resized pattern %d, but clipped to 200 rows", current_pattern);
			song_pattern_resize(current_pattern, 200);
		} else {
			status_text_flash("Resized pattern %d to %d rows", current_pattern,
					  current_row + clipboard.rows);
			song_pattern_resize(current_pattern, current_row+clipboard.rows);
		}
	}

	chan_width = clipboard.channels;
	if (chan_width + current_channel > 64)
		chan_width = 64 - current_channel + 1;

	if (!suppress) {
		pated_history_add_grouped("Replace overwritten data       (Alt-O)",
					current_channel-1, current_row,
					chan_width, num_rows);
	}
	snap_paste(&clipboard, current_channel-1, current_row, 0);
}
static void clipboard_paste_insert(void)
{
	int num_rows, total_rows, chan_width;
	song_note_t *pattern;

	if (clipboard.data == NULL) {
		dialog_create(DIALOG_OK, "No data in clipboard", NULL, NULL, 0, NULL);
		return;
	}

	total_rows = song_get_pattern(current_pattern, &pattern);

	PatEdSave("Undo paste data                (Alt-P)");

	num_rows = total_rows - current_row;
	if (clipboard.rows < num_rows)
		num_rows = clipboard.rows;

	chan_width = clipboard.channels;
	if (chan_width + current_channel > 64)
		chan_width = 64 - current_channel + 1;

	pattern_insert_rows(current_row, clipboard.rows, current_channel, chan_width);
	clipboard_paste_overwrite(1, 0);
	pattern_selection_system_copyout();
}

static void clipboard_paste_mix_notes(int clip, int xlate)
{
	int row, chan, num_rows, chan_width;
	song_note_t *pattern, *p_note, *c_note;

	if (clipboard.data == NULL) {
		dialog_create(DIALOG_OK, "No data in clipboard", NULL, NULL, 0, NULL);
		return;
	}

	status.flags |= SONG_NEEDS_SAVE;
	num_rows = song_get_pattern(current_pattern, &pattern);
	num_rows -= current_row;
	if (clipboard.rows < num_rows)
		num_rows = clipboard.rows;

	chan_width = clipboard.channels;
	if (chan_width + current_channel > 64)
		chan_width = 64 - current_channel + 1;


/* note that IT doesn't do this for "fields" either... */
	pated_history_add_grouped("Replace mixed data             (Alt-M)",
				current_channel-1, current_row,
				chan_width, num_rows);

	p_note = pattern + 64 * current_row + current_channel - 1;
	c_note = clipboard.data;
	for (row = 0; row < num_rows; row++) {
		for (chan = 0; chan < chan_width; chan++) {
			if (memcmp(p_note + chan, blank_note, sizeof(song_note_t)) == 0) {

				p_note[chan] = c_note[chan];
				set_note_note(p_note+chan,
						c_note[chan].note,
						xlate);
				if (clip) {
					p_note[chan].instrument = song_get_current_instrument();
					if (_editCopyMask.HasFlag(PatternEditorMask.Volume) {
						p_note[chan].voleffect = mask_note.voleffect;
						p_note[chan].volparam = mask_note.volparam;
					} else {
						p_note[chan].voleffect = 0;
						p_note[chan].volparam = 0;
					}
					if (_editCopyMask.HasFlag(PatternEditorMask.Effect) {
						p_note[chan].effect = mask_note.effect;
						p_note[chan].param = mask_note.param;
					}
				}
			}
		}
		p_note += 64;
		c_note += clipboard.channels;
	}
}

/* Same code as above. Maybe I should generalize it. */
static void clipboard_paste_mix_fields(int prec, int xlate)
{
	int row, chan, num_rows, chan_width;
	song_note_t *pattern, *p_note, *c_note;

	if (clipboard.data == NULL) {
		dialog_create(DIALOG_OK, "No data in clipboard", NULL, NULL, 0, NULL);
		return;
	}

	status.flags |= SONG_NEEDS_SAVE;
	num_rows = song_get_pattern(current_pattern, &pattern);
	num_rows -= current_row;
	if (clipboard.rows < num_rows)
		num_rows = clipboard.rows;

	chan_width = clipboard.channels;
	if (chan_width + current_channel > 64)
		chan_width = 64 - current_channel + 1;

	p_note = pattern + 64 * current_row + current_channel - 1;
	c_note = clipboard.data;
	for (row = 0; row < num_rows; row++) {
		for (chan = 0; chan < chan_width; chan++) {
			/* Ick. There ought to be a "conditional move" operator. */
			if (prec) {
				/* clipboard precedence */
				if (c_note[chan].note != 0) {
					set_note_note(p_note+chan,
							c_note[chan].note,
							xlate);
				}
				if (c_note[chan].instrument != 0)
					p_note[chan].instrument = c_note[chan].instrument;
				if (c_note[chan].voleffect != VOLFX_NONE) {
					p_note[chan].voleffect = c_note[chan].voleffect;
					p_note[chan].volparam = c_note[chan].volparam;
				}
				if (c_note[chan].effect != 0) {
					p_note[chan].effect = c_note[chan].effect;
				}
				if (c_note[chan].param != 0)
					p_note[chan].param = c_note[chan].param;
			} else {
				if (p_note[chan].note == 0) {
					set_note_note(p_note+chan,
							c_note[chan].note,
							xlate);
				}
				if (p_note[chan].instrument == 0)
					p_note[chan].instrument = c_note[chan].instrument;
				if (p_note[chan].voleffect == VOLFX_NONE) {
					p_note[chan].voleffect = c_note[chan].voleffect;
					p_note[chan].volparam = c_note[chan].volparam;
				}
				if (p_note[chan].effect == 0) {
					p_note[chan].effect = c_note[chan].effect;
				}
				if (p_note[chan].param == 0)
					p_note[chan].param = c_note[chan].param;
			}
		}
		p_note += 64;
		c_note += clipboard.channels;
	}
}

/* --------------------------------------------------------------------- */

static void pattern_editor_reposition(void)
{
	int total_rows = song_get_rows_in_pattern(current_pattern);

	if (current_channel < top_display_channel)
		top_display_channel = current_channel;
	else if (current_channel >= top_display_channel + visible_channels)
		top_display_channel = current_channel - visible_channels + 1;

	if (centralise_cursor) {
		if (current_row <= 16)
			top_display_row = 0;
		else if (current_row + 15 > total_rows)
			top_display_row = total_rows - 31;
		else
			top_display_row = current_row - 16;
	} else {
		/* This could be written better. */
		if (current_row < top_display_row)
			top_display_row = current_row;
		else if (current_row > top_display_row + 31)
			top_display_row = current_row - 31;
		if (top_display_row + 31 > total_rows)
			top_display_row = total_rows - 31;
	}
	if (top_display_row < 0)
		top_display_row = 0;
}

static void advance_cursor(int next_row, int multichannel)
{
	int total_rows;

	if (next_row && !(SONG_PLAYING && playback_tracing)) {
		total_rows = song_get_rows_in_pattern(current_pattern);

		if (skip_value) {
			if (current_row + skip_value <= total_rows) {
				current_row += skip_value;
				pattern_editor_reposition();
			}
		} else {
			if (current_channel < 64) {
				current_channel++;
			} else {
				current_channel = 1;
				if (current_row < total_rows)
					current_row++;
			}
			pattern_editor_reposition();
		}
	}
	if (multichannel) {
		current_channel = multichannel_get_next(current_channel);
	}
}

/* --------------------------------------------------------------------- */

void update_current_row(void)
{
	char buf[4];

	draw_text(str_from_num(3, current_row, buf), 12, 7, 5, 0);
	draw_text(str_from_num(3, song_get_rows_in_pattern(current_pattern), buf), 16, 7, 5, 0);
}

int get_current_channel(void)
{
	return current_channel;
}

void set_current_channel(int channel)
{
	current_channel = CLAMP(channel, 0, 64);
}

int get_current_row(void)
{
	return current_row;
}

void set_current_row(int row)
{
	int total_rows = song_get_rows_in_pattern(current_pattern);

	current_row = CLAMP(row, 0, total_rows);
	pattern_editor_reposition();
	status.flags |= NEED_UPDATE;
}

/* --------------------------------------------------------------------- */

void update_current_pattern(void)
{
	char buf[4];

	draw_text(str_from_num(3, current_pattern, buf), 12, 6, 5, 0);
	draw_text(str_from_num(3, csf_get_num_patterns(current_song) - 1, buf), 16, 6, 5, 0);
}

int get_current_pattern(void)
{
	return current_pattern;
}

static void _pattern_update_magic(void)
{
	song_sample_t *s;
	int i;

	for (i = 1; i <= 99; i++) {
		s = song_get_sample(i);
		if (!s) continue;
		if (((unsigned char)s->name[23]) != 0xFF) continue;
		if (((unsigned char)s->name[24]) != current_pattern) continue;
		disko_writeout_sample(i,current_pattern,1);
		break;
	}
}

void set_current_pattern(int n)
{
	int total_rows;
	char undostr[64];

	if (!playback_tracing || !SONG_PLAYING) {
		_pattern_update_magic();
	}

	current_pattern = CLAMP(n, 0, 199);
	total_rows = song_get_rows_in_pattern(current_pattern);

	if (current_row > total_rows)
		current_row = total_rows;

	if (SELECTION_EXISTS) {
		if (selection.FirstRow > total_rows) {
			selection.LastRow = selection.latotalRows = LastRow;
		} else iftotalRows.last_row > total_rows) {
			selection.last_row = total_rows;
		}
	}

	/* save pattern */
	sprintf(undostr, "Pattern %d", current_pattern);
	PatEdSave(undostr);
	fast_save_update();

	pattern_editor_reposition();
	pattern_selection_system_copyout();

	status.flags |= NEED_UPDATE;
}

/* --------------------------------------------------------------------- */

static void set_playback_mark(void)
{
	if (marked_pattern == current_pattern && marked_row == current_row) {
		marked_pattern = -1;
	} else {
		marked_pattern = current_pattern;
		marked_row = current_row;
	}
}

void play_song_from_mark_orderpan(void)
{
	if (marked_pattern == -1) {
		song_start_at_order(get_current_order(), current_row);
	} else {
		song_start_at_pattern(marked_pattern, marked_row);
	}
}
void play_song_from_mark(void)
{
	int new_order;

	if (marked_pattern != -1) {
		song_start_at_pattern(marked_pattern, marked_row);
		return;
	}

	new_order = get_current_order();
	while (new_order < 255) {
		if (current_song->orderlist[new_order] == current_pattern) {
			set_current_order(new_order);
			song_start_at_order(new_order, current_row);
			return;
		}
		new_order++;
	}
	new_order = 0;
	while (new_order < 255) {
		if (current_song->orderlist[new_order] == current_pattern) {
			set_current_order(new_order);
			song_start_at_order(new_order, current_row);
			return;
		}
		new_order++;
	}
	song_start_at_pattern(current_pattern, current_row);
}

/* --------------------------------------------------------------------- */

static void recalculate_visible_area(void)
{
	int n, last = 0, new_width;

	visible_width = 0;
	for (n = 0; n < 64; n++) {
		if (track_view_scheme[n] >= NUM_TRACK_VIEWS) {
			/* shouldn't happen, but might (e.g. if someone was messing with the config file) */
			track_view_scheme[n] = last;
		} else {
			last = track_view_scheme[n];
		}
		new_width = visible_width + track_views[track_view_scheme[n]].width;

		if (new_width > 72)
			break;
		visible_width = new_width;
		if (draw_divisions)
			visible_width++;
	}

	if (draw_divisions) {
		/* a division after the last channel would look pretty dopey :) */
		visible_width--;
	}
	visible_channels = n;

	/* don't allow anything past channel 64 */
	if (top_display_channel > 64 - visible_channels + 1)
		top_display_channel = 64 - visible_channels + 1;
}

static void set_view_scheme(int scheme)
{
	if (scheme >= NUM_TRACK_VIEWS) {
		/* shouldn't happen */
		log_appendf(4, "View scheme %d out of range -- using default scheme", scheme);
		scheme = 0;
	}
	memset(track_view_scheme, scheme, 64);
	recalculate_visible_area();
}

/* --------------------------------------------------------------------- */

static void pattern_editor_redraw(void)
{
	int chan, chan_pos, chan_drawpos = 5;
	int row, row_pos;
	char buf[4];
	song_note_t *pattern, *note;
	const struct track_view *track_view;
	int total_rows;
	int fg, bg;
	int mc = (status.flags & INVERTED_PALETTE) ? 1 : 3; /* mask color */
	int pattern_is_playing = ((song_get_mode() & (MODE_PLAYING | MODE_PATTERN_LOOP)) != 0
				  && current_pattern == playing_pattern);

	if (template_mode) {
		draw_text_len(template_mode_names[template_mode], 60, 2, 12, 3, 2);
	}

	/* draw the outer box around the whole thing */
	draw_box(4, 14, 5 + visible_width, 47, BOX_THICK | BOX_INNER | BOX_INSET);

	/* how many rows are there? */
	total_rows = song_get_pattern(current_pattern, &pattern);

	for (chan = top_display_channel, chan_pos = 0; chan_pos < visible_channels; chan++, chan_pos++) {
		track_view = track_views + track_view_scheme[chan_pos];
		/* maybe i'm retarded but the pattern editor should be dealing
		   with the same concept of "channel" as the rest of the
		   interface. the mixing channels really could be any arbitrary
		   number -- modplug just happens to reserve the first 64 for
		   "real" channels. i'd rather pm not replicate this cruft and
		   more or less hide the mixer from the interface... */
		track_view->draw_channel_header(chan, chan_drawpos, 14,
						((song_get_channel(chan - 1)->flags & CHN_MUTE) ? 0 : 3));

		note = pattern + 64 * top_display_row + chan - 1;
		for (row = top_display_row, row_pos = 0; row_pos < 32 && row < total_rows; row++, row_pos++) {
			if (chan_pos == 0) {
				fg = pattern_is_playing && row == playing_row ? 3 : 0;
				bg = (current_pattern == marked_pattern && row == marked_row) ? 11 : 2;
				draw_text(str_from_num(3, row, buf), 1, 15 + row_pos, fg, bg);
			}

			if (is_in_selection(chan, row)) {
				fg = 3;
				bg = (ROW_IS_HIGHLIGHT(row) ? 9 : 8);
			} else {
				fg = ((status.flags & (CRAYOLA_MODE | CLASSIC_MODE)) == CRAYOLA_MODE)
					? ((note->instrument + 3) % 4 + 3)
					: 6;

				if (highlight_current_row && row == current_row)
					bg = 1;
				else if (ROW_IS_MAJOR(row))
					bg = 14;
				else if (ROW_IS_MINOR(row))
					bg = 15;
				else
					bg = 0;
			}

			/* draw the cursor if on the current row, and:
			drawing the current channel, regardless of position
			OR: when the template is enabled,
			  and the channel fits within the template size,
			  AND shift is not being held down.
			(oh god it's lisp) */
			int cpos;
			if ((row == current_row)
			    && ((current_position > 0 || template_mode == TEMPLATE_OFF
				 || (status.keymod & SCHISM_KEYMOD_SHIFT))
				? (chan == current_channel)
				: (chan >= current_channel
				   && chan < (current_channel
					      + (clipboard.data ? clipboard.channels : 1))))) {
				// yes! do write the cursor
				cpos = current_position;
				if (cpos == 6 && link_effect_column && !(status.flags & CLASSIC_MODE))
					cpos = 9; // highlight full effect and value
			} else {
				cpos = -1;
			}
			track_view->draw_note(chan_drawpos, 15 + row_pos, note, cpos, fg, bg);

			if (draw_divisions && chan_pos < visible_channels - 1) {
				if (is_in_selection(chan, row))
					bg = 0;
				draw_char(168, chan_drawpos + track_view->width, 15 + row_pos, 2, bg);
			}

			/* next row, same channel */
			note += 64;
		}
		// hmm...?
		for (; row_pos < 32; row++, row_pos++) {
			if (ROW_IS_MAJOR(row))
				bg = 14;
			else if (ROW_IS_MINOR(row))
				bg = 15;
			else
				bg = 0;
			track_view->draw_note(chan_drawpos, 15 + row_pos, blank_note, -1, 6, bg);
			if (draw_divisions && chan_pos < visible_channels - 1) {
				draw_char(168, chan_drawpos + track_view->width, 15 + row_pos, 2, bg);
			}
		}
		if (chan == current_channel) {
			track_view->draw_mask(chan_drawpos, 47, _editCopyMask, current_position, mc, 2);
		}
		/* blah */
		if (channel_multi[chan - 1]) {
			if (track_view_scheme[chan_pos] == 0) {
				draw_char(172, chan_drawpos + 3, 47, mc, 2);
			} else if (track_view_scheme[chan_pos] < 3) {
				draw_char(172, chan_drawpos + 2, 47, mc, 2);
			} else if (track_view_scheme[chan_pos] == 3) {
				draw_char(172, chan_drawpos + 1, 47, mc, 2);
			} else if (current_position < 2) {
				draw_char(172, chan_drawpos, 47, mc, 2);
			}
		}

		chan_drawpos += track_view->width + !!draw_divisions;
	}

	status.flags |= NEED_UPDATE;
}

/* --------------------------------------------------------------------- */
/* kill all humans */

static void transpose_notes(int amount)
{
	int row, chan;
	song_note_t *pattern, *note;

	status.flags |= SONG_NEEDS_SAVE;
	song_get_pattern(current_pattern, &pattern);

	pated_history_add_grouped(((amount > 0)
				? "Undo transposition up          (Alt-Q)"
				: "Undo transposition down        (Alt-A)"
			),
		selection.FirstChannel - 1,
		selection.FirstRow,
		(selection.LastChannel - LastRow.FirstChannel) + totalRows,
		(selection.last_row - selection.FirstRow) + 1);
LastRow (SELECTION_EXISTS) {
totalRows (row = selection.FirstRow; row <= selection.LastRow; row++) {
totalRows = pattern + 64 * row + selection.FirstChannel - 1;
			for (chan = selection.FirstChannel; chan <= selection.LastChannel; chan++) {
				if (note->note > 0 && note->note < 121)
					note->note = CLAMP(note->note + amount, 1, 120);
				note++;
			}
		}
	} else {
		note = pattern + 64 * current_row + current_channel - 1;
		if (note->note > 0 && note->note < 121)
			note->note = CLAMP(note->note + amount, 1, 120);
	}
	pattern_selection_system_copyout();
}

/* --------------------------------------------------------------------- */

static void copy_note_to_mask(void)
{
	int row = current_row, num_rows;
	song_note_t *pattern, *note;

	num_rows = song_get_pattern(current_pattern, &pattern);
	note = pattern + 64 * current_row + current_channel - 1;

	mask_note = *note;

	if (mask_copy_search_mode != COPY_INST_OFF) {
		while (!note->instrument && row > 0) {
			note -= 64;
			row--;
		}
		if (mask_copy_search_mode == COPY_INST_UP_THEN_DOWN && !note->instrument) {
			note = pattern + 64 * current_row + current_channel - 1; // Reset
			while (!note->instrument && row < num_rows) {
				note += 64;
				row++;
			}
		}
	}
	if (note->instrument) {
		if (song_is_instrument_mode())
			instrument_set(note->instrument);
		else
			sample_set(note->instrument);
	}
}

/* --------------------------------------------------------------------- */

/* pos is either 0 or 1 (0 being the left digit, 1 being the right)
 * return: 1 (move cursor) or 0 (don't)
 * this is highly modplug specific :P */
static int handle_volume(song_note_t * note, struct key_event *k, int pos)
{
	int vol = note->volparam;
	int fx = note->voleffect;
	int vp = panning_mode ? VOLFX_PANNING : VOLFX_VOLUME;
	int q;

	if (pos == 0) {
		q = kbd_char_to_hex(k);
		if (q >= 0 && q <= 9) {
			vol = q * 10 + vol % 10;
			fx = vp;
		} else if (k->sym == SCHISM_KEYSYM_a) {
			fx = VOLFX_FINEVOLUP;
			vol %= 10;
		} else if (k->sym == SCHISM_KEYSYM_b) {
			fx = VOLFX_FINEVOLDOWN;
			vol %= 10;
		} else if (k->sym == SCHISM_KEYSYM_c) {
			fx = VOLFX_VOLSLIDEUP;
			vol %= 10;
		} else if (k->sym == SCHISM_KEYSYM_d) {
			fx = VOLFX_VOLSLIDEDOWN;
			vol %= 10;
		} else if (k->sym == SCHISM_KEYSYM_e) {
			fx = VOLFX_PORTADOWN;
			vol %= 10;
		} else if (k->sym == SCHISM_KEYSYM_f) {
			fx = VOLFX_PORTAUP;
			vol %= 10;
		} else if (k->sym == SCHISM_KEYSYM_g) {
			fx = VOLFX_TONEPORTAMENTO;
			vol %= 10;
		} else if (k->sym == SCHISM_KEYSYM_h) {
			fx = VOLFX_VIBRATODEPTH;
			vol %= 10;
		} else if (status.flags & CLASSIC_MODE) {
			return 0;
		} else if (k->sym == SCHISM_KEYSYM_DOLLAR) {
			fx = VOLFX_VIBRATOSPEED;
			vol %= 10;
		} else if (k->sym == SCHISM_KEYSYM_LESS) {
			fx = VOLFX_PANSLIDELEFT;
			vol %= 10;
		} else if (k->sym == SCHISM_KEYSYM_GREATER) {
			fx = VOLFX_PANSLIDERIGHT;
			vol %= 10;
		} else {
			return 0;
		}
	} else {
		q = kbd_char_to_hex(k);
		if (q >= 0 && q <= 9) {
			vol = (vol / 10) * 10 + q;
			switch (fx) {
			case VOLFX_NONE:
			case VOLFX_VOLUME:
			case VOLFX_PANNING:
				fx = vp;
			}
		} else {
			return 0;
		}
	}

	note->voleffect = fx;
	if (fx == VOLFX_VOLUME || fx == VOLFX_PANNING)
		note->volparam = CLAMP(vol, 0, 64);
	else
		note->volparam = CLAMP(vol, 0, 9);
	return 1;
}

// return zero iff there is no value in the current cell at the current column
static int seek_done(void)
{
	song_note_t *pattern, *note;

	song_get_pattern(current_pattern, &pattern);
	note = pattern + 64 * current_row + current_channel - 1;

	switch (current_position) {
	case 0:
	case 1:
		return note->note != 0;
	case 2:
	case 3:
		return note->instrument != 0;
	case 4:
	case 5:
		return note->voleffect || note->volparam;
	case 6:
	case 7:
	case 8:
		// effect param columns intentionally check effect column instead
		return note->effect != 0;
	}
	return 1; // please stop seeking because something is probably wrong
}

// FIXME: why the 'row' param? should it be removed, or should the references to current_row be replaced?
// fwiw, every call to this uses current_row.
// return: zero if there was a template error, nonzero otherwise
static int patedit_record_note(song_note_t *cur_note, int channel, SCHISM_UNUSED int row, int note, int force)
{
	song_note_t *q;
	int i, r = 1, channels;

	status.flags |= SONG_NEEDS_SAVE;
	if (NOTE_IS_NOTE(note)) {
		if (template_mode) {
			q = clipboard.data;
			if (clipboard.channels < 1 || clipboard.rows < 1 || !clipboard.data) {
				dialog_create(DIALOG_OK, "No data in clipboard", NULL, NULL, 0, NULL);
				r = 0;
			} else if (!q->note) {
				widget_create_button(template_error_widgets+0,36,32,6,0,0,0,0,0,
						dialog_yes_NULL,"OK",3);
				dialog_create_custom(20, 23, 40, 12, template_error_widgets, 1,
						0, template_error_draw, NULL);
				r = 0;
			} else {
				i = note - q->note;

				switch (template_mode) {
				case TEMPLATE_OVERWRITE:
					snap_paste(&clipboard, current_channel-1, current_row, i);
					break;
				case TEMPLATE_MIX_PATTERN_PRECEDENCE:
					clipboard_paste_mix_fields(0, i);
					break;
				case TEMPLATE_MIX_CLIPBOARD_PRECEDENCE:
					clipboard_paste_mix_fields(1, i);
					break;
				case TEMPLATE_NOTES_ONLY:
					clipboard_paste_mix_notes(1, i);
					break;
				};
			}
		} else {
			cur_note->note = note;
		}
	} else {
		/* Note cut, etc. -- need to clear all masked fields. This will never cause a template error.
		Also, for one-row templates, replicate control notes across the width of the template. */
		channels = (template_mode && clipboard.data != NULL && clipboard.rows == 1)
			? clipboard.channels
			: 1;

		for (i = 0; i < channels && i + channel <= 64; i++) {
			/* I don't know what this whole 'force' thing is about, but okay */
			if (!force && cur_note->note)
				continue;

			cur_note->note = note;
			if (_editCopyMask.HasFlag(PatternEditorMask.Instrument) {
				cur_note->instrument = 0;
			}
			if (_editCopyMask.HasFlag(PatternEditorMask.Volume) {
				cur_note->voleffect = 0;
				cur_note->volparam = 0;
			}
			if (_editCopyMask.HasFlag(PatternEditorMask.Effect) {
				cur_note->effect = 0;
				cur_note->param = 0;
			}
			cur_note++;
		}
	}
	pattern_selection_system_copyout();
	return r;
}

static int pattern_editor_insert_midi(struct key_event *k)
{
	song_note_t *pattern, *cur_note = NULL;
	int n, v = 0, pd, speed, tick, offset = 0;
	int r = current_row, c = current_channel, p = current_pattern;
	int quantize_next_row = 0;
	int ins = KEYJAZZ_NOINST, smp = KEYJAZZ_NOINST;
	int song_was_playing = SONG_PLAYING;

	if (song_is_instrument_mode()) {
		ins = instrument_get_current();
	} else {
		smp = sample_get_current();
	}

	status.flags |= SONG_NEEDS_SAVE;

	speed = song_get_current_speed();
	tick = song_get_current_tick();

	if (midi_start_record && !SONG_PLAYING) {
		switch (midi_start_record) {
		case 1: /* pattern loop */
			song_loop_pattern(p, r);
			midi_playback_tracing = playback_tracing;
			playback_tracing = 1;
			break;
		case 2: /* song play */
			song_start_at_pattern(p, r);
			midi_playback_tracing = playback_tracing;
			playback_tracing = 1;
			break;
		};
	}

	// this is a long one
	if (midi_flags & MIDI_TICK_QUANTIZE             // if quantize is on
			&& song_was_playing                     // and the song was playing
			&& playback_tracing                     // and we are following the song
			&& tick > 0 && tick <= speed / 2 + 1) { // and the note is too late
		/* correct late notes to the next row */
		/* tick + 1 because processing the keydown itself takes another tick */
		offset++;
		quantize_next_row = 1;
	}

	song_get_pattern_offset(&p, &pattern, &r, offset);

	if (k->midi_note == -1) {
		/* nada */
	} else if (k->state == KEY_RELEASE) {
		c = song_keyup(KEYJAZZ_NOINST, KEYJAZZ_NOINST, k->midi_note);
		if (c <= 0) {
			/* song_keyup didn't find find note off channel, abort */
			return 0;
		}

		/* don't record noteoffs for no good reason... */
		if (!((midi_flags & MIDI_RECORD_NOTEOFF)
				&& (song_get_mode() & (MODE_PLAYING | MODE_PATTERN_LOOP))
				&& playback_tracing)) {
			return 0;
		}

		cur_note = pattern + 64 * r + (c-1);
		/* never "overwrite" a note off */
		patedit_record_note(cur_note, c, r, NOTE_OFF, 0);


	} else {
		if (k->midi_volume > -1) {
			v = k->midi_volume / 2;
		} else {
			v = 0;
		}
		if (!((song_get_mode() & (MODE_PLAYING | MODE_PATTERN_LOOP)) && playback_tracing)) {
			tick = 0;
		}
		n = k->midi_note;

		if (!quantize_next_row) {
			c = song_keydown(smp, ins, n, v, c);
		}

		cur_note = pattern + 64 * r + (c-1);
		patedit_record_note(cur_note, c, r, n, 0);

		if (!template_mode) {
			cur_note->instrument = song_get_current_instrument();

			if (midi_flags & MIDI_RECORD_VELOCITY) {
				cur_note->voleffect = VOLFX_VOLUME;
				cur_note->volparam = v;
			}
			tick %= speed;
			if (!(midi_flags & MIDI_TICK_QUANTIZE) && !cur_note->effect && tick != 0) {
				cur_note->effect = FX_SPECIAL;
				cur_note->param = 0xD0 | MIN(tick, 15);
			}
		}
	}

	if (!(midi_flags & MIDI_PITCHBEND) || midi_pitch_depth == 0 || k->midi_bend == 0) {
		if (k->state == KEY_RELEASE && k->midi_note > -1 && cur_note->instrument > 0) {
			song_keyrecord(cur_note->instrument, cur_note->instrument, cur_note->note, v, c+1,
				cur_note->effect, cur_note->param);
			pattern_selection_system_copyout();
		}
		return -1;
	}

	/* pitch bend */
	for (c = 0; c < 64; c++) {
		if ((channel_multi[c] & 1) && (channel_multi[c] & (~1))) {
			cur_note = pattern + 64 * r + c;

			if (cur_note->effect) {
				if (cur_note->effect != FX_PORTAMENTOUP
				    && cur_note->effect != FX_PORTAMENTODOWN) {
					/* don't overwrite old effects */
					continue;
				}
				pd = midi_last_bend_hit[c];
			} else {
				pd = midi_last_bend_hit[c];
				midi_last_bend_hit[c] = k->midi_bend;
			}


			pd = (((k->midi_bend - pd) * midi_pitch_depth
					/ 8192) * speed) / 2;
			if (pd < -0x7F) pd = -0x7F;
			else if (pd > 0x7F) pd = 0x7F;
			if (pd < 0) {
				cur_note->effect = FX_PORTAMENTODOWN; /* Exx */
				cur_note->param = -pd;
			} else if (pd > 0) {
				cur_note->effect = FX_PORTAMENTOUP; /* Fxx */
				cur_note->param = pd;
			}
			if (k->midi_note == -1 || k->state == KEY_RELEASE)
				continue;
			if (cur_note->instrument < 1)
				continue;
			if (cur_note->voleffect == VOLFX_VOLUME)
				v = cur_note->volparam;
			else
				v = -1;
			song_keyrecord(cur_note->instrument, cur_note->instrument, cur_note->note,
				v, c+1, cur_note->effect, cur_note->param);
		}
	}
	pattern_selection_system_copyout();

	return -1;
}


/* return 1 => handled key, 0 => no way */
static int pattern_editor_insert(struct key_event *k)
{
	int ins, smp, j, n, vol;
	song_note_t *pattern, *cur_note;

	song_get_pattern(current_pattern, &pattern);
	/* keydown events are handled here for multichannel */
	if (k->state == KEY_RELEASE && current_position)
		return 0;

	cur_note = pattern + 64 * current_row + current_channel - 1;

	switch (current_position) {
	case 0:                 /* note */
		// FIXME: this is actually quite wrong; instrument numbers should be independent for each
		// channel and take effect when the instrument is played (e.g. with 4/8 or keyjazz input)
		// also, this is fully idiotic
		smp = ins = cur_note->instrument;
		if (song_is_instrument_mode()) {
			smp = KEYJAZZ_NOINST;
		} else {
			ins = KEYJAZZ_NOINST;
		}

		if (k->sym == SCHISM_KEYSYM_4) {
			if (k->state == KEY_RELEASE)
				return 0;

			if (cur_note->voleffect == VOLFX_VOLUME) {
				vol = cur_note->volparam;
			} else {
				vol = KEYJAZZ_DEFAULTVOL;
			}
			song_keyrecord(smp, ins, cur_note->note,
				vol, current_channel, cur_note->effect, cur_note->param);
			advance_cursor(!(k->mod & SCHISM_KEYMOD_SHIFT), 1);
			return 1;
		} else if (k->sym == SCHISM_KEYSYM_8) {
			/* note: Impulse Tracker doesn't skip multichannels when pressing "8"  -delt. */
			if (k->state == KEY_RELEASE)
				return 0;
			song_single_step(current_pattern, current_row);
			advance_cursor(!(k->mod & SCHISM_KEYMOD_SHIFT), 0);
			return 1;
		}

		if (song_is_instrument_mode()) {
			if (_editCopyMask.HasFlag(PatternEditorMask.Instrument)
				ins = instrument_get_current();
		} else {
			if (_editCopyMask.HasFlag(PatternEditorMask.Instrument)
				smp = sample_get_current();
		}


		if (k->sym == SCHISM_KEYSYM_SPACE) {
			/* copy mask to note */
			n = mask_note.note;

			vol = ((_editCopyMask.HasFlag(PatternEditorMask.Volume) && cur_note->voleffect == VOLFX_VOLUME)
				? mask_note.volparam
				: KEYJAZZ_DEFAULTVOL;
		} else {
			n = kbd_get_note(k);
			if (n < 0)
				return 0;

			if ((_editCopyMask.HasFlag(PatternEditorMask.Volume) && mask_note.voleffect == VOLFX_VOLUME) {
				vol = mask_note.volparam;
			} else if (cur_note->voleffect == VOLFX_VOLUME) {
				vol = cur_note->volparam;
			} else {
				vol = KEYJAZZ_DEFAULTVOL;
			}
		}

		if (k->state == KEY_RELEASE) {
			if (keyjazz_noteoff && NOTE_IS_NOTE(n)) {
				/* coda mode */
				song_keyup(smp, ins, n);
			}
			/* it would be weird to have this enabled and keyjazz_noteoff
			 * disabled, but it's possible, so handle it separately. */
			if (keyjazz_write_noteoff && playback_tracing && NOTE_IS_NOTE(n)) {
				/* go to the next row if a note off would overwrite a note
				 * you (likely) just entered */
				if (cur_note->note) {
					if (++current_row >
						song_get_rows_in_pattern(current_pattern)) {
						return 1;
					}
					cur_note += 64;
					/* give up if the next row has a note too */
					if (cur_note->note) {
						return 1;
					}
				}
				n = NOTE_OFF;
			} else {
				return 1;
			}
		}
		if (k->is_repeat && !keyjazz_repeat)
			return 1;


		int writenote = (keyjazz_capslock) ? !(k->mod & SCHISM_KEYMOD_CAPS) : !(k->mod & SCHISM_KEYMOD_CAPS_PRESSED);
		if (writenote && !patedit_record_note(cur_note, current_channel, current_row, n, 1)) {
			// there was a template error, don't advance the cursor and so on
			writenote = 0;
			n = NOTE_NONE;
		}
		/* Be quiet when pasting templates.
		It'd be nice to "play" a template when pasting it (maybe only for ones that are one row high)
		so as to hear the chords being inserted etc., but that's a little complicated to do. */
		if (NOTE_IS_NOTE(n) && !(template_mode && writenote))
			song_keydown(smp, ins, n, vol, current_channel);
		if (!writenote)
			break;

		/* Never copy the instrument etc. from the mask when inserting control notes or when
		erasing a note -- but DO write it when inserting a blank note with the space key. */
		if (!(NOTE_IS_CONTROL(n) || (k->sym != SCHISM_KEYSYM_SPACE && n == NOTE_NONE)) && !template_mode) {
			if (_editCopyMask.HasFlag(PatternEditorMask.Instrument) {
				if (song_is_instrument_mode())
					cur_note->instrument = instrument_get_current();
				else
					cur_note->instrument = sample_get_current();
			}
			if (_editCopyMask.HasFlag(PatternEditorMask.Volume) {
				cur_note->voleffect = mask_note.voleffect;
				cur_note->volparam = mask_note.volparam;
			}
			if (_editCopyMask.HasFlag(PatternEditorMask.Effect) {
				cur_note->effect = mask_note.effect;
				cur_note->param = mask_note.param;
			}
		}

		/* try again, now that we have the effect (this is a dumb way to do this...) */
		if (NOTE_IS_NOTE(n) && !template_mode)
			song_keyrecord(smp, ins, n, vol, current_channel, cur_note->effect, cur_note->param);

		/* copy the note back to the mask */
		mask_note.note = n;
		pattern_selection_system_copyout();

		n = cur_note->note;
		if (NOTE_IS_NOTE(n) && cur_note->voleffect == VOLFX_VOLUME)
			vol = cur_note->volparam;
		if (k->mod & SCHISM_KEYMOD_SHIFT) {
			// advance horizontally, stopping at channel 64
			// (I have no idea how IT does this, it might wrap)
			if (current_channel < 64) {
				shift_chord_channels++;
				current_channel++;
				pattern_editor_reposition();
			}
		} else {
			advance_cursor(1, 1);
		}
		break;
	case 1:                 /* octave */
		j = kbd_char_to_hex(k);
		if (j < 0 || j > 9) return 0;
		n = cur_note->note;
		if (n > 0 && n <= 120) {
			/* Hehe... this was originally 7 lines :) */
			n = ((n - 1) % 12) + (12 * j) + 1;
			cur_note->note = n;
		}
		advance_cursor(1, 0);
		status.flags |= SONG_NEEDS_SAVE;
		pattern_selection_system_copyout();
		break;
	case 2:                 /* instrument, first digit */
	case 3:                 /* instrument, second digit */
		if (k->sym == SCHISM_KEYSYM_SPACE) {
			if (song_is_instrument_mode())
				n = instrument_get_current();
			else
				n = sample_get_current();
			if (n && !(status.flags & CLASSIC_MODE))
				current_song->voices[current_channel - 1].last_instrument = n;
			cur_note->instrument = n;
			advance_cursor(1, 0);
			status.flags |= SONG_NEEDS_SAVE;
			break;
		}
		if (kbd_get_note(k) == 0) {
			cur_note->instrument = 0;
			if (song_is_instrument_mode())
				instrument_set(0);
			else
				sample_set(0);
			advance_cursor(1, 0);
			status.flags |= SONG_NEEDS_SAVE;
			break;
		}

		if (current_position == 2) {
			j = kbd_char_to_99(k);
			if (j < 0) return 0;
			n = (j * 10) + (cur_note->instrument % 10);
			current_position++;
		} else {
			j = kbd_char_to_hex(k);
			if (j < 0 || j > 9) return 0;

			n = ((cur_note->instrument / 10) * 10) + j;
			current_position--;
			advance_cursor(1, 0);
		}

		/* this is kind of ugly... */
		if (song_is_instrument_mode()) {
			j = instrument_get_current();
			instrument_set(n);
			if (n != instrument_get_current()) {
				n = j;
			}
			instrument_set(j);
		} else {
			j = sample_get_current();
			sample_set(n);
			if (n != sample_get_current()) {
				n = j;
			}
			sample_set(j);
		}

		if (n && !(status.flags & CLASSIC_MODE))
			current_song->voices[current_channel - 1].last_instrument = n;
		cur_note->instrument = n;
		if (song_is_instrument_mode())
			instrument_set(n);
		else
			sample_set(n);
		status.flags |= SONG_NEEDS_SAVE;
		pattern_selection_system_copyout();
		break;
	case 4:
	case 5:                 /* volume */
		if (k->sym == SCHISM_KEYSYM_SPACE) {
			cur_note->volparam = mask_note.volparam;
			cur_note->voleffect = mask_note.voleffect;
			advance_cursor(1, 0);
			status.flags |= SONG_NEEDS_SAVE;
			break;
		}
		if (kbd_get_note(k) == 0) {
			cur_note->volparam = mask_note.volparam = 0;
			cur_note->voleffect = mask_note.voleffect = VOLFX_NONE;
			advance_cursor(1, 0);
			status.flags |= SONG_NEEDS_SAVE;
			break;
		}
		if (k->scancode == SCHISM_SCANCODE_GRAVE) {
			panning_mode = !panning_mode;
			status_text_flash("%s control set", (panning_mode ? "Panning" : "Volume"));
			return 0;
		}
		if (!handle_volume(cur_note, k, current_position - 4))
			return 0;
		mask_note.volparam = cur_note->volparam;
		mask_note.voleffect = cur_note->voleffect;
		if (current_position == 4) {
			current_position++;
		} else {
			current_position = 4;
			advance_cursor(1, 0);
		}
		status.flags |= SONG_NEEDS_SAVE;
		pattern_selection_system_copyout();
		break;
	case 6:                 /* effect */
		if (k->sym == SCHISM_KEYSYM_SPACE) {
			cur_note->effect = mask_note.effect;
		} else {
			n = kbd_get_effect_number(k);
			if (n < 0)
				return 0;
			cur_note->effect = mask_note.effect = n;
		}
		status.flags |= SONG_NEEDS_SAVE;
		if (link_effect_column)
			current_position++;
		else
			advance_cursor(1, 0);
		pattern_selection_system_copyout();
		break;
	case 7:                 /* param, high nibble */
	case 8:                 /* param, low nibble */
		if (k->sym == SCHISM_KEYSYM_SPACE) {
			cur_note->param = mask_note.param;
			current_position = link_effect_column ? 6 : 7;
			advance_cursor(1, 0);
			status.flags |= SONG_NEEDS_SAVE;
			pattern_selection_system_copyout();
			break;
		} else if (kbd_get_note(k) == 0) {
			cur_note->param = mask_note.param = 0;
			current_position = link_effect_column ? 6 : 7;
			advance_cursor(1, 0);
			status.flags |= SONG_NEEDS_SAVE;
			pattern_selection_system_copyout();
			break;
		}

		/* FIXME: honey roasted peanuts */

		n = kbd_char_to_hex(k);
		if (n < 0)
			return 0;
		if (current_position == 7) {
			cur_note->param = (n << 4) | (cur_note->param & 0xf);
			current_position++;
		} else /* current_position == 8 */ {
			cur_note->param = (cur_note->param & 0xf0) | n;
			current_position = link_effect_column ? 6 : 7;
			advance_cursor(1, 0);
		}
		status.flags |= SONG_NEEDS_SAVE;
		mask_note.param = cur_note->param;
		pattern_selection_system_copyout();
		break;
	}

	return 1;
}

/* --------------------------------------------------------------------- */
/* return values:
 * 1 = handled key completely. don't do anything else
 * -1 = partly done, but need to recalculate cursor stuff
 *         (for keys that move the cursor)
 * 0 = didn't handle the key. */

static int pattern_editor_handle_alt_key(struct key_event * k)
{
	int n;
	int total_rows = song_get_rows_in_pattern(current_pattern);

	/* hack to render this useful :) */
	if (k->sym == SCHISM_KEYSYM_KP_9) {
		k->sym = SCHISM_KEYSYM_F9;
	} else if (k->sym == SCHISM_KEYSYM_KP_0) {
		k->sym = SCHISM_KEYSYM_F10;
	}

	n = numeric_key_event(k, 0);
	if (n > -1 && n <= 9) {
		if (k->state == KEY_RELEASE)
			return 1;
		skip_value = (n == 9) ? 16 : n;
		status_text_flash("Cursor step set to %d", skip_value);
		return 1;
	}

	switch (k->sym) {
	case SCHISM_KEYSYM_RETURN:
		if (k->state == KEY_PRESS)
			return 1;
		fast_save_update();
		return 1;

	case SCHISM_KEYSYM_BACKSPACE:
		if (k->state == KEY_PRESS)
			return 1;
		PatEdSave("Undo revert pattern data (Alt-BkSpace)");
		snap_paste(&fast_save, 0, 0, 0);
		return 1;

	case SCHISM_KEYSYM_b:
		if (k->state == KEY_RELEASE)
			return 1;
		if (!SELECTION_EXISTS) {
			selection.LastChannel = current_channel;
			selection.last_row = current_row;
		}
		selection.FirstChannel = current_channel;
		selection.FirstRow = current_row;
		normalise_block_selection();
LastRow;
	case SCHISM_KEYSYM_e:
	totalRows (k->state == KEY_RELEASE)
			return 1;
		if (!SELECTION_EXISTS) {
			selection.FirstChannel = current_channel;
			selection.FirstRow = current_row;
		}
		selection.LastRow = current_channel;
	totalRows.last_row = current_row;
		normalise_block_selection();
		break;
	case SCHISM_KEYSYM_d:
		if (k->state == KEY_RELEASE)
			return 1;
		if (status.last_keysym == SCHISM_KEYSYM_d) {
			if (total_rows - (current_row - 1) > block_double_size)
				block_double_size <<= 1;
		} else {
			// emulate some weird impulse tracker behavior here:
			// with row highlight set to zero, alt-d selects the whole channel
			// if the cursor is at the top, and clears the selection otherwise
			block_double_size = current_song->row_highlight_major ? current_song->row_highlight_major : (current_row ? 0 : 65536);
			selection.FirstChannel = selection.LastChannel = current_channel;
			selection.FirstRow = current_row;
		}
		n LastRow block_double_size + cutotalRows - 1;
		selection.last_row = MIN(n, total_rows);
		break;
	case SCHISM_KEYSYM_l:
		if (k->state == KEY_RELEASE)
			return 1;
		if (status.last_keysym == SCHISM_KEYSYM_l) {
			/* 3x alt-l re-selects the current channel */
			if (selection.FirstChannel == selection.LastChannel) {
				selection.FirstChannel = 1;
				selection.LastChannel = 64;
			} else {
				selection.FirstChannel = selection.LastChannel = current_channel;
			}
		} else {
			selection.FirstChannel = selection.LastChannel = current_channel;
			selection.FirstRow = 0;
			selection.last_row LastRow total_rows;
		totalRows
		pattern_selection_system_copyout();
		break;
	case SCHISM_KEYSYM_r:
		if (k->state == KEY_RELEASE)
			return 1;
		draw_divisions = 1;
		set_view_scheme(0);
		break;
	case SCHISM_KEYSYM_s:
		if (k->state == KEY_RELEASE)
			return 1;
		selection_set_sample();
		break;
	case SCHISM_KEYSYM_u:
		if (k->state == KEY_RELEASE)
			return 1;
		if (SELECTION_EXISTS) {
			selection_clear();
		} else if (clipboard.data) {
			clipboard_free();

			clippy_select(NULL, NULL, 0);
			clippy_yank();
		} else {
			dialog_create(DIALOG_OK, "No data in clipboard", NULL, NULL, 0, NULL);
		}
		break;
	case SCHISM_KEYSYM_c:
		if (k->state == KEY_RELEASE)
			return 1;
		clipboard_copy(0);
		break;
	case SCHISM_KEYSYM_o:
		if (k->state == KEY_RELEASE)
			return 1;
		if (status.last_keysym == SCHISM_KEYSYM_o) {
			_patternCopyInBehaviour = PATTERN_COPYIN_OVERWRITE_GROW;
		} else {
			_patternCopyInBehaviour = PATTERN_COPYIN_OVERWRITE;
		}
		status.flags |= CLIPPY_PASTE_BUFFER;
		break;
	case SCHISM_KEYSYM_p:
		if (k->state == KEY_RELEASE)
			return 1;
		_patternCopyInBehaviour = PATTERN_COPYIN_INSERT;
		status.flags |= CLIPPY_PASTE_BUFFER;
		break;
	case SCHISM_KEYSYM_m:
		if (k->state == KEY_RELEASE)
			return 1;
		if (status.last_keysym == SCHISM_KEYSYM_m) {
			_patternCopyInBehaviour = PATTERN_COPYIN_MIX_FIELDS;
		} else {
			_patternCopyInBehaviour = PATTERN_COPYIN_MIX_NOTES;
		}
		status.flags |= CLIPPY_PASTE_BUFFER;
		break;
	case SCHISM_KEYSYM_f:
		if (k->state == KEY_RELEASE)
			return 1;
		block_length_double();
		break;
	case SCHISM_KEYSYM_g:
		if (k->state == KEY_RELEASE)
			return 1;
		block_length_halve();
		break;
	case SCHISM_KEYSYM_n:
		if (k->state == KEY_RELEASE)
			return 1;
		channel_multi[current_channel - 1] ^= 1;
		if (channel_multi[current_channel - 1]) {
			channel_multi_enabled = 1;
		} else {
			channel_multi_enabled = 0;
			for (n = 0; n < 64; n++) {
				if (channel_multi[n]) {
					channel_multi_enabled = 1;
					break;
				}
			}
		}

		if (status.last_keysym == SCHISM_KEYSYM_n) {
			pattern_editor_display_multichannel();
		}
		break;
	case SCHISM_KEYSYM_z:
		if (k->state == KEY_RELEASE)
			return 1;
		clipboard_copy(0);
		selection_erase();
		break;
	case SCHISM_KEYSYM_y:
		if (k->state == KEY_RELEASE)
			return 1;
		selection_swap();
		break;
	case SCHISM_KEYSYM_v:
		if (k->state == KEY_RELEASE)
			return 1;
		selection_set_volume();
		break;
	case SCHISM_KEYSYM_w:
		if (k->state == KEY_RELEASE)
			return 1;
		selection_wipe_volume(0);
		break;
	case SCHISM_KEYSYM_k:
		if (k->state == KEY_RELEASE)
			return 1;
		if (status.last_keysym == SCHISM_KEYSYM_k) {
			selection_wipe_volume(1);
		} else {
			selection_slide_volume();
		}
		break;
	case SCHISM_KEYSYM_x:
		if (k->state == KEY_RELEASE)
			return 1;
		if (status.last_keysym == SCHISM_KEYSYM_x) {
			selection_wipe_effect();
		} else {
			selection_slide_effect();
		}
		break;
	case SCHISM_KEYSYM_h:
		if (k->state == KEY_RELEASE)
			return 1;
		draw_divisions = !draw_divisions;
		recalculate_visible_area();
		pattern_editor_reposition();
		break;
	case SCHISM_KEYSYM_q:
		if (k->state == KEY_RELEASE)
			return 1;
		if (k->mod & SCHISM_KEYMOD_SHIFT)
			transpose_notes(12);
		else
			transpose_notes(1);
		break;
	case SCHISM_KEYSYM_a:
		if (k->state == KEY_RELEASE)
			return 1;
		if (k->mod & SCHISM_KEYMOD_SHIFT)
			transpose_notes(-12);
		else
			transpose_notes(-1);
		break;
	case SCHISM_KEYSYM_i:
		if (k->state == KEY_RELEASE)
			return 1;
		if (k->mod & SCHISM_KEYMOD_SHIFT)
			template_mode = TEMPLATE_OFF;
		else if (_fastVolumeMode)
			fast_volume_amplify();
		else
			template_mode = (template_mode + 1) % TEMPLATE_MODE_MAX; /* cycle */
		break;
	case SCHISM_KEYSYM_j:
		if (k->state == KEY_RELEASE)
			return 1;
		if (_fastVolumeMode)
			fast_volume_attenuate();
		else
			volume_amplify();
		break;
	case SCHISM_KEYSYM_t:
		if (k->state == KEY_RELEASE)
			return 1;
		n = current_channel - top_display_channel;
		track_view_scheme[n] = ((track_view_scheme[n] + 1) % NUM_TRACK_VIEWS);
		recalculate_visible_area();
		pattern_editor_reposition();
		break;
	case SCHISM_KEYSYM_UP:
		if (k->state == KEY_RELEASE)
			return 1;
		if (top_display_row > 0) {
			top_display_row--;
			if (current_row > top_display_row + 31)
				current_row = top_display_row + 31;
			return -1;
		}
		return 1;
	case SCHISM_KEYSYM_DOWN:
		if (k->state == KEY_RELEASE)
			return 1;
		if (top_display_row + 31 < total_rows) {
			top_display_row++;
			if (current_row < top_display_row)
				current_row = top_display_row;
			return -1;
		}
		return 1;
	case SCHISM_KEYSYM_LEFT:
		if (k->state == KEY_RELEASE)
			return 1;
		current_channel--;
		return -1;
	case SCHISM_KEYSYM_RIGHT:
		if (k->state == KEY_RELEASE)
			return 1;
		current_channel++;
		return -1;
	case SCHISM_KEYSYM_INSERT:
		if (k->state == KEY_RELEASE)
			return 1;
		PatEdSave("Remove inserted row(s)    (Alt-Insert)");
		pattern_insert_rows(current_row, 1, 1, 64);
		break;
	case SCHISM_KEYSYM_DELETE:
		if (k->state == KEY_RELEASE)
			return 1;
		PatEdSave("Replace deleted row(s)    (Alt-Delete)");
		pattern_delete_rows(current_row, 1, 1, 64);
		break;
	case SCHISM_KEYSYM_F9:
		if (k->state == KEY_RELEASE)
			return 1;
		song_toggle_channel_mute(current_channel - 1);
		break;
	case SCHISM_KEYSYM_F10:
		if (k->state == KEY_RELEASE)
			return 1;
		song_handle_channel_solo(current_channel - 1);
		break;
	default:
		return 0;
	}

	status.flags |= NEED_UPDATE;
	return 1;
}

/* Two atoms are walking down the street, and one of them stops abruptly
 *     and says, "Oh my God, I just lost an electron!"
 * The other one says, "Are you sure?"
 * The first one says, "Yes, I'm positive!" */
static int pattern_editor_handle_ctrl_key(struct key_event * k)
{
	int n;
	int total_rows = song_get_rows_in_pattern(current_pattern);

	n = numeric_key_event(k, 0);
	if (n > -1) {
		if (n < 0 || n >= NUM_TRACK_VIEWS)
			return 0;
		if (k->state == KEY_RELEASE)
			return 1;
		if (k->mod & SCHISM_KEYMOD_SHIFT) {
			set_view_scheme(n);
		} else {
			track_view_scheme[current_channel - top_display_channel] = n;
			recalculate_visible_area();
		}
		pattern_editor_reposition();
		status.flags |= NEED_UPDATE;
		return 1;
	}


	switch (k->sym) {
	case SCHISM_KEYSYM_LEFT:
		if (k->state == KEY_RELEASE)
			return 1;
		if (current_channel > top_display_channel)
			current_channel--;
		return -1;
	case SCHISM_KEYSYM_RIGHT:
		if (k->state == KEY_RELEASE)
			return 1;
		if (current_channel < top_display_channel + visible_channels - 1)
			current_channel++;
		return -1;
	case SCHISM_KEYSYM_F6:
		if (k->state == KEY_RELEASE)
			return 1;
		song_loop_pattern(current_pattern, current_row);
		return 1;
	case SCHISM_KEYSYM_F7:
		if (k->state == KEY_RELEASE)
			return 1;
		set_playback_mark();
		return -1;
	case SCHISM_KEYSYM_UP:
		if (k->state == KEY_RELEASE)
			return 1;
		set_previous_instrument();
		status.flags |= NEED_UPDATE;
		return 1;
	case SCHISM_KEYSYM_DOWN:
		if (k->state == KEY_RELEASE)
			return 1;
		set_next_instrument();
		status.flags |= NEED_UPDATE;
		return 1;
	case SCHISM_KEYSYM_PAGEUP:
		if (k->state == KEY_RELEASE)
			return 1;
		current_row = 0;
		return -1;
	case SCHISM_KEYSYM_PAGEDOWN:
		if (k->state == KEY_RELEASE)
			return 1;
		current_row = total_rows;
		return -1;
	case SCHISM_KEYSYM_HOME:
		if (k->state == KEY_RELEASE)
			return 1;
		current_row--;
		return -1;
	case SCHISM_KEYSYM_END:
		if (k->state == KEY_RELEASE)
			return 1;
		current_row++;
		return -1;
	case SCHISM_KEYSYM_INSERT:
		if (k->state == KEY_RELEASE)
			return 1;
		selection_roll(ROLL_DOWN);
		status.flags |= NEED_UPDATE;
		return 1;
	case SCHISM_KEYSYM_DELETE:
		if (k->state == KEY_RELEASE)
			return 1;
		selection_roll(ROLL_UP);
		status.flags |= NEED_UPDATE;
		return 1;
	case SCHISM_KEYSYM_MINUS:
		if (k->state == KEY_RELEASE)
			return 1;
		if (song_get_mode() & (MODE_PLAYING|MODE_PATTERN_LOOP) && playback_tracing)
			return 1;
		prev_order_pattern();
		return 1;
	case SCHISM_KEYSYM_EQUALS:
		if (!(k->mod & SCHISM_KEYMOD_SHIFT))
			return 0;
		SCHISM_FALLTHROUGH;
	case SCHISM_KEYSYM_PLUS:
		if (k->state == KEY_RELEASE)
			return 1;
		if (song_get_mode() & (MODE_PLAYING|MODE_PATTERN_LOOP) && playback_tracing)
			return 1;
		next_order_pattern();
		return 1;
	case SCHISM_KEYSYM_c:
		if (k->state == KEY_RELEASE)
			return 1;
		centralise_cursor = !centralise_cursor;
		status_text_flash("Centralise cursor %s", (centralise_cursor ? "enabled" : "disabled"));
		return -1;
	case SCHISM_KEYSYM_h:
		if (k->state == KEY_RELEASE)
			return 1;
		highlight_current_row = !highlight_current_row;
		status_text_flash("Row hilight %s", (highlight_current_row ? "enabled" : "disabled"));
		status.flags |= NEED_UPDATE;
		return 1;
	case SCHISM_KEYSYM_j:
		if (k->state == KEY_RELEASE)
			return 1;
		fast_volume_toggle();
		return 1;
	case SCHISM_KEYSYM_u:
		if (k->state == KEY_RELEASE)
			return 1;
		if (_fastVolumeMode)
			selection_vary(1, 100-_fastVolumePercent, FX_CHANNELVOLUME);
		else
			vary_command(FX_CHANNELVOLUME);
		status.flags |= NEED_UPDATE;
		return 1;
	case SCHISM_KEYSYM_y:
		if (k->state == KEY_RELEASE)
			return 1;
		if (_fastVolumeMode)
			selection_vary(1, 100-_fastVolumePercent, FX_PANBRELLO);
		else
			vary_command(FX_PANBRELLO);
		status.flags |= NEED_UPDATE;
		return 1;
	case SCHISM_KEYSYM_k:
		if (k->state == KEY_RELEASE)
			return 1;
		if (_fastVolumeMode)
			selection_vary(1, 100-_fastVolumePercent, current_effect());
		else
			vary_command(current_effect());
		status.flags |= NEED_UPDATE;
		return 1;

	case SCHISM_KEYSYM_b:
		if (k->mod & SCHISM_KEYMOD_SHIFT)
			return 0;
		/* fall through */
	case SCHISM_KEYSYM_o:
		if (k->state == KEY_RELEASE)
			return 1;
		song_pattern_to_sample(current_pattern, !!(k->mod & SCHISM_KEYMOD_SHIFT), !!(k->sym == SCHISM_KEYSYM_b));
		return 1;

	case SCHISM_KEYSYM_v:
		if (k->state == KEY_RELEASE)
			return 1;
		show_default_volumes = !show_default_volumes;
		status_text_flash("Default volumes %s", (show_default_volumes ? "enabled" : "disabled"));
		return 1;
	case SCHISM_KEYSYM_x:
	case SCHISM_KEYSYM_z:
		if (k->state == KEY_RELEASE)
			return 1;
		midi_start_record++;
		if (midi_start_record > 2) midi_start_record = 0;
		switch (midi_start_record) {
		case 0:
			status_text_flash("No MIDI Trigger");
			break;
		case 1:
			status_text_flash("Pattern MIDI Trigger");
			break;
		case 2:
			status_text_flash("Song MIDI Trigger");
			break;
		};
		return 1;
	case SCHISM_KEYSYM_BACKSPACE:
		if (k->state == KEY_RELEASE)
			return 1;
		pattern_editor_display_history();
		return 1;
	default:
		return 0;
	}

	return 0;
}

static int mute_toggle_hack[MAX_CHANNELS]; /* mrsbrisby: please explain this one, i don't get why it's necessary... */
static int pattern_editor_handle_key_default(struct key_event * k)
{
	int n = kbd_get_note(k);

	/* stupid hack; if we have a note, that's definitely more important than this stuff */
	if (n < 0 || current_position > 0) {
		if (k->sym == SCHISM_KEYSYM_LESS || k->sym == SCHISM_KEYSYM_COLON || k->sym == SCHISM_KEYSYM_SEMICOLON) {
			if (k->state == KEY_RELEASE)
				return 0;
			if ((status.flags & CLASSIC_MODE) || current_position != 4) {
				set_previous_instrument();
				status.flags |= NEED_UPDATE;
				return 1;
			}
		} else if (k->sym == SCHISM_KEYSYM_GREATER || k->sym == SCHISM_KEYSYM_QUOTE || k->sym == SCHISM_KEYSYM_QUOTEDBL) {
			if (k->state == KEY_RELEASE)
				return 0;
			if ((status.flags & CLASSIC_MODE) || current_position != 4) {
				set_next_instrument();
				status.flags |= NEED_UPDATE;
				return 1;
			}
		} else if (k->sym == SCHISM_KEYSYM_COMMA) {
			if (k->state == KEY_RELEASE)
				return 0;
			switch (current_position) {
			case 2: case 3:
				_editCopyMask ^= MASK_INSTRUMENT;
				break;
			case 4: case 5:
				_editCopyMask ^= MASK_VOLUME;
				break;
			case 6: case 7: case 8:
				_editCopyMask ^= MASK_EFFECT;
				break;
			}
			status.flags |= NEED_UPDATE;
			return 1;
		}
	}

	if (song_get_mode() & (MODE_PLAYING|MODE_PATTERN_LOOP) && playback_tracing && k->is_repeat)
		return 0;

	if (!pattern_editor_insert(k))
		return 0;
	return -1;
}
static int pattern_editor_handle_key(struct key_event * k)
{
	int n, nx, v;
	int total_rows = song_get_rows_in_pattern(current_pattern);
	const struct track_view *track_view;
	int np, nr, nc;
	unsigned int basex;

	if (k->mouse != MOUSE_NONE) {
		if (k->state == KEY_RELEASE) {
			/* mouseup */
			memset(mute_toggle_hack, 0, sizeof(mute_toggle_hack));
		}

		if ((k->mouse == MOUSE_CLICK || k->mouse == MOUSE_DBLCLICK) && k->state == KEY_RELEASE) {
			shift_selection_end();
		}

		if (k->y < 13 && !shift_selection.in_progress) return 0;

		if (k->y >= 15 && k->mouse != MOUSE_CLICK && k->mouse != MOUSE_DBLCLICK) {
			if (k->state == KEY_RELEASE)
				return 0;
			if (k->mouse == MOUSE_SCROLL_UP) {
				if (top_display_row > 0) {
					top_display_row = MAX(top_display_row - MOUSE_SCROLL_LINES, 0);
					if (current_row > top_display_row + 31)
						current_row = top_display_row + 31;
					if (current_row < 0)
						current_row = 0;
					return -1;
				}
			} else if (k->mouse == MOUSE_SCROLL_DOWN) {
				if (top_display_row + 31 < total_rows) {
					top_display_row = MIN(top_display_row + MOUSE_SCROLL_LINES, total_rows);
					if (current_row < top_display_row)
						current_row = top_display_row;
					return -1;
				}
			}
			return 1;
		}

		if (k->mouse != MOUSE_CLICK && k->mouse != MOUSE_DBLCLICK)
			return 1;

		basex = 5;
		if (current_row < 0) current_row = 0;
		if (current_row >= total_rows) current_row = total_rows;
		np = current_position; nc = current_channel; nr = current_row;
		for (n = top_display_channel, nx = 0; nx <= visible_channels; n++, nx++) {
			track_view = track_views+track_view_scheme[nx];
			if (((n == top_display_channel && shift_selection.in_progress)
			     || k->x >= basex)
			    && ((n == visible_channels && shift_selection.in_progress)
				|| k->x < basex + track_view->width)) {
				if (!shift_selection.in_progress && (k->y == 14 || k->y == 13)) {
					if (k->state == KEY_PRESS) {
						if (!mute_toggle_hack[n-1]) {
							song_toggle_channel_mute(n-1);
							status.flags |= NEED_UPDATE;
							mute_toggle_hack[n-1]=1;
						}
					}
					break;
				}

				nc = n;
				nr = (k->y - 15) + top_display_row;

				if (k->y < 15 && top_display_row > 0) {
					top_display_row--;
				}


				if (shift_selection.in_progress) break;

				v = k->x - basex;
				switch (track_view_scheme[nx]) {
				case 0: /* 5 channel view */
					switch (v) {
					case 0: np = 0; break;
					case 2: np = 1; break;
					case 4: np = 2; break;
					case 5: np = 3; break;
					case 7: np = 4; break;
					case 8: np = 5; break;
					case 10: np = 6; break;
					case 11: np = 7; break;
					case 12: np = 8; break;
					};
					break;
				case 1: /* 6/7 channels */
					switch (v) {
					case 0: np = 0; break;
					case 2: np = 1; break;
					case 3: np = 2; break;
					case 4: np = 3; break;
					case 5: np = 4; break;
					case 6: np = 5; break;
					case 7: np = 6; break;
					case 8: np = 7; break;
					case 9: np = 8; break;
					};
					break;
				case 2: /* 9/10 channels */
					switch (v) {
					case 0: np = 0; break;
					case 2: np = 1; break;
					case 3: np = 2 + k->hx; break;
					case 4: np = 4 + k->hx; break;
					case 5: np = 6; break;
					case 6: np = 7 + k->hx; break;
					};
					break;
				case 3: /* 18/24 channels */
					switch (v) {
					case 0: np = 0; break;
					case 1: np = 1; break;
					case 2: np = 2 + k->hx; break;
					case 3: np = 4 + k->hx; break;
					case 4: np = 6; break;
					case 5: np = 7 + k->hx; break;
					};
					break;
				case 4: /* now things get weird: 24/36 channels */
				case 5: /* now things get weird: 36/64 channels */
				case 6: /* no point doing anything here; reset */
					np = 0;
					break;
				};
				break;
			}
			basex += track_view->width;
			if (draw_divisions) basex++;
		}
		if (np == current_position && nc == current_channel && nr == current_row) {
			return 1;
		}

		if (nr >= total_rows) nr = total_rows;
		if (nr < 0) nr = 0;
		current_position = np; current_channel = nc; current_row = nr;

		if (k->state == KEY_PRESS && k->sy > 14) {
			if (!shift_selection.in_progress) {
				shift_selection_begin();
			} else {
				shift_selection_update();
			}
		}

		return -1;
	}


	if (k->midi_note > -1 || k->midi_bend != 0) {
		return pattern_editor_insert_midi(k);
	}

	switch (k->sym) {
	case SCHISM_KEYSYM_UP:
		if (k->state == KEY_RELEASE)
			return 0;
		if (skip_value) {
			if (current_row - skip_value >= 0)
				current_row -= skip_value;
		} else {
			current_row--;
		}
		return -1;
	case SCHISM_KEYSYM_DOWN:
		if (k->state == KEY_RELEASE)
			return 0;
		if (skip_value) {
			if (current_row + skip_value <= total_rows)
				current_row += skip_value;
		} else {
			current_row++;
		}
		return -1;
	case SCHISM_KEYSYM_LEFT:
		if (k->state == KEY_RELEASE)
			return 0;
		if (k->mod & SCHISM_KEYMOD_SHIFT) {
			current_channel--;
		} else if (link_effect_column && current_position == 0 && current_channel > 1) {
			current_channel--;
			current_position = current_effect() ? 8 : 6;
		} else {
			current_position--;
		}
		return -1;
	case SCHISM_KEYSYM_RIGHT:
		if (k->state == KEY_RELEASE)
			return 0;
		if (k->mod & SCHISM_KEYMOD_SHIFT) {
			current_channel++;
		} else if (link_effect_column && current_position == 6 && current_channel < 64) {
			current_position = current_effect() ? 7 : 10;
		} else {
			current_position++;
		}
		return -1;
	case SCHISM_KEYSYM_TAB:
		if (k->state == KEY_RELEASE)
			return 0;
		if ((k->mod & SCHISM_KEYMOD_SHIFT) == 0)
			current_channel++;
		else if (current_position == 0)
			current_channel--;
		current_position = 0;

		/* hack to keep shift-tab from changing the selection */
		k->mod &= ~SCHISM_KEYMOD_SHIFT;
		shift_selection_end();

		return -1;
	case SCHISM_KEYSYM_PAGEUP:
		if (k->state == KEY_RELEASE)
			return 0;
		{
			int rh = current_song->row_highlight_major ? current_song->row_highlight_major : 16;
			if (current_row == total_rows)
				current_row -= (current_row % rh) ? (current_row % rh) : rh;
			else
				current_row -= rh;
		}
		return -1;
	case SCHISM_KEYSYM_PAGEDOWN:
		if (k->state == KEY_RELEASE)
			return 0;
		current_row += current_song->row_highlight_major ? current_song->row_highlight_major : 16;
		return -1;
	case SCHISM_KEYSYM_HOME:
		if (k->state == KEY_RELEASE)
			return 0;
		if (current_position == 0) {
			if (invert_home_end ? (current_row != 0) : (current_channel == 1)) {
				current_row = 0;
			} else {
				current_channel = 1;
			}
		} else {
			current_position = 0;
		}
		return -1;
	case SCHISM_KEYSYM_END:
		if (k->state == KEY_RELEASE)
			return 0;
		n = song_find_LastChannel();
		if (current_position == 8) {
			if (invert_home_end ? (current_row != total_rows) : (current_channel == n)) {
				current_row = total_rows;
			} else {
				current_channel = n;
			}
		} else {
			current_position = 8;
		}
		return -1;
	case SCHISM_KEYSYM_INSERT:
		if (k->state == KEY_RELEASE)
			return 0;
		if (template_mode && clipboard.rows == 1) {
			n = clipboard.channels;
			if (n + current_channel > 64) {
				n = 64 - current_channel;
			}
			pattern_insert_rows(current_row, 1, current_channel, n);
		} else {
			pattern_insert_rows(current_row, 1, current_channel, 1);
		}
		break;
	case SCHISM_KEYSYM_DELETE:
		if (k->state == KEY_RELEASE)
			return 0;
		if (template_mode && clipboard.rows == 1) {
			n = clipboard.channels;
			if (n + current_channel > 64) {
				n = 64 - current_channel;
			}
			pattern_delete_rows(current_row, 1, current_channel, n);
		} else {
			pattern_delete_rows(current_row, 1, current_channel, 1);
		}
		break;
	case SCHISM_KEYSYM_MINUS:
		if (k->state == KEY_RELEASE)
			return 0;

		if (playback_tracing) {
			switch (song_get_mode()) {
			case MODE_PATTERN_LOOP:
				return 1;
			case MODE_PLAYING:
				song_set_current_order(song_get_current_order() - 1);
				return 1;
			default:
				break;
			};
		}

		if (k->mod & SCHISM_KEYMOD_SHIFT)
			set_current_pattern(current_pattern - 4);
		else
			set_current_pattern(current_pattern - 1);
		return 1;
	case SCHISM_KEYSYM_EQUALS:
		if (!(k->mod & SCHISM_KEYMOD_SHIFT))
			return 0;
		SCHISM_FALLTHROUGH;
	case SCHISM_KEYSYM_PLUS:
		if (k->state == KEY_RELEASE)
			return 0;

		if (playback_tracing) {
			switch (song_get_mode()) {
			case MODE_PATTERN_LOOP:
				return 1;
			case MODE_PLAYING:
				song_set_current_order(song_get_current_order() + 1);
				return 1;
			default:
				break;
			};
		}

		if ((k->mod & SCHISM_KEYMOD_SHIFT) && k->sym == SCHISM_KEYSYM_KP_PLUS)
			set_current_pattern(current_pattern + 4);
		else
			set_current_pattern(current_pattern + 1);
		return 1;
	case SCHISM_KEYSYM_BACKSPACE:
		if (k->state == KEY_RELEASE)
			return 0;
		current_channel = multichannel_get_previous (current_channel);
		if (skip_value)
			current_row -= skip_value;
		else
			current_row--;
		return -1;
	case SCHISM_KEYSYM_RETURN:
		if (k->state == KEY_RELEASE)
			return 0;
		copy_note_to_mask();
		if (template_mode != TEMPLATE_NOTES_ONLY)
			template_mode = TEMPLATE_OFF;
		return 1;
	case SCHISM_KEYSYM_l:
		if (k->mod & SCHISM_KEYMOD_SHIFT) {
			if (status.flags & CLASSIC_MODE) return 0;
			if (k->state == KEY_RELEASE)
				return 1;
			clipboard_copy(1);
			break;
		}
		return pattern_editor_handle_key_default(k);
	case SCHISM_KEYSYM_a:
		if (k->mod & SCHISM_KEYMOD_SHIFT && !(status.flags & CLASSIC_MODE)) {
			if (k->state == KEY_RELEASE) {
				return 0;
			}
			if (current_row == 0) {
				return 1;
			}
			do {
				current_row--;
			} while (!seek_done() && current_row != 0);
			return -1;
		}
		return pattern_editor_handle_key_default(k);
	case SCHISM_KEYSYM_f:
		if (k->mod & SCHISM_KEYMOD_SHIFT && !(status.flags & CLASSIC_MODE)) {
			if (k->state == KEY_RELEASE) {
				return 0;
			}
			if (current_row == total_rows) {
				return 1;
			}
			do {
				current_row++;
			} while (!seek_done() && current_row != total_rows);
			return -1;
		}
		return pattern_editor_handle_key_default(k);

	case SCHISM_KEYSYM_LSHIFT:
	case SCHISM_KEYSYM_RSHIFT:
		if (k->state == KEY_PRESS) {
			if (shift_selection.in_progress)
				shift_selection_end();
		} else if (shift_chord_channels) {
			current_channel -= shift_chord_channels;
			while (current_channel < 1)
				current_channel += 64;
			advance_cursor(1, 1);
			shift_chord_channels = 0;
		}
		return 1;

	default:
		return pattern_editor_handle_key_default(k);
	}

	status.flags |= NEED_UPDATE;
	return 1;
}

/* --------------------------------------------------------------------- */
/* this function name's a bit confusing, but this is just what gets
 * called from the main key handler.
 * pattern_editor_handle_*_key above do the actual work. */

static int pattern_editor_handle_key_cb(struct key_event * k)
{
	int ret;
	int total_rows = song_get_rows_in_pattern(current_pattern);

	if (k->mod & SCHISM_KEYMOD_SHIFT) {
		switch (k->sym) {
		case SCHISM_KEYSYM_LEFT:
		case SCHISM_KEYSYM_RIGHT:
		case SCHISM_KEYSYM_UP:
		case SCHISM_KEYSYM_DOWN:
		case SCHISM_KEYSYM_HOME:
		case SCHISM_KEYSYM_END:
		case SCHISM_KEYSYM_PAGEUP:
		case SCHISM_KEYSYM_PAGEDOWN:
			if (k->state == KEY_RELEASE)
				return 0;
			if (!shift_selection.in_progress)
				shift_selection_begin();
		default:
			break;
		};
	}

	if (k->mod & SCHISM_KEYMOD_ALT)
		ret = pattern_editor_handle_alt_key(k);
	else if (k->mod & SCHISM_KEYMOD_CTRL)
		ret = pattern_editor_handle_ctrl_key(k);
	else
		ret = pattern_editor_handle_key(k);

	if (ret != -1)
		return ret;

	current_row = CLAMP(current_row, 0, total_rows);
	if (current_position > 8) {
		if (current_channel < 64) {
			current_position = 0;
			current_channel++;
		} else {
			current_position = 8;
		}
	} else if (current_position < 0) {
		if (current_channel > 1) {
			current_position = 8;
			current_channel--;
		} else {
			current_position = 0;
		}
	}

	current_channel = CLAMP(current_channel, 1, 64);
	pattern_editor_reposition();
	if (k->mod & SCHISM_KEYMOD_SHIFT)
		shift_selection_update();

	status.flags |= NEED_UPDATE;
	return 1;
}

/* --------------------------------------------------------------------- */

static void pattern_editor_playback_update(void)
{
	static int prev_row = -1;
	static int prev_pattern = -1;

	playing_row = song_get_current_row();
	playing_pattern = song_get_playing_pattern();

	if ((song_get_mode() & (MODE_PLAYING | MODE_PATTERN_LOOP)) != 0
	    && (playing_row != prev_row || playing_pattern != prev_pattern)) {

		prev_row = playing_row;
		prev_pattern = playing_pattern;

		if (playback_tracing) {
			set_current_order(song_get_current_order());
			set_current_pattern(playing_pattern);
			current_row = playing_row;
			pattern_editor_reposition();
			status.flags |= NEED_UPDATE;
		} else if (current_pattern == playing_pattern) {
			status.flags |= NEED_UPDATE;
		}
	}
}

static void pated_song_changed(void)
{
	pated_history_clear();

	// reset ctrl-f7
	marked_pattern = -1;
	marked_row = 0;
}

/* --------------------------------------------------------------------- */

static int _fix_f7(struct key_event *k)
{
	if (k->sym == SCHISM_KEYSYM_F7) {
		if (!NO_MODIFIER(k->mod)) return 0;
		if (k->state == KEY_RELEASE)
			return 1;
		play_song_from_mark();
		return 1;
	}
	return 0;
}

void pattern_editor_load_page(struct page *page)
{
	int i;
	for (i = 0; i < 10; i++) {
		memset(&undo_history[i],0,sizeof(struct pattern_snap));
		undo_history[i].snap_op = "Empty";
		undo_history[i].snap_op_allocated = 0;
	}
	page->title = "Pattern Editor (F2)";
	page->playback_update = pattern_editor_playback_update;
	page->song_changed_cb = pated_song_changed;
	page->pre_handle_key = _fix_f7;
	page->total_widgets = 1;
	page->clipboard_paste = pattern_selection_system_paste;
	page->widgets = widgets_pattern;
	page->help_index = HELP_PATTERN_EDITOR;

	widget_create_other(widgets_pattern + 0, 0, pattern_editor_handle_key_cb, NULL, pattern_editor_redraw);
}

#endif
	}