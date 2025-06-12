namespace ChasmTracker.Pages;

public class PatternEditorMultiChannelDialog
{
/* --------------------------------------------------------------------------------------------------------- */
/* multichannel dialog */
static struct widget multichannel_widgets[65];
static void multichannel_close(SCHISM_UNUSED void *data)
{
	int i, m = 0;

	for (i = 0; i < 64; i++) {
		channel_multi[i] = !!multichannel_widgets[i].d.toggle.state;
		if (channel_multi[i])
			m = 1;
	}
	if (m)
		channel_multi_enabled = 1;
}
static int multichannel_handle_key(struct key_event *k)
{
	if (k->sym == SCHISM_KEYSYM_n) {
		if ((k->mod & SCHISM_KEYMOD_ALT) && k->state == KEY_PRESS)
			dialog_yes(NULL);
		else if (NO_MODIFIER(k->mod) && k->state == KEY_RELEASE)
			dialog_cancel(NULL);
		return 1;
	}
	return 0;
}
static void multichannel_draw_const(void)
{
	char sbuf[16];
	int i;

	for (i = 0; i < 64; i++) {
		sprintf(sbuf, "Channel %02d", i+1);
		draw_text(sbuf,
			9 + ((i / 16) * 16), /* X */
			22 + (i % 16),  /* Y */
			0, 2);
	}
	for (i = 0; i < 64; i += 16) {
		draw_box(
			19 + ((i / 16) * 16), /* X */
			21,
			23 + ((i / 16) * 16), /* X */
			38,
			BOX_THIN|BOX_INNER|BOX_INSET);
	}
	draw_text("Multichannel Selection", 29, 19, 3, 2);
}
static void mp_advance_channel(void)
{
	widget_change_focus_to(*selected_widget + 1);
}

static void pattern_editor_display_multichannel(void)
{
	struct dialog *dialog;
	int i;

	for (i = 0; i < 64; i++) {
		widget_create_toggle(multichannel_widgets+i,
			20 + ((i / 16) * 16), /* X */
			22 + (i % 16),  /* Y */

			((i % 16) == 0) ? 64 : (i-1),
			((i % 16) == 15) ? 64 : (i+1),
			(i < 16) ? (i+48) : (i-16),
			((i + 16) % 64),
			((i + 16) % 64),

			mp_advance_channel);
		multichannel_widgets[i].d.toggle.state = !!channel_multi[i];
	}
	widget_create_button(multichannel_widgets + 64, 36, 40, 6, 15, 0, 64, 64, 64, dialog_yes_NULL, "OK", 3);

	dialog = dialog_create_custom(7, 18, 66, 25, multichannel_widgets, 65, 0,
				      multichannel_draw_const, NULL);
	dialog->action_yes = multichannel_close;
	dialog->action_cancel = multichannel_close;
	dialog->handle_key = multichannel_handle_key;
}
}
