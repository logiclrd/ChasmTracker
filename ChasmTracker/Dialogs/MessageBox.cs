using System;

namespace ChasmTracker.Dialogs;

public class MessageBox : Dialog
{
	public static MessageBox Show(MessageBoxTypes type, string message, Action? accept = null, Action? reject = null)
		=> new MessageBox(type, message, accept, reject);

	MessageBox(MessageBoxTypes type, string message, Action? accept = null, Action? reject = null)
		: base(type, message, accept, reject, 0)
	{
	}
}
