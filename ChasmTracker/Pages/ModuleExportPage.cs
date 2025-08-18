namespace ChasmTracker.Pages;

using System.Linq;
using ChasmTracker.Dialogs;
using ChasmTracker.FileTypes;
using ChasmTracker.FileTypes.Exporters;
using ChasmTracker.Songs;

public class ModuleExportPage : ModuleSavePage
{
	public static FileConverter[] ExportFormats = SampleExporter.EnumerateImplementations().OrderBy(n => n.SortOrder).ToArray();

	protected override FileConverter[] Formats => ExportFormats;

	public ModuleExportPage()
		: base(PageNumbers.ModuleExport, "Export Module (Shift-F10)")
	{
	}

	protected override string DefaultGlobPattern => "*.wav; *.aiff; *.aif";

	protected override SaveResult DoSaveAction(string filename, FileConverter selConverter)
		=> Song.CurrentSong.ExportSong(filename, (SampleExporter)selConverter);

	protected override void CheckIfBlank()
	{
		if (Song.CurrentSong.OrderList[0] == SpecialOrders.Last)
			MessageBox.Show(MessageBoxTypes.OK, "You're about to export a blank file...");
	}
}
