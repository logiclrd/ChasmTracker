using System;

namespace ChasmTracker.Pages;

using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class PatternSnap
{
	public SongNote[] Data = Array.Empty<SongNote>();
	public int Channels;
	public int Rows;

	public void AllocateData()
	{
		Data = new SongNote[Channels * Rows];
	}

	public ref SongNote this[int row, int channel]
	{
		get => ref Data[row * Channels + channel];
	}

	/* used by undo/history only */
	public string? SnapOp;
	public Point Position;
	public int PatternNumber;

	PatternSnap Empty => new PatternSnap() { SnapOp = "Empty" };
}
