using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.Dialogs.Samples;
using ChasmTracker.Events;
using ChasmTracker.FileSystem;
using ChasmTracker.Input;
using ChasmTracker.Memory;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

using FileTypes = FileSystem.FileTypes;

public class SampleFileListPageBase : Page
{
	OtherWidget otherFileList;
	TextEntryWidget textEntryFileName;
	NumberEntryWidget numberEntrySampleSpeed;
	MenuToggleWidget menuToggleLoop;
	NumberEntryWidget numberEntryLoopStart;
	NumberEntryWidget numberEntryLoopEnd;
	MenuToggleWidget menuToggleSustainLoop;
	NumberEntryWidget numberEntrySustainLoopStart;
	NumberEntryWidget numberEntrySustainLoopEnd;
	ThumbBarWidget thumbBarDefaultVolume;
	ThumbBarWidget thumbBarGlobalVolume;
	ThumbBarWidget thumbBarVibratoSpeed;
	ThumbBarWidget thumbBarVibratoDepth;
	ThumbBarWidget thumbBarVibratoRate;

	/* the locals */
	VGAMemOverlay _sampleImage;

	const int MaxFileNameLength = 21;

	char[] _currentFileName = new char[MaxFileNameLength - 1];

	bool _libraryMode = false;
	bool _fakeSlotChanged = false;
	int _willMoveTo = -1;
	int _fakeSlot = KeyJazz.NoInstrument;

	protected void SetLibraryMode(bool newMode)
	{
		_libraryMode = newMode;
	}

	enum LoopStates
	{
		[Description("Off")] Off,
		[Description("On Forwards")] Forward,
		[Description("On Ping Pong")] PingPong,
	}

	static string s_sampleCwd = "";

	/* --------------------------------------------------------------------------------------------------------- */

	/* files:
		file type       color   displayed title                 notes
		---------       -----   ---------------                 -----
		unchecked       4       <the filename>                  IT uses color 6 for these
		directory       5       "........Directory........"     dots are char 154 (same for libraries)
		sample          3       <the sample name>
		libraries       6       ".........Library........."     IT uses color 3. maybe use module name here?
		unknown         2       <the filename>                  any regular file that's not recognized
	*/

	int _topFile = 0;
	DateTime _directoryModifiedTimeUTC;
	FileList _flist = new FileList();

	List<char> _searchStr = new List<char>();
	bool _haveSearch = false;

	public SampleFileListPageBase(string title, PageNumbers pageNumber, HelpTexts helpText)
		: base(pageNumber, title, helpText)
	{
		_sampleImage = VGAMem.AllocateOverlay(
			new Point(52, 25),
			new Point(76, 28));

		ClearDirectory();

		otherFileList = new OtherWidget(
			new Point(6, 13),
			new Size(43, 34));

		otherFileList.OtherHandleKey += FileListHandleKey;
		otherFileList.OtherHandleText += FileListHandleTextInput;
		otherFileList.OtherRedraw += FileListDraw;
		otherFileList.OtherAcceptsText = true;

		textEntryFileName = new TextEntryWidget(
			new Point(64, 13),
			13,
			new string(_currentFileName),
			MaxFileNameLength);

		numberEntrySampleSpeed = new NumberEntryWidget(
			new Point(64, 14),
			7,
			0, 9999999,
			new Shared<int>());

		menuToggleLoop = new MenuToggleWidget(
			new Point(64, 15),
			Enum.GetValues<LoopStates>().Select(state => state.GetDescription()).ToArray());
		numberEntryLoopStart = new NumberEntryWidget(
			new Point(64, 16),
			7,
			0, 9999999,
			new Shared<int>());
		numberEntryLoopEnd = new NumberEntryWidget(
			new Point(64, 17),
			7,
			0, 9999999,
			new Shared<int>());

		menuToggleSustainLoop = new MenuToggleWidget(
			new Point(64, 18),
			Enum.GetValues<LoopStates>().Select(state => state.GetDescription()).ToArray());
		numberEntrySustainLoopStart = new NumberEntryWidget(
			new Point(64, 19),
			7,
			0, 9999999,
			new Shared<int>());
		numberEntrySustainLoopEnd = new NumberEntryWidget(
			new Point(64, 20),
			7,
			0, 9999999,
			new Shared<int>());

		thumbBarGlobalVolume = new ThumbBarWidget(
			new Point(63, 33),
			9,
			0, 64);

		thumbBarDefaultVolume = new ThumbBarWidget(
			new Point(63, 34),
			9,
			0, 64);

		thumbBarVibratoSpeed = new ThumbBarWidget(
			new Point(63, 37),
			9,
			0, 64);
		thumbBarVibratoDepth = new ThumbBarWidget(
			new Point(63, 38),
			9,
			0, 32);
		thumbBarVibratoRate = new ThumbBarWidget(
			new Point(63, 39),
			9,
			0, 255);

		textEntryFileName.Changed += HandlePreload;

		numberEntrySampleSpeed.Changed += HandleLoadUpdate;
		menuToggleLoop.Changed += HandleLoadUpdate;
		numberEntryLoopStart.Changed += HandleLoadUpdate;
		numberEntryLoopEnd.Changed += HandleLoadUpdate;
		menuToggleSustainLoop.Changed += HandleLoadUpdate;
		numberEntrySustainLoopStart.Changed += HandleLoadUpdate;
		numberEntrySustainLoopEnd.Changed += HandleLoadUpdate;
		thumbBarGlobalVolume.Changed += HandleLoadUpdate;
		thumbBarDefaultVolume.Changed += HandleLoadUpdate;
		thumbBarVibratoSpeed.Changed += HandleLoadUpdate;
		thumbBarVibratoDepth.Changed += HandleLoadUpdate;
		thumbBarVibratoRate.Changed += HandleLoadUpdate;
	}

