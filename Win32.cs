using System.Runtime.InteropServices;

namespace ChasmTracker;

using ChasmTracker.Input;
using ChasmTracker.Utility;

public static class Win32
{
	const int VK_NUMLOCK = 0x90; // Num lock key
	const int VK_CAPITAL = 0x14; // Caps lock key
	const int VK_LSHIFT = 0xA0; // Left Shift key
	const int VK_RSHIFT = 0xA1; // Right Shift key
	const int VK_LMENU = 0xA4; // Left Alt key
	const int VK_RMENU = 0xA5; // Right Alt key
	const int VK_LCONTROL = 0xA2; // Left Ctrl key
	const int VK_RCONTROL = 0xA3; // Right Ctrl key
	const int VK_LWIN = 0x5B; // Left Windows logo key
	const int VK_RWIN = 0x5C; // Right Windows logo key

	class KeyModMapping
	{
		public int VK;
		public KeyMod KM;

		// whether this key is a held modifier (i.e.
		// ctrl, alt, shift, win) or is toggled (i.e.
		// numlock, scrolllock)
		public bool IsToggle;

		public KeyModMapping(int vk, KeyMod km, bool isToggle)
		{
			VK = vk;
			KM = km;
			IsToggle = isToggle;
		}
	}

	static readonly KeyModMapping[] KeyModMappings =
		[
			new KeyModMapping(VK_NUMLOCK, KeyMod.Num, true),
			new KeyModMapping(VK_CAPITAL, KeyMod.Caps, true),
			new KeyModMapping(VK_CAPITAL, KeyMod.CapsPressed, false),
			new KeyModMapping(VK_LSHIFT, KeyMod.LeftShift, true),
			new KeyModMapping(VK_RSHIFT, KeyMod.RightShift, true),
			new KeyModMapping(VK_LMENU, KeyMod.LeftAlt, true),
			new KeyModMapping(VK_RMENU, KeyMod.RightAlt, true),
			new KeyModMapping(VK_LCONTROL, KeyMod.LeftControl, true),
			new KeyModMapping(VK_RCONTROL, KeyMod.RightControl, true),
			new KeyModMapping(VK_LWIN, KeyMod.LeftGUI, true),
			new KeyModMapping(VK_RWIN, KeyMod.RightGUI, true),
		];

	[DllImport("user32")]
	static extern short GetKeyState(int nVirtKey);
	[DllImport("user32", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool GetKeyboardState(byte[] lpKeyState);

	public static void GetModKey(ref KeyMod mk)
	{
		/* Sometimes GetKeyboardState is out of date and calling GetKeyState
		* fixes it. Any random key will work. */
		GetKeyState(VK_CAPITAL);

		byte[] ks = new byte[256];

		if (!GetKeyboardState(ks)) return;

		foreach (var conv in KeyModMappings)
		{
			/* Clear the original value */
			mk &= ~conv.KM;

			/* Put in our result */
			if (ks[conv.VK].HasBitSet(conv.IsToggle ? 0x01 : 0x80))
				mk |= conv.KM;
		}
	}
}
