namespace ChasmTracker.Utility;

public class SharedInt
{
	public int Value;

	public static implicit operator int(SharedInt shared) => shared.Value;
}