	static int GetTypeColour(FileTypes type)
	{
		if (type == FileTypes.Directory)
			return 5;
		if (!type.HasAnyFlag(FileTypes.ExtendedDataMask))
			return 4; /* unchecked */
		if (type.HasFlag(FileTypes.BrowsableMask))
			return 6; /* library */
		if (type == FileTypes.Unknown)
			return 2;
		return 3; /* sample */
	}

	void ClearDirectory()
	{
		_flist.Clear();
		_fakeSlot = KeyJazz.NoInstrument;
		_fakeSlotChanged = false;
	}

	void FileListReposition()
	{
		FileReference? f = null;

		_flist.SelectedIndex = _flist.SelectedIndex.Clamp(0, _flist.NumFiles - 1);

		// XXX use CLAMP() here too, I can't brain
		if (_flist.SelectedIndex < _topFile)
			_topFile = _flist.SelectedIndex;
		else if (_flist.SelectedIndex > _topFile + 34)
			_topFile = _flist.SelectedIndex - 34;
		if (_flist.SelectedIndex >= 0 && _flist.SelectedIndex < _flist.NumFiles)
			f = _flist[_flist.SelectedIndex];

		Array.Clear(_currentFileName);

		if ((f != null) && !string.IsNullOrEmpty(f.SampleFileName))
			f.SampleFileName!.CopyTo(_currentFileName);
		else if ((f != null) && !string.IsNullOrEmpty(f.BaseName))
		{
			// FIXME
			f.BaseName.CopyTo(_currentFileName);
		}

		textEntryFileName!.FirstCharacter = 0;
		textEntryFileName!.CursorPosition = _currentFileName.Length;

		numberEntrySampleSpeed!.Value = f?.SampleSpeed ?? 0;

		if ((f != null) && f.SampleFlags.HasFlag(SampleFlags.PingPongLoop))
			menuToggleLoop!.State = 2;
		else if ((f != null) && f.SampleFlags.HasFlag(SampleFlags.Loop))
			menuToggleLoop!.State = 1;
		else
			menuToggleLoop!.State = 0;

		numberEntryLoopStart!.Value = f?.SampleLoopStart ?? 0;
		numberEntryLoopEnd!.Value = f?.SampleLoopEnd ?? 0;

		if ((f != null) && f.SampleFlags.HasFlag(SampleFlags.PingPongSustain))
			menuToggleSustainLoop!.State = 2;
		else if ((f != null) && f.SampleFlags.HasFlag(SampleFlags.SustainLoop))
			menuToggleSustainLoop!.State = 1;
		else
			menuToggleSustainLoop!.State = 0;

		numberEntrySustainLoopStart!.Value = f?.SampleSustainStart ?? 0;
		numberEntrySustainLoopEnd!.Value = f?.SampleSustainEnd ?? 0;

		thumbBarDefaultVolume!.Value = f?.SampleDefaultVolume ?? 64;
		thumbBarGlobalVolume!.Value = f?.SampleGlobalVolume ?? 64;
		thumbBarVibratoSpeed!.Value = f?.SampleVibratoSpeed ?? 0;
		thumbBarVibratoDepth!.Value = f?.SampleVibratoDepth ?? 0;
		thumbBarVibratoRate!.Value = f?.SampleVibratoRate ?? 0;

		if (f != null)
		{
			/* autoload some files */
			if (f.Type.HasFlag(FileTypes.SampleExtended)
					&& f.FileSize < 0x4000000 && f.SampleLength < 0x1000000)
				HandlePreload();
		}
	}

