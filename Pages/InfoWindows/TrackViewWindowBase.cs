namespace ChasmTracker.Pages.InfoWindows;

using ChasmTracker.Pages.TrackViews;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public abstract class TrackViewWindowBase : InfoWindow
{
	protected readonly TrackView TrackView;

	public TrackViewWindowBase(int windowType, Shared<int> selectedChannel, int height, int firstChannel, TrackView trackView)
		: base(windowType, selectedChannel, height, firstChannel)
	{
		TrackView = trackView;

		FullChannelWidth = ChannelWidth + (Separator ? 1 : 0);
	}

	protected abstract int ChannelWidth { get; }
	protected abstract bool Separator { get; }

	protected readonly int FullChannelWidth;

	public override int GetNumChannels() => ((74 + (Separator ? 1 : 0)) / FullChannelWidth).Clamp(1, Constants.MaxChannels);

	public override bool UsesFirstRow => true;

	protected int GetBoxWidth() => GetNumChannels() * FullChannelWidth - (Separator ? 1 : 0);
	protected virtual int GetRightEdge() => GetBoxWidth() + 6;

	static Pattern EmptyPattern = Pattern.CreateEmpty();

	protected void DrawTrackView(int @base, int fullHeight)
	{
		/* way too many variables */
		int currentRow = AudioPlayback.CurrentRow;
		int currentOrder = AudioPlayback.CurrentOrder;

		Pattern curPattern;
		Pattern? prevPattern, nextPattern;

		Pattern? pattern = null; /* points to either {cur,prev,next}_pattern */

		int curPatternRows = 0;
		int prevPatternRows = 0;
		int nextPatternRows = 0;

		int totalRows = 0; /* same as {cur,prev_next}_pattern_rows */

		int numChannels = 72 / FullChannelWidth;

#if false
		/* can't do this here -- each view does channel numbers differently, don't draw on top of them */
		VGAMem.DrawBox(new Point(4, @base), new Point(5 + numChannels * FullChannelWidth - (Separator ? 1 : 0), @base + fullHeight - 1),
			BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
#endif

		bool forceStopped = false;

		switch (AudioPlayback.Mode)
		{
			case AudioPlaybackMode.PatternLoop:
				prevPattern = nextPattern = curPattern = Song.CurrentSong.GetPattern(Song.CurrentSong.CurrentPattern) ?? EmptyPattern;
				prevPatternRows = nextPatternRows = curPatternRows = curPattern.Rows.Count;
				break;
			default:
				forceStopped = true;
				goto case AudioPlaybackMode.Playing;
			case AudioPlaybackMode.Playing:
				if (forceStopped || (currentOrder >= Song.CurrentSong.OrderList.Count) || (Song.CurrentSong.OrderList[currentOrder] >= 200))
				{
					/* this does, in fact, happen. just pretend that
					* it's stopped :P */
					/* stopped */
					VGAMem.DrawFillCharacters(new Point(5, @base + 1), new Point(4 + numChannels * FullChannelWidth - (Separator ? 1 : 0),
						@base + fullHeight - 2), (VGAMem.DefaultForeground, 0));
					return;
				}

				curPattern = Song.CurrentSong.GetPattern(Song.CurrentSong.OrderList[currentOrder]) ?? EmptyPattern;
				curPatternRows = curPattern.Rows.Count;

				if (currentOrder > 0 && Song.CurrentSong.OrderList[currentOrder - 1] < 200)
				{
					prevPattern = Song.CurrentSong.GetPattern(Song.CurrentSong.OrderList[currentOrder - 1]) ?? EmptyPattern;
					prevPatternRows = prevPattern.Rows.Count;
				}
				else
					prevPattern = null;

				if (currentOrder + 1 < Song.CurrentSong.OrderList.Count && Song.CurrentSong.OrderList[currentOrder + 1] < 200)
				{
					nextPattern = Song.CurrentSong.GetPattern(Song.CurrentSong.OrderList[currentOrder + 1]) ?? Pattern.CreateEmpty();
					nextPatternRows = nextPattern.Rows.Count;
				}
				else
					nextPattern = null;
				break;
		}

		/* -2 for the top and bottom border, -1 because if there are an even number
		* of rows visible, the current row is drawn above center. */
		int rowsBefore = (fullHeight - 3) / 2;

		/* "fake" channels (hack for 64-channel view) */
		if (numChannels > Constants.MaxChannels)
		{
			DrawFillNotes(5 + Constants.MaxChannels * FullChannelWidth, @base + 1, fullHeight - 2, 0);
			DrawFillNotes(5 + Constants.MaxChannels * FullChannelWidth, @base + 1 + rowsBefore, 1, 14);
			numChannels = Constants.MaxChannels;
		}

		/* draw the area above the current row */
		pattern = curPattern;
		totalRows = curPatternRows;

		int row = currentRow - 1;
		int rowPos = @base + rowsBefore;

		while (rowPos > @base)
		{
			if (row < 0)
			{
				if (prevPattern == null)
				{
					DrawFillNotes(5, @base + 1, rowPos - @base, 0);
					break;
				}
				pattern = prevPattern;
				totalRows = prevPatternRows;
				row = totalRows - 1;
			}

			VGAMem.DrawText(row.ToString("d3"), new Point(1, rowPos), (0, 2));

			for (int chanPos = 0, column = 5; column + FullChannelWidth < 76 && chanPos < Constants.MaxChannels; chanPos++, column += FullChannelWidth)
			{
				ref var note = ref pattern[row][FirstChannel + chanPos];

				TrackView.DrawNote(new Point(column, rowPos), ref note, -1, (6, 0));

				if (Separator && (4 + FullChannelWidth * (chanPos + 1) < 76))
					VGAMem.DrawCharacter(168, new Point(4 + FullChannelWidth * (chanPos + 1), rowPos), (2, 0));
			}

			row--;
			rowPos--;
		}

		/* draw the current row */
		pattern = curPattern;
		totalRows = curPatternRows;
		rowPos = @base + rowsBefore + 1;
		VGAMem.DrawText(currentRow.ToString("d3"), new Point(1, rowPos), (0, 2));

		for (int chanPos = 0, column = 5; column + FullChannelWidth < 76 && chanPos < Constants.MaxChannels; chanPos++, column += FullChannelWidth)
		{
			ref var note = ref pattern[currentRow][FirstChannel + chanPos];

			TrackView.DrawNote(new Point(column, rowPos), ref note, -1, (6, 14));

			if (Separator && (4 + FullChannelWidth * (chanPos + 1) < 76))
				VGAMem.DrawCharacter(168, new Point(4 + FullChannelWidth * (chanPos + 1), rowPos), (2, 14));
		}

		/* draw the area under the current row */
		row = currentRow + 1;
		rowPos++;
		while (rowPos < @base + fullHeight - 1)
		{
			if (row >= totalRows)
			{
				if (nextPattern == null)
				{
					DrawFillNotes(5, rowPos, @base + fullHeight - rowPos - 1, 0);
					break;
				}

				pattern = nextPattern;
				totalRows = nextPatternRows;
				row = 0;
			}

			VGAMem.DrawText(row.ToString("d3"), new Point(1, rowPos), (0, 2));

			for (int chanPos = 0, column = 5; column + FullChannelWidth < 76 && chanPos < Constants.MaxChannels; chanPos++, column += FullChannelWidth)
			{
				ref var note = ref pattern[row][FirstChannel + chanPos];

				TrackView.DrawNote(new Point(column, rowPos), ref note, -1, (6, 0));

				if (Separator && (4 + FullChannelWidth * (chanPos + 1) < 76))
					VGAMem.DrawCharacter(168, new Point(4 + FullChannelWidth * (chanPos + 1), rowPos), (2, 0));
			}

			row++;
			rowPos++;
		}
	}

	void DrawFillNotes(int col, int firstRow, int height, int bg)
	{
		var blankNote = SongNote.Empty;

		for (int rowPos = firstRow; rowPos < firstRow + height; rowPos++)
		{
			for (int chanPos = 0, column = col; column + FullChannelWidth < 76; chanPos++, column += FullChannelWidth)
			{
				TrackView.DrawNote(new Point(column, rowPos), ref blankNote, -1, (6, bg));

				if (Separator && (4 + FullChannelWidth * (chanPos + 1) < 76))
					VGAMem.DrawCharacter(168, new Point(4 + FullChannelWidth * (chanPos + 1), rowPos), (2, bg));
			}
		}
	}

	protected abstract void DrawChannelHeader(int chan, int column, int @base, byte fg);

	public override void Draw(int @base, int fullHeight, bool isActive)
	{
		int right = GetRightEdge();

		VGAMem.DrawBox(new Point(4, @base), new Point(right, @base + fullHeight - 1), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		for (int chan = FirstChannel, column = 5; column + FullChannelWidth < right && chan <= Song.CurrentSong.Channels.Length; chan++, column += FullChannelWidth)
		{
			var channel = Song.CurrentSong.Channels[chan - 1];

			byte fg;

			if (channel.Flags.HasAllFlags(ChannelFlags.Mute))
				fg = (chan == SelectedChannel) ? (byte)6 : (byte)1;
			else
				fg = (chan == SelectedChannel) ? (byte)3 : (isActive ? (byte)2 : (byte)0);

			DrawChannelHeader(chan, column, @base, fg);
		}

		DrawTrackView(@base, fullHeight);
	}

	public override void Click(Point mousePosition)
	{
		int x = mousePosition.X - 4;

		while (x > 0 && FirstChannel <= Constants.MaxChannels)
		{
			if (x < FullChannelWidth)
			{
				SelectedChannel.Value = FirstChannel.Clamp(1, Constants.MaxChannels);
				return;
			}

			FirstChannel++;
			x -= FullChannelWidth;
		}
	}
}
