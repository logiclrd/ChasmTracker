using System;

namespace ChasmTracker.Songs;

public static class EffectsExtensions
{
	public static bool IsEffect(this Effects v)
		=> Enum.IsDefined(v) && (v != Effects.None) && (v != Effects.Unimplemented);
}
