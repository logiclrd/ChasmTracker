namespace ChasmTracker.Configurations;

using ChasmTracker.FileSystem;

public class DirectoriesConfiguration : ConfigurationSection
{
	[ConfigurationKey("initial")]
	public string InitialDirectory = "";
	[ConfigurationKey("disk_write_to")]
	public string DiskWriteToDirectory = "";
	[ConfigurationKey("modules")]
	public string ModulesDirectory = "";
	[ConfigurationKey("samples")]
	public string SamplesDirectory = "";
	[ConfigurationKey("instruments")]
	public string InstrumentsDirectory = "";
	public string ModulePattern = "*.it; *.xm; *.s3m; *.mtm; *.669; *.mod; *.dsm; *.mdl; *.mt2; *.stm; *.stx; *.far; *.ult; *.med; *.ptm; *.okt; *.amf; *.dmf; *.imf; *.sfx; *.mus; *.mid";
	public string ExportPattern = "*.wav; *.aiff; *.aif";
	public string DotSchism = ""; /* the full path to ~/.schism */

	public SortMode SortWith;
}
