				dialog_create_custom(20, 23, 40, 12, template_error_widgets, 1,
						0, template_error_draw, NULL);
static struct widget template_error_widgets[1];
static void template_error_draw(void)
{
	draw_text("Template Error", 33, 25, 0, 2);
	draw_text("No note in the top left position", 23, 27, 0, 2);
	draw_text("of the clipboard on which to", 25, 28, 0, 2);
	draw_text("base translations.", 31, 29, 0, 2);
}



