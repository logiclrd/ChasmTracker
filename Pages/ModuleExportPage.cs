namespace ChasmTracker.Pages;

using ChasmTracker.Dialogs;
using ChasmTracker.Songs;

public class ModuleExportPage : ModuleSavePage
{
	static SaveFormat[] ExportFormats =
		new SaveFormat[]
		{
			new SaveFormat("WAV", "WAV", ".wav") { Exporter = new WAVExporter() },
			new SaveFormat("MWAV", "WAV multi-write", ".wav") { Exporter = new MWAVExporter() },
			new SaveFormat("AIFF", "Audio IFF", ".aiff") { Exporter = new AIFFExporter() },
			new SaveFormat("MAIFF", "Audio IFF multi-write", ".aiff") { Exporter = new MAIFFExporter() },
			new SaveFormat("FLAC", "Free Lossless Audio Codec", ".flac") { Exporter = new FLACExporter() },
			new SaveFormat("MFLAC", "Free Lossless Audio Codec multi-write", ".flac") { Exporter = new MFLACExporter() },
		};

	protected override SaveFormat[] Formats => ExportFormats;

	public ModuleExportPage()
		: base(PageNumbers.ModuleExport, "Export Module (Shift-F10)")
	{
	}

	protected override string DefaultGlobPattern => "*.wav; *.aiff; *.aif";

	protected override SaveResult DoSaveAction(string filename, string selType)
		=> Song.CurrentSong.Export(filename, selType);

	protected override void CheckIfBlank()
	{
		if (Song.CurrentSong.OrderList[0] == SpecialOrders.Last)
			MessageBox.Show(MessageBoxTypes.OK, "You're about to export a blank file...");
	}
}
