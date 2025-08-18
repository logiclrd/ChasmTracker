namespace ChasmTracker.Utility;

public class Shared<T>
	where T : struct
{
	public T Value;

	public Shared()
	{
	}

	public Shared(T value)
	{
		Value = value;
	}

	public static implicit operator T(Shared<T> shared) => shared.Value;
}
