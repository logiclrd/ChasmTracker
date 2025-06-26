using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

using FileTypes = FileSystem.FileTypes;

public class SampleFileListPageBase : Page
{
	TextEntryWidget? textEntryFileName;
	NumberEntryWidget? numberEntrySampleSpeed;
	MenuToggleWidget? menuToggleLoop;
	NumberEntryWidget? numberEntryLoopStart;
	NumberEntryWidget? numberEntryLoopEnd;
	MenuToggleWidget? menuToggleSustainLoop;
	NumberEntryWidget? numberEntrySustainLoopStart;
	NumberEntryWidget? numberEntrySustainLoopEnd;
	ThumbBarWidget? thumbBarDefaultVolume;
	ThumbBarWidget? thumbBarGlobalVolume;
	ThumbBarWidget? thumbBarVibratoSpeed;
	ThumbBarWidget? thumbBarVibratoDepth;
	ThumbBarWidget? thumbBarVibratoRate;

	/* the locals */
	VGAMemOverlay _sampleImage;

	char[] _currentFileName = new char[22];
	int _sampleSpeedPos = 0;
	int _sampleLoopBeg = 0;
	int _sampleLoopEnd = 0;
	int _sampleSusLoopBeg = 0;
	int _sampleSusLoopEnd = 0;

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

	int _searchPos = -1;
	List<char> _searchStr = new List<char>();

	public SampleLoadPage()
		: base("About", PageNumbers.About)
	{
		_sampleImage = VGAMem.AllocateOverlay(
			new Point(52, 25),
			new Point(76, 28));
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
		VGAMem.DrawBox(new Point(5, 12), new Size(50, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
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
				DrawSampleData(_sampleImage, s);
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
		_searchPos = -1;
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
			if (_searchPos > -1)
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
}
