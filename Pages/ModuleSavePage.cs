using System;
using System.IO;
using System.Linq;

namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.FileSystem;
using ChasmTracker.FileTypes;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class ModuleSavePage : ModuleLoadSavePageBase
{
	ToggleButtonWidget[] toggleButtonSaveFormat = Array.Empty<ToggleButtonWidget>();

	public static SongFileConverter[] SaveFormats = SongFileConverter.EnumerateImplementations(requireWrite: true).ToArray();

	protected virtual FileConverter[] Formats => SaveFormats;

	public ModuleSavePage()
		: this(PageNumbers.ModuleSave, "Save Module (F10)")
	{
	}

	protected ModuleSavePage(PageNumbers pageNumber, string title)
		: base(pageNumber, title, HelpTexts.Global)
	{
		/* preload */
		ClearDirectory();
		s_topFile = s_topDir = 0;
		s_flist.SelectedIndex = s_dlist.SelectedIndex = 0;

		DirectoryListReposition();
		FileListReposition();

		SelectedWidgetIndex.Value = 2;

		otherFileList = new OtherWidget(new Point(3, 13), new Size(44, 30));
		otherFileList.OtherAcceptsText = true;
		otherFileList.OtherHandleKey += FileListHandleKey;
		otherFileList.OtherHandleText += FileListHandleTextInput;
		otherFileList.OtherRedraw += FileListDraw;

		otherDirectoryList = new OtherWidget(new Point(50, 13), new Size(18, 21));
		otherDirectoryList.OtherAcceptsText = true;
		otherDirectoryList.OtherHandleKey += DirectoryListHandleKey;
		otherDirectoryList.OtherHandleText += DirectoryListHandleTextInput;
		otherDirectoryList.OtherRedraw += DirectoryListDraw;

		textEntryFileName = new TextEntryWidget(new Point(13, 46), 64, "", Constants.MaxPathLength);
		textEntryFileName.Activated += FilenameEntered;

		textEntryDirectoryName = new TextEntryWidget(new Point(13, 47), 64, "", Constants.MaxPathLength);
		textEntryDirectoryName.Activated += DirectoryNameEntered;

		var formats = Formats;

		toggleButtonSaveFormat = new ToggleButtonWidget[formats.Length];

		// create filetype widgets
		for (int c = 0; c < formats.Length; c++)
		{
			if (!formats[c].IsEnabled)
				continue;

			toggleButtonSaveFormat[c] = new ToggleButtonWidget(
				new Point(70, 13 + (3 * c)), 5,
				formats[c].Label,
				(5 - formats[c].Label.Length) / 2 + 1,
				1);

			toggleButtonSaveFormat[c].State = (c == 0);
			toggleButtonSaveFormat[c].Next.BackTab = otherDirectoryList;
		}

		Widgets.Add(otherFileList);
		Widgets.Add(otherDirectoryList);
		Widgets.Add(textEntryFileName);
		Widgets.Add(textEntryDirectoryName);
		Widgets.AddRange(toggleButtonSaveFormat);
	}

	public override void NotifySongChanged()
	{
		string ptr = Song.CurrentSong.FileName;

		if (string.IsNullOrEmpty(ptr))
			return;

		string ext = Path.GetExtension(ptr);

		for (int i = 0; i < SaveFormats.Length; i++)
			if (ext.Equals(SaveFormats[i].Extension, StringComparison.InvariantCultureIgnoreCase))
			{
				toggleButtonSaveFormat[i].SetState(true);
				break;
			}
	}

	SaveResult DoSaveSong(string? ptr)
	{
		string filename = ptr ?? Song.CurrentSong.FileName;

		SetPage(PageNumbers.Log);

		SongFileConverter? selType = null;

		for (int i = 0; i < SaveFormats.Length; i++)
			if (toggleButtonSaveFormat[i].State)
			{
				selType = SaveFormats[i];
				break;
			}

		SaveResult ret;

		if (selType == null)
		{
			Log.Append(4, "No file format selected?");
			ret = SaveResult.InternalError;
		}
		else
			ret = DoSaveAction(filename, selType);

		if (ret != SaveResult.Success)
			MessageBox.Show(MessageBoxTypes.OK, "Could not save file");

		return ret;
	}

	protected virtual SaveResult DoSaveAction(string filename, FileConverter selType)
		=> Song.CurrentSong.SaveSong(filename, (SongFileConverter)selType);

	public void SaveSongOrSaveAs()
	{
		string f = Song.CurrentSong.FileName;

		if (!string.IsNullOrEmpty(f))
			DoSaveSong(f);
		else
			SetPage(PageNumbers.ModuleSave);
	}

	void DoSaveSongOverwrite(string ptr)
	{
		if (!Status.Flags.HasFlag(StatusFlags.ClassicMode))
		{
			// say what?
			DoSaveSong(ptr);
			return;
		}

		if (!Directory.Exists(Configuration.Directories.ModulesDirectory)
		 || s_directoryLastWriteTimeUTC != Directory.GetLastWriteTimeUtc(Configuration.Directories.ModulesDirectory))
			Status.Flags |= StatusFlags.ModulesDirectoryChanged;

		DoSaveSong(ptr);

		/* this is wrong, sadly... */
		if (Directory.Exists(Configuration.Directories.ModulesDirectory))
			s_directoryLastWriteTimeUTC = Directory.GetLastWriteTimeUtc(Configuration.Directories.ModulesDirectory);
	}

	protected override void HandleFileEntered(string name)
	{
		bool fileExists = File.Exists(name) && Paths.IsRegularFile(name);
		bool directoryExists = Directory.Exists(name);

		if (!fileExists && !directoryExists)
			DoSaveSong(name);
		else
		{
			if (directoryExists)
			{
				/* TODO: maybe change the current directory in this case? */
				Log.Append(4, name + ": Is a directory");
			}
			else if (fileExists)
			{
				var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "Overwrite file?");

				dialog.ChangeFocusTo(1);
				dialog.ActionYes = () => DoSaveSongOverwrite(name);
			}
			else
			{
				/* Log.Append(4, name + ": Not overwriting non-regular file"); */
				MessageBox.Show(MessageBoxTypes.OK, "Not a regular file");
			}
		}
	}

	public override void DrawConst()
	{
		base.DrawConst();

		/* dir list */
		VGAMem.DrawBox(new Point(50, 12), new Point(68, 35), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawFillCharacters(new Point(51, 37), new Point(67, 37), (VGAMem.DefaultForeground, 0));
	}

	protected override int DirectoryListWidth => 68 - 51;

	/* --------------------------------------------------------------------- */

	public override void SetPage()
	{
		UpdateDirectory();

		/* impulse tracker always resets these; so will i */
		SetDefaultGlob(false);

		ClearFilenameEntry();
		ChangeFocusTo(textEntryFileName!);

		CheckIfBlank();
	}

	protected virtual void CheckIfBlank()
	{
		/* Do nothing for the Save page, overridden for the Export page */
	}
}
