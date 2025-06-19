using System;

namespace ChasmTracker.Dialogs;

[Flags]
public enum MessageBoxTypes
{
	None = 0,                    /* 0000 0000 */
	Menu = 1 << 0,               /* 0000 0001 */
	MainMenu = Menu | 1 << 1,    /* 0000 0011 */
	SubMenu = Menu | 1 << 2,     /* 0000 0101 */
	Box = 1 << 3,                /* 0000 1000 */
	OK = Box | 1 << 4,           /* 0001 1000 */
	OKCancel = Box | 1 << 5,     /* 0010 1000 */
	/* yes/no technically has a cancel as well, i.e. the escape key */
	YesNo = Box | 1 << 6,        /* 0100 1000 */
	Custom = Box | 1 << 7,       /* 1000 1000 */
}
