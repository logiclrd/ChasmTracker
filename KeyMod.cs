using System;

namespace ChasmTracker;

[Flags]
public enum KeyMod
{
	None = 0,
	LeftControl = 1 << 0,
	RightControl = 1 << 1,
	LeftShift = 1 << 2,
	RightShift = 1 << 3,
	LeftAlt = 1 << 4,
	RightAlt = 1 << 5,
	LeftGUI = 1 << 6,
	RightGUI = 1 << 7,
	Num = 1 << 8,
	Caps = 1 << 9,
	Mode = 1 << 10,
	Scroll = 1 << 11,
	CapsPressed = 1 << 12,

	Control = LeftControl | RightControl,
	Shift = LeftShift | RightShift,
	Alt = LeftAlt | RightAlt,
	GUI = LeftGUI | RightGUI,

	ControlAlt = Control | Alt,
	ControlAltShift = Control | Alt | Shift,
}