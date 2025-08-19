namespace ChasmTracker.Widgets;

public enum WidgetType
{
	Toggle,
	MenuToggle,
	Button,
	ToggleButton,
	TextEntry,
	NumberEntry,
	ThumbBar,
	BitSet,
	PanBar,
	ListBox,
	/* this last one is for anything that doesn't fit some standard
	type, like the sample list, envelope editor, etc.; a widget of
	this type is just a placeholder so page.c knows there's
	something going on. */
	Other, /* sample list, envelopes, etc. */
}
