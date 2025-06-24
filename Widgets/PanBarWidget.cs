using ChasmTracker.Input;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

namespace ChasmTracker.Widgets;

public class PanBarWidget : Widget
{
	public int Minimum;
	public int Maximum;
	public int Value;
	public int Channel;
	public bool IsReversed;
	public bool IsMuted;
	public bool IsSurround;

	public PanBarWidget(Point position, WidgetNext next, int channel)
		: base(position, width: 24)
	{
		Minimum = 0;
		Maximum = 64;
		Channel = channel;
	}

	public void ChangeValue(int newValue)
	{
		Value = newValue.Clamp(Minimum, Maximum);
		OnChanged();
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	protected override void DrawWidget(bool isSelected, int tfg, int tbg)
	{
		string buf = "        " + Channel.ToString("d2");

		VGAMem.DrawText(buf, Position, isSelected ? (3, 2) : (0, 2));

		if (IsMuted)
			VGAMem.DrawText("  Muted  ", Position.Advance(11), isSelected ? (3, 0) : (5, 0));
		else if (IsSurround)
			VGAMem.DrawText("Surround ", Position.Advance(11), isSelected ? (3, 0) : (5, 0));
		else
		{
			VGAMem.DrawThumbBar(Position.Advance(11), 9, 0, 64, Value, isSelected);
			VGAMem.DrawText(Value.ToString("d3"), Position.Advance(21), (1, 2));
		}
	}

	public override bool? PreHandleKey(KeyEvent k)
	{
		if (k.Mouse == MouseState.DoubleClick)
		{
			if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				return false;

			IsMuted = !IsMuted;

			OnChanged();

			return true;
		}

		if (k.Mouse == MouseState.Click)
		{
			if (Status.Flags.HasFlag(StatusFlags.DiskWriterActive))
				return false;

			/* swallow it */
			if (!k.OnTarget)
				return false;

			int fMin = Minimum;
			int fMax = Maximum;

			int n = k.MousePositionFine.X - (Position.X + 11) * k.CharacterResolution.X;
			int wx = (Size.Width - 16) * k.CharacterResolution.X;

			if (n < 0)
				n = 0;
			else if (n >= wx)
				n = wx;

			n = fMin + n * (fMax - fMin) / wx;

			if (n < fMin)
				n = fMin;
			else if (n > fMax)
				n = fMax;

			IsMuted = false;
			IsSurround = false;

			if ((k.MousePosition.X - Position.X >= 11)
			 && (k.MousePosition.X - Position.X <= 19))
				ChangeValue(n);

			return true;
		}

		return default;
	}

	public override bool? HandleArrow(KeyEvent k)
	{
		IsMuted = false;
		IsSurround = false;

		/* I'm handling the key modifiers differently than Impulse Tracker, but only
		because I think this is much more useful. :) */
		int n = 1;

		if (k.Modifiers.HasAnyFlag(KeyMod.Alt | KeyMod.GUI))
			n *= 8;
		if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
			n *= 4;
		if (k.Modifiers.HasAnyFlag(KeyMod.Control))
			n *= 2;

		if (k.Sym == KeySym.Left)
			n = Value - n;
		else if (k.Sym == KeySym.Right)
			n = Value + n;
		else
			return default;

		ChangeValue(n);

		return true;
	}

	public override bool HandleKey(KeyEvent k)
	{
		switch (k.Sym)
		{
			case KeySym.Home:
				IsMuted = false;
				IsSurround = false;
				ChangeValue(Minimum);
				return true;
			case KeySym.End:
				IsMuted = false;
				IsSurround = false;
				ChangeValue(Maximum);
				return true;
			case KeySym.Space:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				IsMuted = !IsMuted;

				Page.ChangeFocusTo(Next.Down);

				OnChanged();

				return true;
			case KeySym.l:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					Song.CurrentSong.SetPanScheme(PanSchemes.Left);
				else
				{
					IsMuted = false;
					IsSurround = false;
					ChangeValue(0);
				}

				return true;
			case KeySym.m:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					Song.CurrentSong.SetPanScheme(PanSchemes.Mono);
				else
				{
					IsMuted = false;
					IsSurround = false;
					ChangeValue(32);
				}

				return true;
			case KeySym.r:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					Song.CurrentSong.SetPanScheme(PanSchemes.Right);
				else
				{
					IsMuted = false;
					IsSurround = false;
					ChangeValue(32);
				}

				return true;
			case KeySym.s:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					Song.CurrentSong.SetPanScheme(PanSchemes.Stereo);
				else
				{
					IsMuted = false;
					IsSurround = true;
					OnChanged();
					Status.Flags |= StatusFlags.NeedUpdate;
				}

				return true;
			case KeySym.a:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					Song.CurrentSong.SetPanScheme(PanSchemes.Amiga);
					return true;
				}

				break;
#if false
			case KeySym.x:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					Song.CurrentSong.SetPanScheme(PanSchemes.Cross);
					return true;
				}

				break;
#endif
			case KeySym.Slash:
			case KeySym.KP_Divide:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					Song.CurrentSong.SetPanScheme(PanSchemes.Slash);
					return true;
				}

				break;
			case KeySym.Backslash:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					Song.CurrentSong.SetPanScheme(PanSchemes.Backslash);
					return true;
				}

				break;
		}

		return false;
	}
}
