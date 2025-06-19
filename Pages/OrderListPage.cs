using System;

namespace ChasmTracker.Pages;

using ChasmTracker.Songs;

public abstract class OrderListPage : Page
{
	public OrderListPage(PageNumbers number, HelpTexts helpText)
		: base(number, "Order List and Panning (F11)", helpText)
	{
	}

	public override void SynchronizeWith(Page other)
	{
		base.SynchronizeWith(other);

		AllPages.OrderList = this;
	}

	// These values are shared between the subclasses of OrderListPage, which
	// collectively act as a single page.
	static int s_currentOrder;
	static int s_topOrder;

	public int CurrentOrder
	{
		get => s_currentOrder;
		set
		{
			int newOrder = value;

			newOrder = Math.Min(Song.CurrentSong?.OrderList?.Count ?? 0, newOrder);
			newOrder = Math.Max(0, newOrder);

			if (s_currentOrder == newOrder)
				return;

			s_currentOrder = newOrder;

			// TODO: order_list_reposition();

			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	public int TopOrder
	{
		get => s_topOrder;
		set => s_topOrder = value;
	}

	void Reposition()
	{
		if (CurrentOrder < TopOrder)
			TopOrder = CurrentOrder;
		else if (CurrentOrder > TopOrder + 31)
			TopOrder = CurrentOrder - 31;
	}

	/* --------------------------------------------------------------------- */
	/* called from the pattern editor on ctrl-plus/minus */

	public void PreviousOrderPattern()
	{
		int newOrder = CurrentOrder;
		int lastPattern = Song.CurrentSong.OrderList[newOrder];

		do
		{
			if (--newOrder < 0)
			{
				newOrder = 0;
				break;
			}
		} while (!Status.Flags.HasFlag(StatusFlags.ClassicMode)
				&& lastPattern == Song.CurrentSong.OrderList[newOrder]
				&& Song.CurrentSong.OrderList[newOrder] == SpecialOrders.Skip);

		if (Song.CurrentSong.OrderList[newOrder] < 200)
		{
			CurrentOrder = newOrder;
			Reposition();
			AllPages.PatternEditor.CurrentPattern = Song.CurrentSong.OrderList[newOrder];
		}
	}

	public void NextOrderPattern()
	{
		int new_order = CurrentOrder;
		int last_pattern = Song.CurrentSong.OrderList[new_order];

		do
		{
			if (++new_order > 255)
			{
				new_order = 255;
				break;
			}
		} while (!Status.Flags.HasFlag(StatusFlags.ClassicMode)
				&& last_pattern == Song.CurrentSong.OrderList[new_order]
				&&  Song.CurrentSong.OrderList[new_order] == SpecialOrders.Skip);

		if (Song.CurrentSong.OrderList[new_order] < 200) {
			CurrentOrder = new_order;
			Reposition();
			AllPages.PatternEditor.CurrentPattern = Song.CurrentSong.OrderList[new_order];
		}
	}
}
