using System.Runtime.InteropServices;

using SDL3;

namespace ChasmTracker;

using ChasmTracker.Input;

public static class OS
{
	public static void GetModKey(ref KeyMod km)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			Win32.GetModKey(ref km);
	}

	public static void ShowMessageBox(string title, string message, OSMessageBoxTypes type)
	{
		var buttonData = new SDL.MessageBoxButtonData();

		buttonData.ButtonID = 0;
		buttonData.Text = "OK";
		buttonData.Flags = SDL.MessageBoxButtonFlags.ReturnkeyDefault | SDL.MessageBoxButtonFlags.EscapekeyDefault;

		var buttonDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<SDL.MessageBoxButtonData>());

		Marshal.StructureToPtr(buttonData, buttonDataPtr, fDeleteOld: false);

		var flags =
			type switch
			{
				OSMessageBoxTypes.Info => SDL.MessageBoxFlags.Information,
				OSMessageBoxTypes.Warning => SDL.MessageBoxFlags.Warning,
				OSMessageBoxTypes.Error => SDL.MessageBoxFlags.Error,

				_ => SDL.MessageBoxFlags.Information,
			};

		try
		{
			SDL.ShowMessageBox(
				new SDL.MessageBoxData()
				{
					Title = title,
					Message = message,
					NumButtons = 1,
					Buttons = buttonDataPtr,
					Flags = flags,
				},
				out var _);
		}
		finally
		{
			Marshal.FreeHGlobal(buttonDataPtr);
		}
	}
}
