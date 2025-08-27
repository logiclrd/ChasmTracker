namespace ChasmTracker.Audio;

public class AudioDevice
{
	public readonly AudioBackend Backend;

	public AudioBackendCapabilities Capabilities;

	public int ID;
	public string Name;

	public AudioDevice(AudioBackend backend, int id, string name)
	{
		Backend = backend;

		ID = id;
		Name = name;
	}
}
