using System;
using System.Collections.Generic;
using System.Linq;

namespace ChasmTracker.Audio;

public abstract class AudioBackend
{
	public const int DefaultID = ~0;

	public static AudioBackend[] Backends = Array.Empty<AudioBackend>();

	public static AudioDriver[] Drivers = Array.Empty<AudioDriver>();

	public static AudioBackend? Current = default!;

	public static AudioDevice[] Devices = Array.Empty<AudioDevice>();

	/* called when SCHISM_AUDIODEVICEADDED/SCHISM_AUDIODEVICEREMOVED event received */
	public static void RefreshAudioDeviceList()
	{
		if (Current == null)
			return;

		var deviceList = new List<AudioDevice>();

		foreach (var device in Current.EnumerateDevices())
		{
			device.ID = deviceList.Count;
			deviceList.Add(device);
		}

		Devices = deviceList.ToArray();
	}

	public void CreateDriversList()
	{
		Drivers = Backends.SelectMany(backend => backend.EnumerateDrivers()).ToArray();
	}

	public abstract IEnumerable<AudioDevice> EnumerateDevices();
	public abstract IEnumerable<AudioDriver> EnumerateDrivers();

	public abstract bool Initialize();
	public abstract void Quit();

	public abstract string GetDeviceName(int deviceID);

	public abstract bool InitializeDriver(string driverName);
	public abstract void QuitDriver();

	public abstract AudioDevice OpenDevice(int deviceID, AudioSpecs desired, out AudioSpecs obtained);
	public abstract void PauseDevice(AudioDevice device, bool paused);
	public abstract void CloseDevice(AudioDevice device);

	public static void FlashReinitializedText(bool success)
	{
		if (success)
			Status.FlashText(Status.Flags.HasFlag(StatusFlags.ClassicMode)
				? "Sound Blaster 16 reinitialised"
				: "Audio output reinitialised");
		else /* ... */
			Status.FlashText("Failed to reinitialise audio!");
	}
}
