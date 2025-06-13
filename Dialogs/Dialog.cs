using System;
using System.Collections.Generic;
using System.Linq;

using ChasmTracker.Menus;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

namespace ChasmTracker.Dialogs;

public class Dialog
{
	public DialogTypes Type;
	public Point Position;
	public Size Size;
	public List<Widget> Widgets = new List<Widget>();
	public SharedInt SelectedWidget = new SharedInt();

	public string? Text;
	public int TextX;
	public object? Data;
	public Action<object?>? ActionYes;
	public Action<object?>? ActionNo;
	public Action<object?>? ActionCancel;

	static Stack<Dialog> s_activeDialogs = new Stack<Dialog>();

	public static Dialog Create<T>()
		where T : Dialog, new()
	{
		var dialog = new T();

		s_activeDialogs.Push(dialog);

		return dialog;
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

	public virtual void DrawConst(VGAMem vgaMem)
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

		buttonOK.Changed += DialogButtonYes;
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

		buttonOK.Changed += DialogButtonYes;
		buttonCancel.Changed += DialogButtonCancel;

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

		buttonOK.Changed += DialogButtonYes;
		buttonCancel.Changed += DialogButtonNo;

		Widgets.Add(buttonOK);
		Widgets.Add(buttonCancel);
	}

	/* --------------------------------------------------------------------- */
	/* type can be DialogTypes.OK, DialogTypes.Cancel, or DialogTypes.YesNo
	 * default_widget: 0 = ok/yes, 1 = cancel/no */
	protected Dialog(DialogTypes type, string text, Action<object?>? actionYes,
		Action<object?>? actionNo, int defaultWidget, object? data)
	{
		if (!type.HasFlag(DialogTypes.Box))
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
		SelectedWidget.Value = defaultWidget;

		switch (type)
		{
			case DialogTypes.OK:
				DialogCreateOK();
				break;
			case DialogTypes.OKCancel:
				DialogCreateOKCancel();
				break;
			case DialogTypes.YesNo:
				DialogCreateYesNo();
				break;
			default:
				Console.Error.WriteLine("this man should not be seen");
				type = DialogTypes.OKCancel;
				goto case DialogTypes.OKCancel;
		}

		s_activeDialogs.Push(this);

		Page.ActiveWidgets = Widgets;
		Page.SelectedActiveWidget = SelectedWidget;

		Status.DialogType = type;
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	protected Dialog(Point position, Size size)
	{
		Type = DialogTypes.Custom;

		Position = position;
		Size = size;

		Initialize();

		for (int i = 0; i < Widgets.Count; i++)
		{
			var @this = Widgets[i];

			var centerMass = @this.Position.Advance(@this.Size / 2);

			ref var next = ref Widgets[i].Next;

			if ((next.BackTab == null) && (i > 0))
				next.BackTab = Widgets[i - 1];
			if ((next.Tab == null) && (i + 1 < Widgets.Count))
				next.Tab = Widgets[i + 1];

			Widget? Search(int dx, int dy, int expandX, int expandY)
			{
				Widget? bestCandidate = null;
				double bestCandidateDistance = double.MaxValue;

				Point scanStart = @this.Position;
				Point scanEnd = @this.Position.Advance(@this.Size);

				bool Intersects(Widget test)
				{
					var topLeft = test.Position;
					var bottomRight = test.Position.Advance(test.Size);

					if (topLeft.X > scanEnd.X)
						return false;
					if (topLeft.Y > scanEnd.Y)
						return false;
					if (bottomRight.X < scanStart.X)
						return false;
					if (bottomRight.Y < scanEnd.Y)
						return false;

					return true;
				}

				bool ScanIsOnScreen()
				{
					var topLeft = new Point(Math.Min(scanStart.X, scanEnd.X), Math.Min(scanStart.Y, scanEnd.Y));
					var bottomRight = new Point(Math.Max(scanStart.X, scanEnd.X), Math.Max(scanStart.Y, scanEnd.Y));

					return (topLeft.X <= 80) && (topLeft.Y <= 50) && (bottomRight.X >= 0) && (bottomRight.Y >= 0);
				}

				while (ScanIsOnScreen())
				{
					for (int j = 0; j < Widgets.Count; j++)
					{
						if (j == i)
							continue;

						var other = Widgets[j];

						if (Intersects(other))
						{
							var otherCenterMass = other.Position.Advance(other.Size / 2);

							var distance = centerMass.DistanceTo(otherCenterMass);

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
					scanStart.Y += expandY;
				}

				return bestCandidate;
			}

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

	protected virtual void Initialize() { }

	public static void DialogButtonYes(object? data)
	{
		// TODO
	}

	public static void DialogButtonNo(object? data)
	{
		// TODO
	}

	public static void DialogButtonCancel(object? data)
	{
		// TODO
	}

	public static void DialogButtonYes() => DialogButtonYes(default);
	public static void DialogButtonNo() => DialogButtonNo(default);
	public static void DialogButtonCancel() => DialogButtonCancel(default);
}
