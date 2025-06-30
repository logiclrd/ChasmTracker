namespace ChasmTracker.Configurations;

using ChasmTracker.FileSystem;

public class DirectoriesConfiguration : ConfigurationSection
{
	public string InitialDirectory = "";
	public string DiskWriteToDirectory = "";
	public string ModulesDirectory = "";
	public string SamplesDirectory = "";
	public string InstrumentsDirectory = "";
	public string DotSchism = ""; /* the full path to ~/.schism */

	public SortMode SortWith;
}
