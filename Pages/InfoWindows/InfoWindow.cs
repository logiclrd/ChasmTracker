namespace ChasmTracker.Pages.InfoWindows;

using System;
using ChasmTracker.Utility;

public abstract class InfoWindow
{
	public abstract string ConfigurationID { get; }

	public int WindowType;
	public Shared<int> SelectedChannel;

	public InfoWindow(int windowType, Shared<int> selectedChannel, int height, int firstChannel)
	{
		SelectedChannel = selectedChannel;

		Height = height;
		FirstChannel = firstChannel;
	}

	public int Height;
	public int FirstChannel;

	public abstract int GetNumChannels();
	/* if this is true, the first row contains actual text (not just the top part of a box) */
	public abstract bool UsesFirstRow { get; }
	public abstract void Draw(int @base, int height, bool isActive);
	public abstract void Click(Point mousePosition);

	public static Func<int, int, InfoWindow>[] WindowTypes;

	public InfoWindow ConvertToNextWindowType()
	{
		int nextType = (WindowType + 1) % WindowTypes.Length;

		return WindowTypes[nextType](nextType, Height);
	}

	public InfoWindow ConvertToPreviousWindowType()
	{
		int nextType = (WindowType + WindowTypes.Length - 1) % WindowTypes.Length;

		return WindowTypes[nextType](nextType, Height);
	}
}
