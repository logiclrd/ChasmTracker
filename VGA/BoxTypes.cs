using System;

namespace ChasmTracker.VGA;

[Flags]
public enum BoxTypes
{
	Outset = 0,
	Inset = 1,
	FlatLight = 2,
	FlatDark = 3,
	ShadeNone = 4,

	Inner = 0 << 4,
	Outer = 1 << 4,
	Corner = 2 << 4,

	Thin = 0 << 6,
	Thick = 1 << 6,

	ShadeMask = 7,
	TypeMask = 3 << 4,
	ThicknessMask = 1 << 6,
}