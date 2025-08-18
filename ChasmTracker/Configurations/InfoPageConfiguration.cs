namespace ChasmTracker.Configurations;

public class InfoPageConfiguration : ConfigurationSection
{
	// New hotness
	public string? Layout;

	// Old & broke
	[ValueWhenInvalid(-1)]
	public int NumWindows;
	[ValueWhenInvalid(-1)]
	public int Window1;
	[ValueWhenInvalid(-1)]
	public int Window2;
	[ValueWhenInvalid(-1)]
	public int Window3;
	[ValueWhenInvalid(-1)]
	public int Window4;
	[ValueWhenInvalid(-1)]
	public int Window5;
}
