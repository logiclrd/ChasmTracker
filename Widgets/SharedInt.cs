namespace ChasmTracker.Widgets;

public class SharedInt
{
	public int Value;

	public static implicit operator int(SharedInt shared) => shared.Value;
}
