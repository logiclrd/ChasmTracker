using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ChasmTracker.Pages;

using System.IO;
using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

using FileTypes = FileSystem.FileTypes;

public class SampleLoadPage : Page
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

	enum LoopStates
	{
		[Description("Off")] Off,
		[Description("On Forwards")] Forward,
		[Description("On Ping Pong")] PingPong,
	}

	string _sampleCwd = "";

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
	FileList _flist;

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

	void ReadDirectory(void)
	{
		ClearDirectory();

		try
		{
			_directoryModifiedTimeUTC = Directory.GetLastWriteTimeUtc(_sampleCwd);
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
				_sampleCwd,
				_flist,
				dirList: null,
				DirectoryScanner.ReadSampleLibrary);
		}
		catch (Exception e)
		{
			Log.AppendException(e);
		}

		dmoz_filter_filelist(&flist, dmoz_fill_ext_data, &current_file, file_list_reposition);
		dmoz_cache_lookup(samp_cwd, &flist, NULL);

		FileListReposition();
	}
}
