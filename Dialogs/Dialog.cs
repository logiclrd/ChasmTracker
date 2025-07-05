using System;
using System.Collections.Generic;
using System.Linq;

namespace ChasmTracker.Dialogs;

using ChasmTracker.Input;
using ChasmTracker.Menus;
using ChasmTracker.Pages;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class Dialog
{
	public DialogTypes Type;
	public Point Position;
	public Size Size;
	public List<Widget> Widgets = new List<Widget>();
	public Shared<int> SelectedWidgetIndex = new Shared<int>();

	public string? Text;
	public int TextX;
	public object? Data;
	public Action<object?>? ActionYes;
	public Action<object?>? ActionNo;
	public Action<object?>? ActionCancel;

	static Stack<Dialog> s_activeDialogs = new Stack<Dialog>();

	public static T Show<T>()
		where T : Dialog, new()
		=> Show(new T());

	public static T Show<T>(T dialog)
		where T : Dialog
	{
		s_activeDialogs.Push(dialog);

		return dialog;
	}

	public Widget ActiveWidget
	{
		get
		{
			if ((SelectedWidgetIndex >= 0) && (SelectedWidgetIndex < Widgets.Count))
				return Widgets[SelectedWidgetIndex];

			return Widgets[0];
		}
	}

	public Dialog ChangeFocusTo(Widget widget)
	{
		int index = Widgets.IndexOf(widget);

		if (index >= 0)
			ChangeFocusTo(index);

		return this;
	}

	public Dialog ChangeFocusTo(int newWidgetIndex)
	{
		if ((newWidgetIndex == SelectedWidgetIndex)
		 || (newWidgetIndex < 0)
		 || (newWidgetIndex >= Widgets.Count))
			return this;

		if ((SelectedWidgetIndex >= 0) && (SelectedWidgetIndex < Widgets.Count))
			ActiveWidget.IsDepressed = false;

		SelectedWidgetIndex.Value = newWidgetIndex;

		ActiveWidget.IsDepressed = false;

		if (ActiveWidget is TextEntryWidget textEntry)
			textEntry.CursorPosition = textEntry.Text.Length;

		Status.Flags |= StatusFlags.NeedUpdate;

		return this;
	}

	public static void Destroy()
	{
		if (!s_activeDialogs.Any())
			return;

		s_activeDialogs.Pop();

		if (s_activeDialogs.Any())
		{
			var dialog = s_activeDialogs.Peek();

			Page.ActiveWidgets = dialog.Widgets;
			Status.DialogType = dialog.Type;
		}
		else
		{
			Page.ActiveWidgets = Status.CurrentPage.Widgets;
			Status.DialogType = DialogTypes.None;
		}

		/* it's up to the calling function to redraw the page */
	}

	public static void DestroyAll()
	{
		while (s_activeDialogs.Any())
			Destroy();
	}

	public static void DrawActiveDialogs()
	{
		foreach (var dialog in s_activeDialogs)
		{
			/* draw the border and background */
			VGAMem.DrawBox(
				dialog.Position,
				dialog.Position.Advance(dialog.Size).Advance(-1, -1),
				BoxTypes.Thick | BoxTypes.Outer | BoxTypes.FlatLight);

			VGAMem.DrawFillCharacters(
				dialog.Position.Advance(1, 1),
				dialog.Position.Advance(dialog.Size).Advance(-2, -2),
				(VGAMem.DefaultForeground, 2));

			/* then the rest of the stuff */
			dialog.DrawConst();

			if (!string.IsNullOrWhiteSpace(dialog.Text))
				VGAMem.DrawText(dialog.Text, new Point(dialog.TextX, 27), (0, 2));

			for (int i = 0; i < dialog.Widgets.Count; i++)
			{
				var widget = dialog.Widgets[i];
				bool isSelected = (i == dialog.SelectedWidgetIndex);

				widget.DrawWidget(isSelected);
			}
		}
	}

	public static bool HasCurrentDialog => s_activeDialogs.Any();

	public static bool HandleKeyForCurrentDialog(KeyEvent k)
	{
		if (!s_activeDialogs.Any())
		{
			Console.Error.WriteLine("{0} called with no dialog", nameof(HandleKeyForCurrentDialog));
			return false;
		}

		var dialog = s_activeDialogs.Peek();

		if (dialog.HandleKey(k))
			return true;

		/* this SHOULD be handling on k.State press but the widget key handler is stealing that key. */
		if (k.State == KeyState.Release && !k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
		{
			switch (k.Sym)
			{
				case KeySym.y:
					switch ((MessageBoxTypes)Status.DialogType)
					{
						case MessageBoxTypes.YesNo:
						case MessageBoxTypes.OKCancel:
							DialogButtonYes(dialog.Data);
							return true;
					}
					break;
				case KeySym.n:
					switch ((MessageBoxTypes)Status.DialogType)
					{
						case MessageBoxTypes.YesNo:
							/* in Impulse Tracker, 'n' means cancel, not "no"!
							(results in different behavior on sample quality convert dialog) */
							if (!Status.Flags.HasFlag(StatusFlags.ClassicMode))
							{
								DialogButtonNo(dialog.Data);
								return true;
							}
							goto case MessageBoxTypes.OKCancel;
						case MessageBoxTypes.OKCancel:
							DialogButtonCancel(dialog.Data);
							return true;
					}

					break;
				case KeySym.c:
					switch ((MessageBoxTypes)Status.DialogType)
					{
						case MessageBoxTypes.YesNo:
						case MessageBoxTypes.OKCancel:
							break;
						default:
							return false;
					}

					goto case KeySym.Escape;
				case KeySym.Escape:
					DialogButtonCancel(dialog.Data);
					return true;
				case KeySym.o:
					switch ((MessageBoxTypes)Status.DialogType)
					{
						case MessageBoxTypes.YesNo:
						case MessageBoxTypes.OKCancel:
							break;
						default:
							return false;
					}

					goto case KeySym.Return;
				case KeySym.Return:
					DialogButtonYes(dialog.Data);
					return true;
				default:
					break;
			}
		}

		return false;
	}

	public virtual void DrawConst()
	{
	}

	public virtual bool HandleKey(KeyEvent keyEvent)
	{
		return false;
	}

	/* --------------------------------------------------------------------- */
	/* these get called from dialog_create below */
	void DialogCreateOK()
	{
		Text ??= "";

		TextX = 40 - Text.Length / 2;

		/* make the dialog as wide as either the ok button or the text,
		 * whichever is more */
		if (Text.Length > 21)
		{
			Position.X = TextX - 2;
			Size.Width = Text.Length + 4;
		}
		else
		{
			Position.X = 26;
			Size.Width = 29;
		}

		Size.Height = 8;
		Position.Y = 25;

		var buttonOK = new ButtonWidget(new Point(36, 30), 6, "OK", 3);

		Widgets.Add(buttonOK);

		buttonOK.Clicked += DialogButtonYes;
	}

	void DialogCreateOKCancel()
	{
		/* the ok/cancel buttons (with the borders and all) are 21 chars,
		 * so if the text is shorter, it needs a bit of padding. */
		TextX = 40 - Text!.Length / 2;

		if (Text.Length > 21)
		{
			Position.X = TextX - 4;
			Size.Width = Text.Length + 8;
		}
		else
		{
			Position.X = 26;
			Size.Width = 29;
		}

		Size.Height = 8;
		Position.Y = 25;

		var buttonOK = new ButtonWidget(new Point(31, 30), 6, "OK", 3);
		var buttonCancel = new ButtonWidget(new Point(42, 30), 6, "Cancel", 3);

		buttonOK.Clicked += DialogButtonYes;
		buttonCancel.Clicked += DialogButtonCancel;

		Widgets.Add(buttonOK);
		Widgets.Add(buttonCancel);
	}

	void DialogCreateYesNo()
	{
		TextX = 40 - Text!.Length / 2;

		if (Text.Length > 21)
		{
			Position.X = TextX - 4;
			Size.Width = Text.Length + 8;
		}
		else
		{
			Position.X = 26;
			Size.Width = 29;
		}

		Size.Height = 8;
		Position.Y = 25;

		var buttonOK = new ButtonWidget(new Point(30, 30), 6, "Yes", 3);
		var buttonCancel = new ButtonWidget(new Point(42, 30), 6, "No", 3);

		buttonOK.Clicked += DialogButtonYes;
		buttonCancel.Clicked += DialogButtonNo;

		Widgets.Add(buttonOK);
		Widgets.Add(buttonCancel);
	}

	/* --------------------------------------------------------------------- */
	/* type can be DialogTypes.OK, DialogTypes.Cancel, or DialogTypes.YesNo
	 * default_widget: 0 = ok/yes, 1 = cancel/no */
	protected Dialog(MessageBoxTypes type, string text, Action<object?>? actionYes,
		Action<object?>? actionNo, int defaultWidget, object? data)
	{
		if (!type.HasFlag(MessageBoxDialogTypes.Box))
		{
			Console.Error.WriteLine("dialog_create called with bogus dialog type {0}", type);
			throw new ArgumentException(nameof(type));
		}

		/* FIXME | hmm... a menu should probably be hiding itself when a widget gets selected. */
		if (Status.DialogType.HasFlag(DialogTypes.Menu))
			Menu.Hide();

		Text = text;
		Data = data;
		ActionYes = actionYes;
		ActionNo = actionNo;
		ActionCancel = null; /* ??? */
		SelectedWidgetIndex.Value = defaultWidget;

		switch (type)
		{
			case MessageBoxTypes.OK:
				DialogCreateOK();
				break;
			case MessageBoxTypes.OKCancel:
				DialogCreateOKCancel();
				break;
			case MessageBoxTypes.YesNo:
				DialogCreateYesNo();
				break;
			default:
				Console.Error.WriteLine("this man should not be seen");
				type = MessageBoxTypes.OKCancel;
				goto case MessageBoxTypes.OKCancel;
		}

		s_activeDialogs.Push(this);

		Page.ActiveWidgets = Widgets;
		Page.SelectedActiveWidgetIndex = SelectedWidgetIndex;

		Status.DialogType = (DialogTypes)type;
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	protected Dialog(Point position, Size size)
	{
		Type = DialogTypes.Custom;

		Position = position;
		Size = size;

		Initialize();

		ToggleButtonWidget.BuildGroups(Widgets);

		WidgetNext.Initialize(Widgets);

		SetInitialFocus();
	}

	protected virtual void Initialize() { }
	protected virtual void SetInitialFocus() { }

	static void DialogButton(string functionName, Func<Dialog, Action<object?>?> getFunctor, object? data)
	{
		if (!s_activeDialogs.Any())
		{
			Console.Error.WriteLine("{0} called with no dialog", functionName);
			return;
		}

		var dialog = s_activeDialogs.Peek();

		var action = getFunctor(dialog);

		if (data == null)
			data = dialog.Data;

		Dialog.Destroy();

		action?.Invoke(data);

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	public static void DialogButtonYes(object? data)
	{
		DialogButton(nameof(DialogButtonYes), d => d.ActionYes, data);
	}

	public static void DialogButtonNo(object? data)
	{
		DialogButton(nameof(DialogButtonYes), d => d.ActionNo, data);
	}

	public static void DialogButtonCancel(object? data)
	{
		DialogButton(nameof(DialogButtonYes), d => d.ActionCancel, data);
	}

	public static void DialogButtonYes() => DialogButtonYes(default);
	public static void DialogButtonNo() => DialogButtonNo(default);
	public static void DialogButtonCancel() => DialogButtonCancel(default);
}