	void ReadDirectory()
	{
		ClearDirectory();

		try
		{
			_directoryModifiedTimeUTC = Directory.GetLastWriteTimeUtc(s_sampleCwd);
		}
		catch
		{
			_directoryModifiedTimeUTC = default;
		}

		/* if the stat call failed, this will probably break as well, but
		at the very least, it'll add an entry for the root directory. */
		_flist.Clear();

		try
		{
			DirectoryScanner.Populate(
				s_sampleCwd,
				_flist,
				dirList: null,
				DirectoryScanner.ReadSampleLibraryThunk);
		}
		catch (Exception e)
		{
			Log.AppendException(e);
		}

		DirectoryScanner.SetAsynchronousFileListParameters(
			_flist,
			DirectoryScanner.FillExtendedData,
			_flist.SelectedIndexRef,
			FileListReposition);

		DirectoryScanner.CacheLookup(s_sampleCwd, _flist, null);

		FileListReposition();
	}

	/* return: 1 = success, 0 = failure
	TODO: provide some sort of feedback if something went wrong. */
	void ChangeDirectory(string dir)
	{
		dir = DirectoryScanner.NormalizePath(dir);

		DirectoryScanner.CacheUpdate(Configuration.Directories.SamplesDirectory, _flist, null);

		if (Directory.Exists(dir))
			Configuration.Directories.SamplesDirectory = dir;

		s_sampleCwd = dir;

		ReadDirectory();
	}

	/* --------------------------------------------------------------------------------------------------------- */

