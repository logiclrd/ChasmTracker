using System.IO;

namespace ChasmTracker.Pages;

using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class ModuleLoadPage : ModuleLoadSavePageBase
{
	public ModuleLoadPage()
		: base(PageNumbers.ModuleLoad, "Load Module (F9)", HelpTexts.Global)
	{
		ClearDirectory();
		s_topFile = s_topDir = 0;
		s_flist.SelectedIndex = s_dlist.SelectedIndex = 0;

		DirectoryListReposition();
		FileListReposition();

		otherFileList = new OtherWidget(new Point(3, 13), new Size(44, 30));
		otherFileList.OtherAcceptsText = true;
		otherFileList.OtherHandleKey += FileListHandleKey;
		otherFileList.OtherHandleText += FileListHandleTextInput;
		otherFileList.OtherRedraw += FileListDraw;

		otherDirectoryList = new OtherWidget(new Point(50, 13), new Size(27, 22));
		otherDirectoryList.OtherAcceptsText = true;
		otherDirectoryList.OtherHandleKey += DirectoryListHandleKey;
		otherDirectoryList.OtherHandleText += DirectoryListHandleTextInput;
		otherDirectoryList.OtherRedraw += DirectoryListDraw;

		textEntryFileName = new TextEntryWidget(new Point(13, 46), 64, "", Constants.MaxPathLength);
		textEntryFileName.Activated += FilenameEntered;

		textEntryDirectoryName = new TextEntryWidget(new Point(13, 47), 64, "", Constants.MaxPathLength);
		textEntryDirectoryName.Activated += DirectoryNameEntered;

		AddWidget(otherFileList);
		AddWidget(otherDirectoryList);
		AddWidget(textEntryFileName);
		AddWidget(textEntryDirectoryName);
	}

	protected override void HandleFileEntered(string ptr)
	{
		/* these shenanigans force the file to take another trip... */
		if (!File.Exists(ptr))
			return;

		if (Song.Load(ptr) is Song loaded)
			Song.CurrentSong = loaded;
		else
		{
			Log.Append(4, "Failed to load: " + Path.GetFileName(ptr));
			SetPage(PageNumbers.Log);
		}
	}

	public override void DrawConst()
	{
		base.DrawConst();

		/* dir list */
		VGAMem.DrawBox(new Point(50, 12), new Point(77, 35), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawFillCharacters(new Point(51, 37), new Point(76, 37), (VGAMem.DefaultForeground, 0));
	}

	protected override int DirectoryListWidth => 77 - 51;

	/* --------------------------------------------------------------------- */

	public override void SetPage()
	{
		if (UpdateDirectory())
			ChangeFocusTo(s_flist.NumFiles > 0 ? otherFileList! : otherDirectoryList!);

		ResetGlob();
	}
}
