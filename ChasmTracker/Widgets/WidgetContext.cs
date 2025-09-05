using System;
using System.Collections.Generic;

namespace ChasmTracker.Widgets;

using ChasmTracker.Utility;

public class WidgetContext
{
	List<Widget> _widgets = new List<Widget>();

	// When a shared widget is active on this context, what is its WidgetNext?
	Dictionary<Widget, WidgetNext> _sharedWidgetNext = new Dictionary<Widget, WidgetNext>();

	public IReadOnlyList<Widget> Widgets => _widgets;
	public Shared<int> SelectedWidgetIndex = new Shared<int>();

	protected void LinkSelectedWidgetIndex(Shared<int> commonSelectedWidgetIndex)
	{
		SelectedWidgetIndex = commonSelectedWidgetIndex;
	}

	public void AddWidget(Widget widget)
	{
		if (widget.WidgetContext != null)
			throw new Exception("Widget is already in a WidgetContext");

		widget.WidgetContext = this;
		_widgets.Add(widget);
	}

	public void AddSharedWidget(Widget widget)
	{
		widget.IsShared = true;
		_widgets.Add(widget);
		_sharedWidgetNext[widget] = new WidgetNext();
	}

	public void AddWidgets(IEnumerable<Widget> widgets)
	{
		foreach (var widget in widgets)
			AddWidget(widget);
	}

	public void AddSharedWidgets(IEnumerable<Widget> widgets)
	{
		foreach (var widget in widgets)
			AddSharedWidget(widget);
	}

	public Widget? SelectedWidget
	{
		get
		{
			if ((SelectedWidgetIndex < 0) || (SelectedWidgetIndex >= Widgets.Count))
				return null;

			return Widgets[SelectedWidgetIndex];
		}
	}

	public void TakeOwnershipOfSharedWidgets()
	{
		foreach (var widget in _widgets)
			if (widget.IsShared)
			{
				widget.WidgetContext = this;
				widget.Next = _sharedWidgetNext[widget];
			}
	}

	public bool ChangeFocusTo(int newWidgetIndex)
	{
		if (newWidgetIndex == SelectedWidgetIndex)
			return true;

		if ((newWidgetIndex < 0)
		 || (newWidgetIndex >= Widgets.Count))
			return false;

		if (SelectedWidget != null)
			SelectedWidget.IsDepressed = false;

		SelectedWidgetIndex.Value = newWidgetIndex;

		if (SelectedWidget != null)
		{
			SelectedWidget.IsDepressed = false;

			if (SelectedWidget is TextEntryWidget textEntry)
				textEntry.CursorPosition = textEntry.TextLength;
		}

		Status.Flags |= StatusFlags.NeedUpdate;

		return true;
	}

	public bool ChangeFocusTo(Widget? newWidget)
	{
		if (newWidget != null)
			return ChangeFocusTo(_widgets.IndexOf(newWidget));
		else
			return ChangeFocusTo(-1);
	}

	public bool ChangeFocusToXY(Point pt)
	{
		for (int i = 0; i < Widgets.Count; i++)
			if (Widgets[i].ContainsPoint(pt))
				return ChangeFocusTo(i);

		return false;
	}

	public Widget? FindWidgetXY(Point pt)
	{
		for (int i = 0; i < Widgets.Count; i++)
			if (Widgets[i].ContainsPoint(pt))
				return Widgets[i];

		return null;
	}
}