	public override void DrawConst()
	{
		VGAMem.DrawBox(new Point(5, 12), new Point(50, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawFillCharacters(new Point(6, 13), new Point(49, 47), (VGAMem.DefaultForeground, 0));

		VGAMem.DrawFillCharacters(new Point(64, 13), new Point(77, 22), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(62, 32), new Point(72, 35), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(62, 36), new Point(72, 40), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawBox(new Point(63, 12), new Point(77, 23), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawBox(new Point(51, 24), new Point(77, 29), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawFillCharacters(new Point(52, 25), new Point(76, 28), (VGAMem.DefaultForeground, 0));

		VGAMem.DrawBox(new Point(51, 30), new Point(77, 42), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawFillCharacters(new Point(59, 44), new Point(76, 47), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(58, 43), new Point(77, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		if ((_flist.SelectedIndex >= 0) && (_flist.SelectedIndex < _flist.NumFiles))
		{
			var f = _flist[_flist.SelectedIndex];

			VGAMem.DrawTextLen(f.SampleLength.ToString("d7"), 13, new Point(64, 22), (2, 0));

			if ((f.SampleLength == 0) && (f.SampleFileName == null) && (f.SampleFlags == default))
				VGAMem.DrawTextLen("No sample", 13, new Point(64, 21), (2, 0));
			else if (f.SampleFlags.HasFlag(SampleFlags.Stereo))
				VGAMem.DrawTextLen(
					f.SampleFlags.HasFlag(SampleFlags._16Bit)
					? "16 bit Stereo" : "8 bit Stereo",
					13, new Point(64, 21), (2, 0));
			else
				VGAMem.DrawTextLen(
					f.SampleFlags.HasFlag(SampleFlags._16Bit)
					? "16 bit" : "8 bit",
					13, new Point(64, 21), (2, 0));

			if (!string.IsNullOrWhiteSpace(f.Description))
				VGAMem.DrawTextLen(f.Description,
					18,
					new Point(59, 44), (5, 0));
			else
			{
				switch (f.Type)
				{
					case FileTypes.Directory:
						VGAMem.DrawText("Directory",
							new Point(59, 44), (5, 0));
						break;
					default:
						VGAMem.DrawText("Unknown format",
							new Point(59, 44), (5, 0));
						break;
				}
			}

			VGAMem.DrawText(f.FileSize.ToString("d7"), new Point(59, 45), (5, 0));
			VGAMem.DrawText(f.TimeStamp.ToString(Configuration.General.DateFormat.GetFormatString()), new Point(59, 46), (5, 0));
			VGAMem.DrawText(f.TimeStamp.ToString(Configuration.General.TimeFormat.GetFormatString()), new Point(59, 47), (5, 0));
		}

		/* these are exactly the same as in page_samples.c, apart from
		* 'quality' and 'length' being one line higher */
		VGAMem.DrawText("Filename", new Point(55, 13), (0, 2));
		VGAMem.DrawText("Speed", new Point(58, 14), (0, 2));
		VGAMem.DrawText("Loop", new Point(59, 15), (0, 2));
		VGAMem.DrawText("LoopBeg", new Point(56, 16), (0, 2));
		VGAMem.DrawText("LoopEnd", new Point(56, 17), (0, 2));
		VGAMem.DrawText("SusLoop", new Point(56, 18), (0, 2));
		VGAMem.DrawText("SusLBeg", new Point(56, 19), (0, 2));
		VGAMem.DrawText("SusLEnd", new Point(56, 20), (0, 2));
		VGAMem.DrawText("Quality", new Point(56, 21), (0, 2));
		VGAMem.DrawText("Length", new Point(57, 22), (0, 2));

		/* these abbreviations are sucky and lame. any suggestions? */
		VGAMem.DrawText("Def. Vol.", new Point(53, 33), (0, 2));
		VGAMem.DrawText("Glb. Vol.", new Point(53, 34), (0, 2));
		VGAMem.DrawText("Vib.Speed", new Point(53, 37), (0, 2));
		VGAMem.DrawText("Vib.Depth", new Point(53, 38), (0, 2));
		VGAMem.DrawText("Vib. Rate", new Point(53, 39), (0, 2));

		VGAMem.DrawText("Format", new Point(52, 44), (0, 2));
		VGAMem.DrawText("Size", new Point(54, 45), (0, 2));
		VGAMem.DrawText("Date", new Point(54, 46), (0, 2));
		VGAMem.DrawText("Time", new Point(54, 47), (0, 2));

		if (_fakeSlot != KeyJazz.NoInstrument)
		{
			var s = Song.CurrentSong.GetSample(_fakeSlot);

			_sampleImage.Clear(0);

			if (s != null)
				VGAMem.DrawSampleData(_sampleImage, s);
			else
				VGAMem.ApplyOverlay(_sampleImage);
		}
	}

	/* --------------------------------------------------------------------------------------------------------- */

	protected void CommonSetPage()
	{
		if (string.IsNullOrWhiteSpace(s_sampleCwd))
			s_sampleCwd = Configuration.Directories.SamplesDirectory;

		/* if we have a list, the directory didn't change, and the mtime is the same, we're set */
		if (_flist.NumFiles > 0
				&& !Status.Flags.HasFlag(StatusFlags.SamplesDirectoryChanged)
				&& (Directory.GetLastWriteTimeUtc(s_sampleCwd) == _directoryModifiedTimeUTC))
			return;

		ChangeDirectory(s_sampleCwd);

		Status.Flags &= ~StatusFlags.SamplesDirectoryChanged;

		_fakeSlot = KeyJazz.NoInstrument;
		_fakeSlotChanged = false;

		SelectedWidgetIndex.Value = 0;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	void FileListDraw()
	{
		/* there's no need to have if (files) { ... } like in the load-module page,
			because there will always be at least "/" in the list */
		if (_topFile < 0) _topFile = 0;
		if (_flist.SelectedIndex < 0) _flist.SelectedIndex = 0;

		int pos = 13;

		for (int n = _topFile; n < _flist.NumFiles && pos < 48; n++, pos++)
		{
			var file = _flist[n];

			int fg, bg;

			if (n == _flist.SelectedIndex && SelectedActiveWidgetIndex?.Value == 0)
			{
				fg = 0;
				bg = 3;
			}
			else
			{
				fg = GetTypeColour(file.Type);
				bg = 0;
			}

			VGAMem.DrawText((n + 1).ToString("d3"), new Point(2, pos), (0, 2));
			VGAMem.DrawTextLen(file.Title, 25, new Point(6, pos), (fg, bg));
			VGAMem.DrawCharacter((char)168, new Point(31, pos), (2, bg));
			VGAMem.DrawTextLen(file.BaseName, 18, new Point(32, pos), (fg, bg));

			/* this is stupid */
			if (_haveSearch)
			{
				int highlightLength = file.BaseName.Length;

				for (int i = 0; (i < file.BaseName.Length) && (i < _searchStr.Count); i++)
				{
					if (char.ToLowerInvariant(file.BaseName[i]) != char.ToLowerInvariant(_searchStr[i]))
					{
						highlightLength = i;
						break;
					}
				}

				if (highlightLength > 18)
					highlightLength = 18;

				VGAMem.DrawTextLen(file.BaseName, highlightLength, new Point(32, pos), (3, 1));
			}
		}

		/* draw the info for the current file (or directory...) */

		while (pos < 48)
			VGAMem.DrawCharacter((char)168, new Point(31, pos++), (2, 0));
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* Nasty mess to load a sample and prompt for stereo convert / create host instrument as necessary. */
	// TODO

	void RepositionAtSlashSearch()
	{
		if (!_haveSearch)
			return;

		int bl = -1;
		int b = -1;

		for (int i = 0; i < _flist.NumFiles; i++)
		{
			var f = _flist[i];

			if ((f == null) || string.IsNullOrWhiteSpace(f.BaseName))
				continue;

			int j;

			for (j = 0; (j < f.BaseName.Length) && (j < _searchStr.Count); j++)
				if (char.ToLower(f.BaseName[j]) != char.ToLower(_searchStr[j]))
					break;

			if (bl < j)
			{
				bl = j;
				b = i;
			}
		}

		if (bl > 0)
		{
			_flist.SelectedIndex = b;
			FileListReposition();
		}
	}

	void FinishLoad(int cur)
	{
		Status.Flags |= StatusFlags.SongNeedsSave;
		MemoryUsage.NotifySongChanged();
		var smp = Song.CurrentSong.GetSample(cur);

		if ((smp != null) && smp.Flags.HasFlag(SampleFlags.Stereo))
		{
			var dialog = Dialog.Show<StereoConversionDialog>();

			dialog.SelectionMade +=
				selection =>
				{
					switch (selection)
					{
						case StereoConversionSelection.Left:
							SampleEditOperations.MonoLeft(smp);
							break;
						case StereoConversionSelection.Right:
							SampleEditOperations.MonoRight(smp);
							break;
					}

					MemoryUsage.NotifySongChanged();

					Dialog.Destroy();

					if (selection == StereoConversionSelection.Both)
						SampleListPage.ShowSampleHostDialog(PageNumbers.SampleList);
					else
						FinishLoad(cur);
				};

			return;
		}

		SampleListPage.ShowSampleHostDialog(PageNumbers.SampleList);
	}

	/* on the file list, that is */
	void HandleEnterKey()
	{
		int cur = AllPages.SampleList.CurrentSample;

		if ((_flist.SelectedIndex < 0) || (_flist.SelectedIndex >= _flist.NumFiles))
			return;

		var file = _flist[_flist.SelectedIndex];

		DirectoryScanner.CacheUpdate(Configuration.Directories.SamplesDirectory, _flist, null);
		DirectoryScanner.FillExtendedData(file);

		if (file.Type.HasAnyFlag(FileTypes.BrowsableMask | FileTypes.InstrumentMask)
		&& !file.Type.HasAnyFlag(FileTypes.SampleMask))
		{
			ChangeDirectory(file.FullPath);
			Status.Flags |= StatusFlags.NeedUpdate;
		}
		else if (_libraryMode)
			return;
		else if (file.Sample != null)
		{
			/* it's already been loaded, so copy it */
			Song.CurrentSong.Samples[cur] = file.Sample;
			FinishLoad(cur);
			MemoryUsage.NotifySongChanged();
		}
		else if (file.Type.HasAnyFlag(FileTypes.SampleMask))
		{
			/* load the sample */
			Song.CurrentSong.LoadSample(cur, file.FullPath);
			FinishLoad(cur);
			MemoryUsage.NotifySongChanged();
		}
	}

	void DoDiscardChangesAndMove()
	{
		_fakeSlot = KeyJazz.NoInstrument;
		_fakeSlotChanged = false;
		_searchStr.Clear();
		_haveSearch = false;
		_flist.SelectedIndex = _willMoveTo;

		FileListReposition();

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void DoDeleteFile()
	{
		if (_flist.SelectedIndex < 0 || _flist.SelectedIndex >= _flist.NumFiles)
			return;

		var ptr = _flist[_flist.SelectedIndex].FullPath;


		/* would be neat to send it to the trash can if there is one */
		File.Delete(ptr);

		/* remember the list positions */
		var oldTopFile = _topFile;
		var oldSelectedIndex = _flist.SelectedIndex;

		ReadDirectory();

		/* put the list positions back */
		_topFile = oldTopFile;
		_flist.SelectedIndex = oldSelectedIndex;

		/* edge case: if this was the last file, move the cursor up */
		if (_flist.SelectedIndex >= _flist.NumFiles)
			_flist.SelectedIndex--;

		FileListReposition();
	}

	bool FileListHandleTextInput(TextInputEvent evt)
	{
		bool success = false;

		foreach (var ch in evt.Text)
		{
			if ((ch >= 32) && (_searchStr.Any() || (_flist.SelectedIndex >= 0) && (_flist.SelectedIndex < _flist.NumFiles)))
			{
				_searchStr.Add(ch);

				RepositionAtSlashSearch();

				Status.Flags |= StatusFlags.NeedUpdate;
			}

			success = true;
		}

		return success;
	}

	bool FileListHandleKey(KeyEvent k)
	{
		int newFile = _flist.SelectedIndex;

		newFile = newFile.Clamp(0, _flist.NumFiles - 1);

		if (!Status.Flags.HasFlag(StatusFlags.ClassicMode) && k.Sym == KeySym.n && k.Modifiers.HasAnyFlag(KeyMod.Alt))
		{
			if (k.State == KeyState.Release)
				AudioPlayback.ToggleMultichannelMode();
			return true;
		}

		if (k.Mouse != default)
		{
			if (k.MousePosition.X >= 6 && k.MousePosition.X <= 49 && k.MousePosition.Y >= 13 && k.MousePosition.Y <= 47)
			{
				_searchStr.Clear();

				if (k.Mouse == MouseState.ScrollUp)
					newFile -= Constants.MouseScrollLines;
				else if (k.Mouse == MouseState.ScrollDown)
					newFile += Constants.MouseScrollLines;
				else
					newFile = _topFile + (k.MousePosition.Y - 13);
			}
		}

		switch (k.Sym)
		{
			case KeySym.Up: newFile--; _searchStr.Clear(); break;
			case KeySym.Down: newFile++; _searchStr.Clear(); break;
			case KeySym.PageUp: newFile -= 35; _searchStr.Clear(); break;
			case KeySym.PageDown: newFile += 35; _searchStr.Clear(); break;
			case KeySym.Home: newFile = 0; _searchStr.Clear(); break;
			case KeySym.End: newFile = _flist.NumFiles - 1; _searchStr.Clear(); break;

			case KeySym.Escape:
				if (!_haveSearch)
				{
					if (k.State == KeyState.Release && !k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						SetPage(PageNumbers.SampleList);
					return true;
				} /* else fall through */
				goto case KeySym.Return;
			case KeySym.Return:
				if (!_haveSearch)
				{
					if (k.State == KeyState.Press)
						return false;
					HandleEnterKey();
					_searchStr.Clear();
					_haveSearch = false;
				}
				else
				{
					if (k.State == KeyState.Press)
						return true;
					_searchStr.Clear();
					_haveSearch = false;
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
				return true;
			case KeySym.Delete:
				if (k.State == KeyState.Release)
					return true;
				_searchStr.Clear();
				_haveSearch = false;
				if (_flist.NumFiles > 0)
				{
					MessageBox.Show(MessageBoxTypes.OKCancel, "Delete file?", accept: DoDeleteFile)
						.ChangeFocusTo(1);
				}
				return true;
			case KeySym.Backspace:
				if (_haveSearch)
				{
					if (k.State == KeyState.Release)
						return true;
					_searchStr.RemoveAt(_searchStr.Count - 1);
					Status.Flags |= StatusFlags.NeedUpdate;
					RepositionAtSlashSearch();
					return true;
				}
				goto case KeySym.Slash;
			case KeySym.Slash:
				if (!_haveSearch)
				{
					if (k.State == KeyState.Press)
						return false;
					_searchStr.Clear();
					_haveSearch = true;
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
				goto default;
			default:
				if (!string.IsNullOrEmpty(k.Text))
					FileListHandleTextInput(k.ToTextInputEvent());

				if (k.Mouse == default) return false;

				break;
		}

		if (k.Mouse == MouseState.Click)
		{
			if (k.State == KeyState.Press)
				return false;
		}
		else if (k.Mouse == MouseState.DoubleClick)
		{
			HandleEnterKey();
			return true;
		}
		else
		{
			/* prevent moving the cursor twice from a single key press */
			if (k.State == KeyState.Release)
				return true;
		}

		newFile = newFile.Clamp(0, _flist.NumFiles - 1);

		if (newFile != _flist.SelectedIndex)
		{
			if (_fakeSlot != KeyJazz.NoInstrument && _fakeSlotChanged)
			{
				_willMoveTo = newFile;

				var dialog = MessageBox.Show(MessageBoxTypes.YesNo,
					"Discard Changes?");

				dialog.ActionYes = DoDiscardChangesAndMove;

				return true;
				/* support saving? XXX */
				/*"Save Sample?" OK Cancel*/
				/*"Discard Changes?" OK Cancel*/
			}

			_fakeSlot = KeyJazz.NoInstrument;
			_fakeSlotChanged = false;
			_searchStr.Clear();

			_flist.SelectedIndex = newFile;
			FileListReposition();
			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return true;
	}

	public override bool? HandleKey(KeyEvent k)
	{
		if (k.State == KeyState.Press && k.Sym == KeySym.Escape && !k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
		{
			SetPage(PageNumbers.SampleList);
			return true;
		}

		if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
			return null;

		int n, v;

		if (k.MIDINote > -1)
		{
			n = k.MIDINote;
			if (k.MIDIVolume > -1)
				v = k.MIDIVolume / 2;
			else
				v = KeyJazz.DefaultVolume;
		}
		else if (k.IsRepeat)
			return false;
		else
		{
			n = k.NoteValue;
			v = KeyJazz.DefaultVolume;
			if (n <= 0 || n > 120)
				return false;
		}

		HandlePreload();

		if (_fakeSlot != KeyJazz.NoInstrument)
		{
			if (k.State == KeyState.Press)
				Song.CurrentSong.KeyDown(KeyJazz.FakeInstrument, KeyJazz.NoInstrument, n, v, KeyJazz.CurrentChannel);
			else
				Song.CurrentSong.KeyUp(KeyJazz.FakeInstrument, KeyJazz.NoInstrument, n);
		}

		return false;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	void HandlePreload()
	{
		if (_fakeSlot == KeyJazz.NoInstrument && _flist.SelectedIndex >= 0 && _flist.SelectedIndex < _flist.NumFiles)
		{
			var file = _flist.SelectedFile;

			if ((file != null) && file.Type.HasAnyFlag(FileTypes.SampleMask))
			{
				_fakeSlotChanged = false;
				_fakeSlot = Song.CurrentSong.PreloadSample(file); // either 0 or KEYJAZZ_NOTINST
			}
		}
	}

	void HandlePrenameOp() => HandlePreload();

	void HandleLoadCopyValue<T>(T value, ref T destination)
		where T : notnull
	{
		if (!value.Equals(destination))
		{
			destination = value;
			_fakeSlotChanged = true;
		}
	}

	void HandleLoadCopy(SongSample s)
	{
		HandleLoadCopyValue(numberEntrySampleSpeed!.Value, ref s.C5Speed);
		HandleLoadCopyValue(numberEntryLoopStart!.Value, ref s.LoopStart);
		HandleLoadCopyValue(numberEntryLoopEnd!.Value, ref s.LoopEnd);
		HandleLoadCopyValue(numberEntrySustainLoopStart!.Value, ref s.SustainStart);
		HandleLoadCopyValue(numberEntrySustainLoopEnd!.Value, ref s.SustainEnd);
		HandleLoadCopyValue(thumbBarDefaultVolume!.Value, ref s.Volume);
		HandleLoadCopyValue(thumbBarGlobalVolume!.Value, ref s.GlobalVolume);
		HandleLoadCopyValue(thumbBarVibratoRate!.Value, ref s.VibratoRate);
		HandleLoadCopyValue(thumbBarVibratoDepth!.Value, ref s.VibratoDepth);
		HandleLoadCopyValue(thumbBarVibratoSpeed!.Value, ref s.VibratoSpeed);

		switch (menuToggleLoop!.State)
		{
			case 0:
				if (s.Flags.HasAnyFlag(SampleFlags.Loop | SampleFlags.PingPongLoop))
				{
					s.Flags &= ~(SampleFlags.Loop | SampleFlags.PingPongLoop);
					_fakeSlotChanged = true;
				}
				break;
			case 1:
				if ((s.Flags & (SampleFlags.Loop | SampleFlags.PingPongLoop)) == SampleFlags.Loop)
				{
					s.Flags &= ~(SampleFlags.Loop | SampleFlags.PingPongLoop);
					s.Flags |= (SampleFlags.Loop);
					_fakeSlotChanged = true;
				}
				break;
			case 2:
				if ((s.Flags & (SampleFlags.Loop | SampleFlags.PingPongLoop)) == SampleFlags.PingPongLoop)
				{
					s.Flags &= ~(SampleFlags.Loop | SampleFlags.PingPongLoop);
					s.Flags |= (SampleFlags.PingPongLoop);
					_fakeSlotChanged = true;
				}
				break;
		}

		switch (menuToggleSustainLoop.State)
		{
			case 0:
				if (s.Flags.HasAnyFlag(SampleFlags.SustainLoop | SampleFlags.PingPongSustain))
				{
					s.Flags &= ~(SampleFlags.SustainLoop | SampleFlags.PingPongSustain);
					_fakeSlotChanged = true;
				}
				break;
			case 1:
				if ((s.Flags & (SampleFlags.SustainLoop | SampleFlags.PingPongSustain)) == SampleFlags.SustainLoop)
				{
					s.Flags &= ~(SampleFlags.SustainLoop | SampleFlags.PingPongSustain);
					s.Flags |= (SampleFlags.SustainLoop);
					_fakeSlotChanged = true;
				}
				break;
			case 2:
				if ((s.Flags & (SampleFlags.SustainLoop | SampleFlags.PingPongSustain)) == SampleFlags.PingPongSustain)
				{
					s.Flags &= ~(SampleFlags.SustainLoop | SampleFlags.PingPongSustain);
					s.Flags |= (SampleFlags.PingPongSustain);
					_fakeSlotChanged = true;
				}
				break;
		}
	}

	void HandleLoadUpdate()
	{
		HandlePreload();

		if (_fakeSlot != KeyJazz.NoInstrument)
		{
			var s = Song.CurrentSong.GetSample(_fakeSlot);

			if (s != null)
			{
				HandleLoadCopy(s);
				Song.CurrentSong.UpdatePlayingSample(_fakeSlot);
			}
		}
	}
}
