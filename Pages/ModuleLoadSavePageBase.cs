using System;
using System.IO;
using System.Linq;
using System.Text;

using DQD.Glob;

namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.Events;
using ChasmTracker.FileSystem;
using ChasmTracker.Input;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public abstract class ModuleLoadSavePageBase : Page
{
	protected OtherWidget? otherFileList;
	protected OtherWidget? otherDirectoryList;
	protected TextEntryWidget? textEntryFileName;
	protected TextEntryWidget? textEntryDirectoryName;

	protected static int s_topFile;
	protected static int s_topDir;
	protected static DateTime s_directoryLastWriteTimeUTC;
	protected FileList s_flist = new FileList();
	protected DirectoryList s_dlist = new DirectoryList();

	public ModuleLoadSavePageBase(PageNumbers pageNumber, string title, HelpTexts helpText)
		: base(pageNumber, title, helpText)
	{
	}

	/*
		_filenameEntry is generally a glob pattern, but typing a file/path name directly and hitting enter
		will load the file.

		_glob is a Globber object (DQD.Glob) that gets updated if enter is pressed
		while filename_entry contains a '*' or '?' character.

		_dirnameEntry is copied from the module directory (off the vars page) when this page is loaded, and copied back
		when the directory is changed. in general the two variables will be the same, but editing the text directly
		won't screw up the directory listing or anything. (hitting enter will cause the changed callback, which will
		copy the text from s_dirnameEntry to the actual configured string and update the current directory.)
		*/

		/*
		impulse tracker's glob list:
			*.it; *.xm; *.s3m; *.mtm; *.669; *.mod
		unsupported formats that the title reader knows about, even though we can't load them:
			*.f2r; *.liq; *.dtm; *.ntk; *.mf
		formats that might be supported, but which i have never seen and thus don't actually care about:
			*.dbm; *.dsm; *.psm
		other formats that i wouldn't bother presenting in the loader even if we could load them:
			*.mid; *.wav; *.mp3; *.ogg; *.sid; *.umx
		formats that modplug pretends to support, but fails hard:
			*.ams

		TODO: scroller hack on selected filename
		*/

	const string GlobClassic = "*.it; *.xm; *.s3m; *.mtm; *.669; *.mod";
	const string GlobDefault = GlobClassic + "; *.d00; *.psm; *.dsm; *.mdl; *.mt2; *.stm; *.stx; *.far; *.ult; *.med; *.ptm; *.okt; *.amf; *.dmf; *.imf; *.sfx; *.mus; *.mid";

	StringBuilder _filenameEntry = new StringBuilder();
	StringBuilder _dirnameEntry = new StringBuilder();

	protected void ClearFilenameEntry()
	{
		_filenameEntry.Clear();
	}

	protected virtual string DefaultGlobPattern => GlobDefault;

	Globber? _glob;
	string? _globListSrc; // the pattern used to make glob_list (this is an icky hack)

	protected void ResetGlob()
	{
		// Don't reparse the glob if it hasn't changed; that will mess with the cursor position
		if ((_globListSrc != null) && _globListSrc.Equals(Configuration.Files.ModulePattern, StringComparison.InvariantCultureIgnoreCase))
			_filenameEntry.Clear().Append(_globListSrc);
		else
			SetDefaultGlob(true);
	}

	/* there should be a more useful way to determine which page to set. i.e., if there were errors/warnings, show
	the log; otherwise, switch to the blank page (or, for the loader, maybe the previously set page if classic mode
	is off?)
	idea for return codes:
		0 = couldn't load/save, error dumped to log
		1 = no warnings or errors were printed.
		2 = there were warnings, but the song was still loaded/saved. */

	protected abstract void HandleFileEntered(string ptr);

	/* --------------------------------------------------------------------- */

	/* get a color index from a dmoz_file_t 'type' field */
	static int GetTypeColour(FileTypes type)
	{
		/* 7 unknown
		   3 it
		   5 s3m
		   6 xm
		   2 mod
		   4 other
		   7 sample */
		switch (type)
		{
			case FileTypes.ModuleMOD: return 2;
			case FileTypes.ModuleS3M: return 5;
			case FileTypes.ModuleXM: return 6;
			case FileTypes.ModuleIT: return 3;
			case FileTypes.SampleCompressed: return 4; /* mp3/ogg 'sample'... i think */
			default: return 7;
		}
	}

	protected void ClearDirectory()
	{
		s_flist?.Clear();
		s_dlist?.Clear();
	}

	bool ModGrep(FileReference f)
	{
		return _glob?.IsMatch(f.BaseName) ?? false;
	}

	/* --------------------------------------------------------------------- */

	protected void FileListReposition()
	{
		if (s_flist.SelectedIndex >= s_flist.NumFiles)
			s_flist.SelectedIndex = s_flist.NumFiles - 1;

		if (s_flist.SelectedIndex < 0) s_flist.SelectedIndex = 0;

		if (s_flist.SelectedIndex < s_topFile)
			s_topFile = s_flist.SelectedIndex;

		else if (s_flist.SelectedIndex > s_topFile + 30)
			s_topFile = s_flist.SelectedIndex - 30;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	protected void DirectoryListReposition()
	{
		if (s_dlist.SelectedIndex >= s_dlist.NumDirectories)
			s_dlist.SelectedIndex = s_dlist.NumDirectories - 1;

		if (s_dlist.SelectedIndex < 0) s_dlist.SelectedIndex = 0;

		if (s_dlist.SelectedIndex < s_topDir)
			s_topDir = s_dlist.SelectedIndex;
		else if (s_dlist.SelectedIndex > s_topDir + 21)
			s_topDir = s_dlist.SelectedIndex - 21;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void ReadDirectory()
	{
		ClearDirectory();

		if (!Directory.Exists(Configuration.Directories.ModulesDirectory))
			s_directoryLastWriteTimeUTC = default;
		else
			s_directoryLastWriteTimeUTC = Directory.GetLastWriteTimeUtc(Configuration.Directories.ModulesDirectory);

		try
		{
			/* if the stat call failed, this will probably break as well, but
			at the very least, it'll add an entry for the root directory. */
			if (!DirectoryScanner.Populate(Configuration.Directories.ModulesDirectory, s_flist, s_dlist, loadLibrary: null))
				Log.Append(4, Configuration.Directories.ModulesDirectory + ": error while scanning directory");

			var filterOperation = s_flist.BeginFilter(ModGrep, s_flist.SelectedIndexRef);

			filterOperation.RunToCompletion();

			DirectoryScanner.CacheLookup(Configuration.Directories.ModulesDirectory, s_flist, s_dlist);

			// background the title checker
			DirectoryScanner.SetAsynchronousFileListParameters(s_flist, DirectoryScanner.FillExtendedData, s_flist.SelectedIndexRef, FileListReposition);

			FileListReposition();
			DirectoryListReposition();
		}
		catch (Exception e)
		{
			Log.AppendException(e);
		}
	}

	/* --------------------------------------------------------------------- */

	void SetGlob(string globSpec)
	{
		_glob = new Globber();

		_globListSrc = globSpec;

		foreach (string item in globSpec.Split(';').Select(s => s.Trim()))
			if (!string.IsNullOrWhiteSpace(item))
				_glob.AddExpression(item, ignoreCase: true);

		/* this is kinda lame. dmoz should have a way to reload the list without rereading the directory.
		could be done with a "visible" flag, which affects the list's sort order, along with adjusting
		the file count... */
		ReadDirectory();
	}

	protected void SetDefaultGlob(bool setFilename)
	{
		string s = DefaultGlobPattern;

		if (setFilename)
			_filenameEntry.Clear().Append(s);

		SetGlob(s);
	}

	/* --------------------------------------------------------------------- */

	static StringBuilder s_searchText = new StringBuilder();
	static int s_searchFirstChar = 0;       /* first visible character */

	void SearchRedraw()
	{
		VGAMem.DrawFillCharacters(new Point(51, 37), new Point(76, 37), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawTextBIOSLen(s_searchText, s_searchFirstChar, 25, new Point(51, 37), (5, 0));

		/* draw the cursor if it's on the dir/file list */
		var selectedWidget = SelectedActiveWidgetIndex;

		if ((selectedWidget != null) && ((selectedWidget == 0) || (selectedWidget == 1)))
			VGAMem.DrawCharacter(0, new Point(51 + s_searchText.Length + s_searchFirstChar, 37), (0, 6));
	}

	void SearchUpdate()
	{
		if (s_searchText.Length > 25)
			s_searchFirstChar = s_searchText.Length - 25;
		else
			s_searchFirstChar = 0;

		/* go through the file/dir list (whatever one is selected) and
		* find the first entry matching the text */
		string prefix = s_searchText.ToString();

		if (SelectedWidgetIndex == 0)
		{
			for (int n = 0; n < s_flist.NumFiles; n++)
			{
				if (s_flist[n].BaseName.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
				{
					s_flist.SelectedIndex = n;
					FileListReposition();
				}
			}
		}
		else
		{
			for (int n = 0; n < s_dlist.NumDirectories; n++)
			{
				if (s_dlist[n].BaseName.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
				{
					s_dlist.SelectedIndex = n;
					FileListReposition();
				}
			}
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	bool SearchTextAddChar(char ch)
	{
		if (ch < ' ')
			return false;

		s_searchText.Append(ch);

		SearchUpdate();

		return true;
	}

	void SearchTextDeleteChar()
	{
		if (s_searchText.Length == 0)
			return;

		s_searchText.Length--;

		if (s_searchText.Length > 25)
			s_searchFirstChar = s_searchText.Length - 25;
		else
			s_searchFirstChar = 0;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void SearchTextClear()
	{
		s_searchText.Clear();
		s_searchFirstChar = 0;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	/* --------------------------------------------------------------------- */

	/* return: 1 = success, 0 = failure
	TODO: provide some sort of feedback if something went wrong. */
	bool ChangeDirectory(string dir)
	{
		string ptr = DirectoryScanner.NormalizePath(dir);

		if (string.IsNullOrEmpty(ptr))
			return false;

		DirectoryScanner.CacheUpdate(Configuration.Directories.ModulesDirectory, s_flist, s_dlist);

		Configuration.Directories.ModulesDirectory = ptr;

		// TODO charset_strncpy ?
		_dirnameEntry.Clear().Append(ptr);

		/* probably not all of this is needed everywhere */
		SearchTextClear();
		ReadDirectory();

		return true;
	}

	/* --------------------------------------------------------------------- */
	/* unfortunately, there's not enough room with this layout for labels by
	* the search box and file information. :( */

	public override void DrawConst()
	{
		VGAMem.DrawText("Filename", new Point(4, 46), (0, 2));
		VGAMem.DrawText("Directory", new Point(3, 47), (0, 2));
		VGAMem.DrawCharacter(0, new Point(51, 37), (0, 6));
		VGAMem.DrawBox(new Point(2, 12), new Point(49, 44), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset); /* file list */
		VGAMem.DrawBox(new Point(50, 36), new Point(77, 38), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset); /* search */
		VGAMem.DrawBox(new Point(50, 39), new Point(77, 44), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset); /* file info */
		VGAMem.DrawBox(new Point(12, 45), new Point(77, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset); /* filename and directory input */

		VGAMem.DrawFillCharacters(new Point(13, 46), new Point(76, 47), (VGAMem.DefaultForeground, 0));
	}

	/* --------------------------------------------------------------------- */

	protected void FileListDraw()
	{
		VGAMem.DrawFillCharacters(new Point(3, 13), new Point(48, 43), (VGAMem.DefaultForeground, 0));

		int pos;

		if (s_flist.NumFiles > 0)
		{
			if (s_topFile < 0) s_topFile = 0;
			if (s_flist.SelectedIndex < 0) s_flist.SelectedIndex = 0;

			pos = 13;

			for (int n = s_topFile; n < s_flist.NumFiles && pos < 44; n++, pos++)
			{
				var file = s_flist[n];

				int fg1, fg2, bg;

				if (n == s_flist.SelectedIndex && (SelectedActiveWidgetIndex?.Value ?? -1) == 0)
				{
					fg1 = fg2 = 0;
					bg = 3;
				}
				else
				{
					fg1 = GetTypeColour(file.Type);
					fg2 = file.Type.HasAnyFlag(FileTypes.ModuleMask) ? 3 : 7;
					bg = 0;
				}

				VGAMem.DrawTextLen(file.BaseName, 20, new Point(3, pos), (fg1, bg));

				VGAMem.DrawCharacter(168, new Point(23, pos), (2, bg));
				VGAMem.DrawTextLen(file.Title, 25, new Point(24, pos), (fg2, bg));
			}

			/* info for the current file */
			if (s_flist.SelectedFile is FileReference selectedFile)
			{
				VGAMem.DrawTextLen(selectedFile.Description, 26, new Point(51, 40), (5, 0));
				VGAMem.DrawTextLen(selectedFile.FileSize.ToString("d9"), 26, new Point(51, 41), (5, 0));
				VGAMem.DrawTextLen(selectedFile.TimeStamp.ToString(Configuration.General.DateFormat.GetFormatString()), 26, new Point(51, 42), (5, 0));
				VGAMem.DrawTextLen(selectedFile.TimeStamp.ToString(Configuration.General.TimeFormat.GetFormatString()), 26, new Point(51, 43), (5, 0));
			}
		}
		else
		{
			if ((SelectedActiveWidgetIndex != null) && (SelectedActiveWidgetIndex == 0))
			{
				VGAMem.DrawText("No files.", new Point(3, 13), (0, 3));
				VGAMem.DrawFillCharacters(new Point(12, 13), new Point(48, 13), (VGAMem.DefaultForeground, 3));
				VGAMem.DrawCharacter(168, new Point(23, 13), (2, 3));
				pos = 14;
			}
			else
			{
				VGAMem.DrawText("No files.", new Point(3, 13), (7, 0));
				pos = 13;
			}
			VGAMem.DrawFillCharacters(new Point(51, 40), new Point(76, 43), (VGAMem.DefaultForeground, 0));
		}

		while (pos < 44)
			VGAMem.DrawCharacter(168, new Point(23, pos++), (2, 0));

		/* bleh */
		SearchRedraw();
	}

	void DoDeleteFile()
	{
		int oldTopFile, oldSelectedFile, oldTopDir, oldSelectedDir;

		if (s_flist.SelectedIndex < 0 || s_flist.SelectedIndex >= s_flist.NumFiles)
			return;

		var ptr = s_flist[s_flist.SelectedIndex].FullPath;

		/* would be neat to send it to the trash can if there is one */
		File.Delete(ptr);

		/* remember the list positions */
		oldTopFile = s_topFile;
		oldSelectedFile = s_flist.SelectedIndex;
		oldTopDir = s_topDir;
		oldSelectedDir = s_dlist.SelectedIndex;

		SearchTextClear();
		ReadDirectory();

		/* put the list positions back */
		s_topFile = oldTopFile;
		s_flist.SelectedIndex = oldSelectedFile;
		s_topDir = oldTopDir;
		s_dlist.SelectedIndex = oldSelectedDir;

		/* edge case: if this was the last file, move the cursor up */
		if (s_flist.SelectedIndex >= s_flist.NumFiles)
			s_flist.SelectedIndex = s_flist.NumFiles - 1;

		FileListReposition();
	}

	void ShowSelectedSongLength()
	{
		if (s_flist.SelectedIndex < 0 || s_flist.SelectedIndex >= s_flist.NumFiles)
			return;

		var ptr = s_flist[s_flist.SelectedIndex].FullPath;

		try
		{
			var song = Song.CreateLoad(ptr);

			if (song == null)
			{
				Log.Append(4, ptr + ": failed to load");
				return;
			}

			ShowLengthDialog(Path.GetFileName(ptr), song.GetLength());
		}
		catch (Exception e)
		{
			Log.AppendException(e, ptr);
		}
	}

	protected bool FileListHandleTextInput(TextInputEvent evt)
	{
		bool success = false;

		foreach (char ch in evt.Text)
			success |= SearchTextAddChar(ch);

		return success;
	}

	protected bool FileListHandleKey(KeyEvent k)
	{
		int newFile = s_flist.SelectedIndex;

		switch (k.Sym)
		{
			case KeySym.Up:
				newFile--;
				break;
			case KeySym.Down:
				newFile++;
				break;
			case KeySym.PageUp:
				newFile -= 31;
				break;
			case KeySym.PageDown:
				newFile += 31;
				break;
			case KeySym.Home:
				newFile = 0;
				break;
			case KeySym.End:
				newFile = s_flist.NumFiles - 1;
				break;
			case KeySym.Return:
				if (k.State == KeyState.Press)
					return true;

				if (s_flist.SelectedIndex < s_flist.NumFiles)
				{
					DirectoryScanner.CacheUpdate(Configuration.Directories.ModulesDirectory, s_flist, s_dlist);
					HandleFileEntered(s_flist[s_flist.SelectedIndex].FullPath);
				}

				SearchTextClear();

				return true;
			case KeySym.Delete:
				if (k.State == KeyState.Release)
					return true;
				if (s_flist.NumFiles > 0)
					MessageBox.Show(MessageBoxTypes.OKCancel, "Delete file?", DoDeleteFile);
				return true;
			case KeySym.Backspace:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
					SearchTextClear();
				else
					SearchTextDeleteChar();
				return true;
			case KeySym.p:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt) && k.State == KeyState.Press)
				{
					ShowSelectedSongLength();
					return true;
				} /* else fall through */
				goto default;
			default:
				if (k.Mouse == MouseState.None)
				{
					if (!string.IsNullOrEmpty(k.Text))
					{
						FileListHandleTextInput(k.ToTextInputEvent());
						return true;
					}

					return false;
				}

				break;
		}

		if (k.Mouse != MouseState.None && !(k.MousePosition.X >= 3 && k.MousePosition.X <= 51 && k.MousePosition.Y >= 13 && k.MousePosition.Y <= 43))
			return false;

		switch (k.Mouse)
		{
			case MouseState.Click:
				if (k.State == KeyState.Press)
					return false;
				newFile = (k.MousePosition.Y - 13) + s_topFile;
				break;
			case MouseState.DoubleClick:
				if (s_flist.SelectedIndex < s_flist.NumFiles)
				{
					DirectoryScanner.CacheUpdate(Configuration.Directories.ModulesDirectory, s_flist, s_dlist);
					HandleFileEntered(s_flist[s_flist.SelectedIndex].FullPath);
				}

				SearchTextClear();

				return true;
			case MouseState.ScrollUp:
			case MouseState.ScrollDown:
				if (k.State == KeyState.Press)
					return false;
				s_topFile += (k.Mouse == MouseState.ScrollUp) ? -Constants.MouseScrollLines : Constants.MouseScrollLines;
				/* don't allow scrolling down past either end.
					this can't be CLAMP'd because the first check might scroll
					too far back if the list is small.
					(hrm, should add a BOTTOM_FILE macro or something) */
				if (s_topFile > s_flist.NumFiles - 31)
					s_topFile = s_flist.NumFiles - 31;
				if (s_topFile < 0)
					s_topFile = 0;
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			default:
				/* prevent moving the cursor twice from a single key press */
				if (k.State == KeyState.Release)
					return true;
				break;
		}

		newFile = newFile.Clamp(0, s_flist.NumFiles - 1);

		if (newFile != s_flist.SelectedIndex)
		{
			s_flist.SelectedIndex = newFile;
			FileListReposition();
			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return true;
	}

	/* --------------------------------------------------------------------- */
	/* These could check for the current page, but that's kind of weird to do
	* and I just don't like that idea in general :) */

	protected abstract int DirectoryListWidth { get; }

	protected void DirectoryListDraw()
	{
		int width = DirectoryListWidth;

		VGAMem.DrawFillCharacters(new Point(51, 13), new Point(51 + width - 1, 34), (VGAMem.DefaultForeground, 0));

		for (int n = s_topDir, pos = 13; pos < 35; n++, pos++)
		{
			if (n < 0) continue; /* er... */
			if (n >= s_dlist.NumDirectories)
				break;

			int fg, bg;

			if ((n == s_dlist.SelectedIndex) && (SelectedActiveWidgetIndex?.Value == 1))
			{
				fg = 0;
				bg = 3;
			}
			else
			{
				fg = 5;
				bg = 0;
			}

			VGAMem.DrawTextLen(s_dlist[n].BaseName, width, new Point(51, pos), (fg, bg));
		}

		/* bleh */
		SearchRedraw();
	}

	protected bool DirectoryListHandleTextInput(TextInputEvent evt)
	{
		foreach (char ch in evt.Text)
		{
			if (ch < ' ')
				return false;

			s_searchText.Append(ch);
		}

		SearchUpdate();

		return true;
	}

	protected bool DirectoryListHandleKey(KeyEvent k)
	{
		int width = DirectoryListWidth;

		int newDir = s_dlist.SelectedIndex;

		if (k.Mouse != MouseState.None)
		{
			if (k.MousePosition.X >= 51 && k.MousePosition.X <= (51 + width - 1) && k.MousePosition.Y >= 13 && k.MousePosition.Y <= 34)
			{
				switch (k.Mouse)
				{
					case MouseState.Click:
						newDir = (k.MousePosition.Y - 13) + s_topDir;
						break;
					case MouseState.DoubleClick:
						s_topFile = s_flist.SelectedIndex = 0;
						ChangeDirectory(s_dlist[s_dlist.SelectedIndex].FullPath);

						if (s_flist.NumFiles > 0)
							ChangeFocusTo((ActiveWidgets ?? Widgets)[0]);

						Status.Flags |= StatusFlags.NeedUpdate;
						return true;
					case MouseState.ScrollUp:
					case MouseState.ScrollDown:
						s_topDir += (k.Mouse == MouseState.ScrollUp) ? -Constants.MouseScrollLines : Constants.MouseScrollLines;
						if (s_topDir > s_dlist.NumDirectories - 21)
							s_topDir = s_dlist.NumDirectories - 21;
						if (s_topDir < 0)
							s_topDir = 0;
						Status.Flags |= StatusFlags.NeedUpdate;
						break;
					default:
						break;
				}
			}
			else
				return false;
		}

		switch (k.Sym)
		{
			case KeySym.Up:
				newDir--;
				break;
			case KeySym.Down:
				newDir++;
				break;
			case KeySym.PageUp:
				newDir -= 21;
				break;
			case KeySym.PageDown:
				newDir += 21;
				break;
			case KeySym.Home:
				newDir = 0;
				break;
			case KeySym.End:
				newDir = s_dlist.NumDirectories - 1;
				break;
			case KeySym.Return:
				if (k.State == KeyState.Press)
					return false;
				/* reset */
				s_topFile = s_flist.SelectedIndex = 0;
				if (s_dlist.SelectedIndex >= 0 && s_dlist.SelectedIndex < s_dlist.NumDirectories)
					ChangeDirectory(s_dlist[s_dlist.SelectedIndex].FullPath);

				if (s_flist.NumFiles > 0)
					ChangeFocusTo((ActiveWidgets ?? Widgets)[0]);
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.Backspace:
				if (k.State == KeyState.Release)
					return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
					SearchTextClear();
				else
					SearchTextDeleteChar();
				return true;
			case KeySym.Backslash:
				if (Path.DirectorySeparatorChar == '\\')
					goto case KeySym.Slash;
				break;
			case KeySym.Slash:
				if (k.State == KeyState.Release)
					return false;

				if (s_searchText.Length == 0 && s_dlist.SelectedIndex != 0)
				{
					// slash -> go to top (root) dir
					newDir = 0;
				}
				else if (s_dlist.SelectedIndex > 0 && s_dlist.SelectedIndex < s_dlist.NumDirectories)
				{
					ChangeDirectory(s_dlist[s_dlist.SelectedIndex].FullPath);
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
				break;
			default:
				if (k.Mouse == MouseState.None)
				{
					if (!string.IsNullOrEmpty(k.Text))
					{
						DirectoryListHandleTextInput(k.ToTextInputEvent());
						return true;
					}

					return false;
				}

				break;
		}

		if (k.Mouse == MouseState.Click)
		{
			if (k.State == KeyState.Press)
				return false;
		}
		else
		{
			if (k.State == KeyState.Release)
				return false;
		}

		newDir = newDir.Clamp(0, s_dlist.NumDirectories - 1);
		if (newDir != s_dlist.SelectedIndex)
		{
			s_dlist.SelectedIndex = newDir;
			DirectoryListReposition();
			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return true;
	}

	/* --------------------------------------------------------------------- */
	/* these handle when enter is pressed on the file/directory textboxes at the bottom of the screen. */

	static readonly char[] GlobCharacters = { '?', '*' };

	protected void FilenameEntered()
	{
		string entry = _filenameEntry.ToString();

		if (entry.IndexOfAny(GlobCharacters) >= 0)
			SetGlob(entry);
		else
		{
			string ptr = Path.Join(Configuration.Directories.ModulesDirectory, entry);

			HandleFileEntered(ptr);
		}
	}

	/* strangely similar to the dir list's code :) */
	protected void DirectoryNameEntered()
	{
		string @out = _dirnameEntry.ToString();

		if (!ChangeDirectory(@out))
			return;

		ChangeFocusTo(s_flist.NumFiles > 0 ? otherFileList! : otherDirectoryList!);

		Status.Flags |= StatusFlags.NeedUpdate;

		/* reset */
		s_topFile = s_flist.SelectedIndex = 0;
	}

	/* --------------------------------------------------------------------- */

	/* used by SetPage. return 1 => contents changed */
	protected bool UpdateDirectory()
	{
		/* if we have a list, the directory didn't change, and the mtime is the same, we're set. */
		if (!Status.Flags.HasFlag(StatusFlags.ModulesDirectoryChanged)
		 && Directory.Exists(Configuration.Directories.ModulesDirectory)
		 && Directory.GetLastWriteTimeUtc(Configuration.Directories.ModulesDirectory) == s_directoryLastWriteTimeUTC)
			return false;

		ChangeDirectory(Configuration.Directories.ModulesDirectory);

		/* TODO: what if it failed? */

		Status.Flags &= ~StatusFlags.ModulesDirectoryChanged;

		return true;
	}

}
