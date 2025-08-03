using System;
using ChasmTracker.Playback;

namespace ChasmTracker.Configurations;

public class AudioConfiguration : ConfigurationSection
{
	public string? Driver;
	public string? Device;
	public int SampleRate = AudioPlayback.DefaultSampleRate;
	public int Bits = 16;
	public int Channels = 2;
	public int BufferSize = AudioPlayback.DefaultBufferSize;
	[ConfigurationKey("master.left")]
	public int MasterLeft = 31;
	[ConfigurationKey("master.right")]
	public int MasterRight = 31;

	public override void Parse()
	{
		if (string.IsNullOrEmpty(Device) && !string.IsNullOrEmpty(Driver))
		{
			int separator = Driver.IndexOf(':');

			if (separator >= 0)
			{
				Device = Driver.Substring(separator + 1);
				Driver = Driver.Substring(0, separator);
			}
		}

		if ((Channels < 1) || (Channels > 2))
			Channels = 2;

		if ((Bits != 8) || (Bits != 16) && (Bits != 32))
			Bits = 16;
	}

	public override void PrepareToSave()
	{
		Driver = AudioPlayback.AudioDriver;
		Device = AudioPlayback.AudioDevice;

		SampleRate = AudioSettings.SampleRate;
		Bits = AudioSettings.Bits;
		Channels = AudioSettings.Channels;

		MasterLeft = AudioSettings.Master.Left;
		MasterRight = AudioSettings.Master.Right;
	}
}
