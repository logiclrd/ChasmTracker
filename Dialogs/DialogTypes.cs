using System;

namespace ChasmTracker.Dialogs;

[Flags]
public enum DialogTypes
{
	None = 0,                    /* 0000 0000 */
	Menu = 1 << 0,               /* 0000 0001 */
	MainMenu = Menu | 1 << 1,    /* 0000 0011 */
	SubMenu = Menu | 1 << 2,     /* 0000 0101 */
	Box = 1 << 3,                /* 0000 1000 */
	Custom = Box | 1 << 7,       /* 1000 1000 */

	MessageBoxTypeMask = 0x78,   /* 0111 1000 */
}
