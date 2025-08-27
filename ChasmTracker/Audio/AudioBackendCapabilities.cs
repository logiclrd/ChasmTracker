using System;

namespace ChasmTracker.Audio;

[Flags]
public enum AudioBackendCapabilities
{
	Input = 1,
	Output = 2,
}
