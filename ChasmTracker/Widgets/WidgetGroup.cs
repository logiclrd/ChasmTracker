namespace ChasmTracker.Widgets;

public class WidgetGroup
{
	public int[] Indices;
	public Widget?[] Widgets;

	public WidgetGroup(int[] indices)
	{
		Indices = indices;
		Widgets = new Widget[indices.Length];
	}

	public WidgetGroup(int[] indices, Widget[] widgets)
	{
		Indices = indices;
		Widgets = widgets;
	}
}
