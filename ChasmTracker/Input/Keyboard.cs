using System;

namespace ChasmTracker.Input;

using ChasmTracker.Pages;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class Keyboard
{
	/* emulate SDL 1.2 style key repeat */
	public static int RepeatDelayMilliseconds;
	public static int RepeatIntervalMilliseconds;
	public static bool RepeatEnabled;
	public static DateTime RepeatNextTick = default;

	public static KeyEvent CachedKeyEvent = default;

	static int s_currentOctave;

	public static int CurrentOctave
	{
		get => s_currentOctave;
		set
		{
			s_currentOctave = value.Clamp(0, 8);

			/* a full screen update for one lousy letter... */
			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	public static void SetRepeat(int delay, int rate)
	{
		/* I don't know why this check is here but i'm keeping it to
		 * retain compatibility */
		if (delay != 0)
		{
			RepeatDelayMilliseconds = delay;
			RepeatIntervalMilliseconds = rate;
			RepeatEnabled = true;
		}
	}

	public static void HandleKeyRepeat()
	{
		if ((RepeatNextTick == default) || !RepeatEnabled)
			return;

		var now = DateTime.UtcNow;

		if (now >= RepeatNextTick)
		{
			/* handle key functions have the ability to
			* change the values of the key_event structure.
			*
			* see: issue #465 */
			Page.MainHandleKey(CachedKeyEvent);
			RepeatNextTick = now.AddMilliseconds(RepeatIntervalMilliseconds);
		}
	}

	public static void CacheKeyRepeat(KeyEvent kk)
	{
		if (!RepeatEnabled)
			return;

		CachedKeyEvent = kk;
		CachedKeyEvent.IsRepeat = true;

		RepeatNextTick = DateTime.UtcNow.AddMilliseconds(RepeatDelayMilliseconds + RepeatIntervalMilliseconds);
	}

	public static void EmptyKeyRepeat()
	{
		if (!RepeatEnabled)
			return;

		CachedKeyEvent = default;

		RepeatNextTick = default;
	}

	const string PTMEffects = ".0123456789ABCDRFFT????GHK?YXPLZ()?";

	public static byte GetPTMEffectNumber(char effect)
	{
		if ((effect >= 'a') && (effect <= 'z'))
			effect = (char)(effect - 32);

		return unchecked((byte)PTMEffects.IndexOf(effect));
	}

	public static Effects? GetPTMEffectByCharacter(char effect)
	{
		var number = GetPTMEffectNumber(effect);

		if ((number == byte.MaxValue) || (effect == '?'))
			return Songs.Effects.None;
		else
			return (Effects)number;
	}

	const string Effects = ".JFEGHLKRXODB!CQATI?SMNVW$UY?P&Z()?";

	public static byte GetEffectNumber(char effect)
	{
		if ((effect >= 'a') && (effect <= 'z'))
		{
			effect = (char)(effect - 32);
		}
		else if (!((effect >= '0' && effect <= '9')
				|| (effect >= 'A' && effect <= 'Z')
				|| (effect == '.')))
		{
			/* don't accept pseudo-effects */
			if (Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
				return byte.MaxValue;
		}

		return unchecked((byte)Effects.IndexOf(effect));
	}

	public static Effects? GetEffectByCharacter(char effect)
	{
		var number = GetEffectNumber(effect);

		if ((number == byte.MaxValue) || (effect == '?'))
			return Songs.Effects.None;
		else
			return (Effects)number;
	}
}
