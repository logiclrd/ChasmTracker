using System;
using System.Collections.Generic;
using System.Linq;

namespace ChasmTracker.Audio;

public abstract class AudioBackend
{
	public const int DefaultID = ~0;

	// TODO: audio.h

	public static AudioBackend[] Backends = Array.Empty<AudioBackend>();

	public static AudioDriver[] Drivers = Array.Empty<AudioDriver>();

	public static AudioBackend Current => default!;
	public static AudioDevice[] Devices = Array.Empty<AudioDevice>();

	/* called when SCHISM_AUDIODEVICEADDED/SCHISM_AUDIODEVICEREMOVED event received */
	public static void RefreshAudioDeviceList()
	{
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
