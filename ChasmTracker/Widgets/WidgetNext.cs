using System;
using System.Collections.Generic;

namespace ChasmTracker.Widgets;

using ChasmTracker.Utility;

public class WidgetNext
{
	public Widget? Up;
	public Widget? Down;
	public Widget? Left;
	public Widget? Right;
	public Widget? Tab;
	public Widget? BackTab;

	public static void Initialize(IReadOnlyList<Widget> widgets)
	{
		for (int i = 0; i < widgets.Count; i++)
		{
			var @this = widgets[i];

			var next = @this.Next;

			if ((next.BackTab == null) && (i > 0))
				next.BackTab = widgets.FindPreviousWithLoop(i, w => w.IsTabStop);
			if ((next.Tab == null) && (i + 1 < widgets.Count))
				next.Tab = widgets.FindNextWithLoop(i, w => w.IsTabStop);

			Widget? Search(int dx, int dy, int expandX, int expandY)
				=> WidgetNext.Search(widgets, @this, dx, dy, expandX, expandY);

			if (next.Up == null)
				next.Up = Search(0, -1, 1, 0) ?? Search(0, -1, 100, 0);
			if (next.Down == null)
				next.Down = Search(0, +1, 1, 0) ?? Search(0, +1, 100, 0);
			if (next.Left == null)
				next.Left = Search(-1, 0, 0, 1) ?? Search(-1, 0, 0, 100);
			if (next.Right == null)
				next.Right = Search(1, 0, 0, 1) ?? Search(1, 0, 0, 100);
		}
	}

	public static Widget? Search(WidgetContext? widgetContext, Widget @this, Point origin, SearchDirection direction)
	{
		if (widgetContext == null)
			return null;

		return Search(widgetContext.Widgets, @this, origin, direction);
	}

	public static Widget? Search(IReadOnlyList<Widget> widgets, Widget @this, Point origin, SearchDirection direction)
	{
		int dx, dy;

		switch (direction)
		{
			case SearchDirection.Up:    (dx, dy) = (0, -1); break;
			case SearchDirection.Down:  (dx, dy) = (0, +1); break;
			case SearchDirection.Left:  (dx, dy) = (-1, 0); break;
			case SearchDirection.Right: (dx, dy) = (+1, 0); break;

			default: return null;
		}

		return Search(widgets, @this, origin, dx, dy, Math.Abs(dy), Math.Abs(dx));
	}

	static Widget? Search(IReadOnlyList<Widget> widgets, Widget @this, int dx, int dy, int expandX, int expandY)
	{
		var centreMass = @this.Position.Advance(@this.Size / 2);

		var origin = centreMass;

		if (dx < 0)
			origin.X = @this.Position.X;
		if (dx > 0)
			origin.X = @this.Position.X + @this.Size.Width - 1;

		if (dy < 0)
			origin.Y = @this.Position.Y;
		if (dy > 0)
			origin.Y = @this.Position.Y + @this.Size.Height - 1;

		return Search(widgets, @this, origin, dx, dy, expandX, expandY);
	}

	static Widget? Search(IReadOnlyList<Widget> widgets, Widget @this, Point origin, int dx, int dy, int expandX, int expandY)
	{
		Widget? bestCandidate = null;
		double bestCandidateDistance = double.MaxValue;

		Point scanStart = origin;
		Point scanEnd = origin.Advance(1, 1);

		if (dx != 0)
		{
			scanStart.Y -= @this.Size.Height / 3;
			scanEnd.Y += @this.Size.Height / 3;
		}

		if (dy != 0)
		{
			scanStart.X -= @this.Size.Width / 3;
			scanEnd.X += @this.Size.Width / 3;
		}

		bool ScanIsOnScreen()
		{
			var topLeft = new Point(Math.Min(scanStart.X, scanEnd.X), Math.Min(scanStart.Y, scanEnd.Y));
			var bottomRight = new Point(Math.Max(scanStart.X, scanEnd.X), Math.Max(scanStart.Y, scanEnd.Y));

			return (topLeft.X <= 80) && (topLeft.Y <= 50) && (bottomRight.X >= 0) && (bottomRight.Y >= 0);
		}

		while (ScanIsOnScreen())
		{
			for (int j = 0; j < widgets.Count; j++)
			{
				if (widgets[j] == @this)
					continue;

				var other = widgets[j];

				if (other.Size.IsEmpty)
					continue;

				var overlap = new Rect(other.Position, other.Size).Intersection(
					new Rect(scanStart, scanEnd - scanStart));

				if (!overlap.IsEmpty)
				{
					var pointOfContact = new Point(
						origin.X.Clamp(overlap.TopLeft.X, overlap.BottomRight.X),
						origin.Y.Clamp(overlap.TopLeft.Y, overlap.BottomRight.Y));

					var distance = origin.DistanceTo(pointOfContact);

					if (distance < bestCandidateDistance)
					{
						bestCandidate = other;
						bestCandidateDistance = distance;
					}
				}
			}

			scanStart.X += dx;
			scanStart.Y += dy;

			scanEnd.X += dx;
			scanEnd.Y += dy;

			scanStart.X -= expandX;
			scanEnd.X += expandX;

			scanStart.Y -= expandY;
			scanEnd.Y += expandY;
		}

		return bestCandidate;
	}
}
