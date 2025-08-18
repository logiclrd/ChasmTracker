namespace ChasmTracker.Audio;

public class AudioDriver
{
	public AudioBackend Backend;
	public string Name;

	public AudioDriver(AudioBackend backend, string name)
	{
		Backend = backend;
		Name = name;
	}
}
