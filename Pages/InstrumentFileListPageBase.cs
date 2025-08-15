using System;
using System.Collections.Generic;
using System.IO;

namespace ChasmTracker.Pages;

using ChasmTracker;
using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.Events;
using ChasmTracker.FileSystem;
using ChasmTracker.Input;
using ChasmTracker.Memory;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

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
public abstract class InstrumentFileListPageBase : Page
{
	OtherWidget? otherFileList;

	static string s_instrumentCwd = "";

	int _topFile;
	DateTime _directoryLastWriteTimeUTC;
	FileList _flist = new FileList();

	List<char> _slashSearchStr = new List<char>();
	bool _haveSlashSearch = false;

	public InstrumentFileListPageBase(PageNumbers pageNumber, string title)
		: base(pageNumber, title, HelpTexts.Global)
	{
		ClearDirectory();

		otherFileList = new OtherWidget();

		otherFileList.OtherHandleKey += otherFileList_HandleKey;
		otherFileList.OtherHandleText += otherFileList_HandleText;
		otherFileList.OtherRedraw += otherFileList_Redraw;
		otherFileList.OtherAcceptsText = true;

		AddWidget(otherFileList);
	}

	public override bool? HandleKey(KeyEvent k)
	{
		if (k.State == KeyState.Release)
			return null;

		if (k.Sym == KeySym.Escape && !k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
		{
			SetPage(PageNumbers.InstrumentList);
			return true;
		}

		return null;
	}

	protected abstract bool IsLibraryMode { get; }

	/* get a color index from a dmoz_file_t 'type' field */
	static int GetTypeColour(FileTypes type)
	{
		if (type == FileTypes.Directory)
			return 5;
		if (!type.HasAnyFlag(FileTypes.ExtendedDataMask))
			return 4; /* unchecked */
		if (type.HasAllFlags(FileTypes.BrowsableMask))
			return 6; /* library */
		if (type == FileTypes.Unknown)
			return 2;
		return 3; /* sample */
	}

	void ClearDirectory()
	{
		_flist.Clear();
	}

	static bool InstrumentGrep(FileReference f)
	{
		f.FillExtendedData();

		return f.Type.HasAnyFlag(FileTypes.InstrumentMask | FileTypes.BrowsableMask);
	}

	/* --------------------------------------------------------------------------------------------------------- */

	void FileListReposition()
	{
		if (_flist.SelectedIndex >= _flist.NumFiles)
			_flist.SelectedIndex = _flist.NumFiles - 1;
		if (_flist.SelectedIndex < 0) _flist.SelectedIndex = 0;
		if (_flist.SelectedIndex < _topFile)
			_topFile = _flist.SelectedIndex;
		else if (_flist.SelectedIndex > _topFile + 34)
			_topFile = _flist.SelectedIndex - 34;
	}

	void ReadDirectory()
	{
		ClearDirectory();

		try
		{
			_directoryLastWriteTimeUTC = Directory.GetLastWriteTimeUtc(s_instrumentCwd);
		}
		catch
		{
			_directoryLastWriteTimeUTC = DateTime.MinValue;
		}

		/* if the stat call failed, this will probably break as well, but
		at the very least, it'll add an entry for the root directory. */
		try
		{
			DirectoryScanner.Populate(s_instrumentCwd, _flist, null, DirectoryScanner.ReadInstrumentLibraryThunk);
		}
		catch (Exception e)
		{
			Log.AppendException(e);
		}

		DirectoryScanner.SetAsynchronousFileListParameters(_flist, InstrumentGrep, _flist.SelectedIndexRef, FileListReposition);
		DirectoryScanner.CacheLookup(s_instrumentCwd, _flist, null);

		FileListReposition();
	}

	/* return: true = success, false = failure
	TODO: provide some sort of feedback if something went wrong. */
	bool ChangeDirectory(string dir)
	{
		string ptr = DirectoryScanner.NormalizePath(dir);

		if (string.IsNullOrEmpty(ptr))
			return false;

		DirectoryScanner.CacheUpdate(s_instrumentCwd, _flist, null);

		if (Directory.Exists(ptr))
			Configuration.Directories.InstrumentsDirectory = ptr;

		s_instrumentCwd = ptr;

		ReadDirectory();

		return true;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	public override void DrawConst()
	{
		VGAMem.DrawFillCharacters(new Point(6, 13), new Point(67, 47), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(50, 12), new Point(61, 48), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.ShadeNone);
		VGAMem.DrawBox(new Point(5, 12), new Point(68, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}

	/* --------------------------------------------------------------------------------------------------------- */

	public override void SetPage()
	{
		if (string.IsNullOrEmpty(s_instrumentCwd))
			s_instrumentCwd = Configuration.Directories.InstrumentsDirectory;

		/* if we have a list, the directory didn't change, and the mtime is the same, we're set */
		if (_flist.NumFiles > 0
				&& !Status.Flags.HasAllFlags(StatusFlags.InstrumentsDirectoryChanged)
				&& Directory.Exists(s_instrumentCwd)
				&& Directory.GetLastWriteTimeUtc(s_instrumentCwd) == _directoryLastWriteTimeUTC)
			return;

		ChangeDirectory(s_instrumentCwd);

		Status.Flags &= ~StatusFlags.InstrumentsDirectoryChanged;

		SelectedWidgetIndex.Value = 0;
		_haveSlashSearch = false;
	}

	/* --------------------------------------------------------------------------------------------------------- */

	bool otherFileList_HandleKey(KeyEvent k)
	{
		int newFile = _flist.SelectedIndex;

		newFile = newFile.Clamp(0, _flist.NumFiles - 1);

		if (k.Mouse != MouseState.None)
		{
			if (k.MousePosition.X >= 6 && k.MousePosition.X <= 67 && k.MousePosition.Y >= 13 && k.MousePosition.Y <= 47)
			{
				_haveSlashSearch = false;

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
			case KeySym.Up:           newFile--; _haveSlashSearch = false; break;
			case KeySym.Down:         newFile++; _haveSlashSearch = false; break;
			case KeySym.PageUp:       newFile -= 35; _haveSlashSearch = false; break;
			case KeySym.PageDown:     newFile += 35; _haveSlashSearch = false; break;
			case KeySym.Home:         newFile = 0; _haveSlashSearch = false; break;
			case KeySym.End:          newFile = _flist.NumFiles - 1; _haveSlashSearch = false; break;

			case KeySym.Escape:
				if (!_haveSlashSearch)
				{
					if (k.State == KeyState.Release && !k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
						SetPage(PageNumbers.InstrumentList);
					return true;
				} /* else fall through */
				goto case KeySym.Return;
			case KeySym.Return:
				if (!_haveSlashSearch)
				{
					if (k.State == KeyState.Press)
						return false;
					HandleEnterKey();
					_haveSlashSearch = false;
				}
				else
				{
					if (k.State == KeyState.Press)
						return true;
					_haveSlashSearch = false;
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
				return true;
			case KeySym.Delete:
				if (k.State == KeyState.Release)
					return true;
				_haveSlashSearch = false;
				if (_flist.NumFiles > 0)
				{
					var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "Delete file?");

					dialog.ChangeFocusTo(1);

					dialog.ActionYes =
						() =>
						{
							if (_flist.SelectedIndex < 0 || _flist.SelectedIndex >= _flist.NumFiles)
								return;

							var ptr = _flist[_flist.SelectedIndex];

							/* would be neat to send it to the trash can if there is one */
							File.Delete(ptr.FullPath);

							/* remember the list positions */
							int oldTopFile = _topFile;
							int oldCurrentFile = _flist.SelectedIndex;

							ReadDirectory();

							/* put the list positions back */
							_topFile = oldTopFile;
							_flist.SelectedIndex = oldCurrentFile;
							/* edge case: if this was the last file, move the cursor up */
							if (_flist.SelectedIndex >= _flist.NumFiles)
								_flist.SelectedIndex = _flist.NumFiles - 1;
							FileListReposition();
						};
				}

				return true;
			case KeySym.Backspace:
				if (_haveSlashSearch)
				{
					if (k.State == KeyState.Release)
						return true;

					if (_slashSearchStr.Count > 0)
						_slashSearchStr.RemoveAt(_slashSearchStr.Count - 1);
					else
						_haveSlashSearch = false;

					Status.Flags |= StatusFlags.NeedUpdate;
					RepositionAtSlashSearch();
					return true;
				}
				goto case KeySym.Slash;
			case KeySym.Slash:
				if (!_haveSlashSearch)
				{
					if (k.State == KeyState.Press)
						return false;
					_haveSlashSearch = true;
					_slashSearchStr.Clear();
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
				goto default;
			default:
				if (!string.IsNullOrEmpty(k.Text))
					otherFileList!.HandleText(k.ToTextInputEvent());

				if (k.Mouse == default)
					return false;

				break;
		}

		if (k.Mouse == MouseState.Click)
		{
			if (k.State == KeyState.Release)
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

		newFile = newFile.Clamp( 0, _flist.NumFiles - 1);

		if (newFile != _flist.SelectedIndex)
		{
			_flist.SelectedIndex = newFile;
			FileListReposition();
			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return true;
	}

	bool otherFileList_HandleText(TextInputEvent text)
	{
		var f = _flist[_flist.SelectedIndex];

		for (int i=0; i < text.Text.Length; i++)
		{
			if (text.Text[i] >= 32 && (_haveSlashSearch || ((f != null) && f.Type.HasAnyFlag(FileTypes.Directory))))
			{
				_slashSearchStr.Add(text.Text[i]);
				RepositionAtSlashSearch();
				Status.Flags |= StatusFlags.NeedUpdate;

				return true;
			}
		}

		return false;
	}

	void otherFileList_Redraw()
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

			if (n == _flist.SelectedIndex && (ActiveWidgetContext?.SelectedWidgetIndex ?? SelectedWidgetIndex) == 0)
			{
				fg = 0;
				bg = 3;
			}
			else
			{
				fg = GetTypeColour(file.Type);
				bg = 0;
			}

			VGAMem.DrawText(n.ToString("d3"), new Point(2, pos), (0, 2));
			VGAMem.DrawTextLen(file.Title,
							25, new Point(6, pos), (fg, bg));
			VGAMem.DrawCharacter(168, new Point(31, pos), (2, bg));
			VGAMem.DrawTextLen(file.BaseName, 18, new Point(32, pos), (fg, bg));

			if (!string.IsNullOrEmpty(file.BaseName) && _haveSlashSearch)
			{
				int highlightLength = file.BaseName.Length;

				for (int i = 0; (i < file.BaseName.Length) && (i < _slashSearchStr.Count); i++)
				{
					if (char.ToLowerInvariant(file.BaseName[i]) != char.ToLowerInvariant(_slashSearchStr[i]))
					{
						highlightLength = i;
						break;
					}
				}

				if (highlightLength > 18)
					highlightLength = 18;

				VGAMem.DrawTextLen(file.BaseName, highlightLength, new Point(32, pos), (3, 1));
			}

			if (file.SampleCount > 1)
				VGAMem.DrawTextLen(file.SampleCount + " Samples", 10, new Point(51, pos), (fg, bg));
			else if (file.SampleCount == 1)
				VGAMem.DrawText("1 Sample  ", new Point(51, pos), (fg, bg));
			else if (file.Type.HasAnyFlag(FileTypes.ModuleMask))
				VGAMem.DrawText("\x9a\x9aModule\x9a\x9a", new Point(51, pos), (fg, bg));
			else
				VGAMem.DrawText("          ", new Point(51, pos), (fg, bg));

			if (file.FileSize > 1048576)
				VGAMem.DrawTextLen((file.FileSize / 1048576) + "m", 6, new Point(62, pos), (fg, bg));
			else if (file.FileSize > 1024)
				VGAMem.DrawTextLen((file.FileSize / 1024) + "k", 6, new Point(62, pos), (fg, bg));
			else if (file.FileSize > 0)
				VGAMem.DrawTextLen(file.FileSize.ToString(), 6, new Point(62, pos), (fg, bg));
		}

		/* draw the info for the current file (or directory...) */

		while (pos < 48)
			VGAMem.DrawCharacter(168, new Point(31, pos++), (2, 0));
	}

	void RepositionAtSlashSearch()
	{
		if (!_haveSlashSearch)
			return;

		int bl = -1;
		int b = -1;

		for (int i = 0; i < _flist.NumFiles; i++)
		{
			var f = _flist[i];

			if (string.IsNullOrEmpty(f.BaseName))
				continue;

			int j;

			for (j = 0; (j < f.BaseName.Length) && (j < _slashSearchStr.Count); j++)
				if (char.ToLower(f.BaseName[j]) != char.ToLower(_slashSearchStr[j]))
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

	/* on the file list, that is */
	void HandleEnterKey()
	{
		int cur = AllPages.InstrumentList.CurrentInstrument;

		if ((_flist.SelectedIndex < 0) || (_flist.SelectedIndex >= _flist.NumFiles))
			return;

		var file = _flist[_flist.SelectedIndex];

		DirectoryScanner.CacheUpdate(s_instrumentCwd, _flist, null);
		DirectoryScanner.FillExtendedData(file);

		if (file.Type.HasAnyFlag(FileTypes.BrowsableMask))
		{
			ChangeDirectory(file.FullPath);
			Status.Flags |= StatusFlags.NeedUpdate;
		}
		else if (file.Type.HasAnyFlag(FileTypes.InstrumentMask))
		{
			if (IsLibraryMode) return;

			Status.Flags |= StatusFlags.SongNeedsSave;

			if (file.InstrumentNumber > -1)
				Song.CurrentSong.LoadInstrumentEx(cur, null, file.FullPath, file.InstrumentNumber);
			else
				Song.CurrentSong.LoadInstrument(cur, file.FullPath);

			if (!Song.CurrentSong.IsInstrumentMode)
				Song.CurrentSong.PromptEnableInstrumentMode();
			else
				SetPage(PageNumbers.InstrumentList);

			MemoryUsage.NotifySongChanged();
		}

		/* TODO */
	}
}
