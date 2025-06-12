namespace ChasmTracker.Pages;

public enum CopySearchMode
{
	Off = 0, /* no search (IT style) */
	Up = 1, /* search above the cursor for an instrument number */
	UpThenDown = 2, /* search both ways, up to row 0 first, then down */
}
