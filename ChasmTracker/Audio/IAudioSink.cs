using System;

namespace ChasmTracker.Audio;

public interface IAudioSink
{
	void Callback(Span<byte> data);
}
