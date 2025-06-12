using System;


using ChasmTracker.Songs;

namespace ChasmTracker.Pages;

public abstract class OrderListPage : Page
{
	public OrderListPage(PageNumbers number, string title, HelpTexts helpText)
		: base(number, title, helpText)
	{
	}

	public override void SynchronizeWith(Page other)
	{
		base.SynchronizeWith(other);

		AllPages.OrderList = this;
	}

	static int s_currentOrder;

	public int CurrentOrder
	{
		get => s_currentOrder;
		set
		{
			int newOrder = value;

			newOrder = Math.Min(Song.CurrentSong?.Order?.Count ?? 0, newOrder);
			newOrder = Math.Max(0, newOrder);

			if (s_currentOrder == newOrder)
				return;

			s_currentOrder = newOrder;

			// TODO: order_list_reposition();

			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}
}
