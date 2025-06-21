using System;

namespace ChasmTracker.Dialogs;

class MessageBoxDialogTypes
{
	public const MessageBoxTypes Box = (MessageBoxTypes)DialogTypes.Box;
}

[Flags]
public enum MessageBoxTypes
{
	None = 0,

	OK = MessageBoxDialogTypes.Box | 1 << 4,           /* 0001 1000 */
	OKCancel = MessageBoxDialogTypes.Box | 1 << 5,     /* 0010 1000 */
	/* yes/no technically has a cancel as well, i.e. the escape key */
	YesNo = MessageBoxDialogTypes.Box | 1 << 6,        /* 0100 1000 */
}
