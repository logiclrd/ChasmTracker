using System;
using System.Linq;

namespace ChasmTracker.Pages;

using ChasmTracker.Dialogs;
using ChasmTracker.Dialogs.Instruments;
using ChasmTracker.Input;
using ChasmTracker.Memory;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public abstract class InstrumentListEnvelopeSubpageBase : InstrumentListPage
{
	protected OtherWidget otherEnvelope;
	protected Widget widgetEnvelopeEnabled;
	protected ToggleWidget toggleEnvelopeCarry;
	protected ToggleWidget toggleEnvelopeLoop;
	protected NumberEntryWidget numberEntryEnvelopeLoopBegin;
	protected NumberEntryWidget numberEntryEnvelopeLoopEnd;
	protected ToggleWidget toggleEnvelopeSustainLoop;
	protected NumberEntryWidget numberEntryEnvelopeSustainLoopBegin;
	protected NumberEntryWidget numberEntryEnvelopeSustainLoopEnd;

	Shared<int> _numEntryCursorPos = new Shared<int>();

	public InstrumentListEnvelopeSubpageBase(PageNumbers pageNumber)
		: base(pageNumber)
	{
		otherEnvelope = new OtherWidget(new Point(32, 18), new Size(45, 8));
		otherEnvelope.OtherHandleKey += EnvelopeHandleKey;
		otherEnvelope.OtherRedraw += EnvelopeDraw;

		widgetEnvelopeEnabled = CreateEnvelopeEnabledWidget();
		toggleEnvelopeCarry = new ToggleWidget(new Point(54, 29));

		toggleEnvelopeLoop = new ToggleWidget(new Point(54, 32));
		numberEntryEnvelopeLoopBegin = new NumberEntryWidget(new Point(54, 33), 3, 0, 1, _numEntryCursorPos);
		numberEntryEnvelopeLoopEnd = new NumberEntryWidget(new Point(54, 34), 3, 0, 1, _numEntryCursorPos);

		toggleEnvelopeSustainLoop = new ToggleWidget(new Point(54, 37));
		numberEntryEnvelopeSustainLoopBegin = new NumberEntryWidget(new Point(54, 38), 3, 0, 1, _numEntryCursorPos);
		numberEntryEnvelopeSustainLoopEnd = new NumberEntryWidget(new Point(54, 39), 3, 0, 1, _numEntryCursorPos);

		widgetEnvelopeEnabled.Changed += UpdateValues;
		toggleEnvelopeCarry.Changed += UpdateValues;
		toggleEnvelopeLoop.Changed += UpdateValues;
		numberEntryEnvelopeLoopBegin.Changed += UpdateValues;
		numberEntryEnvelopeLoopEnd.Changed += UpdateValues;
		toggleEnvelopeSustainLoop.Changed += UpdateValues;
		numberEntryEnvelopeSustainLoopBegin.Changed += UpdateValues;
		numberEntryEnvelopeSustainLoopEnd.Changed += UpdateValues;

		AddWidget(otherEnvelope);
		AddWidget(widgetEnvelopeEnabled);
		AddWidget(toggleEnvelopeCarry);
		AddWidget(toggleEnvelopeLoop);
		AddWidget(numberEntryEnvelopeLoopBegin);
		AddWidget(numberEntryEnvelopeLoopEnd);
		AddWidget(toggleEnvelopeSustainLoop);
		AddWidget(numberEntryEnvelopeSustainLoopBegin);
		AddWidget(numberEntryEnvelopeSustainLoopEnd);
	}

	// Pitch has a fancy tristate
	protected virtual Widget CreateEnvelopeEnabledWidget()
	{
		return new ToggleWidget(new Point(54, 28));
	}

	protected abstract void UpdateValues();

	protected abstract bool EnvelopeHandleKey(KeyEvent k);
	protected abstract void EnvelopeDraw();

	/* --------------------------------------------------------------------------------------------------------- */
	/* envelope helper functions */

	void EnvelopeDrawAxes(bool middle)
	{
		int n, y = middle ? 31 : 62;
		for (n = 0; n < 64; n += 2)
			s_envOverlay[3, n] = 12;
		for (n = 0; n < 256; n += 2)
			s_envOverlay[1 + n, y] = 12;
	}

	void EnvelopeDrawNode(Point pt, bool on)
	{
		byte c = Status.Flags.HasAllFlags(StatusFlags.ClassicMode) ? (byte)12 : (byte)5;

		s_envOverlay[pt.X - 1, pt.Y - 1] = c;
		s_envOverlay[pt.X - 1, pt.Y] = c;
		s_envOverlay[pt.X - 1, pt.Y + 1] = c;

		s_envOverlay[pt.X, pt.Y - 1] = c;
		s_envOverlay[pt.X, pt.Y] = c;
		s_envOverlay[pt.X, pt.Y + 1] = c;

		s_envOverlay[pt.X + 1, pt.Y - 1] = c;
		s_envOverlay[pt.X + 1, pt.Y] = c;
		s_envOverlay[pt.X + 1, pt.Y + 1] = c;

		if (on)
		{
			s_envOverlay[pt.X - 3, pt.Y - 1] = c;
			s_envOverlay[pt.X - 3, pt.Y] = c;
			s_envOverlay[pt.X - 3, pt.Y + 1] = c;

			s_envOverlay[pt.X + 3, pt.Y - 1] = c;
			s_envOverlay[pt.X + 3, pt.Y] = c;
			s_envOverlay[pt.X + 3, pt.Y + 1] = c;
		}
	}

	void EnvelopeDrawLoop(int xs, int xe, bool sustain)
	{
		int y = 0;
		byte c = Status.Flags.HasAllFlags(StatusFlags.ClassicMode) ? (byte)12 : (byte)3;

		if (sustain)
		{
			while (y < 62)
			{
				/* unrolled once */
				s_envOverlay[xs, y] = c;
				s_envOverlay[xe, y] = c; y++;
				s_envOverlay[xs, y] = 0;
				s_envOverlay[xe, y] = 0; y++;
				s_envOverlay[xs, y] = c;
				s_envOverlay[xe, y] = c; y++;
				s_envOverlay[xs, y] = 0;
				s_envOverlay[xe, y] = 0; y++;
			}
		}
		else
		{
			while (y < 62)
			{
				s_envOverlay[xs, y] = 0;
				s_envOverlay[xe, y] = 0; y++;
				s_envOverlay[xs, y] = c;
				s_envOverlay[xe, y] = c; y++;
				s_envOverlay[xs, y] = c;
				s_envOverlay[xe, y] = c; y++;
				s_envOverlay[xs, y] = 0;
				s_envOverlay[xe, y] = 0; y++;
			}
		}
	}

	// TODO: envPos is one of
	//			envpos[0] = channel->vol_env_position;
	//			envpos[1] = channel->pan_env_position;
	//			envpos[2] = channel->pitch_env_position;
	protected void EnvelopeDraw(Envelope? env, bool middle, int currentNode,
				bool envOn, bool loopOn, bool sustainOn, int envPos)
	{
		// TODO: show envelopes that aren't there yet to kickstart editing on new instruments
		if (env == null)
			return;

		Point lastPt = new Point(0, 0);
		int maxTicks = 50;

		while (env.Nodes.Last().Tick >= maxTicks)
			maxTicks *= 2;

		s_envOverlay.Clear(0);

		/* draw the axis lines */
		EnvelopeDrawAxes(middle);

		for (int n = 0; n < env.Nodes.Count; n++)
		{
			ref var node = ref env.Nodes[n];

			Point thisPt;

			thisPt.X = 4 + node.Tick * 256 / maxTicks;

			/* 65 values are being crammed into 62 pixels => have to lose three pixels somewhere.
			* This is where IT compromises -- I don't quite get how the lines are drawn, though,
			* because it changes for each value... (apart from drawing 63 and 64 the same way) */
			thisPt.Y = node.Value;
			if (thisPt.Y > 63) thisPt.Y--;
			if (thisPt.Y > 42) thisPt.Y--;
			if (thisPt.Y > 21) thisPt.Y--;
			thisPt.Y = 62 - thisPt.Y;

			EnvelopeDrawNode(thisPt, n == currentNode);

			if (lastPt.X > 0)
				s_envOverlay.DrawLine(lastPt, thisPt, 12);

			lastPt = thisPt;
		}

		if (sustainOn)
			EnvelopeDrawLoop(4 + env.Nodes[env.SustainStart].Tick * 256 / maxTicks,
							4 + env.Nodes[env.SustainEnd].Tick * 256 / maxTicks, sustain: true);
		if (loopOn)
			EnvelopeDrawLoop(4 + env.Nodes[env.LoopStart].Tick * 256 / maxTicks,
							4 + env.Nodes[env.LoopEnd].Tick * 256 / maxTicks, sustain: false);

		if (envOn)
		{
			maxTicks = env.Nodes.Last().Tick;

			int[] channelList;

			if (maxTicks > 0)
			{
				channelList = Song.CurrentSong.GetMixState(out var m);

				while (m-- > 0)
				{
					ref var channel = ref Song.CurrentSong.Voices[channelList[m]];

					if (channel.Instrument != Song.CurrentSong.GetInstrument(CurrentInstrument))
						continue;

					int x = 4 + (envPos * (lastPt.X - 4) / maxTicks);
					if (x > lastPt.X)
						x = lastPt.X;

					byte c = Status.Flags.HasAllFlags(StatusFlags.ClassicMode)
						? (byte)12
						: (channel.Flags.HasAnyFlag(ChannelFlags.KeyOff | ChannelFlags.NoteFade) ? (byte)8 : (byte)6);

					for (int y = 0; y < 62; y++)
						s_envOverlay[x, y] = c;
				}
			}
		}

		VGAMem.DrawFillCharacters(new Point(65, 18), new Point(76, 25), (VGAMem.DefaultForeground, 0));
		VGAMem.ApplyOverlay(s_envOverlay);

		VGAMem.DrawText($"Node {currentNode}/{env.Nodes.Count}", new Point(66, 19), (2, 0));
		VGAMem.DrawText($"Tick {env.Nodes[currentNode].Tick}", new Point(66, 21), (2, 0));
		VGAMem.DrawText($"Value {env.Nodes[currentNode].Value - (middle ? 32 : 0)}", new Point(66, 23), (2, 0));
	}

	/* return: the new current node */
	protected static int EnvelopeNodeAdd(Envelope env, int currentNode, int overrideTick, int overrideValue)
	{
		Status.Flags |= StatusFlags.SongNeedsSave;

		if (env.Nodes.Count > 24 || currentNode == env.Nodes.Count - 1)
			return currentNode;

		int newTick = (env.Nodes[currentNode].Tick + env.Nodes[currentNode + 1].Tick) / 2;
		int newValue = (env.Nodes[currentNode].Value + env.Nodes[currentNode + 1].Value) / 2;

		if (overrideTick > -1 && overrideValue > -1)
		{
			newTick = overrideTick;
			newValue = overrideValue;
		}
		else if (newTick == env.Nodes[currentNode].Tick || newTick == env.Nodes[currentNode + 1].Tick)
		{
			Console.WriteLine("Not enough room!");
			return currentNode;
		}

		env.Nodes.Insert(currentNode + 1, new EnvelopeNode(newTick, newValue));

		if (env.LoopEnd > currentNode) env.LoopEnd++;
		if (env.LoopStart > currentNode) env.LoopStart++;
		if (env.SustainEnd > currentNode) env.SustainEnd++;
		if (env.SustainStart > currentNode) env.SustainStart++;

		return currentNode;
	}

	/* return: the new current node */
	protected static int EnvelopeNodeRemove(Envelope env, int currentNode)
	{
		Status.Flags |= StatusFlags.SongNeedsSave;

		if (currentNode == 0 || env.Nodes.Count < 3)
			return currentNode;

		env.Nodes.RemoveAt(currentNode);

		if (env.LoopStart >= env.Nodes.Count)
			env.LoopStart = env.Nodes.Count - 1;
		else if (env.LoopStart > currentNode)
			env.LoopStart--;
		if (env.LoopEnd >= env.Nodes.Count)
			env.LoopEnd = env.Nodes.Count - 1;
		else if (env.LoopEnd > currentNode)
			env.LoopEnd--;
		if (env.SustainStart >= env.Nodes.Count)
			env.SustainStart = env.Nodes.Count - 1;
		else if (env.SustainStart > currentNode)
			env.SustainStart--;
		if (env.SustainEnd >= env.Nodes.Count)
			env.SustainEnd = env.Nodes.Count - 1;
		else if (env.SustainEnd > currentNode)
			env.SustainEnd--;
		if (currentNode >= env.Nodes.Count)
			currentNode = env.Nodes.Count - 1;

		return currentNode;
	}

	protected static void DoPreLoopCut(Envelope env)
	{
		int bt = env.Nodes[env.LoopStart].Tick;

		for (int i = env.LoopStart; i < 32; i++)
			env.Nodes[i - env.LoopStart] = (env.Nodes[i].Tick - bt, env.Nodes[i].Value);

		env.Nodes.RemoveRange(env.LoopStart, env.Nodes.Count - env.LoopStart);

		if (env.SustainStart > env.LoopStart)
			env.SustainStart -= env.LoopStart;
		else
			env.SustainStart = 0;

		if (env.SustainEnd > env.LoopStart)
			env.SustainEnd -= env.LoopStart;
		else
			env.SustainEnd = 0;

		if (env.LoopEnd > env.LoopStart)
			env.LoopEnd -= env.LoopStart;
		else
			env.LoopEnd = 0;

		env.LoopStart = 0;

		if (env.LoopStart > env.LoopEnd)
			env.LoopEnd = env.LoopStart;
		if (env.SustainStart > env.SustainEnd)
			env.SustainEnd = env.SustainStart;

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	protected static void DoPostLoopCut(Envelope env)
	{
		env.Nodes.RemoveRange(env.LoopEnd + 1, env.Nodes.Count - env.LoopEnd - 1);
	}

	protected static void EnvelopeResize(Envelope env, int ticks)
	{
		int old = env.Nodes.Last().Tick;

		if (ticks > 9999)
			ticks = 9999;

		for (int n = 1; n < env.Nodes.Count; n++)
		{
			int t = env.Nodes[n].Tick * ticks / old;

			t = Math.Max(t, env.Nodes[n - 1].Tick + 1);

			env.Nodes[n].Tick = t;
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	protected static void ShowEnvelopeResizeDialog(Envelope env)
	{
		var dialog = Dialog.Show(new EnvelopeResizeDialog(env));

		dialog.ActionYes += () => EnvelopeResize(env, dialog.NewTickLength);
	}

	void EnvelopeADSR(SongInstrument ins, int a, int d, int s, int r)
	{
		// FIXME | move env flags into the envelope itself, where they should be in the first place.
		// FIXME | then this nonsense can go away.
		var env = ins.VolumeEnvelope;

		if (env == null)
			env = ins.VolumeEnvelope = new Envelope();

		int v1 = Math.Max(a, a * a / 16);
		int v2 = Math.Max(v1 + d * d / 16, v1 + d);
		int v3 = Math.Max(v2 + r * r / 4, v2 + r);

		if (a != 0)
			env.Nodes.Add((0, 0));

		if (d != 0)
			env.Nodes.Add((v1, 64));

		env.SustainStart = env.Nodes.Count;

		env.Nodes.Add((v2, s / 2));
		env.Nodes.Add((v3, 0));

		for (int n = 0; n < env.Nodes.Count - 1; n++)
			if (env.Nodes[n].Tick >= env.Nodes[n + 1].Tick)
				env.Nodes[n + 1].Tick = env.Nodes[n].Tick + 1;

		ins.Flags |= InstrumentFlags.VolumeEnvelopeSustain | InstrumentFlags.VolumeEnvelope; // arghhhhh
	}

	protected void ShowEnvelopeADSRDialog(Envelope enve)
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		var dialog = Dialog.Show(new EnvelopeADSRDialog());

		dialog.ActionYes += () => EnvelopeADSR(ins, dialog.Attack, dialog.Decay, dialog.Sustain, dialog.Release);
	}

	/* the return value here is actually a bitmask:
	r & 1 => the key was handled
	r & 2 => the envelope ()hanged (i.e., it should be enabled) */
	protected int EnvelopeHandleKeyViewMode(KeyEvent k, Envelope? env, ref int currentNode, InstrumentFlags sec)
	{
		if (env == null)
			return 0;

		int newNode = currentNode;

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.State == KeyState.Release)
					return 0;
				ChangeFocusTo(1);
				return 1;
			case KeySym.Down:
				if (k.State == KeyState.Release)
					return 0;
				ChangeFocusTo(6);
				return 1;
			case KeySym.Left:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				newNode--;
				break;
			case KeySym.Right:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				newNode++;
				break;
			case KeySym.Insert:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				currentNode = EnvelopeNodeAdd(env, currentNode, -1, -1);
				Status.Flags |= StatusFlags.NeedUpdate;
				return 1 | 2;
			case KeySym.Delete:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				currentNode = EnvelopeNodeRemove(env, currentNode);
				Status.Flags |= StatusFlags.NeedUpdate;
				return 1 | 2;
			case KeySym.Space:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				if (k.IsRepeat)
					return 1;

				if (k.State == KeyState.Press)
				{
					Song.CurrentSong.KeyDown(KeyJazz.NoInstrument, CurrentInstrument, s_lastNote, 64, KeyJazz.CurrentChannel);
					return 1;
				}
				else if (k.State == KeyState.Release)
				{
					Song.CurrentSong.KeyUp(KeyJazz.NoInstrument, CurrentInstrument, s_lastNote);
					return 1;
				}

				return 0;
			case KeySym.Return:
				if (k.State == KeyState.Press)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;

				s_envelopeEditMode = true;
				Status.Flags |= StatusFlags.NeedUpdate;
				return 1 | 2;
			case KeySym.l:
				if (k.State == KeyState.Press)
					return 0;
				if (!k.Modifiers.HasAnyFlag(KeyMod.Alt)) return 0;
				if (env.LoopEnd < env.Nodes.Count - 1)
				{
					var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "Cut envelope?");

					dialog.SelectedWidgetIndex.Value = 1;
					dialog.ActionYes += () => DoPostLoopCut(env);

					return 1;
				}

				return 0;
			case KeySym.b:
				if (k.State == KeyState.Press)
					return 0;
				if (!(k.Modifiers.HasAnyFlag(KeyMod.Alt))) return 0;
				if (env.LoopStart > 0)
				{
					var dialog = MessageBox.Show(MessageBoxTypes.OKCancel, "Cut envelope?");

					dialog.SelectedWidgetIndex.Value = 1;
					dialog.ActionYes += () => DoPreLoopCut(env);

					return 1;
				}

				return 0;

			// F/G for key symmetry with pattern double/halve block
			// E for symmetry with sample resize
			case KeySym.f:
				if (k.State == KeyState.Press)
					return 0;
				if (!(k.Modifiers.HasAnyFlag(KeyMod.Alt))) return 0;
				EnvelopeResize(env, env.Nodes.Last().Tick * 2);
				return 1;
			case KeySym.g:
				if (k.State == KeyState.Press)
					return 0;
				if (!(k.Modifiers.HasAnyFlag(KeyMod.Alt))) return 0;
				EnvelopeResize(env, env.Nodes.Last().Tick / 2);
				return 1;
			case KeySym.e:
				if (k.State == KeyState.Press)
					return 0;
				if (!(k.Modifiers.HasAnyFlag(KeyMod.Alt))) return 0;
				ShowEnvelopeResizeDialog(env);
				return 1;

			case KeySym.z:
				if (k.State == KeyState.Press)
					return 0;
				if (!k.Modifiers.HasAnyFlag(KeyMod.Alt)) return 0;
				ShowEnvelopeADSRDialog(env);
				return 1;

			default:
				if (k.State == KeyState.Press)
					return 0;

				int n = k.NumericValue(false);

				if (n > -1)
				{
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAlt))
					{
						SaveEnvelope(n, env, sec);
						Status.FlashText("Envelope copied into slot " + n);
					}
					else if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					{
						RestoreEnvelope(n, env, sec);
						if (!Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
							Status.FlashText("Pasted envelope from slot " + n);
					}
					return 1;
				}
				return 0;
		}

		newNode = newNode.Clamp(0, env.Nodes.Count - 1);

		if (currentNode != newNode)
		{
			currentNode = newNode;
			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return 1;
	}

	/* mouse handling routines for envelope */
	protected bool EnvelopeHandleMouse(KeyEvent k, Envelope? env, ref int currentNode)
	{
		if (env == null)
			return false;

		int maxTicks = 50;

		if (k.Mouse != MouseState.Click) return false;

		if (k.State == KeyState.Release)
		{
			/* mouse release */
			if (s_envelopeMouseEdit)
			{
				Video.Mouse.Shape = MouseCursorShapes.Arrow;

				if (currentNode != 0)
				{
					for (int i = 0; i < env.Nodes.Count - 1; i++)
					{
						if (currentNode == i) continue;

						if (env.Nodes[currentNode] == env.Nodes[i])
						{
							Status.FlashText("Removed node " + currentNode);

							Status.Flags |= StatusFlags.SongNeedsSave;

							currentNode = EnvelopeNodeRemove(env, currentNode);
							break;
						}
					}
				}

				Status.Flags |= StatusFlags.NeedUpdate;
			}

			MemoryUsage.NotifySongChanged();
			s_envelopeMouseEdit = false;
			return true;
		}

		while (env.Nodes.Last().Tick >= maxTicks)
			maxTicks *= 2;

		if (s_envelopeMouseEdit)
		{
			Video.Mouse.Shape = MouseCursorShapes.Crosshair;

			int x, y;

			if (k.MousePositionFine.X < 259)
				x = 0;
			else
				x = (k.MousePositionFine.X - 259) * maxTicks / 256;

			y = 64 - (k.MousePositionFine.Y - 144);

			if (y > 63) y++;
			if (y > 42) y++;
			if (y > 21) y++;
			if (y > 64) y = 64;
			if (y < 0) y = 0;

			if ((currentNode != 0) && env.Nodes[currentNode - 1].Tick >= x)
				x = env.Nodes[currentNode - 1].Tick + 1;

			if (currentNode < (env.Nodes.Count - 1))
			{
				if (env.Nodes[currentNode + 1].Tick <= x)
					x = env.Nodes[currentNode + 1].Tick - 1;
			}

			if (env.Nodes[currentNode].Tick == x && env.Nodes[currentNode].Tick == y)
				return true;

			if (x < 0) x = 0;
			if (x > s_envelopeTickLimit) x = s_envelopeTickLimit;
			if (x > 9999) x = 9999;
			if (currentNode != 0) env.Nodes[currentNode].Tick = x;
			env.Nodes[currentNode].Value = (byte)y;
			Status.Flags |= StatusFlags.SongNeedsSave;
			Status.Flags |= StatusFlags.NeedUpdate;
		}
		else
		{
			int bestDist = 0;
			int bestDistNode = -1;

			if (k.MousePosition.X < 32 || k.MousePosition.Y < 18 || k.MousePosition.X > 32 + 45 || k.MousePosition.Y > 18 + 8)
				return false;

			for (int n = 0; n < env.Nodes.Count; n++)
			{
				int x = 259 + env.Nodes[n].Tick * 256 / maxTicks;
				int y = env.Nodes[n].Value;

				if (y > 63) y--;
				if (y > 42) y--;
				if (y > 21) y--;

				y = 206 - y;

				int dx = Math.Abs(x - k.MousePositionFine.X);
				int dy = Math.Abs(y - k.MousePositionFine.Y);

				int dist = (int)Math.Round(Math.Sqrt((dx * dx) + (dy * dy)));

				if (bestDistNode == -1 || dist < bestDist)
				{
					if (dist <= 5)
					{
						bestDist = dist;
						bestDistNode = n;
					}
				}
			}

			if (bestDistNode == -1)
			{
				int x = (k.MousePositionFine.X - 259) * maxTicks / 256;
				int y = 64 - (k.MousePositionFine.Y - 144);

				if (y > 63) y++;
				if (y > 42) y++;
				if (y > 21) y++;
				if (y > 64) y = 64;
				if (y < 0) y = 0;

				if (x > 0 && x < maxTicks)
				{
					currentNode = 0;

					for (int i = 1; i < env.Nodes.Count; i++)
					{
						/* something too close */
						if (env.Nodes[i].Tick <= x) currentNode = i;
						if (Math.Abs(env.Nodes[i].Tick - x) < 2) return false;
					}

					bestDistNode = (EnvelopeNodeAdd(env, currentNode, x, y)) + 1;

					Status.FlashText("Created node " + bestDistNode);
				}

				if (bestDistNode == -1) return false;
			}

			s_envelopeTickLimit = env.Nodes.Last().Tick * 2;
			s_envelopeMouseEdit = true;

			currentNode = bestDistNode;

			Status.Flags |= StatusFlags.SongNeedsSave;
			Status.Flags |= StatusFlags.NeedUpdate;

			return true;
		}

		return false;
	}


	/* - this function is only ever called when the envelope is in edit mode
		- s_envelopeEditMode is only ever assigned a true value once, in _env_handle_key_viewmode.
		- when _env_handle_key_viewmode enables s_envelopeEditMode, it indicates in its return value
			that the envelope should be enabled.
		- therefore, the envelope will always be enabled when this function is called, so there is
			no reason to indicate a change in the envelope here. */
	protected int EnvelopeHandleKeyEditMode(KeyEvent k, Envelope? env, ref int currentNode)
	{
		if (env == null)
			return 0;

		int newNode = currentNode, newTick = env.Nodes[currentNode].Tick;
		byte newValue = env.Nodes[currentNode].Value;

		/* TODO: when does adding/removing a node alter loop points? */

		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					newValue += 16;
				else
					newValue++;
				break;
			case KeySym.Down:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					newValue -= 16;
				else
					newValue--;
				break;
			case KeySym.PageUp:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				newValue += 16;
				break;
			case KeySym.PageDown:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				newValue -= 16;
				break;
			case KeySym.Left:
				if (k.State == KeyState.Release)
					return 1;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
					newNode--;
				else if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					newTick -= 16;
				else
					newTick--;
				break;
			case KeySym.Right:
				if (k.State == KeyState.Release)
					return 1;
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
					newNode++;
				else if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
					newTick += 16;
				else
					newTick++;
				break;
			case KeySym.Tab:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					newTick -= 16;
				else
					newTick += 16;
				break;
			case KeySym.Home:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				newTick = 0;
				break;
			case KeySym.End:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				newTick = 10000;
				break;
			case KeySym.Insert:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				currentNode = EnvelopeNodeAdd(env, currentNode, -1, -1);
				Status.Flags |= StatusFlags.NeedUpdate;
				return 1;
			case KeySym.Delete:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				currentNode = EnvelopeNodeRemove(env, currentNode);
				Status.Flags |= StatusFlags.NeedUpdate;
				return 1;
			case KeySym.Space:
				if (k.State == KeyState.Release)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				Song.CurrentSong.KeyUp(KeyJazz.NoInstrument, CurrentInstrument, s_lastNote);
				Song.CurrentSong.KeyDown(KeyJazz.NoInstrument, CurrentInstrument, s_lastNote, 64, KeyJazz.CurrentChannel);
				return 1;
			case KeySym.Return:
				if (k.State == KeyState.Press)
					return 0;
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return 0;
				s_envelopeEditMode = false;
				MemoryUsage.NotifySongChanged();
				Status.Flags |= StatusFlags.NeedUpdate;
				break;
			default:
				return 0;
		}

		newNode = newNode.Clamp(0, env.Nodes.Count - 1);

		if (newNode != currentNode)
		{
			Status.Flags |= StatusFlags.NeedUpdate;
			currentNode = newNode;
			return 1;
		}

		newTick = (newNode == 0) ? 0 : newTick.Clamp(
						env.Nodes[newNode - 1].Tick + 1,
								(newNode == env.Nodes.Count - 1)
						? 10000 : env.Nodes[newNode + 1].Tick - 1);
		if (newTick != env.Nodes[newNode].Tick)
		{
			env.Nodes[currentNode].Tick = newTick;
			Status.Flags |= StatusFlags.SongNeedsSave;
			Status.Flags |= StatusFlags.NeedUpdate;
			return 1;
		}

		newValue = newValue.Clamp(0, 64);

		if (newValue != env.Nodes[newNode].Value)
		{
			env.Nodes[currentNode].Value = newValue;

			Status.Flags |= StatusFlags.SongNeedsSave;
			Status.Flags |= StatusFlags.NeedUpdate;

			return 1;
		}

		return 1;
	}

	protected static void DrawEnvelopeLabel(string envName, bool isSelected)
	{
		int pos = 33;

		pos += VGAMem.DrawText(envName, new Point(pos, 16), isSelected ? (3, 2) : (0, 2));
		pos += VGAMem.DrawText(" Envelope", new Point(pos, 16), isSelected ? (3, 2) : (0, 2));
		if (s_envelopeEditMode || s_envelopeMouseEdit)
			VGAMem.DrawText(" (Edit)", new Point(pos, 16), isSelected ? (3, 2) : (0, 2));
	}

	protected abstract string Label { get; }

	public override void DrawConst()
	{
		base.DrawConst();

		VGAMem.DrawFillCharacters(new Point(57, 28), new Point(62, 29), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawFillCharacters(new Point(57, 32), new Point(62, 34), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawFillCharacters(new Point(57, 37), new Point(62, 39), (VGAMem.DefaultForeground, 0));

		VGAMem.DrawBox(new Point(31, 17), new Point(77, 26), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(53, 27), new Point(63, 30), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(53, 31), new Point(63, 35), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(53, 36), new Point(63, 40), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawText(Label + " Envelope", new Point(44 - Label.Length, 28), (0, 2));
		VGAMem.DrawText("Carry", new Point(48, 29), (0, 2));
		VGAMem.DrawText("Envelope Loop", new Point(40, 32), (0, 2));
		VGAMem.DrawText("Loop Begin", new Point(43, 33), (0, 2));
		VGAMem.DrawText("Loop End", new Point(45, 34), (0, 2));
		VGAMem.DrawText("Sustain Loop", new Point(41, 37), (0, 2));
		VGAMem.DrawText("SusLoop Begin", new Point(40, 38), (0, 2));
		VGAMem.DrawText("SusLoop End", new Point(42, 39), (0, 2));
	}
}
