using System;

namespace ChasmTracker.Dialogs;

public class MessageBox : Dialog
{
	public static MessageBox Show(MessageBoxTypes type, string message, Action<object?>? accept = null, Action<object?>? reject = null, object? data = null)
		=> new MessageBox(type, message, accept, reject, data);

	MessageBox(MessageBoxTypes type, string message, Action<object?>? accept = null, Action<object?>? reject = null, object? data = null)
		: base(type, message, accept, reject, 0, data)
	{
	}
}
