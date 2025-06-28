using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace ChasmTracker;

using ChasmTracker.Clipboard;
using ChasmTracker.Dialogs;
using ChasmTracker.DiskOutput;
using ChasmTracker.Input;
using ChasmTracker.Memory;
using ChasmTracker.MIDI;
using ChasmTracker.Pages;
using ChasmTracker.Pages.TrackViews;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
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

		for (int i = 0; i < 10; i++)
			_undoHistory.Add(new PatternSnap() { SnapOp = "Empty" });
	}

	bool RowIsMajor(int r) => (Song.CurrentSong.RowHighlightMajor != 0) && ((r % Song.CurrentSong.RowHighlightMajor) == 0);
	bool RowIsMinor(int r) => (Song.CurrentSong.RowHighlightMinor != 0) && ((r % Song.CurrentSong.RowHighlightMinor) == 0);
	bool RowIsHighlight(int r) => RowIsMinor(r) || RowIsMajor(r);

	/* this is actually used by pattern-view.c */
	bool _showDefaultVolumes = false;

	int CurrentInstrument
	{
		get
		{
			return
				Song.CurrentSong.IsInstrumentMode
				? AllPages.InstrumentList.CurrentInstrument
				: AllPages.SampleList.CurrentSample;
		}
		set
		{
			if (Song.CurrentSong.IsInstrumentMode)
				AllPages.InstrumentList.CurrentInstrument = value;
			else
				AllPages.SampleList.CurrentSample = value;
		}
	}

	static Random s_rnd = new Random();

	/* --------------------------------------------------------------------- */
	/* The (way too many) static variables */

	int _midiStartRecord = 0;

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

	bool _keyjazzNoteOff = false;      /* issue noteoffs when releasing note */
	bool _keyjazzWriteNoteOff = false; /* write noteoffs when releasing note */
	bool _keyjazzRepeat = true;        /* insert multiple notes on key repeat */
	bool _keyjazzCapsLock = false;     /* keyjazz when capslock is on, not while it is down */

	/* this is, of course, what the current pattern is */
	int _currentPattern = 0;

	public int CurrentPattern
	{
		get => _currentPattern;
		set
		{
			if (!_playbackTracing || !Song.IsPlaying)
				UpdateMagicBytes();

			_currentPattern = value.Clamp(0, 199);

			var pattern = Song.CurrentSong.GetPattern(_currentPattern);

			if (pattern == null)
				return;

			if (_currentRow >= pattern.Rows.Count)
				_currentRow = pattern.Rows.Count - 1;

			if (SelectionExists())
			{
				if (_selection.FirstRow >= pattern.Rows.Count)
					_selection.FirstRow = _selection.LastRow = pattern.Rows.Count - 1;
				else if (_selection.LastRow >= pattern.Rows.Count)
					_selection.LastRow = pattern.Rows.Count - 1;
			}

			/* save pattern */
			Save("Pattern " + _currentPattern);
			FastSaveUpdate();

			Reposition();
			PatternSelectionSystemCopyOut();

			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	public int CurrentRow
	{
		get => _currentRow;
		set
		{
			int totalRows = Song.CurrentSong.GetPatternLength(_currentPattern);

			_currentRow = value.Clamp(0, totalRows);
			Reposition();
			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	public int CurrentChannel
	{
		get => _currentChannel;
		set => _currentChannel = value.Clamp(0, Constants.MaxChannels);
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

	bool _centraliseCursor = false;
	bool _highlightCurrentRow = false;

	bool _playbackTracing = false;     /* scroll lock */
	bool _midiPlaybackTracing = false;

	public bool PlaybackTracing
	{
		get => _playbackTracing;
		set => _playbackTracing = value;
	}

	public bool MIDIPlaybackTracing
	{
		get => _midiPlaybackTracing;
		set => _midiPlaybackTracing = value;
	}

	bool _panningMode = false;

	/* these should fix the playback tracing position discrepancy */
	int _playingRow = -1;
	int _playingPattern = -1; // TODO: should be in Song??

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
		ShowNoSelectionError();
	}

	/* --------------------------------------------------------------------- */
	/* this is for the multiple track views stuff. */

	static readonly TrackView[] TrackViews =
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

		if (_channelMulti[curChannel])
		{
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
		/* Status.FlashText ("Newly selected channel is %d", (int) i + 1); */
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

		int copyInX;
		int copyInW, copyInH;
		Func<char, byte>? fxMap;
		int x;
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
		copyInX = 0;

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

			if (n.HasNote)
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
						case 'c': n.VolumeEffect = VolumeEffects.VolumeSlideUp; break;
						case 'd': n.VolumeEffect = VolumeEffects.VolumeSlideDown; break;
						case 'a': n.VolumeEffect = VolumeEffects.FineVolumeUp; break;
						case 'b': n.VolumeEffect = VolumeEffects.FineVolumeDown; break;
						case 'u': n.VolumeEffect = VolumeEffects.VibratoSpeed; break;
						case 'h': n.VolumeEffect = VolumeEffects.VibratoDepth; break;
						case 'l': n.VolumeEffect = VolumeEffects.PanningSlideLeft; break;
						case 'r': n.VolumeEffect = VolumeEffects.PanningSlideRight; break;
						case 'g': n.VolumeEffect = VolumeEffects.TonePortamento; break;
						case 'f': n.VolumeEffect = VolumeEffects.PortamentoUp; break;
						case 'e': n.VolumeEffect = VolumeEffects.PortamentoDown; break;
						default: n.VolumeEffect = VolumeEffects.None; n.VolumeParameter = 0; break;
					}
				}
				else
				{
					n.EffectByte = fxMap(str[x]);
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
		SnapCopyFromPattern(new Pattern(ds, copyInW), snap, new Point(0, 0), new Size(copyInW, copyInH));

		return true;
	}

	/* this is here so that we could possibly handle other clipboard formats. */
	readonly Func<string, PatternSnap, bool>[] PasteHandlers;

	bool PatternSelectionSystemPaste(int cb, byte[] data)
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

		return true;
	}

	void ClipboardPasteMixNotes(bool clip, int xlate)
	{
		if (_clipboard.Data == null)
		{
			MessageBox.Show(MessageBoxTypes.OK, "No data in clipboard");
			return;
		}

		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

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
			new Point(_currentChannel - 1, _currentRow),
			new Size(chanWidth, numRows));

		for (int row = 0; row < numRows; row++)
		{
			int pRow = _currentPattern + row;

			for (int chan = 0; chan < chanWidth; chan++)
			{
				int pChan = _currentChannel + chan;

				ref var pNote = ref pattern[pRow][pChan];

				if (pNote.IsBlank)
				{
					pNote = _clipboard[row, chan];
					pNote.SetNoteNote(
						_clipboard[row, chan].Note,
						xlate);

					if (clip)
					{
						pNote.Instrument = (byte)CurrentInstrument;

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
			MessageBox.Show(MessageBoxTypes.OK, "No data in clipboard");
			return;
		}

		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

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

				ref var pNote = ref pattern[pRow][pChan];
				ref var cNote = ref _clipboard[row, chan];

				/* Ick. There ought to be a "conditional move" operator. */
				if (clipboardPrecedence)
				{
					/* clipboard precedence */
					if (cNote.HasNote)
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

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

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
			var curNote = pattern[y][curChannel];

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
					case VolumeEffects.VolumeSlideUp: str.Append('c'); break;
					case VolumeEffects.VolumeSlideDown: str.Append('d'); break;
					case VolumeEffects.FineVolumeUp: str.Append('a'); break;
					case VolumeEffects.FineVolumeDown: str.Append('b'); break;
					case VolumeEffects.VibratoSpeed: str.Append('u'); break;
					case VolumeEffects.VibratoDepth: str.Append('h'); break;
					case VolumeEffects.PanningSlideLeft: str.Append('l'); break;
					case VolumeEffects.PanningSlideRight: str.Append('r'); break;
					case VolumeEffects.TonePortamento: str.Append('g'); break;
					case VolumeEffects.PortamentoUp: str.Append('f'); break;
					case VolumeEffects.PortamentoDown: str.Append('e'); break;
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
				curNote = pattern[y][curChannel];
			}

			str.Append("\r\n");
		}

		Clippy.Select(Widgets[0], str.ToString());
	}

	public override void SetPage()
	{
		/* only one widget, but MAN is it complicated :) */
		// TODO: other widget??
		base.SetPage();
	}

	void SnapPaste(PatternSnap s, Point position, int xlate)
	{
		Status.Flags |= StatusFlags.SongNeedsSave;

		if (position.X < 0)
			position.X = s.Position.X;
		if (position.Y < 0)
			position.Y = s.Position.Y;

		var pattern = Song.CurrentSong.GetPattern(CurrentPattern);

		if (pattern == null)
			return;

		int numRows = pattern.Rows.Count - position.Y;

		if (numRows > s.Rows)
			numRows = s.Rows;

		if (numRows <= 0)
			return;

		int chanWidth = s.Channels;

		if (position.X + chanWidth >= Constants.MaxChannels)
			chanWidth = Constants.MaxChannels - position.X;

		for (int row = 0; row < numRows; row++)
		{
			pattern[position.Y].CopyNotes(
				s.Data,
				row * s.Channels,
				position.X,
				chanWidth);

			if (xlate != 0)
			{
				for (int chan = 0; chan < chanWidth; chan++)
				{
					if (chan + position.X >= 64) /* defensive */
						break;

					pattern[position.Y][chan + position.X].SetNoteNote(
						pattern[position.Y][chan + position.X].Note,
						xlate);
				}
			}
		}

		PatternSelectionSystemCopyOut();
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
			var dialog = Dialog.Show(new FastVolumeDialog(_fastVolumePercent));

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

		var dialog = Dialog.Show(new VolumeAmplifyDialog(_volumePercent));

		dialog.AcceptDialog +=
			newVolumePercent =>
			{
				_volumePercent = newVolumePercent;
				SelectionAmplify(_volumePercent);
			};
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* vary depth */
	Effects _currentVary;

	void VaryCommand(Effects how)
	{
		_currentVary = how;

		var dialog = Dialog.Show(new VaryCommandDialog(_varyDepth));

		dialog.AcceptDialog +=
			newVaryDepth =>
			{
				_varyDepth = newVaryDepth;
				SelectionVary(false, _varyDepth, _currentVary);
			};
	}

	Effects? GetCurrentEffect()
	{
		if (Song.CurrentSong.GetPattern(_currentPattern) is Pattern pattern)
		{
			var effect = pattern[_currentRow][_currentChannel].Effect;

			if (effect != Effects.None)
				return effect;
		}

		return null;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* settings */
	void ConfigSave(ConfigurationFile cfg)
	{
		void SetNumber(string name, int value)
			=> cfg.SetNumber("Pattern Editor", name, value);
		void SetEnum<T>(string name, T value) where T : Enum
			=> cfg.SetNumber("Pattern Editor", name, Convert.ToInt32(value));
		void SetBool(string name, bool value)
			=> cfg.SetBool("Pattern Editor", name, value);
		void SetString(string name, string value)
			=> cfg.SetString("Pattern Editor", name, value);

		SetBool("link_effect_column", _linkEffectColumn);
		SetBool("draw_divisions", _drawDivisions);
		SetBool("centralise_cursor", _centraliseCursor);
		SetBool("highlight_current_row", _highlightCurrentRow);
		SetEnum("edit_copy_mask", _editCopyMask);
		SetNumber("volume_percent", _volumePercent);
		SetNumber("fast_volume_percent", _fastVolumePercent);
		SetBool("fast_volume_mode", _fastVolumeMode);
		SetBool("keyjazz_noteoff", _keyjazzNoteOff);
		SetBool("keyjazz_write_noteoff", _keyjazzWriteNoteOff);
		SetBool("keyjazz_repeat", _keyjazzRepeat);
		SetBool("keyjazz_capslock", _keyjazzCapsLock);
		SetEnum("mask_copy_search_mode", _maskCopySearchMode);
		SetBool("invert_home_end", _invertHomeEnd);

		SetBool("crayola_mode", Status.Flags.HasFlag(StatusFlags.CrayolaMode));

		char[] channelData = new char[Constants.MaxChannels];

		for (int n = 0; n < Constants.MaxChannels; n++)
			channelData[n] = (char)('a' + _trackViewScheme[n]);

		SetString("track_view_scheme", new string(channelData));

		for (int n = 0; n < Constants.MaxChannels; n++)
			channelData[n] = _channelMulti[n] ? 'M' : '-';

		SetString("channel_multi", new string(channelData));
	}

	void ConfigLoad(ConfigurationFile cfg)
	{
		int GetNumber(string name, int defaultValue)
			=> cfg.GetNumber("Pattern Editor", name, defaultValue);
		T GetEnum<T>(string name, T defaultValue) where T : Enum
			=> (T)(object)cfg.GetNumber("Pattern Editor", name, Convert.ToInt32(defaultValue));
		bool GetBool(string name, bool defaultValue)
			=> cfg.GetBool("Pattern Editor", name, defaultValue);
		string GetString(string name, string defaultValue)
			=> cfg.GetString("Pattern Editor", name, defaultValue);

		_linkEffectColumn = GetBool("link_effect_column", false);
		_drawDivisions = GetBool("draw_divisions", true);
		_centraliseCursor = GetBool("centralise_cursor", false);
		_highlightCurrentRow = GetBool("highlight_current_row", false);
		_editCopyMask = GetEnum("edit_copy_mask", PatternEditorMask.Note | PatternEditorMask.Instrument | PatternEditorMask.Volume);
		_volumePercent = GetNumber("volume_percent", 100);
		_fastVolumePercent = GetNumber("fast_volume_percent", 67);
		_fastVolumeMode = GetBool("fast_volume_mode", false);
		_keyjazzNoteOff = GetBool("keyjazz_noteoff", false);
		_keyjazzWriteNoteOff = GetBool("keyjazz_write_noteoff", false);
		_keyjazzRepeat = GetBool("keyjazz_repeat", true);
		_keyjazzCapsLock = GetBool("keyjazz_capslock", false);
		_maskCopySearchMode = GetEnum("mask_copy_search_mode", CopySearchMode.Off);
		_invertHomeEnd = GetBool("invert_home_end", false);

		bool crayolaMode = GetBool("crayola_mode", false);

		if (crayolaMode)
			Status.Flags |= StatusFlags.CrayolaMode;
		else
			Status.Flags &= ~StatusFlags.CrayolaMode;

		string channelData = GetString("track_view_scheme", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

		/* "decode" the track view scheme */
		for (int n = 0; (n < Constants.MaxChannels) && (n < channelData.Length); n++)
		{
			char ch = channelData[n];

			if ((ch >= 'a') && (ch <= 'z'))
				_trackViewScheme[n] = ch - 'a';
			else if ((ch >= 'A') && (ch <= 'Z'))
				_trackViewScheme[n] = ch - 'A';
			else
				_trackViewScheme[n] = -1;

			if ((_trackViewScheme[n] < 0) || (_trackViewScheme[n] >= TrackViews.Length))
			{
				Log.Append(4, "Track view scheme corrupted; using default");

				for (n = 0; n < Constants.MaxChannels; n++)
					_trackViewScheme[n] = TrackViews.Length - 1;

				break;
			}
		}

		channelData = GetString("channel_multi", "----------------------------------------------------------------");

		for (int n = 0; n < Constants.MaxChannels; n++)
			_channelMulti[n] = char.IsLetter(channelData[n]);

		RecalculateVisibleArea();
		Reposition();

		if (Status.CurrentPage is PatternEditorPage)
			Status.Flags |= StatusFlags.NeedUpdate;
	}

	/* --------------------------------------------------------------------- */
	/* selection handling functions */

	bool IsInSelection(int channel, int row)
	{
		return SelectionExists()
			&& (channel >= _selection.FirstChannel) && (channel <= _selection.LastChannel)
			&& (row >= _selection.FirstChannel) && (row <= _selection.LastChannel);
	}

	void NormaliseBlockSelection()
	{
		if (!SelectionExists())
			return;

		if (_selection.FirstChannel > _selection.LastChannel)
			(_selection.FirstChannel, _selection.LastChannel) = (_selection.LastChannel, _selection.FirstChannel);

		if (_selection.FirstRow < 0) _selection.FirstRow = 0;
		if (_selection.LastRow < 0) _selection.LastRow = 0;
		if (_selection.FirstChannel < 1) _selection.FirstChannel = 1;
		if (_selection.LastChannel < 1) _selection.LastChannel = 1;

		if (_selection.FirstRow > _selection.LastRow)
			(_selection.FirstRow, _selection.LastRow) = (_selection.LastRow, _selection.FirstRow);
	}

	void ShiftSelectionBegin()
	{
		_shiftSelection.InProgress = true;
		_shiftSelection.FirstChannel = _currentChannel;
		_shiftSelection.FirstRow = _currentRow;
	}

	void ShiftSelectionUpdate()
	{
		if (_shiftSelection.InProgress)
		{
			_selection.FirstChannel = _shiftSelection.FirstChannel;
			_selection.LastChannel = _currentChannel;
			_selection.FirstRow = _shiftSelection.FirstRow;
			_selection.LastRow = _currentRow;

			NormaliseBlockSelection();
		}
	}

	void ShiftSelectionEnd()
	{
		_shiftSelection.InProgress = false;
		PatternSelectionSystemCopyOut();
	}

	void SelectionClear()
	{
		_selection.FirstChannel = 0;
		PatternSelectionSystemCopyOut();
	}

	// FIXME | this misbehaves if height is an odd number -- e.g. if an odd number
	// FIXME | of rows is selected and 2 * sel_rows overlaps the end of the pattern
	void BlockLengthDouble()
	{
		if (!SelectionExists())
			return;

		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count)
			_selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow)
			_selection.FirstRow = _selection.LastRow;

		int selectedRows = _selection.LastRow - _selection.FirstRow + 1;
		int offset = _selection.FirstChannel - 1;
		int width = _selection.LastChannel - offset;

		// end = first row that is NOT affected
		int destEnd = Math.Min(_selection.FirstRow + 2 * selectedRows, pattern.Rows.Count);
		int height = destEnd - _selection.FirstRow;
		int srcEnd = _selection.FirstRow + height / 2;

		int srcRow = srcEnd - 1;
		int destRow = destEnd - 1;

		HistoryAdd("Undo block length double       (Alt-F)",
			new Point(offset, _selection.FirstRow), new Size(width, height));

		while (destRow > srcRow)
		{
			for (int i = 0; i < width; i++)
				pattern[destRow][offset + i] = SongNote.Empty;

			destRow--;

			for (int i = 0; i < width; i++)
				pattern[destRow][offset + i] = pattern[srcRow][offset + i];

			destRow--;
			srcRow--;
		}

		PatternSelectionSystemCopyOut();
	}

	// FIXME: this should erase the end of the selection if 2 * sel_rows > total_rows
	void BlockLengthHalve()
	{
		if (!SelectionExists())
			return;

		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count)
			_selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow)
			_selection.FirstRow = _selection.LastRow;

		int selectedRows = _selection.LastRow - _selection.FirstRow + 1;
		int offset = _selection.FirstChannel - 1;
		int width = _selection.LastChannel - offset;

		int srcEnd = Math.Min(_selection.FirstRow + 2 * selectedRows, pattern.Rows.Count);
		int height = srcEnd - _selection.FirstRow;
		int srcRow = _selection.FirstRow;
		int destRow = srcRow;

		HistoryAdd("Undo block length halve        (Alt-G)",
			new Point(offset, _selection.FirstRow), new Size(width, height));

		for (int row = 0; row < height / 2; row++)
		{
			pattern[srcRow].CopyNotes(
				pattern[destRow],
				offset,
				offset,
				width);

			srcRow += 2;
			destRow++;
		}

		PatternSelectionSystemCopyOut();
	}

	void SelectionErase()
	{
		if (!SelectionExists())
			return;

		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count) _selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow) _selection.FirstRow = _selection.LastRow;

		HistoryAdd("Undo block cut                 (Alt-Z)",
			new Point(_selection.FirstChannel - 1, _selection.FirstRow),
			new Size(
				(_selection.LastChannel - _selection.FirstChannel) + 1,
				(_selection.LastRow - _selection.FirstRow) + 1));

		for (int row = _selection.FirstRow; row <= _selection.LastRow; row++)
		{
			var patternRow = pattern[row];

			for (int channel = _selection.FirstChannel; channel <= _selection.LastChannel; channel++)
				patternRow[channel] = SongNote.Empty;
		}

		PatternSelectionSystemCopyOut();
	}

	void SelectionSetSample()
	{
		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count) _selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow) _selection.FirstRow = _selection.LastRow;

		HistoryAdd("Undo set sample/instrument     (Alt-S)",
			new Point(_selection.FirstChannel - 1, _selection.FirstRow),
			new Size(
				(_selection.LastChannel - _selection.FirstChannel) + 1,
				(_selection.LastRow - _selection.FirstRow) + 1));

		byte currentInstrument = (byte)CurrentInstrument;

		if (SelectionExists())
		{
			for (int row = _selection.FirstRow; row <= _selection.LastRow; row++)
			{
				for (int chan = _selection.FirstChannel; chan <= _selection.LastChannel; chan++)
				{
					ref var note = ref pattern[row][chan];

					if (note.Instrument != 0)
						note.Instrument = currentInstrument;
				}
			}
		}
		else
		{
			ref var note = ref pattern[_currentRow][_currentChannel];

			if (note.Instrument != 0)
				note.Instrument = currentInstrument;
		}

		PatternSelectionSystemCopyOut();
	}

	void SelectionSwap()
	{
		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count) _selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow) _selection.FirstRow = _selection.LastRow;

		int selectedRows = _selection.LastRow - _selection.FirstRow + 1;
		int selectedChans = _selection.LastChannel - _selection.FirstChannel + 1;

		int affectedWidth = Math.Max(_selection.LastChannel, _currentChannel + selectedChans - 1)
				- Math.Min(_selection.FirstChannel, _currentChannel) + 1;
		int affectedHeight = Math.Max(_selection.LastRow, _currentRow + selectedRows - 1)
				- Math.Min(_selection.FirstRow, _currentRow) + 1;

		/* The minimum combined size for the two blocks is double the number of rows in the selection by
		* double the number of channels. So, if the width and height don't add up, they must overlap. It's
		* of course possible to have the blocks adjacent but not overlapping -- there is only overlap if
		* *both* the width and height are less than double the size. */
		if (affectedWidth < 2 * selectedChans && affectedHeight < 2 * selectedRows)
		{
			MessageBox.Show(MessageBoxTypes.OK, "   Swap blocks overlap    ");
			return;
		}

		if (_currentRow + selectedRows > pattern.Rows.Count || _currentChannel + selectedChans - 1 > Constants.MaxChannels)
		{
			MessageBox.Show(MessageBoxTypes.OK, "   Out of pattern range   ");
			return;
		}

		HistoryAdd("Undo swap block                (Alt-Y)",
			new Point(
				Math.Min(_selection.FirstRow, _currentRow),
				Math.Min(_selection.FirstChannel, _currentChannel) - 1),
			new Size(affectedWidth, affectedHeight));

		for (int row = 0; row <= selectedRows; row++)
		{
			var sRow = pattern[row + _selection.FirstRow];
			var pRow = pattern[row + _currentRow];

			for (int chan = _selection.FirstChannel; chan <= _selection.LastChannel; chan++)
				(sRow[chan], pRow[chan]) = (pRow[chan], sRow[chan]);
		}

		PatternSelectionSystemCopyOut();
	}

	void SelectionSetVolume()
	{
		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count) _selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow) _selection.FirstRow = _selection.LastRow;

		HistoryAdd("Undo set volume/panning        (Alt-V)",
			new Point(_selection.FirstChannel - 1, _selection.FirstRow),
			new Size(
				(_selection.LastChannel - _selection.FirstChannel) + 1,
				(_selection.LastRow - _selection.FirstRow) + 1));

		for (int rowNumber = _selection.FirstRow; rowNumber <= _selection.LastRow; rowNumber++)
		{
			var row = pattern[rowNumber];

			for (int channelNumber = _selection.FirstChannel; channelNumber <= _selection.LastChannel; channelNumber++)
			{
				ref var note = ref row[channelNumber];

				note.VolumeParameter = _maskNote.VolumeParameter;
				note.VolumeEffect = _maskNote.VolumeEffect;
			}
		}

		PatternSelectionSystemCopyOut();
	}

	/* The logic for this one makes my head hurt. */
	void SelectionSlideVolume()
	{
		/* FIXME: if there's no selection, should this display a dialog, or bail silently? */
		/* Impulse Tracker displays a box "No block is marked" */
		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count) _selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow) _selection.FirstRow = _selection.LastRow;

		/* can't slide one row */
		if (_selection.FirstRow == _selection.LastRow)
			return;

		Status.Flags |= StatusFlags.SongNeedsSave;

		HistoryAdd("Undo volume or panning slide   (Alt-K)",
			new Point(_selection.FirstChannel - 1, _selection.FirstRow),
			new Size(
				_selection.LastChannel - _selection.FirstChannel + 1,
				_selection.LastRow - _selection.FirstRow + 1));

		/* the channel loop has to go on the outside for this one */
		for (int channelNumber = _selection.FirstChannel; channelNumber <= _selection.LastChannel; channelNumber++)
		{
			ref var note = ref pattern[_selection.FirstRow][channelNumber];
			ref var lastNote = ref pattern[_selection.LastRow][channelNumber];

			/* valid combinations:
			*     [ volume - volume ]
			*     [panning - panning]
			*     [ volume - none   ] \ only valid if the 'none'
			*     [   none - volume ] / note has a sample number
			* in any other case, no slide occurs. */

			var ve = note.VolumeEffect;
			var lve = lastNote.VolumeEffect;

			int first = note.VolumeParameter;
			int last = lastNote.VolumeParameter;

			/* Note: IT only uses the sample's default volume if there is an instrument number *AND* a
			note. I'm just checking the instrument number, as it's the minimal information needed to
			get the default volume for the instrument.

			Would be nice but way hard to do: if there's a note but no sample number, look back in the
			pattern and use the last sample number in that channel (if there is one). */
			if (ve == VolumeEffects.None)
			{
				if (note.Instrument == 0)
					continue;

				ve = VolumeEffects.Volume;

				/* Modplug hack: volume bit shift */
				first = (Song.CurrentSong.GetSample(note.Instrument)?.Volume ?? 0) >> 2;
			}

			if (lve == VolumeEffects.None)
			{
				if (lastNote.Instrument == 0)
					continue;

				lve = VolumeEffects.Volume;
				last = (Song.CurrentSong.GetSample(lastNote.Instrument)?.Volume ?? 0) >> 2;
			}

			if (!(ve == lve && (ve == VolumeEffects.Volume || ve == VolumeEffects.Panning)))
				continue;

			for (int rowNumber = _selection.FirstRow; rowNumber <= _selection.LastRow; rowNumber++)
			{
				ref var sNote = ref pattern[rowNumber][channelNumber];

				sNote.VolumeEffect = ve;
				sNote.VolumeParameter = unchecked((byte)(((last - first)
						* (rowNumber - _selection.FirstRow)
						/ (_selection.LastRow - _selection.FirstRow)
						) + first));
			}
		}

		PatternSelectionSystemCopyOut();
	}

	public void SelectionWipeVolume(bool reckless)
	{
		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count) _selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow) _selection.FirstRow = _selection.LastRow;

		Status.Flags |= StatusFlags.SongNeedsSave;

		HistoryAdd((reckless
					? "Recover volumes/pannings     (2*Alt-K)"
					: "Replace extra volumes/pannings (Alt-W)"),
			new Point(_selection.FirstChannel - 1, _selection.FirstRow),
			new Size(
				_selection.LastChannel - _selection.FirstChannel + 1,
				_selection.LastRow - _selection.FirstRow + 1));

		for (int rowNumber = _selection.FirstRow; rowNumber <= _selection.LastRow; rowNumber++)
		{
			for (int channelNumber = _selection.FirstChannel; channelNumber <= _selection.LastChannel; channelNumber++)
			{
				ref var note = ref pattern[rowNumber][channelNumber];

				if (reckless || (note.Instrument == 0 && !note.NoteIsNote))
				{
					note.VolumeParameter = 0;
					note.VolumeEffect = VolumeEffects.None;
				}
			}
		}

		PatternSelectionSystemCopyOut();
	}

	byte VaryValue(int ov, int limit, int depth)
	{
		int j;

		j = (int)(limit * s_rnd.NextDouble());
		j = (limit >> 1) - j;
		j = ov + (j * depth / 100);
		if (j < 0) j = 0;
		if (j > limit) j = limit;

		return unchecked((byte)j);
	}

	Effects CommonVariableGroup(Effects effect)
	{
		switch (effect)
		{
			case Effects.PortamentoDown:
			case Effects.PortamentoUp:
			case Effects.TonePortamento:
				return Effects.TonePortamento;
			case Effects.VolumeSlide:
			case Effects.TonePortamentoVolume:
			case Effects.VibratoVolume:
				return Effects.VolumeSlide;
			case Effects.Panning:
			case Effects.PanningSlide:
			case Effects.Panbrello:
				return Effects.Panning;
			default:
				return effect; /* err... */
		}
	}

	void SelectionVary(bool fast, int depth, Effects how)
	{
		/* don't ever vary these things */
		switch (how)
		{
			default:
				if (!how.IsEffect())
					return;
				break;

			case Effects.None:
			case Effects.Special:
			case Effects.Speed:
			case Effects.PositionJump:
			case Effects.PatternBreak:

			case Effects.KeyOff:
			case Effects.SetEnvelopePosition:
			case Effects.Volume:
			case Effects.NoteSlideUp:
			case Effects.NoteSlideDown:
				return;
		}

		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

		Status.Flags |= StatusFlags.SongNeedsSave;

		string varyHow;

		switch (how)
		{
			case Effects.ChannelVolume:
			case Effects.ChannelVolumeSlide:
				varyHow = "Undo volume-channel vary      (Ctrl-U)";
				if (fast) Status.FlashText("Fast volume vary");
				break;
			case Effects.Panning:
			case Effects.PanningSlide:
			case Effects.Panbrello:
				varyHow = "Undo panning vary             (Ctrl-Y)";
				if (fast) Status.FlashText("Fast panning vary");
				break;
			default:
				string lastVary = $"Undo {(char)CommonVariableGroup(how)}xx effect-value".PadRight(28) + "  (Ctrl-K)";
				if (fast) Status.FlashText("Fast " + lastVary.Substring(5).PadRight(21));
				varyHow = lastVary;
				break;
		}

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count) _selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow) _selection.FirstRow = _selection.LastRow;

		HistoryAdd(varyHow,
			new Point(_selection.FirstChannel - 1, _selection.FirstRow),
			new Size(
				_selection.LastChannel - _selection.FirstChannel + 1,
				_selection.LastRow - _selection.FirstRow + 1));

		for (int rowNumber = _selection.FirstRow; rowNumber <= _selection.LastRow; rowNumber++)
		{
			for (int channelNumber = _selection.FirstChannel; channelNumber <= _selection.LastChannel; channelNumber++)
			{
				ref var note = ref pattern[rowNumber][channelNumber];

				if (how == Effects.ChannelVolume || how == Effects.ChannelVolumeSlide)
				{
					if (note.VolumeEffect == VolumeEffects.Volume)
					{
						note.VolumeParameter = VaryValue(note.VolumeParameter, 64, depth);
					}
				}
				if (how == Effects.PanningSlide || how == Effects.Panning || how == Effects.Panbrello)
				{
					if (note.VolumeEffect == VolumeEffects.Panning)
						note.VolumeParameter = VaryValue(note.VolumeParameter, 64, depth);
				}

				var ch = note.Effect;
				if (ch.IsEffect()) continue;
				if (CommonVariableGroup(ch) != CommonVariableGroup(how)) continue;
				switch (ch)
				{
					/* these are .0 0. and .f f. values */
					case Effects.VolumeSlide:
					case Effects.ChannelVolumeSlide:
					case Effects.PanningSlide:
					case Effects.GlobalVolumeSlide:
					case Effects.VibratoVolume:
					case Effects.TonePortamentoVolume:
						if ((note.Parameter & 15) == 15) continue;
						if ((note.Parameter & 0xF0) == (0xF0)) continue;
						if ((note.Parameter & 15) == 0)
							note.Parameter = (byte)((1 + VaryValue(note.Parameter >> 4, 15, depth)) << 4);
						else
							note.Parameter = (byte)(1 + VaryValue(note.Parameter & 15, 15, depth));
						break;
					/* tempo has a slide */
					case Effects.Tempo:
						if ((note.Parameter & 15) == 15) continue;
						if ((note.Parameter & 0xF0) == (0xF0)) continue;
						/* but otherwise it's absolute */
						note.Parameter = (byte)(1 + VaryValue(note.Parameter, 255, depth));
						break;
					/* don't vary .E. and .F. values */
					case Effects.PortamentoDown:
					case Effects.PortamentoUp:
						if ((note.Parameter & 15) == 15) continue;
						if ((note.Parameter & 15) == 14) continue;
						if ((note.Parameter & 0xF0) == (0xF0)) continue;
						if ((note.Parameter & 0xF0) == (0xE0)) continue;
						note.Parameter = (byte)(16 + VaryValue(note.Parameter - 16, 224, depth));
						break;
					/* these are all "xx" commands */
					// FIXME global/channel volume should be limited to 0-128 and 0-64, respectively
					case Effects.TonePortamento:
					case Effects.ChannelVolume:
					case Effects.Offset:
					case Effects.GlobalVolume:
					case Effects.Panning:
						note.Parameter = (byte)(1 + VaryValue(note.Parameter, 255, depth));
						break;
					/* these are all "xy" commands */
					case Effects.Vibrato:
					case Effects.Tremor:
					case Effects.Arpeggio:
					case Effects.Retrigger:
					case Effects.Tremolo:
					case Effects.Panbrello:
					case Effects.FineVibrato:
						note.Parameter = (byte)((1 + VaryValue(note.Parameter & 15, 15, depth))
							| (((1 + VaryValue((note.Parameter >> 4) & 15, 15, depth))) << 4));
						break;
				}
			}
		}

		PatternSelectionSystemCopyOut();
	}

	void SelectionAmplify(int percentage)
	{
		if (!SelectionExists())
			return;

		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count) _selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow) _selection.FirstRow = _selection.LastRow;

		/* it says Alt-J even when Alt-I was used */
		HistoryAdd(
			"Undo volume amplification      (Alt-J)",
			new Point(_selection.FirstChannel - 1, _selection.FirstRow),
			new Size(
				_selection.LastChannel - _selection.FirstChannel + 1,
				_selection.LastRow - _selection.FirstRow + 1));

		for (int rowNumber = _selection.FirstRow; rowNumber <= _selection.LastRow; rowNumber++)
		{
			for (int channelNumber = _selection.FirstChannel; channelNumber <= _selection.LastChannel; channelNumber++)
			{
				ref var note = ref pattern[rowNumber][channelNumber];

				int volume;

				if (note.VolumeEffect == VolumeEffects.None && note.Instrument != 0)
				{
					/* Modplug hack: volume bit shift */
					if (Song.CurrentSong.IsInstrumentMode)
						volume = 64; /* XXX */
					else
						volume = Song.CurrentSong.GetSample(note.Instrument)!.Volume >> 2;
				}
				else if (note.VolumeEffect == VolumeEffects.Volume)
					volume = note.VolumeParameter;
				else
					continue;

				volume *= percentage;
				volume /= 100;

				if (volume > 64) volume = 64;
				else if (volume < 0) volume = 0;

				note.VolumeParameter = (byte)volume;
				note.VolumeEffect = VolumeEffects.Volume;
			}
		}

		PatternSelectionSystemCopyOut();
	}

	void SelectionSlideEffect()
	{
		/* FIXME: if there's no selection, should this display a dialog, or bail silently? */
		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count) _selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow) _selection.FirstRow = _selection.LastRow;

		if (_selection.FirstRow == _selection.LastRow)
			return;

		Status.Flags |= StatusFlags.SongNeedsSave;

		HistoryAdd("Undo effect data slide         (Alt-X)",
			new Point(_selection.FirstChannel - 1, _selection.FirstRow),
			new Size(
				_selection.LastChannel - _selection.FirstChannel + 1,
				_selection.LastRow - _selection.FirstRow + 1));

		/* the channel loop has to go on the outside for this one */
		for (int channelNumber = _selection.FirstChannel; channelNumber <= _selection.LastChannel; channelNumber++)
		{
			int first = pattern[_selection.FirstRow][channelNumber].Parameter;
			int last = pattern[_selection.LastRow][channelNumber].Parameter;

			for (int rowNumber = _selection.FirstRow; rowNumber <= _selection.LastRow; rowNumber++)
			{
				pattern[rowNumber][channelNumber].Parameter = (byte)(
					(((last - first)
					* (rowNumber - _selection.FirstRow)
					/ (_selection.LastRow - _selection.FirstRow)
					) + first));
			}
		}

		PatternSelectionSystemCopyOut();
	}

	void SelectionWipeEffect()
	{
		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count) _selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow) _selection.FirstRow = _selection.LastRow;

		Status.Flags |= StatusFlags.SongNeedsSave;

		HistoryAdd("Recover effects/effect data  (2*Alt-X)",
			new Point(_selection.FirstChannel - 1, _selection.FirstRow),
			new Size(
				_selection.LastChannel - _selection.FirstChannel + 1,
				_selection.LastRow - _selection.FirstRow + 1));

		for (int rowNumber = _selection.FirstRow; rowNumber <= _selection.LastRow; rowNumber++)
		{
			for (int channelNumber = _selection.FirstChannel; channelNumber <= _selection.LastChannel; channelNumber++)
			{
				ref var note = ref pattern[rowNumber][channelNumber];

				note.Effect = Effects.None;
				note.Parameter = 0;
			}
		}

		PatternSelectionSystemCopyOut();
	}

	enum RollDirection
	{
		Down = -1,
		Up = +1,
	}

	void SelectionRoll(RollDirection direction)
	{
		if (!SelectionExists())
		{
			ShowNoSelectionError();
			return;
		}

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_selection.LastRow >= pattern.Rows.Count) _selection.LastRow = pattern.Rows.Count - 1;
		if (_selection.FirstRow > _selection.LastRow) _selection.FirstRow = _selection.LastRow;

		int selectedRows = _selection.LastRow - _selection.FirstRow + 1;
		int selectedChannels = _selection.LastChannel - _selection.FirstChannel + 1;
		if (selectedRows < 2) return;

		var wrapRow = new SongNote[selectedChannels];

		int rowNumber = (direction == RollDirection.Down) ? selectedRows - 1 : 0;

		for (int channelNumber = _selection.FirstChannel; channelNumber <= _selection.LastChannel; channelNumber++)
			wrapRow[channelNumber - _selection.FirstChannel] = pattern[rowNumber][channelNumber];

		for (int n = 1; n < selectedRows; n++, rowNumber += (int)direction)
		{
			int nextRowNumber = rowNumber + (int)direction;

			for (int channelNumber = _selection.FirstChannel; channelNumber <= _selection.LastChannel; channelNumber++)
				pattern[rowNumber][channelNumber] = pattern[nextRowNumber][channelNumber];
		}

		for (int channelNumber = _selection.FirstChannel; channelNumber <= _selection.LastChannel; channelNumber++)
			pattern[rowNumber][channelNumber] = wrapRow[channelNumber - _selection.FirstChannel];

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* Row shifting operations */

	/* A couple of the param names here might seem a bit confusing, so:
	*     what_row = what row to start the insert (generally this would be _currentRow)
	*     num_rows = the number of rows to insert */
	void PatternInsertRows(int whatRow, int numRows, int firstChannel, int channelWidth)
	{
		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		Status.Flags |= StatusFlags.SongNeedsSave;

		if (firstChannel < 1)
			firstChannel = 1;
		if (channelWidth + firstChannel - 1 > 64)
			channelWidth = 64 - firstChannel + 1;

		if (numRows + whatRow > pattern.Rows.Count)
			numRows = pattern.Rows.Count - whatRow;

		/* shift the area down */
		for (int row = pattern.Rows.Count - numRows - 1; row >= whatRow; row--)
		{
			for (int column = 0; column < channelWidth; column++)
			{
				int channel = column + firstChannel;

				pattern[row + numRows][channel] = pattern[row][channel];
			}
		}

		/* clear the inserted rows */
		for (int row = whatRow; row < whatRow + numRows; row++)
		{
			for (int column = 0; column < channelWidth; column++)
			{
				int channel = column + firstChannel;

				pattern[row][channel] = SongNote.Empty;
			}
		}

		PatternSelectionSystemCopyOut();
	}

	/* Same as above, but with a couple subtle differences. */
	void PatternDeleteRows(int whatRow, int numRows, int firstChannel, int channelWidth)
	{
		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		Status.Flags |= StatusFlags.SongNeedsSave;

		if (firstChannel < 1)
			firstChannel = 1;
		if (channelWidth + firstChannel - 1 > 64)
			channelWidth = 64 - firstChannel + 1;

		if (numRows + whatRow > pattern.Rows.Count)
			numRows = pattern.Rows.Count - whatRow;

		/* shift the area up */
		for (int row = whatRow; row <= pattern.Rows.Count - numRows - 1; row++)
		{
			for (int column = 0; column < channelWidth; column++)
			{
				int channel = column + firstChannel;

				pattern[row][channel] = pattern[row + numRows][channel];
			}
		}

		/* clear the last rows */
		for (int row = pattern.Rows.Count - numRows; row < pattern.Rows.Count; row++)
		{
			for (int column = 0; column < channelWidth; column++)
			{
				int channel = column + firstChannel;

				pattern[row][channel] = SongNote.Empty;
			}
		}

		PatternSelectionSystemCopyOut();
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* history/undo */

	void HistoryClear()
	{
		// clear undo history
		_undoHistory.Clear();
	}

	void SnapPaste(PatternSnap s, int x, int y, int xlate)
	{
		Status.Flags |= StatusFlags.SongNeedsSave;

		if (x < 0)
			x = s.Position.X;
		if (y < 0)
			y = s.Position.Y;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		int numRows = pattern.Rows.Count - y;

		if (numRows > s.Rows)
			numRows = s.Rows;
		if (numRows <= 0)
			return;

		int chanWidth = s.Channels;

		if (chanWidth + x >= Constants.MaxChannels)
			chanWidth = Constants.MaxChannels - x;

		for (int row = 0; row < numRows; row++)
		{
			for (int chan = 0; chan < chanWidth; chan++)
			{
				if (chan + x >= Constants.MaxChannels) /* defensive */
					break;

				ref var pNote = ref pattern[y + row][x + chan];

				pNote = s[row, chan];

				if (xlate != 0)
					pNote.SetNoteNote(pNote.Note, xlate);
			}
		}

		PatternSelectionSystemCopyOut();
	}

	void SnapCopyFromPattern(Pattern pattern, PatternSnap s, Point position, Size size)
	{
		MemoryUsage.NotifySongChanged();

		s.Channels = size.Width;
		s.Rows = size.Height;

		s.AllocateData();

		s.Position = position;

		for (int row = 0; row < size.Height && row < pattern.Rows.Count; row++)
			for (int chan = 0; chan < size.Width; chan++)
				s[row, chan] = pattern[row + position.Y][chan + position.X];
	}

	void SnapCopy(PatternSnap s, Point position, Size size)
	{
		if (Song.CurrentSong.GetPattern(CurrentPattern) is Pattern pattern)
			SnapCopyFromPattern(pattern, s, position, size);
	}

	bool SnapHonourMute(PatternSnap s, int baseChannel)
	{
		bool didAny = false;

		bool[] mute = new bool[s.Channels];

		for (int i = 0; i < s.Channels; i++)
			mute[i] = Song.CurrentSong.GetChannel(i + baseChannel)!.Flags.HasFlag(ChannelFlags.Mute);

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

	void HistoryRestore(int n)
	{
		if (n < 0 || n > 9) return;

		SnapPaste(_undoHistory[n], new Point(-1, -1), 0);
	}

	void Save(string descr)
	{
		if (Song.CurrentSong.GetPattern(_currentPattern) is Pattern pattern)
			HistoryAdd(descr, new Point(0, 0), new Size(64, pattern.Rows.Count));
	}

	void HistoryAdd(string descr, Point position, Size size)
	{
		HistoryAdd(false, descr, position, size);
	}

	void HistoryAddGrouped(string descr, Point position, Size size)
	{
		HistoryAdd(true, descr, position, size);
	}

	void HistoryAdd(bool groupedF, string descr, Point position, Size size)
	{
		if (groupedF && (_undoHistory.Count > 0))
		{
			var top = _undoHistory[_undoHistory.Count - 1];

			if ((top.PatternNumber == _currentPattern)
			&& (top.Position == position)
			&& (top.Channels == size.Width)
			&& (top.Rows == size.Height)
			&& (top.SnapOp == descr))
			{
				/* do nothing; use the previous bit of history */
			}
			else
			{
				var newTop = new PatternSnap();

				SnapCopy(newTop, position, size);

				newTop.SnapOp = descr;
				newTop.PatternNumber = _currentPattern;

				_undoHistory.Add(newTop);
			}
		}
	}

	void FastSaveUpdate()
	{
		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern != null)
			SnapCopy(_fastSave, new Point(0, 0), new Size(Constants.MaxChannels, pattern.Rows.Count));
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
			new Point(_selection.FirstChannel - 1, _selection.FirstRow),
			new Size(
				_selection.LastChannel - _selection.FirstChannel + 1,
				_selection.LastRow - _selection.FirstRow + 1));

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
			MessageBox.Show(MessageBoxTypes.OK, "No data in clipboard");
			return;
		}

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

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
				new Point(_currentChannel - 1, _currentRow),
				new Size(chanWidth, numRows));
		}

		SnapPaste(_clipboard, _currentChannel - 1, _currentRow, 0);
	}

	void ClipboardPasteInsert()
	{
		if (_clipboard.Data == null)
		{
			MessageBox.Show(MessageBoxTypes.OK, "No data in clipboard");
			return;
		}

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		Save("Undo paste data                (Alt-P)");

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

	public override bool ClipboardPaste(int cb, byte[] cptr)
	{
		return PatternSelectionSystemPaste(cb, cptr);
	}

	/* --------------------------------------------------------------------- */

	public void Reposition()
	{
		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		if (_currentChannel < _topDisplayChannel)
			_topDisplayChannel = _currentChannel;
		else if (_currentChannel >= _topDisplayChannel + _visibleChannels)
			_topDisplayChannel = _currentChannel - _visibleChannels + 1;

		if (_centraliseCursor)
		{
			if (_currentRow <= 16)
				_topDisplayRow = 0;
			else if (_currentRow + 15 > pattern.Rows.Count)
				_topDisplayRow = pattern.Rows.Count - 31;
			else
				_topDisplayRow = _currentRow - 16;
		}
		else
		{
			/* This could be written better. */
			if (_currentRow < _topDisplayRow)
				_topDisplayRow = _currentRow;
			else if (_currentRow > _topDisplayRow + 31)
				_topDisplayRow = _currentRow - 31;
			if (_topDisplayRow + 31 > pattern.Rows.Count)
				_topDisplayRow = pattern.Rows.Count - 31;
		}

		if (_topDisplayRow < 0)
			_topDisplayRow = 0;
	}

	void AdvanceCursor(bool nextRow, bool multichannel)
	{
		if (nextRow && !(Song.IsPlaying && _playbackTracing))
		{
			var pattern = Song.CurrentSong.GetPattern(_currentPattern);

			if (pattern == null)
				return;

			if (_skipValue > 0)
			{
				if (_currentRow + _skipValue <= pattern.Rows.Count)
				{
					_currentRow += _skipValue;
					Reposition();
				}
			}
			else
			{
				if (_currentChannel < 64)
				{
					_currentChannel++;
				}
				else
				{
					_currentChannel = 1;
					if (_currentRow < pattern.Rows.Count)
						_currentRow++;
				}
				Reposition();
			}
		}

		if (multichannel)
			_currentChannel = MultiChannelGetNext(_currentChannel);
	}

	/* --------------------------------------------------------------------- */

	public void UpdateCurrentRow()
	{
		var numRows = Song.CurrentSong.GetPatternLength(_currentPattern);

		VGAMem.DrawText(_currentRow.ToString("d3"), new Point(12, 7), 5, 0);
		VGAMem.DrawText(numRows.ToString("d3"), new Point(16, 7), 5, 0);
	}

	public void UpdateCurrentPattern()
	{
		int numPatterns = Song.CurrentSong.Patterns.Count;

		VGAMem.DrawText(_currentPattern.ToString("d3"), new Point(12, 6), 5, 0);
		VGAMem.DrawText(numPatterns.ToString("d3"), new Point(16, 6), 5, 0);
	}

	/* --------------------------------------------------------------------- */

	void UpdateMagicBytes()
	{
		for (int i = 1; i <= 99; i++)
		{
			var s = Song.CurrentSong.GetSample(i);

			if (s == null) continue;

			if (s.DiskWriterBoundPattern == _currentPattern)
			{
				DiskWriter.WriteOutSample(i, _currentPattern, true);
				break;
			}
		}
	}

	/* --------------------------------------------------------------------- */

	void SetPlaybackMark()
	{
		if (_markedPattern == _currentPattern && _markedRow == _currentRow)
			_markedPattern = -1;
		else
		{
			_markedPattern = _currentPattern;
			_markedRow = _currentRow;
		}
	}

	void PlaySongFromMarkOrderPan()
	{
		if (_markedPattern == -1)
			AudioPlayback.StartAtOrder(AllPages.OrderList.CurrentOrder, _currentRow);
		else
			AudioPlayback.StartAtPattern(_markedPattern, _markedRow);
	}

	void PlaySongFromMark()
	{
		int newOrder;

		if (_markedPattern != -1)
		{
			AudioPlayback.StartAtPattern(_markedPattern, _markedRow);
			return;
		}

		newOrder = AllPages.OrderList.CurrentOrder;

		while (newOrder < Song.CurrentSong.OrderList.Count)
		{
			if (Song.CurrentSong.OrderList[newOrder] == _currentPattern)
			{
				AllPages.OrderList.CurrentOrder = newOrder;
				AudioPlayback.StartAtOrder(newOrder, _currentRow);
				return;
			}

			newOrder++;
		}

		newOrder = 0;

		while (newOrder < Song.CurrentSong.OrderList.Count)
		{
			if (Song.CurrentSong.OrderList[newOrder] == _currentPattern)
			{
				AllPages.OrderList.CurrentOrder = newOrder;
				AudioPlayback.StartAtOrder(newOrder, _currentRow);
				return;
			}

			newOrder++;
		}

		AudioPlayback.StartAtPattern(_currentPattern, _currentRow);
	}

	/* --------------------------------------------------------------------- */

	void RecalculateVisibleArea()
	{
		int last = 0;

		_visibleWidth = 0;
		_visibleChannels = 0;

		for (int n = 0; n < 64; n++)
		{
			/* shouldn't happen, but might (e.g. if someone was messing with the config file) */
			if (_trackViewScheme[n] >= TrackViews.Length)
				_trackViewScheme[n] = last;
			else
				last = _trackViewScheme[n];

			int newWidth = _visibleWidth + TrackViews[_trackViewScheme[n]].Width;

			if (newWidth > 72)
				break;

			_visibleWidth = newWidth;
			_visibleChannels++;

			if (_drawDivisions)
				_visibleWidth++;
		}

		if (_drawDivisions)
		{
			/* a division after the last channel would look pretty dopey :) */
			_visibleWidth--;
		}

		/* don't allow anything past channel 64 */
		if (_topDisplayChannel > 64 - _visibleChannels + 1)
			_topDisplayChannel = 64 - _visibleChannels + 1;
	}

	void SetViewScheme(int scheme)
	{
		if (scheme >= TrackViews.Length)
		{
			/* shouldn't happen */
			Log.Append(4, "View scheme {0} out of range -- using default scheme", scheme);
			scheme = 0;
		}

		for (int i = 0; i < _trackViewScheme.Length; i++)
			_trackViewScheme[i] = scheme;

		RecalculateVisibleArea();
	}

	/* --------------------------------------------------------------------- */

	public override void Redraw()
	{
		int chanDrawPos = 5;

		int maskColour = Status.Flags.HasFlag(StatusFlags.InvertedPalette) ? 1 : 3; /* mask colour */

		bool patternIsPlaying = Song.IsPlaying && (_currentPattern == _playingPattern);

		if (_templateMode != TemplateMode.Off)
			VGAMem.DrawTextLen(_templateMode.GetDescription(), 60, new Point(2, 12), 3, 2);

		/* draw the outer box around the whole thing */
		VGAMem.DrawBox(new Point(4, 14), new Point(5 + _visibleWidth, 47), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		/* how many rows are there? */
		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		pattern ??= Pattern.Empty;

		int totalRows = pattern.Rows.Count;

		for (int chan = _topDisplayChannel, chanPos = 0; chanPos < _visibleChannels; chan++, chanPos++)
		{
			var trackView = TrackViews[_trackViewScheme[chanPos]];

			/* maybe i'm retarded but the pattern editor should be dealing
				with the same concept of "channel" as the rest of the
				interface. the mixing channels really could be any arbitrary
				number -- modplug just happens to reserve the first 64 for
				"real" channels. i'd rather pm not replicate this cruft and
				more or less hide the mixer from the interface... */
			trackView.DrawChannelHeader(chan, new Point(chanDrawPos, 14),
				Song.CurrentSong.GetChannel(chan - 1)!.Flags.HasFlag(ChannelFlags.Mute) ? 0 : 3);

			int row, rowPos;

			for (row = _topDisplayRow, rowPos = 0; rowPos < 32 && row < totalRows; row++, rowPos++)
			{
				ref var note = ref pattern[row][chan];

				int fg, bg;

				if (chanPos == 0)
				{
					fg = patternIsPlaying && row == _playingRow ? 3 : 0;
					bg = (_currentPattern == _markedPattern && row == _markedRow) ? 11 : 2;
					VGAMem.DrawText(row.ToString("d3"), new Point(1, 15 + rowPos), fg, bg);
				}

				if (IsInSelection(chan, row))
				{
					fg = 3;
					bg = RowIsHighlight(row) ? 9 : 8;
				}
				else
				{
					fg = ((Status.Flags & (StatusFlags.CrayolaMode | StatusFlags.ClassicMode)) == StatusFlags.CrayolaMode)
						? ((note.Instrument + 3) % 4 + 3)
						: 6;

					if (_highlightCurrentRow && row == _currentRow)
						bg = 1;
					else if (RowIsMajor(row))
						bg = 14;
					else if (RowIsMinor(row))
						bg = 15;
					else
						bg = 0;
				}

				int cpos;

				/* draw the cursor if on the current row, and:
				drawing the current channel, regardless of position
				OR: when the template is enabled,
					and the channel fits within the template size,
					AND shift is not being held down.
				(oh god it's lisp) */
				bool cursorIsOnCurrentRow = (row == _currentRow);
				bool cursorIsInsideColumn = (_currentPosition > 0); // not just on the note value
				bool templateIsEnabled = (_templateMode != TemplateMode.Off);
				bool shiftIsPressed = Status.KeyMod.HasAnyFlag(KeyMod.Shift);
				int clipboardDataWidth = (_clipboard.Data != null ? _clipboard.Channels : 1);

				if (cursorIsOnCurrentRow
						&& ((cursorIsInsideColumn || !templateIsEnabled || shiftIsPressed)
							? (chan == _currentChannel)
							: ((chan >= _currentChannel) && (chan < _currentChannel + clipboardDataWidth))))
				{
					// yes! do write the cursor
					cpos = _currentPosition;
					if (cpos == 6 && _linkEffectColumn && !Status.Flags.HasFlag(StatusFlags.ClassicMode))
						cpos = 9; // highlight full effect and value
				}
				else
					cpos = -1;

				trackView.DrawNote(new Point(chanDrawPos, 15 + rowPos), note, cpos, fg, bg);

				if (_drawDivisions && chanPos < _visibleChannels - 1)
				{
					if (IsInSelection(chan, row))
						bg = 0;

					VGAMem.DrawCharacter((char)168, new Point(chanDrawPos + trackView.Width, 15 + rowPos), 2, bg);
				}
			}

			// hmm...?
			for (; rowPos < 32; row++, rowPos++)
			{
				int bg;

				if (RowIsMajor(row))
					bg = 14;
				else if (RowIsMinor(row))
					bg = 15;
				else
					bg = 0;

				trackView.DrawNote(new Point(chanDrawPos, 15 + rowPos), SongNote.Empty, -1, 6, bg);

				if (_drawDivisions && chanPos < _visibleChannels - 1)
				{
					VGAMem.DrawCharacter((char)168, new Point(chanDrawPos + trackView.Width, 15 + rowPos), 2, bg);
				}
			}

			if (chan == _currentChannel)
				trackView.DrawMask(new Point(chanDrawPos, 47), _editCopyMask, _currentPosition, maskColour, 2);

			/* blah */
			if (_channelMulti[chan - 1])
			{
				if (_trackViewScheme[chanPos] == 0)
					VGAMem.DrawCharacter((char)172, new Point(chanDrawPos + 3, 47), maskColour, 2);
				else if (_trackViewScheme[chanPos] < 3)
					VGAMem.DrawCharacter((char)172, new Point(chanDrawPos + 2, 47), maskColour, 2);
				else if (_trackViewScheme[chanPos] == 3)
					VGAMem.DrawCharacter((char)172, new Point(chanDrawPos + 1, 47), maskColour, 2);
				else if (_currentPosition < 2)
					VGAMem.DrawCharacter((char)172, new Point(chanDrawPos, 47), maskColour, 2);
			}

			chanDrawPos += trackView.Width + (_drawDivisions ? 1 : 0);
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	/* --------------------------------------------------------------------- */
	/* kill all humans */

	void TransposeNotes(int amount)
	{
		Status.Flags |= StatusFlags.SongNeedsSave;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		HistoryAddGrouped((amount > 0)
					? "Undo transposition up          (Alt-Q)"
					: "Undo transposition down        (Alt-A)",
			new Point(_selection.FirstChannel - 1, _selection.FirstRow),
			new Size(
				_selection.LastChannel - _selection.FirstChannel + 1,
				_selection.LastRow - _selection.FirstRow + 1));

		if (SelectionExists())
		{
			for (int row = _selection.FirstRow; row <= _selection.LastRow; row++)
			{
				for (int chan = _selection.FirstChannel; chan <= _selection.LastChannel; chan++)
				{
					ref var note = ref pattern[row][_selection.FirstChannel];

					if (note.Note > 0 && note.Note < 121)
						note.Note = byte.Clamp((byte)(note.Note + amount), 1, 120);
				}
			}
		}
		else
		{
			ref var note = ref pattern[_currentRow][_currentChannel];

			if (note.Note > 0 && note.Note < 121)
				note.Note = byte.Clamp((byte)(note.Note + amount), 1, 120);
		}

		PatternSelectionSystemCopyOut();
	}

	/* --------------------------------------------------------------------- */

	void CopyNoteToMask()
	{
		int row = _currentRow;

		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return;

		_maskNote = pattern[_currentRow][_currentChannel];

		if (_maskCopySearchMode != CopySearchMode.Off)
		{
			while ((pattern[row][_currentChannel].Instrument == 0) && (row > 0))
				row--;

			if (_maskCopySearchMode == CopySearchMode.UpThenDown
			 && (pattern[row][_currentChannel].Instrument == 0))
			{
				row = _currentRow;

				while ((pattern[row][_currentChannel].Instrument == 0) && (row < pattern.Rows.Count))
					row++;
			}
		}

		ref var note = ref pattern[row][_currentChannel];

		if (note.Instrument != 0)
		{
			if (Song.CurrentSong.IsInstrumentMode)
				AllPages.InstrumentList.CurrentInstrument = note.Instrument;
			else
				AllPages.SampleList.CurrentSample = note.Instrument;
		}
	}

	/* --------------------------------------------------------------------- */

	/* pos is either 0 or 1 (0 being the left digit, 1 being the right)
	* return: 1 (move cursor) or 0 (don't)
	* this is highly modplug specific :P */
	bool HandleVolume(ref SongNote note, KeyEvent k, int pos)
	{
		int vol = note.VolumeParameter;
		VolumeEffects fx = note.VolumeEffect;
		VolumeEffects vp = _panningMode ? VolumeEffects.Panning : VolumeEffects.Volume;

		if (pos == 0)
		{
			int q = k.HexValue;
			if (q >= 0 && q <= 9)
			{
				vol = q * 10 + vol % 10;
				fx = vp;
			}
			else if (k.Sym == KeySym.a)
			{
				fx = VolumeEffects.FineVolumeUp;
				vol %= 10;
			}
			else if (k.Sym == KeySym.b)
			{
				fx = VolumeEffects.FineVolumeDown;
				vol %= 10;
			}
			else if (k.Sym == KeySym.c)
			{
				fx = VolumeEffects.VolumeSlideUp;
				vol %= 10;
			}
			else if (k.Sym == KeySym.d)
			{
				fx = VolumeEffects.VolumeSlideDown;
				vol %= 10;
			}
			else if (k.Sym == KeySym.e)
			{
				fx = VolumeEffects.PortamentoDown;
				vol %= 10;
			}
			else if (k.Sym == KeySym.f)
			{
				fx = VolumeEffects.PortamentoUp;
				vol %= 10;
			}
			else if (k.Sym == KeySym.g)
			{
				fx = VolumeEffects.TonePortamento;
				vol %= 10;
			}
			else if (k.Sym == KeySym.h)
			{
				fx = VolumeEffects.VibratoDepth;
				vol %= 10;
			}
			else if (Status.Flags.HasFlag(StatusFlags.ClassicMode))
				return false;
			else if (k.Sym == KeySym.Dollar)
			{
				fx = VolumeEffects.VibratoSpeed;
				vol %= 10;
			}
			else if (k.Sym == KeySym.Less)
			{
				fx = VolumeEffects.PanningSlideLeft;
				vol %= 10;
			}
			else if (k.Sym == KeySym.Greater)
			{
				fx = VolumeEffects.PanningSlideRight;
				vol %= 10;
			}
			else
				return false;
		}
		else
		{
			int q = k.HexValue;
			if (q >= 0 && q <= 9)
			{
				vol = (vol / 10) * 10 + q;
				switch (fx)
				{
					case VolumeEffects.None:
					case VolumeEffects.Volume:
					case VolumeEffects.Panning:
						fx = vp;
						break;
				}
			}
			else
				return false;
		}

		note.VolumeEffect = fx;
		if (fx == VolumeEffects.Volume || fx == VolumeEffects.Panning)
			note.VolumeParameter = (byte)vol.Clamp(0, 64);
		else
			note.VolumeParameter = (byte)vol.Clamp(0, 9);

		return true;
	}

	// return false iff there is no value in the current cell at the current column
	bool SeekDone()
	{
		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return true;

		ref var note = ref pattern[_currentRow][_currentChannel];

		switch (_currentPosition)
		{
			case 0:
			case 1:
				return note.HasNote;
			case 2:
			case 3:
				return note.HasInstrument;
			case 4:
			case 5:
				return (note.VolumeEffect != VolumeEffects.None) || (note.VolumeParameter != 0);
			case 6:
			case 7:
			case 8:
				// effect param columns intentionally check effect column instead
				return note.Effect != Effects.None;
		}

		return true; // please stop seeking because something is probably wrong
	}

	// FIXME: why the 'row' param? should it be removed, or should the references to _currentRow be replaced?
	// fwiw, every call to this uses _currentRow.
	// return: false if there was a template error, true otherwise
	bool RecordNote(Pattern pattern, int row, int channel, int note, bool force)
	{
		bool success = true;

		Status.Flags |= StatusFlags.SongNeedsSave;

		if (SongNote.IsNote(note))
		{
			if (_templateMode != TemplateMode.Off)
			{
				if (_clipboard.Channels < 1 || _clipboard.Rows < 1 || (_clipboard.Data == null))
				{
					MessageBox.Show(MessageBoxTypes.OK, "No data in clipboard");
					success = false;
				}
				else
				{
					ref var q = ref _clipboard.Data[0];

					if (q.Note == 0)
					{
						Dialog.Show<TemplateErrorDialog>();
						success = false;
					}
					else
					{
						int xlate = note - q.Note;

						switch (_templateMode)
						{
							case TemplateMode.Overwrite:
								SnapPaste(_clipboard, _currentChannel - 1, _currentRow, xlate);
								break;
							case TemplateMode.MixPatternPrecedence:
								ClipboardPasteMixFields(false, xlate);
								break;
							case TemplateMode.MixClipboardPrecedence:
								ClipboardPasteMixFields(true, xlate);
								break;
							case TemplateMode.NotesOnly:
								ClipboardPasteMixNotes(true, xlate);
								break;
						}
					}
				}
			}
			else
				pattern[row][channel].Note = (byte)note;
		}
		else
		{
			/* Note cut, etc. -- need to clear all masked fields. This will never cause a template error.
			Also, for one-row templates, replicate control notes across the width of the template. */
			int channels = ((_templateMode != TemplateMode.Off) && (_clipboard.Data != null) && (_clipboard.Rows == 1))
				? _clipboard.Channels
				: 1;

			for (int i = 0; i < channels && i + channel <= Constants.MaxChannels; i++)
			{
				ref var curNote = ref pattern[row][channel + i];

				/* I don't know what this whole 'force' thing is about, but okay */
				if (!force && curNote.HasNote)
					continue;

				curNote.Note = (byte)note;
				if (_editCopyMask.HasFlag(PatternEditorMask.Instrument))
					curNote.Instrument = 0;

				if (_editCopyMask.HasFlag(PatternEditorMask.Volume))
				{
					curNote.VolumeEffect = VolumeEffects.None;
					curNote.VolumeParameter = 0;
				}

				if (_editCopyMask.HasFlag(PatternEditorMask.Effect))
				{
					curNote.Effect = Effects.None;
					curNote.Parameter = 0;
				}
			}
		}

		PatternSelectionSystemCopyOut();

		return success;
	}

	bool? InsertMIDI(KeyEvent k)
	{
		int r = _currentRow, c = _currentChannel, p = _currentPattern;
		bool quantizeNextRow = false;
		int ins = KeyJazz.NoInstrument, smp = KeyJazz.NoInstrument;
		bool songWasPlaying = Song.IsPlaying;

		if (Song.CurrentSong.IsInstrumentMode)
			ins = AllPages.InstrumentList.CurrentInstrument;
		else
			smp = AllPages.SampleList.CurrentSample;

		Status.Flags |= StatusFlags.SongNeedsSave;

		int speed = Song.CurrentSpeed;
		int tick = Song.CurrentTick;

		if ((_midiStartRecord != 0) && !Song.IsPlaying)
		{
			switch (_midiStartRecord)
			{
				case 1: /* pattern loop */
					AudioPlayback.LoopPattern(p, r);
					_midiPlaybackTracing = _playbackTracing;
					_playbackTracing = true;
					break;
				case 2: /* song play */
					AudioPlayback.StartAtPattern(p, r);
					_midiPlaybackTracing = _playbackTracing;
					_playbackTracing = true;
					break;
			}
		}

		int offset = 0;

		// this is a long one
		if (MIDIEngine.Flags.HasFlag(MIDIFlags.TickQuantize) // if quantize is on
				&& songWasPlaying                       // and the song was playing
				&& _playbackTracing                     // and we are following the song
				&& tick > 0 && tick <= speed / 2 + 1)   // and the note is too late
		{
			/* correct late notes to the next row */
			/* tick + 1 because processing the keydown itself takes another tick */
			offset++;
			quantizeNextRow = true;
		}

		var pattern = Song.CurrentSong.GetPatternOffset(ref p, ref r, offset);

		if (pattern == null)
			return false;

		int v = -1;
		int n = -1;

		if (k.MIDINote == -1)
		{
			/* nada */
		}
		else if (k.State == KeyState.Release)
		{
			c = Song.KeyUp(KeyJazz.NoInstrument, KeyJazz.NoInstrument, k.MIDINote);

			/* song_keyup didn't find find note off channel, abort */
			if (c <= 0)
				return false;

			/* don't record noteoffs for no good reason... */
			if (!(MIDIEngine.Flags.HasFlag(MIDIFlags.RecordNoteOff)
					&& Song.IsPlaying
					&& _playbackTracing))
				return false;
		}
		else
		{
			if (k.MIDIVolume > -1)
				v = k.MIDIVolume / 2;
			else
				v = 0;

			if (!(Song.IsPlaying && _playbackTracing))
				tick = 0;

			n = k.MIDINote;

			if (!quantizeNextRow)
				c = Song.KeyDown(smp, ins, n, v, c);
		}

		ref var curNote = ref pattern[r][c];

		if (k.MIDINote == -1)
		{
			/* nada */
		}
		else if (k.State == KeyState.Release)
		{
			/* never "overwrite" a note off */
			RecordNote(pattern, r, c, SpecialNotes.NoteOff, false);
		}
		else
		{
			RecordNote(pattern, r, c, n, false);

			if (_templateMode == TemplateMode.Off)
			{
				curNote.Instrument = (byte)CurrentInstrument;

				if (MIDIEngine.Flags.HasFlag(MIDIFlags.RecordVelocity))
				{
					curNote.VolumeEffect = VolumeEffects.Volume;
					curNote.VolumeParameter = (byte)v;
				}

				tick %= speed;

				if (!MIDIEngine.Flags.HasFlag(MIDIFlags.TickQuantize)
				 && (curNote.Effect == Effects.None)
				 && tick != 0)
				{
					curNote.Effect = Effects.Special;
					curNote.Parameter = (byte)(0xD0 | tick.Clamp(0, 15));
				}
			}
		}

		if (!MIDIEngine.Flags.HasFlag(MIDIFlags.PitchBend)
		 || MIDIEngine.PitchDepth == 0
		 || k.MIDIBend == 0)
		{
			if (k.State == KeyState.Release
			 && k.MIDINote > -1
			 && curNote.Instrument > 0)
			{
				Song.KeyRecord(curNote.Instrument, curNote.Instrument, curNote.Note, v, c + 1,
					curNote.Effect, curNote.Parameter);
				PatternSelectionSystemCopyOut();
			}

			return null;
		}

		/* pitch bend */
		for (c = 0; c < Constants.MaxChannels; c++)
		{
			/* Dead code?? */
			// if ((_channelMulti[c] & 1) && (channel_multi[c] & (~1)))
			if (_channelMulti[c] && !_channelMulti[c])
			{
				ref var curNote2 = ref pattern[r][c];

				int pd = MIDIEngine.LastBendHit[c];

				if (curNote2.Effect != Effects.None)
				{
					/* don't overwrite old effects */
					if (curNote2.Effect != Effects.PortamentoUp
					 && curNote2.Effect != Effects.PortamentoDown)
						continue;

				}
				else
				{
					MIDIEngine.LastBendHit[c] = k.MIDIBend;
				}

				pd = (k.MIDIBend - pd) * MIDIEngine.PitchDepth / 8192;
				pd = pd * speed / 2;

				pd = pd.Clamp(-0x7F, +0x7F);

				if (pd < 0)
				{
					curNote2.Effect = Effects.PortamentoDown; /* Exx */
					curNote2.Parameter = unchecked((byte)-pd);
				}
				else if (pd > 0)
				{
					curNote2.Effect = Effects.PortamentoUp; /* Fxx */
					curNote2.Parameter = unchecked((byte)pd);
				}

				if (k.MIDINote == -1 || k.State == KeyState.Release)
					continue;
				if (curNote2.Instrument < 1)
					continue;

				if (curNote2.VolumeEffect == VolumeEffects.Volume)
					v = curNote2.VolumeParameter;
				else
					v = -1;

				Song.KeyRecord(curNote2.Instrument, curNote2.Instrument, curNote2.Note,
					v, c + 1, curNote2.Effect, curNote2.Parameter);
			}
		}

		PatternSelectionSystemCopyOut();

		return null;
	}

	/* return true => handled key, false => no way */
	bool Insert(KeyEvent k)
	{
		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return false;

		/* keydown events are handled here for multichannel */
		if (k.State == KeyState.Release && (_currentPosition > 0))
			return false;

		ref var curNote = ref pattern[_currentRow][_currentChannel];

		int note = -1;
		int vol = -1;
		int smp = -1;
		int ins = -1;

		if (_currentPosition == 0) /* note */
		{
			// FIXME: this is actually quite wrong; instrument numbers should be independent for each
			// channel and take effect when the instrument is played (e.g. with 4/8 or keyjazz input)
			// also, this is fully idiotic
			smp = curNote.Instrument;
			ins = curNote.Instrument;

			if (Song.CurrentSong.IsInstrumentMode)
				smp = KeyJazz.NoInstrument;
			else
				ins = KeyJazz.NoInstrument;

			if (k.Sym == KeySym._4)
			{
				if (k.State == KeyState.Release)
					return false;

				int newVol;

				if (curNote.VolumeEffect == VolumeEffects.Volume)
					newVol = curNote.VolumeParameter;
				else
					newVol = KeyJazz.DefaultVolume;

				Song.KeyRecord(smp, ins, curNote.Note,
					newVol, _currentChannel, curNote.Effect, curNote.Parameter);

				AdvanceCursor(!k.Modifiers.HasAnyFlag(KeyMod.Shift), true);

				return true;
			}
			else if (k.Sym == KeySym._8)
			{
				/* note: Impulse Tracker doesn't skip multichannels when pressing "8"  -delt. */
				if (k.State == KeyState.Release)
					return false;

				AudioPlayback.SingleStep(_currentPattern, _currentRow);

				AdvanceCursor(!k.Modifiers.HasAnyFlag(KeyMod.Shift), false);

				return true;
			}

			if (Song.CurrentSong.IsInstrumentMode)
			{
				if (_editCopyMask.HasFlag(PatternEditorMask.Instrument))
					ins = AllPages.InstrumentList.CurrentInstrument;
			}
			else
			{
				if (_editCopyMask.HasFlag(PatternEditorMask.Instrument))
					smp = AllPages.SampleList.CurrentSample;
			}

			if (k.Sym == KeySym.Space)
			{
				/* copy mask to note */
				note = _maskNote.Note;

				vol = (_editCopyMask.HasFlag(PatternEditorMask.Volume) && curNote.VolumeEffect == VolumeEffects.Volume)
					? _maskNote.VolumeParameter
					: KeyJazz.DefaultVolume;
			}
			else
			{
				note = k.NoteValue;

				if (note < 0)
					return false;

				if (_editCopyMask.HasFlag(PatternEditorMask.Volume) && _maskNote.VolumeEffect == VolumeEffects.Volume)
					vol = _maskNote.VolumeParameter;
				else if (curNote.VolumeEffect == VolumeEffects.Volume)
					vol = curNote.VolumeParameter;
				else
					vol = KeyJazz.DefaultVolume;
			}

			if (k.State == KeyState.Release)
			{
				if (_keyjazzNoteOff && SongNote.IsNote(note))
				{
					/* coda mode */
					Song.KeyUp(smp, ins, note);
				}

				/* it would be weird to have this enabled and keyjazz_noteoff
				* disabled, but it's possible, so handle it separately. */
				if (_keyjazzWriteNoteOff && _playbackTracing && SongNote.IsNote(note))
				{
					/* go to the next row if a note off would overwrite a note
					* you (likely) just entered */
					if (curNote.HasNote)
					{
						if (++_currentRow >= Song.CurrentSong.GetPatternLength(_currentPattern))
							return true;

						ref var nextNote = ref pattern[_currentRow][_currentChannel];

						/* give up if the next row has a note too */
						if (nextNote.HasNote)
							return true;
					}

					note = SpecialNotes.NoteOff;
				}
				else
					return true;
			}
		}

		// _currentRow may have changed
		ref var curNote2 = ref pattern[_currentRow][_currentChannel];
		if (k.IsRepeat && !_keyjazzRepeat)
			return true;

		if (_currentPosition == 0) /* note */
		{
			bool writeNote = _keyjazzCapsLock ? !k.Modifiers.HasFlag(KeyMod.Caps) : !k.Modifiers.HasFlag(KeyMod.CapsPressed);

			if (writeNote && !RecordNote(pattern, _currentRow, _currentChannel, note, true))
			{
				// there was a template error, don't advance the cursor and so on
				writeNote = false;
				note = SpecialNotes.None;
			}

			/* Be quiet when pasting templates.
			It'd be nice to "play" a template when pasting it (maybe only for ones that are one row high)
			so as to hear the chords being inserted etc., but that's a little complicated to do. */
			if (SongNote.IsNote(note) && !((_templateMode != TemplateMode.Off) && writeNote))
				Song.KeyDown(smp, ins, note, vol, _currentChannel);

			if (writeNote)
			{
				/* Never copy the instrument etc. from the mask when inserting control notes or when
				erasing a note -- but DO write it when inserting a blank note with the space key. */
				if (!(SongNote.IsControl(note) || (k.Sym != KeySym.Space && note == SpecialNotes.None)) && (_templateMode == TemplateMode.Off))
				{
					if (_editCopyMask.HasFlag(PatternEditorMask.Instrument))
					{
						if (Song.CurrentSong.IsInstrumentMode)
							curNote2.Instrument = (byte)AllPages.InstrumentList.CurrentInstrument;
						else
							curNote2.Instrument = (byte)AllPages.SampleList.CurrentSample;
					}

					if (_editCopyMask.HasFlag(PatternEditorMask.Volume))
					{
						curNote2.VolumeEffect = _maskNote.VolumeEffect;
						curNote2.VolumeParameter = _maskNote.VolumeParameter;
					}

					if (_editCopyMask.HasFlag(PatternEditorMask.Effect))
					{
						curNote2.Effect = _maskNote.Effect;
						curNote2.Parameter = _maskNote.Parameter;
					}
				}

				/* try again, now that we have the effect (this is a dumb way to do this...) */
				if (SongNote.IsNote(note) && (_templateMode == TemplateMode.Off))
					Song.KeyRecord(smp, ins, note, vol, _currentChannel, curNote2.Effect, curNote2.Parameter);

				/* copy the note back to the mask */
				_maskNote.Note = (byte)note;
				PatternSelectionSystemCopyOut();

				note = curNote2.Note;
				if (SongNote.IsNote(note) && curNote2.VolumeEffect == VolumeEffects.Volume)
					vol = curNote2.VolumeParameter;

				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					// advance horizontally, stopping at channel 64
					// (I have no idea how IT does this, it might wrap)
					if (_currentChannel < 64)
					{
						_shiftChordChannels++;
						_currentChannel++;
						Reposition();
					}
				}
				else
					AdvanceCursor(true, true);
			}
		}

		switch (_currentPosition)
		{
			case 1:                 /* octave */
			{
				int j = k.HexValue;
				if (j < 0 || j > 9)
					return false;
				int n = curNote2.Note;
				if (n > 0 && n <= 120)
				{
					/* Hehe... this was originally 7 lines :) */
					n = ((n - 1) % 12) + (12 * j) + 1;
					curNote2.Note = (byte)n;
				}
				AdvanceCursor(true, false);
				Status.Flags |= StatusFlags.SongNeedsSave;
				PatternSelectionSystemCopyOut();
				break;
			}
			case 2:                 /* instrument, first digit */
			case 3:                 /* instrument, second digit */
			{
				int n;

				if (k.Sym == KeySym.Space)
				{
					if (Song.CurrentSong.IsInstrumentMode)
						n = AllPages.InstrumentList.CurrentInstrument;
					else
						n = AllPages.SampleList.CurrentSample;

					Song.CurrentSong.Voices[_currentChannel - 1].LastInstrumentNumber = n;
					curNote2.Instrument = (byte)n;
					AdvanceCursor(true, false);
					Status.Flags |= StatusFlags.SongNeedsSave;
					break;
				}

				if (k.NoteValue == 0)
				{
					curNote2.Instrument = 0;
					if (Song.CurrentSong.IsInstrumentMode)
						AllPages.InstrumentList.CurrentInstrument = 0;
					else
						AllPages.SampleList.CurrentSample = 0;

					AdvanceCursor(true, false);
					Status.Flags |= StatusFlags.SongNeedsSave;
					break;
				}

				if (_currentPosition == 2)
				{
					int j = k.Character99Value;

					if (j < 0)
						return false;

					n = (j * 10) + (curNote2.Instrument % 10);

					_currentPosition++;
				}
				else
				{
					int j = k.HexValue;
					if (j < 0 || j > 9)
						return false;

					n = ((curNote2.Instrument / 10) * 10) + j;

					_currentPosition--;

					AdvanceCursor(true, false);
				}

				/* this is kind of ugly... */
				if (Song.CurrentSong.IsInstrumentMode)
				{
					int j = AllPages.InstrumentList.CurrentInstrument;

					AllPages.InstrumentList.CurrentInstrument = n;

					if (n != AllPages.InstrumentList.CurrentInstrument)
						n = j;

					AllPages.InstrumentList.CurrentInstrument = j;
				}

				else
				{
					int j = AllPages.SampleList.CurrentSample;

					AllPages.SampleList.CurrentSample = n;

					if (n != AllPages.SampleList.CurrentSample)
						n = j;

					AllPages.SampleList.CurrentSample = j;
				}

				Song.CurrentSong.Voices[_currentChannel - 1].LastInstrumentNumber = n;

				curNote2.Instrument = (byte)n;

				if (Song.CurrentSong.IsInstrumentMode)
					AllPages.InstrumentList.CurrentInstrument = n;
				else
					AllPages.SampleList.CurrentSample = n;

				Status.Flags |= StatusFlags.SongNeedsSave;
				PatternSelectionSystemCopyOut();
				break;
			}
			case 4:
			case 5:                 /* volume */
			{
				if (k.Sym == KeySym.Space)
				{
					curNote2.VolumeParameter = _maskNote.VolumeParameter;
					curNote2.VolumeEffect = _maskNote.VolumeEffect;
					AdvanceCursor(true, false);
					Status.Flags |= StatusFlags.SongNeedsSave;
					break;
				}

				if (k.NoteValue == 0)
				{
					curNote2.VolumeParameter = _maskNote.VolumeParameter = 0;
					curNote2.VolumeEffect = _maskNote.VolumeEffect = VolumeEffects.None;
					AdvanceCursor(true, false);
					Status.Flags |= StatusFlags.SongNeedsSave;
					break;
				}

				if (k.ScanCode == ScanCode.Grave)
				{
					_panningMode = !_panningMode;
					Status.FlashText((_panningMode ? "Panning" : "Volume") + " control set");
					return false;
				}

				if (!HandleVolume(ref curNote2, k, _currentPosition - 4))
					return false;

				_maskNote.VolumeParameter = curNote2.VolumeParameter;
				_maskNote.VolumeEffect = curNote2.VolumeEffect;

				if (_currentPosition == 4)
					_currentPosition++;
				else
				{
					_currentPosition = 4;
					AdvanceCursor(true, false);
				}

				Status.Flags |= StatusFlags.SongNeedsSave;
				PatternSelectionSystemCopyOut();
				break;
			}
			case 6:                 /* effect */
			{
				if (k.Sym == KeySym.Space)
					curNote2.Effect = _maskNote.Effect;
				else
				{
					Effects f = k.EffectValue ?? Effects.None;
					if (f == Effects.None)
						return false;
					curNote2.Effect = _maskNote.Effect = f;
				}
				Status.Flags |= StatusFlags.SongNeedsSave;
				if (_linkEffectColumn)
					_currentPosition++;
				else
					AdvanceCursor(true, false);
				PatternSelectionSystemCopyOut();
				break;
			}
			case 7:                 /* param, high nibble */
			case 8:                 /* param, low nibble */
			{
				if (k.Sym == KeySym.Space)
				{
					curNote2.Parameter = _maskNote.Parameter;
					_currentPosition = _linkEffectColumn ? 6 : 7;
					AdvanceCursor(true, false);
					Status.Flags |= StatusFlags.SongNeedsSave;
					PatternSelectionSystemCopyOut();
					break;
				}
				else if (k.NoteValue == 0)
				{
					curNote2.Parameter = _maskNote.Parameter = 0;
					_currentPosition = _linkEffectColumn ? 6 : 7;
					AdvanceCursor(true, false);
					Status.Flags |= StatusFlags.SongNeedsSave;
					PatternSelectionSystemCopyOut();
					break;
				}

				/* FIXME: honey roasted peanuts */

				int n = k.HexValue;
				if (n < 0)
					return false;

				if (_currentPosition == 7)
				{
					curNote2.Parameter = (byte)((n << 4) | (curNote2.Parameter & 0xf));
					_currentPosition++;
				}
				else /* _currentPosition == 8 */
				{
					curNote2.Parameter = (byte)((curNote2.Parameter & 0xf0) | n);
					_currentPosition = _linkEffectColumn ? 6 : 7;
					AdvanceCursor(true, false);
				}
				Status.Flags |= StatusFlags.SongNeedsSave;
				_maskNote.Parameter = curNote2.Parameter;
				PatternSelectionSystemCopyOut();
				break;
			}
		}

		return true;
	}

	/* return values:
	* true = handled key completely. don't do anything else
	* null = partly done, but need to recalculate cursor stuff
	*         (for keys that move the cursor)
	* false = didn't handle the key. */

	bool? HandleAltKey(KeyEvent k)
	{
		int maxRowNumber = Song.CurrentSong.GetPatternLength(_currentPattern) - 1;

		/* hack to render this useful :) */
		if (k.Sym == KeySym.KP_9)
			k.Sym = KeySym.F9;
		else if (k.Sym == KeySym.KP_0)
			k.Sym = KeySym.F10;

		int n = k.NumericValue(false);

		if (n > -1)
		{
			if (k.State == KeyState.Release)
				return true;
			_skipValue = (n == 9) ? 16 : n;
			Status.FlashText("Cursor step set to " + _skipValue);
			return true;
		}

		switch (k.Sym)
		{
			case KeySym.Return:
				if (k.State == KeyState.Press)
					return true;
				FastSaveUpdate();
				return true;

			case KeySym.Backspace:
				if (k.State == KeyState.Press)
					return true;
				Save("Undo revert pattern data (Alt-BkSpace)");
				SnapPaste(_fastSave, 0, 0, 0);
				return true;

			case KeySym.b:
				if (k.State == KeyState.Release)
					return true;

				if (!SelectionExists())
				{
					_selection.LastChannel = _currentChannel;
					_selection.LastRow = _currentRow;
				}

				_selection.FirstChannel = _currentChannel;
				_selection.FirstRow = _currentRow;

				NormaliseBlockSelection();

				break;

			case KeySym.e:
				if (k.State == KeyState.Release)
					return true;

				if (!SelectionExists())
				{
					_selection.FirstChannel = _currentChannel;
					_selection.FirstRow = _currentRow;
				}

				_selection.LastChannel = _currentChannel;
				_selection.LastRow = _currentRow;

				NormaliseBlockSelection();

				break;
			case KeySym.d:
				if (k.State == KeyState.Release)
					return true;

				if (Status.LastKeySym == KeySym.d)
				{
					if (maxRowNumber - (_currentRow - 1) > _blockDoubleSize)
						_blockDoubleSize <<= 1;
				}
				else
				{
					// emulate some weird impulse tracker behavior here:
					// with row highlight set to zero, alt-d selects the whole channel
					// if the cursor is at the top, and clears the selection otherwise
					_blockDoubleSize = (Song.CurrentSong.RowHighlightMajor > 0)
						? Song.CurrentSong.RowHighlightMajor
						: ((_currentRow > 0) ? 0 : 65536);
					_selection.FirstChannel = _selection.LastChannel = _currentChannel;
					_selection.FirstRow = _currentRow;
				}
				n = _blockDoubleSize + _currentRow - 1;
				_selection.LastRow = Math.Min(n, maxRowNumber);
				break;
			case KeySym.l:
				if (k.State == KeyState.Release)
					return true;
				if (Status.LastKeySym == KeySym.l)
				{
					/* 3x alt-l re-selects the current channel */
					if (_selection.FirstChannel == _selection.LastChannel)
					{
						_selection.FirstChannel = 1;
						_selection.LastChannel = 64;
					}
					else
					{
						_selection.FirstChannel = _selection.LastChannel = _currentChannel;
					}
				}
				else
				{
					_selection.FirstChannel = _selection.LastChannel = _currentChannel;
					_selection.FirstRow = 0;
					_selection.LastRow = maxRowNumber;
				}
				PatternSelectionSystemCopyOut();
				break;
			case KeySym.r:
				if (k.State == KeyState.Release)
					return true;
				_drawDivisions = true;
				SetViewScheme(0);
				break;
			case KeySym.s:
				if (k.State == KeyState.Release)
					return true;
				SelectionSetSample();
				break;
			case KeySym.u:
				if (k.State == KeyState.Release)
					return true;
				if (SelectionExists())
					SelectionClear();
				else if (_clipboard.Data != null)
				{
					ClipboardFree();

					Clippy.Select(null, null, 0);
					Clippy.Yank();
				}
				else
					MessageBox.Show(MessageBoxTypes.OK, "No data in clipboard");

				break;
			case KeySym.c:
				if (k.State == KeyState.Release)
					return true;
				ClipboardCopy(false);
				break;
			case KeySym.o:
				if (k.State == KeyState.Release)
					return true;

				if (Status.LastKeySym == KeySym.o)
					_patternCopyInBehaviour = PatternCopyInBehaviour.OverwriteGrow;
				else
					_patternCopyInBehaviour = PatternCopyInBehaviour.Overwrite;

				Status.Flags |= StatusFlags.ClippyPasteBuffer;
				break;
			case KeySym.p:
				if (k.State == KeyState.Release)
					return true;
				_patternCopyInBehaviour = PatternCopyInBehaviour.Insert;
				Status.Flags |= StatusFlags.ClippyPasteBuffer;
				break;
			case KeySym.m:
				if (k.State == KeyState.Release)
					return true;
				if (Status.LastKeySym == KeySym.m)
					_patternCopyInBehaviour = PatternCopyInBehaviour.MixFields;
				else
					_patternCopyInBehaviour = PatternCopyInBehaviour.MixNotes;
				Status.Flags |= StatusFlags.ClippyPasteBuffer;
				break;
			case KeySym.f:
				if (k.State == KeyState.Release)
					return true;
				BlockLengthDouble();
				break;
			case KeySym.g:
				if (k.State == KeyState.Release)
					return true;
				BlockLengthHalve();
				break;
			case KeySym.n:
				if (k.State == KeyState.Release)
					return true;
				_channelMulti[_currentChannel - 1] ^= true;
				if (_channelMulti[_currentChannel - 1])
					_channelMultiEnabled = true;
				else
					_channelMultiEnabled = _channelMulti.Any(v => v);

				if (Status.LastKeySym == KeySym.n)
				{
					var dialog = Dialog.Show(new MultiChannelDialog(_channelMulti));

					dialog.ActionYes = MultiChannelClose;
					dialog.ActionCancel = MultiChannelClose;

					void MultiChannelClose(object? data)
					{
						bool m = false;

						for (int i = 0; i < Constants.MaxChannels; i++)
						{
							_channelMulti[i] = dialog.IsMultiChannelEnabledForChannel(i);

							if (_channelMulti[i])
								m = true;
						}

						if (m)
							_channelMultiEnabled = true;
					}

				}
				break;
			case KeySym.z:
				if (k.State == KeyState.Release)
					return true;
				ClipboardCopy(false);
				SelectionErase();
				break;
			case KeySym.y:
				if (k.State == KeyState.Release)
					return true;
				SelectionSwap();
				break;
			case KeySym.v:
				if (k.State == KeyState.Release)
					return true;
				SelectionSetVolume();
				break;
			case KeySym.w:
				if (k.State == KeyState.Release)
					return true;
				SelectionWipeVolume(false);
				break;
			case KeySym.k:
				if (k.State == KeyState.Release)
					return true;
				if (Status.LastKeySym == KeySym.k)
					SelectionWipeVolume(true);
				else
					SelectionSlideVolume();
				break;
			case KeySym.x:
				if (k.State == KeyState.Release)
					return true;
				if (Status.LastKeySym == KeySym.x)
					SelectionWipeEffect();
				else
					SelectionSlideEffect();
				break;
			case KeySym.h:
				if (k.State == KeyState.Release)
					return true;
				_drawDivisions = !_drawDivisions;
				RecalculateVisibleArea();
				Reposition();
				break;
			case KeySym.q:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					TransposeNotes(12);
				else
					TransposeNotes(1);
				break;
			case KeySym.a:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					TransposeNotes(-12);
				else
					TransposeNotes(-1);
				break;
			case KeySym.i:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					_templateMode = TemplateMode.Off;
				else if (_fastVolumeMode)
					FastVolumeAmplify();
				else
					_templateMode = _templateMode.Cycle();
				break;
			case KeySym.j:
				if (k.State == KeyState.Release)
					return true;
				if (_fastVolumeMode)
					FastVolumeAttenuate();
				else
					VolumeAmplify();
				break;
			case KeySym.t:
				if (k.State == KeyState.Release)
					return true;
				n = _currentChannel - _topDisplayChannel;
				_trackViewScheme[n] = _trackViewScheme[n].Cycle(TrackViews.Length);
				RecalculateVisibleArea();
				Reposition();
				break;
			case KeySym.Up:
				if (k.State == KeyState.Release)
					return true;
				if (_topDisplayRow > 0)
				{
					_topDisplayRow--;
					if (_currentRow > _topDisplayRow + 31)
						_currentRow = _topDisplayRow + 31;
					return null;
				}
				return true;
			case KeySym.Down:
				if (k.State == KeyState.Release)
					return true;
				if (_topDisplayRow + 31 < maxRowNumber)
				{
					_topDisplayRow++;
					if (_currentRow < _topDisplayRow)
						_currentRow = _topDisplayRow;
					return null;
				}
				return true;
			case KeySym.Left:
				if (k.State == KeyState.Release)
					return true;
				_currentChannel--;
				return null;
			case KeySym.Right:
				if (k.State == KeyState.Release)
					return true;
				_currentChannel++;
				return null;
			case KeySym.Insert:
				if (k.State == KeyState.Release)
					return true;
				Save("Remove inserted row(s)    (Alt-Insert)");
				PatternInsertRows(_currentRow, 1, 1, 64);
				break;
			case KeySym.Delete:
				if (k.State == KeyState.Release)
					return true;
				Save("Replace deleted row(s)    (Alt-Delete)");
				PatternDeleteRows(_currentRow, 1, 1, 64);
				break;
			case KeySym.F9:
				if (k.State == KeyState.Release)
					return true;
				Song.CurrentSong.ToggleChannelMute(_currentChannel - 1);
				break;
			case KeySym.F10:
				if (k.State == KeyState.Release)
					return true;
				Song.CurrentSong.HandleChannelSolo(_currentChannel - 1);
				break;
			default:
				return false;
		}

		Status.Flags |= StatusFlags.NeedUpdate;
		return true;
	}

	/* Two atoms are walking down the street, and one of them stops abruptly
	*     and says, "Oh my God, I just lost an electron!"
	* The other one says, "Are you sure?"
	* The first one says, "Yes, I'm positive!" */
	bool? HandleControlKey(KeyEvent k)
	{
		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return false;

		int n = k.NumericValue(false);

		if (n > -1)
		{
			if (n < 0 || n >= TrackViews.Length)
				return false;
			if (k.State == KeyState.Release)
				return true;

			if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				SetViewScheme(n);
			else
			{
				_trackViewScheme[_currentChannel - _topDisplayChannel] = n;
				RecalculateVisibleArea();
			}

			Reposition();

			Status.Flags |= StatusFlags.NeedUpdate;
			return true;
		}

		switch (k.Sym)
		{
			case KeySym.Left:
				if (k.State == KeyState.Release)
					return true;
				if (_currentChannel > _topDisplayChannel)
					_currentChannel--;
				return null;
			case KeySym.Right:
				if (k.State == KeyState.Release)
					return true;
				if (_currentChannel < _topDisplayChannel + _visibleChannels - 1)
					_currentChannel++;
				return null;
			case KeySym.F6:
				if (k.State == KeyState.Release)
					return true;
				AudioPlayback.LoopPattern(_currentPattern, _currentRow);
				return true;
			case KeySym.F7:
				if (k.State == KeyState.Release)
					return true;
				SetPlaybackMark();
				return null;
			case KeySym.Up:
				if (k.State == KeyState.Release)
					return true;
				CurrentInstrument--;
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.Down:
				if (k.State == KeyState.Release)
					return true;
				CurrentInstrument++;
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.PageUp:
				if (k.State == KeyState.Release)
					return true;
				_currentRow = 0;
				return null;
			case KeySym.PageDown:
				if (k.State == KeyState.Release)
					return true;
				_currentRow = pattern.Rows.Count - 1;
				return null;
			case KeySym.Home:
				if (k.State == KeyState.Release)
					return true;
				_currentRow--;
				return null;
			case KeySym.End:
				if (k.State == KeyState.Release)
					return true;
				_currentRow++;
				return null;
			case KeySym.Insert:
				if (k.State == KeyState.Release)
					return true;
				SelectionRoll(RollDirection.Down);
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.Delete:
				if (k.State == KeyState.Release)
					return true;
				SelectionRoll(RollDirection.Up);
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.Minus:
				if (k.State == KeyState.Release)
					return true;
				if (Song.IsPlaying && _playbackTracing)
					return true;

				AllPages.OrderList.PreviousOrderPattern();
				return true;
			case KeySym.Equals:
				if (!k.Modifiers.HasAnyFlag(KeyMod.Shift))
					return false;
				goto case KeySym.Plus;
			case KeySym.Plus:
				if (k.State == KeyState.Release)
					return true;
				if (Song.IsPlaying && _playbackTracing)
					return true;
				AllPages.OrderList.NextOrderPattern();
				return true;
			case KeySym.c:
				if (k.State == KeyState.Release)
					return true;
				_centraliseCursor = !_centraliseCursor;
				Status.FlashText(_centraliseCursor ? "Centralise cursor enabled" : "Centralise cursor disabled");
				return null;
			case KeySym.h:
				if (k.State == KeyState.Release)
					return true;
				_highlightCurrentRow = !_highlightCurrentRow;
				Status.FlashText(_highlightCurrentRow ? "Row hilight enabled" : "Row hilight disabled");
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.j:
				if (k.State == KeyState.Release)
					return true;
				FastVolumeToggle();
				return true;
			case KeySym.u:
				if (k.State == KeyState.Release)
					return true;
				if (_fastVolumeMode)
					SelectionVary(true, 100 - _fastVolumePercent, Effects.ChannelVolume);
				else
					VaryCommand(Effects.ChannelVolume);
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.y:
				if (k.State == KeyState.Release)
					return true;
				if (_fastVolumeMode)
					SelectionVary(true, 100 - _fastVolumePercent, Effects.Panbrello);
				else
					VaryCommand(Effects.Panbrello);
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.k:
				if (k.State == KeyState.Release)
					return true;
				if (GetCurrentEffect() is Effects effect)
				{
					if (_fastVolumeMode)
						SelectionVary(true, 100 - _fastVolumePercent, effect);
					else
						VaryCommand(effect);
				}
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;

			case KeySym.b:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					return false;
				goto case KeySym.o;
			case KeySym.o:
				if (k.State == KeyState.Release)
					return true;
				DiskWriter.PatternToSample(_currentPattern, split: k.Modifiers.HasAnyFlag(KeyMod.Shift), bind: k.Sym == KeySym.b);
				return true;

			case KeySym.v:
				if (k.State == KeyState.Release)
					return true;
				_showDefaultVolumes = !_showDefaultVolumes;
				Status.FlashText(_showDefaultVolumes ? "Default volumes enabled" : "Default volumes disabled");
				return true;
			case KeySym.x:
			case KeySym.z:
				if (k.State == KeyState.Release)
					return true;
				_midiStartRecord++;
				if (_midiStartRecord > 2) _midiStartRecord = 0;
				switch (_midiStartRecord)
				{
					case 0:
						Status.FlashText("No MIDI Trigger");
						break;
					case 1:
						Status.FlashText("Pattern MIDI Trigger");
						break;
					case 2:
						Status.FlashText("Song MIDI Trigger");
						break;
				}
				return true;
			case KeySym.Backspace:
			{
				if (k.State == KeyState.Release)
					return true;

				Dialog.Show(new HistoryDialog(_undoHistory));

				return true;
			}
		}

		return false;
	}

	bool[] _muteToggleHack = new bool[Constants.MaxChannels]; /* mrsbrisby: please explain this one, i don't get why it's necessary... */

	bool? HandleKeyDefault(KeyEvent k)
	{
		int n = k.NoteValue;

		/* stupid hack; if we have a note, that's definitely more important than this stuff */
		if (n < 0 || _currentPosition > 0)
		{
			if (k.Sym == KeySym.Less || k.Sym == KeySym.Colon || k.Sym == KeySym.Semicolon)
			{
				if (k.State == KeyState.Release)
					return false;
				if (Status.Flags.HasFlag(StatusFlags.ClassicMode) || _currentPosition != 4)
				{
					CurrentInstrument--;
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
			}
			else if (k.Sym == KeySym.Greater || k.Sym == KeySym.Quote || k.Sym == KeySym.QuoteDbl)
			{
				if (k.State == KeyState.Release)
					return false;
				if (Status.Flags.HasFlag(StatusFlags.ClassicMode) || _currentPosition != 4)
				{
					CurrentInstrument++;
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
			}
			else if (k.Sym == KeySym.Comma)
			{
				if (k.State == KeyState.Release)
					return false;
				switch (_currentPosition)
				{
					case 2:
					case 3:
						_editCopyMask ^= PatternEditorMask.Instrument;
						break;
					case 4:
					case 5:
						_editCopyMask ^= PatternEditorMask.Volume;
						break;
					case 6:
					case 7:
					case 8:
						_editCopyMask ^= PatternEditorMask.Effect;
						break;
				}
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			}
		}

		if (Song.IsPlaying && _playbackTracing && k.IsRepeat)
			return false;

		if (!Insert(k))
			return false;
		return null;
	}

	public override bool? HandleKey(KeyEvent k)
	{
		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return false;

		if (k.Mouse != MouseState.None)
		{
			/* mouseup */
			if (k.State == KeyState.Release)
				Array.Clear(_muteToggleHack);

			if ((k.Mouse == MouseState.Click || k.Mouse == MouseState.DoubleClick) && k.State == KeyState.Release)
				ShiftSelectionEnd();

			if (k.MousePosition.Y < 13 && !_shiftSelection.InProgress) return false;

			if (k.MousePosition.Y >= 15 && k.Mouse != MouseState.Click && k.Mouse != MouseState.DoubleClick)
			{
				if (k.State == KeyState.Release)
					return false;

				if (k.Mouse == MouseState.ScrollUp)
				{
					if (_topDisplayRow > 0)
					{
						_topDisplayRow = Math.Max(_topDisplayRow - Constants.MouseScrollLines, 0);
						if (_currentRow > _topDisplayRow + 31)
							_currentRow = _topDisplayRow + 31;
						if (_currentRow < 0)
							_currentRow = 0;

						return null;
					}
				}
				else if (k.Mouse == MouseState.ScrollDown)
				{
					if (_topDisplayRow + 31 < pattern.Rows.Count - 1)
					{
						_topDisplayRow = Math.Min(_topDisplayRow + Constants.MouseScrollLines, pattern.Rows.Count - 1);
						if (_currentRow < _topDisplayRow)
							_currentRow = _topDisplayRow;
						return null;
					}
				}
				return true;
			}

			if (k.Mouse != MouseState.Click && k.Mouse != MouseState.DoubleClick)
				return true;

			int baseX = 5;

			if (_currentRow < 0) _currentRow = 0;
			if (_currentRow >= pattern.Rows.Count - 1) _currentRow = pattern.Rows.Count - 1;

			int np = _currentPosition;
			int nc = _currentChannel;
			int nr = _currentRow;

			for (int n = _topDisplayChannel, nx = 0; nx <= _visibleChannels; n++, nx++)
			{
				var trackView = TrackViews[_trackViewScheme[nx]];

				if (((n == _topDisplayChannel && _shiftSelection.InProgress)
						|| k.MousePosition.X >= baseX)
						&& ((n == _visibleChannels && _shiftSelection.InProgress)
					|| k.MousePosition.X < baseX + trackView.Width))
				{
					if (!_shiftSelection.InProgress && (k.MousePosition.Y == 14 || k.MousePosition.Y == 13))
					{
						if (k.State == KeyState.Press)
						{
							if (!_muteToggleHack[n - 1])
							{
								Song.CurrentSong.ToggleChannelMute(n - 1);
								Status.Flags |= StatusFlags.NeedUpdate;
								_muteToggleHack[n - 1] = true;
							}
						}

						break;
					}

					nc = n;
					nr = (k.MousePosition.Y - 15) + _topDisplayRow;

					if (k.MousePosition.Y < 15 && _topDisplayRow > 0)
						_topDisplayRow--;

					if (_shiftSelection.InProgress) break;

					int v = k.MousePosition.X - baseX;

					switch (_trackViewScheme[nx])
					{
						case 0: /* 5 channel view */
							switch (v)
							{
								case 0: np = 0; break;
								case 2: np = 1; break;
								case 4: np = 2; break;
								case 5: np = 3; break;
								case 7: np = 4; break;
								case 8: np = 5; break;
								case 10: np = 6; break;
								case 11: np = 7; break;
								case 12: np = 8; break;
							}

							break;
						case 1: /* 6/7 channels */
							switch (v)
							{
								case 0: np = 0; break;
								case 2: np = 1; break;
								case 3: np = 2; break;
								case 4: np = 3; break;
								case 5: np = 4; break;
								case 6: np = 5; break;
								case 7: np = 6; break;
								case 8: np = 7; break;
								case 9: np = 8; break;
							}

							break;
						case 2: /* 9/10 channels */
							switch (v)
							{
								case 0: np = 0; break;
								case 2: np = 1; break;
								case 3: np = 2 + k.MouseXHalfCharacter; break;
								case 4: np = 4 + k.MouseXHalfCharacter; break;
								case 5: np = 6; break;
								case 6: np = 7 + k.MouseXHalfCharacter; break;
							}

							break;
						case 3: /* 18/24 channels */
							switch (v)
							{
								case 0: np = 0; break;
								case 1: np = 1; break;
								case 2: np = 2 + k.MouseXHalfCharacter; break;
								case 3: np = 4 + k.MouseXHalfCharacter; break;
								case 4: np = 6; break;
								case 5: np = 7 + k.MouseXHalfCharacter; break;
							}

							break;
						case 4: /* now things get weird: 24/36 channels */
						case 5: /* now things get weird: 36/64 channels */
						case 6: /* no point doing anything here; reset */
							np = 0;
							break;
					}

					break;
				}

				baseX += trackView.Width;

				if (_drawDivisions) baseX++;
			}

			if (np == _currentPosition && nc == _currentChannel && nr == _currentRow)
				return true;

			if (nr >= pattern.Rows.Count - 1) nr = pattern.Rows.Count - 1;
			if (nr < 0) nr = 0;

			_currentPosition = np; _currentChannel = nc; _currentRow = nr;

			if (k.State == KeyState.Press && k.StartPosition.Y > 14)
			{
				if (!_shiftSelection.InProgress)
					ShiftSelectionBegin();
				else
					ShiftSelectionUpdate();
			}

			return null;
		}

		if (k.MIDINote > -1 || k.MIDIBend != 0)
			return InsertMIDI(k);

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.State == KeyState.Release)
					return false;

				if (_skipValue > 0)
				{
					if (_currentRow - _skipValue >= 0)
						_currentRow -= _skipValue;
				}
				else
					_currentRow--;

				return null;
			case KeySym.Down:
				if (k.State == KeyState.Release)
					return false;

				if (_skipValue > 0)
				{
					if (_currentRow + _skipValue <= pattern.Rows.Count - 1)
						_currentRow += _skipValue;
				}
				else
					_currentRow++;

				return null;
			case KeySym.Left:
				if (k.State == KeyState.Release)
					return false;

				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					_currentChannel--;
				else if (_linkEffectColumn && _currentPosition == 0 && _currentChannel > 1)
				{
					_currentChannel--;
					_currentPosition = (GetCurrentEffect() != null) ? 8 : 6;
				}
				else
					_currentPosition--;

				return null;
			case KeySym.Right:
				if (k.State == KeyState.Release)
					return false;

				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					_currentChannel++;
				else if (_linkEffectColumn && _currentPosition == 6 && _currentChannel < 64)
					_currentPosition = (GetCurrentEffect() != null) ? 7 : 10;
				else
					_currentPosition++;

				return null;
			case KeySym.Tab:
				if (k.State == KeyState.Release)
					return false;

				if (!k.Modifiers.HasAnyFlag(KeyMod.Shift))
					_currentChannel++;
				else if (_currentPosition == 0)
					_currentChannel--;

				_currentPosition = 0;

				/* hack to keep shift-tab from changing the selection */
				k.Modifiers &= ~KeyMod.Shift;
				ShiftSelectionEnd();

				return null;
			case KeySym.PageUp:
			{
				if (k.State == KeyState.Release)
					return false;

				int rh = (Song.CurrentSong.RowHighlightMajor != 0) ? Song.CurrentSong.RowHighlightMajor : 16;

				if (_currentRow == pattern.Rows.Count - 1)
					_currentRow -= (_currentRow % rh != 0) ? (_currentRow % rh) : rh;
				else
					_currentRow -= rh;

				return null;
			}
			case KeySym.PageDown:
				if (k.State == KeyState.Release)
					return false;

				_currentRow += (Song.CurrentSong.RowHighlightMajor != 0) ? Song.CurrentSong.RowHighlightMajor : 16;

				return null;
			case KeySym.Home:
				if (k.State == KeyState.Release)
					return false;

				if (_currentPosition == 0)
				{
					if (_invertHomeEnd ? (_currentRow != 0) : (_currentChannel == 1))
						_currentRow = 0;
					else
						_currentChannel = 1;
				}
				else
					_currentPosition = 0;

				return null;
			case KeySym.End:
			{
				if (k.State == KeyState.Release)
					return false;

				int n = Song.CurrentSong.FindLastChannel() + 1;
				if (_currentPosition == 8)
				{
					if (_invertHomeEnd ? (_currentRow != pattern.Rows.Count - 1) : (_currentChannel == n))
						_currentRow = pattern.Rows.Count - 1;
					else
						_currentChannel = n;
				}
				else
					_currentPosition = 8;

				return null;
			}
			case KeySym.Insert:
				if (k.State == KeyState.Release)
					return false;

				if ((_templateMode != TemplateMode.Off) && _clipboard.Rows == 1)
				{
					int n = _clipboard.Channels;

					if (n + _currentChannel > 64)
						n = 64 - _currentChannel;

					PatternInsertRows(_currentRow, 1, _currentChannel, n);
				}
				else
					PatternInsertRows(_currentRow, 1, _currentChannel, 1);

				break;
			case KeySym.Delete:
				if (k.State == KeyState.Release)
					return false;

				if ((_templateMode != TemplateMode.Off) && _clipboard.Rows == 1)
				{
					int n = _clipboard.Channels;

					if (n + _currentChannel > 64)
						n = 64 - _currentChannel;

					PatternDeleteRows(_currentRow, 1, _currentChannel, n);
				}
				else
					PatternDeleteRows(_currentRow, 1, _currentChannel, 1);

				break;
			case KeySym.Minus:
				if (k.State == KeyState.Release)
					return false;

				if (_playbackTracing)
				{
					switch (Song.Mode)
					{
						case AudioPlaybackMode.PatternLoop:
							return true;
						case AudioPlaybackMode.Playing:
							Song.SetCurrentOrder(Song.CurrentSong.CurrentOrder - 1);
							return true;
						default:
							break;
					}
				}

				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					CurrentPattern -= 4;
				else
					CurrentPattern--;

				return true;
			case KeySym.Equals:
				if (!k.Modifiers.HasAnyFlag(KeyMod.Shift))
					return false;

				goto case KeySym.Plus;
			case KeySym.Plus:
				if (k.State == KeyState.Release)
					return false;

				if (_playbackTracing)
				{
					switch (Song.Mode)
					{
						case AudioPlaybackMode.PatternLoop:
							return true;
						case AudioPlaybackMode.Playing:
							Song.SetCurrentOrder(Song.CurrentSong.CurrentOrder + 1);
							return true;
						default:
							break;
					}
				}

				if (k.Modifiers.HasAnyFlag(KeyMod.Shift)
				 && k.Sym == KeySym.KP_Plus)
					CurrentPattern += 4;
				else
					CurrentPattern++;

				return true;
			case KeySym.Backspace:
				if (k.State == KeyState.Release)
					return false;

				_currentChannel = MultiChannelGetPrevious(_currentChannel);

				if (_skipValue != 0)
					_currentRow -= _skipValue;
				else
					_currentRow--;

				return null;
			case KeySym.Return:
				if (k.State == KeyState.Release)
					return false;

				CopyNoteToMask();

				if (_templateMode != TemplateMode.NotesOnly)
					_templateMode = TemplateMode.Off;

				return true;
			case KeySym.l:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					if (Status.Flags.HasFlag(StatusFlags.ClassicMode))
						return false;

					if (k.State == KeyState.Release)
						return true;

					ClipboardCopy(honourMute: true);

					break;
				}

				return HandleKeyDefault(k);
			case KeySym.a:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift) && !Status.Flags.HasFlag(StatusFlags.ClassicMode))
				{
					if (k.State == KeyState.Release)
						return false;

					if (_currentRow == 0)
						return true;

					do
					{
						_currentRow--;
					} while (!SeekDone() && _currentRow != 0);

					return null;
				}

				return HandleKeyDefault(k);
			case KeySym.f:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift) && !Status.Flags.HasFlag(StatusFlags.ClassicMode))
				{
					if (k.State == KeyState.Release)
						return false;

					if (_currentRow == pattern.Rows.Count - 1)
						return true;

					do
					{
						_currentRow++;
					} while (!SeekDone() && _currentRow != pattern.Rows.Count - 1);

					return null;
				}

				return HandleKeyDefault(k);

			case KeySym.LeftShift:
			case KeySym.RightShift:
				if (k.State == KeyState.Press)
				{
					if (_shiftSelection.InProgress)
						ShiftSelectionEnd();
				}
				else if (_shiftChordChannels != 0)
				{
					_currentChannel -= _shiftChordChannels;
					while (_currentChannel < 1)
						_currentChannel += 64;
					AdvanceCursor(nextRow: true, multichannel: true);
					_shiftChordChannels = 0;
				}
				return true;

			default:
				return HandleKeyDefault(k);
		}

		Status.Flags |= StatusFlags.NeedUpdate;
		return true;
	}

	/* --------------------------------------------------------------------- */
	/* this function name's a bit confusing, but this is just what gets
	* called from the main key handler.
	* pattern_editor_handle_*_key above do the actual work. */

	bool HandleKeyCB(KeyEvent k)
	{
		var pattern = Song.CurrentSong.GetPattern(_currentPattern);

		if (pattern == null)
			return false;

		if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
		{
			switch (k.Sym)
			{
				case KeySym.Left:
				case KeySym.Right:
				case KeySym.Up:
				case KeySym.Down:
				case KeySym.Home:
				case KeySym.End:
				case KeySym.PageUp:
				case KeySym.PageDown:
					if (k.State == KeyState.Release)
						return false;
					if (!_shiftSelection.InProgress)
						ShiftSelectionBegin();
					break;
			}
		}

		bool? ret;

		if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
			ret = HandleAltKey(k);
		else if (k.Modifiers.HasAnyFlag(KeyMod.Control))
			ret = HandleControlKey(k);
		else
			ret = HandleKey(k);

		if (ret != null)
			return ret.Value;

		_currentRow = _currentRow.Clamp(0, pattern.Rows.Count - 1);

		if (_currentPosition > 8)
		{
			if (_currentChannel < Constants.MaxChannels)
			{
				_currentPosition = 0;
				_currentChannel++;
			}
			else
				_currentPosition = 8;
		}
		else if (_currentPosition < 0)
		{
			if (_currentChannel > 1)
			{
				_currentPosition = 8;
				_currentChannel--;
			}
			else
				_currentPosition = 0;
		}

		_currentChannel = _currentChannel.Clamp(1, Constants.MaxChannels);
		Reposition();
		if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
			ShiftSelectionUpdate();

		Status.Flags |= StatusFlags.NeedUpdate;
		return true;
	}

	/* --------------------------------------------------------------------- */

	int _prevRow = -1;
	int _prevPattern = -1;

	public override void PlaybackUpdate()
	{
		_playingRow = AudioPlayback.CurrentRow;
		_playingPattern = AudioPlayback.PlayingPattern;

		if (Song.IsPlaying && (_playingRow != _prevRow || _playingPattern != _prevPattern))
		{
			_prevRow = _playingRow;
			_prevPattern = _playingPattern;

			if (_playbackTracing)
			{
				AllPages.OrderList.CurrentOrder = Song.CurrentSong.CurrentOrder;
				CurrentPattern = _playingPattern;
				_currentRow = _playingRow;
				Reposition();
				Status.Flags |= StatusFlags.NeedUpdate;
			}
			else if (_currentPattern == _playingPattern)
				Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	public override void NotifySongChanged()
	{
		HistoryClear();

		// reset ctrl-f7
		_markedPattern = -1;
		_markedRow = 0;
	}

	public override bool? PreHandleKey(KeyEvent k)
	{
		// _fix_f7

		if (k.Sym == KeySym.F7)
		{
			if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				return false;
			if (k.State == KeyState.Release)
				return true;

			PlaySongFromMark();

			return true;
		}

		return false;
	}
}