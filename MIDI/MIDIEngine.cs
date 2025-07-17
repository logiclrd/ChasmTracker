using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ChasmTracker.MIDI;

using ChasmTracker.Configurations;
using ChasmTracker.Events;
using ChasmTracker.Input;
using ChasmTracker.MIDI.Drivers;
using ChasmTracker.Pages;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class MIDIEngine : IMIDISink
{
	static bool s_connected = false;

	/* midi_mutex is locked by the main thread,
	midi_port_mutex is for the port thread(s),
	and midi_record_mutex is by the event/sound thread
	*/
	static object s_mutex = new object();
	static object s_recordMutex = new object();
	static object s_portMutex = new object();

	static IEnumerable<Type> EnumeratePortProviders()
		=> typeof(MIDIEngine).Assembly.GetTypes().Where(t => typeof(MIDIPort).IsAssignableFrom(t));

	static void Connect()
	{
		if (!s_connected)
		{
			foreach (var type in EnumeratePortProviders())
			{
				var setupMethod = type.GetMethod("Setup", BindingFlags.Public | BindingFlags.Static);

				setupMethod?.Invoke(null, null);
			}

			s_connected = true;
		}
	}

	/* configurable midi stuff */
	public static MIDIFlags Flags =
		MIDIFlags.TickQuantize |
		MIDIFlags.RecordNoteOff |
		MIDIFlags.RecordVelocity |
		MIDIFlags.RecordAftertouch |
		MIDIFlags.PitchBend;

	public static int PitchWheelDepth = 12;
	public static int Amplification = 100;
	public static int C5Note = 60;

	public static int[] BendHit = new int[Constants.MaxChannels];
	public static int[] LastBendHit = new int[Constants.MaxChannels];

	static int s_midiBytesPerMillisecond;

	public static void PollPorts()
	{
		if (s_portMutex == null)
			return;

		lock (s_portMutex)
			foreach (var port in s_ports.OfType<MIDIPort>())
				port.Poll();
	}

	public static void Start()
	{
		Connect();
	}

	public static void Reset()
	{
		if (!s_connected)
			return;

		Stop();
		Start();
	}

	public static void Stop()
	{
		if (!s_connected)
			return;
		if (s_mutex == null)
			return;

		lock (s_mutex)
		{
			s_connected = false;

			foreach (var type in EnumeratePortProviders())
			{
				foreach (var port in s_ports.Where(p => type.IsAssignableFrom(p!.GetType())))
					UnregisterPort(port!.Number);

				var shutdown = type.GetMethod("Shutdown", BindingFlags.Static | BindingFlags.Public);

				shutdown?.Invoke(null, null);
			}
		}
	}

	/* ------------------------------------------------------------- */
	/* PORT system */

	static List<MIDIPort?> s_ports = new List<MIDIPort?>();

	public static MIDIPort GetPort(int index)
	{
		lock (s_portMutex)
			return s_ports.OfType<MIDIPort>().Skip(index).First();
	}

	public static int GetPortCount()
		=> s_ports.OfType<MIDIPort>().Count();

	public static int GetIPPortCount()
		=> s_ports.OfType<IPMIDIPort>().Count();

	public static void SetIPPortCount(int newCount)
	{
		lock (s_portMutex)
		{
			int portNumber = 0;

			for (int i = 0; i < s_ports.Count; i++)
			{
				if (s_ports[i] is IPMIDIPort)
				{
					if (portNumber < newCount)
						portNumber++;
					else
						UnregisterPort(i);
				}
			}

			while (portNumber < newCount)
			{
				RegisterPort(new IPMIDIPort(portNumber));
				portNumber++;
			}
		}
	}

	/* midi engines list ports this way */
	public static int RegisterPort(MIDIPort p)
	{
		if (s_portMutex == null)
			return -1;

		lock (s_portMutex)
		{
			p.Number = -1;

			for (int i = 0; i < s_ports.Count; i++)
			{
				if (s_ports[i] == null)
				{
					p.Number = i;
					s_ports[i] = p;
				}
			}

			if (p.Number < 0)
			{
				p.Number = s_ports.Count;

				s_ports.Add(p);
			}
		}

		Configuration.LoadMIDIPortConfiguration(p);

		p.Received += port_Received;

		return p.Number;
	}

	public static void UnregisterPort(int num)
	{
		if (s_portMutex == null)
			return;

		lock (s_portMutex)
		{
			if (s_ports[num] is MIDIPort q)
			{
				q.Received -= port_Received;

				q.Disable();

				s_ports[num] = null;
			}
		}
	}

	// both of these functions return true on success and false on failure
	public bool EnablePort(MIDIPort p)
	{
		bool r = false;

		lock (s_recordMutex)
			lock (s_portMutex)
			{
				r = p.Enable();

				if (!r)
					p.IO = MIDIIO.None; // why
			}

		return r;
	}

	public bool DisablePort(MIDIPort p)
	{
		bool r = false;

		lock (s_recordMutex)
			lock (s_portMutex)
				r = p.Disable();

		return r;
	}

	public static void QueueAlloc(int bufferLength, int sampleSize, int samplesPerSecond)
	{
		// bytes per millisecond, rounded up
		s_midiBytesPerMillisecond = sampleSize * samplesPerSecond;
		s_midiBytesPerMillisecond += 1000 - (s_midiBytesPerMillisecond % 1000);
		s_midiBytesPerMillisecond /= 1000;

		// nothing else to do now
	}

	/*----------------------------------------------------------------------------------*/

	static void port_Received(MIDIPort src, byte[] data)
	{
		if (data.Length == 0)
			return;

		int len = data.Length;

		if (len < 4)
		{
			byte[] d4 = new byte[4];

			Array.Copy(data, d4, len);

			data = d4;
		}

		/* just for fun... */
		lock (s_recordMutex)
		{
			Status.LastMIDIRealLength = len;

			if (len > Status.LastMIDIEvent.Length)
				Status.LastMIDILength = Status.LastMIDIEvent.Length;
			else
				Status.LastMIDILength = len;

			Array.Copy(data, Status.LastMIDIEvent, Status.LastMIDILength);

			Status.Flags |= StatusFlags.MIDIEventChanged;
			Status.LastMIDIPort = src;
			Status.LastMIDITick = DateTime.UtcNow;
		}

		/* pass through midi events when on midi page */
		if (Status.CurrentPageNumber == PageNumbers.MIDI)
		{
			SendNow(data, len);
			Status.Flags |= StatusFlags.NeedUpdate;
		}

		int cmd = (data[0] & 0xF0) >> 4;

		if (cmd == 0x8 || (cmd == 0x9 && data[2] == 0))
			EventNote(MIDINote.NoteOff, data[0] & 15, data[1], 0);
		else if (cmd == 0x9)
			EventNote(MIDINote.NoteOn, data[0] & 15, data[1], data[2]);
		else if (cmd == 0xA)
			EventNote(MIDINote.KeyPress, data[0] & 15, data[1], data[2]);
		else if (cmd == 0xB)
			EventController(data[0] & 15, data[1], data[2]);
		else if (cmd == 0xC)
			EventProgram(data[0] & 15, data[1]);
		else if (cmd == 0xD)
			EventAfterTouch(data[0] & 15, data[1]);
		else if (cmd == 0xE)
			EventPitchBend(data[0] & 15, data[1]);
		else if (cmd == 0xF)
		{
			switch (data[0] & 15)
			{
				case 0: /* sysex */
					if (len <= 2) return;
					EventSysEx(new ArraySegment<byte>(data, offset: 1, len - 2));
					break;
				case 6: /* tick */
					EventTick();
					break;
				default:
					/* something else */
					EventSystem(data[0] & 15, (data[1])
							| (data[2] << 8)
							| (data[3] << 16));
					break;
			}
		}
	}

	public static void EventNote(MIDINote mnStatus, int channel, int note, int velocity)
	{
		var @event = new MIDINoteEvent();

		@event.Status = mnStatus;
		@event.Channel = channel;
		@event.Note = note;
		@event.Velocity = velocity;

		EventHub.PushEvent(@event);
	}

	public static void EventController(int channel, int param, int value)
	{
		var @event = new MIDIControllerEvent();

		@event.Value = value;
		@event.Param = param;
		@event.Channel = channel;

		EventHub.PushEvent(@event);
	}

	public static void EventProgram(int channel, int value)
	{
		var @event = new MIDIProgramEvent();

		@event.Value = value;
		@event.Channel = channel;

		EventHub.PushEvent(@event);
	}

	public static void EventAfterTouch(int channel, int value)
	{
		var @event = new MIDIAfterTouchEvent();

		@event.Value = value;
		@event.Channel = channel;

		EventHub.PushEvent(@event);
	}

	public static void EventPitchBend(int channel, int value)
	{
		var @event = new MIDIPitchBendEvent();

		@event.Value = value;
		@event.Channel = channel;

		EventHub.PushEvent(@event);
	}

	public static void EventSystem(int argv, int param)
	{
		var @event = new MIDISystemEvent();

		@event.ArgV = argv;
		@event.Param = param;

		EventHub.PushEvent(@event);
	}

	public static void EventTick()
	{
		EventHub.PushEvent(new MIDITickEvent());
	}

	public static void EventSysEx(ArraySegment<byte> data)
	{
		var @event = new MIDISysExEvent();

		@event.Packet = data.ToArray();

		EventHub.PushEvent(@event);
	}


	static bool NeedFlush()
	{
		if (s_recordMutex == null)
			return false;

		foreach (var ptr in s_ports.OfType<MIDIPort>())
			if (ptr.IO.HasFlag(MIDIIO.Output) && !ptr.CanDrain && ptr.CanSendNow)
				return true;

		return false;
	}

	public static void SendFlush()
	{
		if (s_recordMutex == null)
			return;

		lock (s_recordMutex)
		{
			foreach (var ptr in s_ports)
				if ((ptr != null) && ptr.IO.HasFlag(MIDIIO.Output) && ptr.CanDrain)
					ptr.Drain();
		}
	}

	// Get the length of a MIDI event in bytes
	static int EventLength(byte firstByte)
	{
		switch (firstByte & 0xF0)
		{
			case 0xC0:
			case 0xD0:
				return 2;
			case 0xF0:
				switch(firstByte)
				{
					case 0xF1:
					case 0xF3:
						return 2;
					case 0xF2:
						return 3;
					default:
						return 1;
				}

			default:
				return 3;
		}
	}

	public static bool HandleEvent(Event ev)
	{
		KeyEvent kk = new KeyEvent();

		kk.IsSynthetic = false;

		if (Flags.HasFlag(MIDIFlags.DisableRecord))
			return false;

		switch (ev)
		{
			case MIDINoteEvent midiNote:
				if (midiNote.Status == MIDINote.NoteOn)
					kk.State = KeyState.Press;
				else
				{
					if (!Flags.HasAnyFlag(MIDIFlags.RecordNoteOff))
					{
						/* don't record noteoff? okay... */
						break;
					}

					kk.State = KeyState.Release;
				}

				kk.MIDIChannel = midiNote.Channel + 1;
				kk.MIDINote = (midiNote.Note + 1 + C5Note) - 60;
				if (Flags.HasFlag(MIDIFlags.RecordVelocity))
					kk.MIDIVolume = midiNote.Velocity;
				else
					kk.MIDIVolume = 128;
				kk.MIDIVolume = kk.MIDIVolume * Amplification / 100;
				Page.MainHandleKey(kk);
				return true;
			case MIDIPitchBendEvent pitchBendEvent:
				/* wheel */
				kk.MIDIChannel = pitchBendEvent.Channel + 1;
				kk.MIDIVolume = -1;
				kk.MIDINote = -1;
				kk.MIDIBend = pitchBendEvent.Value;
				Page.MainHandleKey(kk);
				return true;
			case MIDIControllerEvent:
				/* controller events */
				return true;
			case MIDISystemEvent systemEvent:
				switch (systemEvent.ArgV)
				{
					case 0x8: /* MIDI tick */
						break;
					case 0xA: /* MIDI start */
					case 0xB: /* MIDI continue */
						AudioPlayback.Start();
						break;
					case 0xC: /* MIDI stop */
					case 0xF: /* MIDI reset */
						/* this is helpful when miditracking */
						AudioPlayback.Stop();
						break;
				}

				return true;
			case MIDISysExEvent: /* but missing the F0 and the stop byte (F7) */
				/* tfw midi ports just hand us friggin packets yo */
				return true;
			default:
				return false;
		}

		return true;
	}

	static bool SendUnlocked(byte[] data, int len, TimeSpan delay, MIDIFrom from)
	{
		bool needTimer = false;

#if false
		Console.Write("MIDI: ");
		for (int i = 0; i < len; i++)
			Console.Write("{0:X2} ", data[i]);
		Console.WriteLine();
		Console.Out.Flush();
#endif

		switch (from)
		{
			case MIDIFrom.Immediate:
				/* everyone plays */
				foreach (var ptr in s_ports!)
				{
					if ((ptr != null) && ptr.IO.HasFlag(MIDIIO.Output))
					{
						if (ptr.CanSendNow)
							ptr.SendNow(data, len, TimeSpan.Zero);
						else if (ptr.CanSendLater)
							ptr.SendLater(data, len, TimeSpan.Zero);
					}
				}
				break;
			case MIDIFrom.Now:
				/* only "now" plays */
				foreach (var ptr in s_ports!)
				{
					if ((ptr != null) && ptr.IO.HasFlag(MIDIIO.Output))
					{
						if (ptr.CanSendNow)
							ptr.SendNow(data, len, TimeSpan.Zero);
					}
				}
				break;
			case MIDIFrom.Later:
				/* only "later" plays */
				foreach (var ptr in s_ports!)
				{
					if ((ptr != null) && ptr.IO.HasFlag(MIDIIO.Output))
					{
						if (ptr.CanSendLater)
							ptr.SendLater(data, len, delay);
						else if (ptr.CanSendNow)
							needTimer = true;
					}
				}
				break;
			default:
				break;
		}

		return needTimer;
	}

	public static void SendNow(byte[] seq, int len)
	{
		if (s_recordMutex == null) return;

		lock (s_recordMutex)
			SendUnlocked(seq, len, TimeSpan.Zero, MIDIFrom.Immediate);
	}

	static void SendTimerCallback(byte[] msg, int len)
	{
		// once more

		// make sure the midi system is actually still running to prevent
		// a crash on exit
		if ((s_recordMutex == null) || !s_connected) return;

		lock (s_recordMutex)
			SendUnlocked(msg, len, TimeSpan.Zero, MIDIFrom.Now);
	}

	void IMIDISink.OutRaw(Song csf, byte[] data, int len, int samplesDelay)
	{
		Assert.IsTrue(
			() => Song.CurrentSong == csf,
			"Hardware MIDI out should only be processed for the current playing song"); // AGH!

#if SCHISM_MIDI_DEBUG
		/* prints all of the raw midi messages into the terminal; useful for debugging output */
		//int i = (8000*AudioPlayback.AudioBufferSamples) / AudioPlayback.MixFrequency;

		for (int i = 0; i < len; i++)
			Console.Write("{0:x2} ", data[i]);
		Console.WriteLine(); /* newline */
#endif

		SendBuffer(data, len, TimeSpan.FromMilliseconds(samplesDelay / s_midiBytesPerMillisecond));
	}

	public static void SendBuffer(byte[] data, int len, TimeSpan pos)
	{
		lock (s_recordMutex)
		{
			/* just for fun... */
			if (Status.CurrentPageNumber == PageNumbers.MIDI)
			{
				Status.LastMIDIRealLength = len;
				if (len > Status.LastMIDIEvent.Length)
					Status.LastMIDILength = Status.LastMIDIEvent.Length;
				else
					Status.LastMIDILength = len;

				Array.Copy(data, 0, Status.LastMIDIEvent, 0, Status.LastMIDILength);

				Status.LastMIDIPort = null;
				Status.LastMIDITick = DateTime.UtcNow;
				Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.MIDIEventChanged;
			}

			if (s_midiBytesPerMillisecond > 0) // should always be true but I'm paranoid
			{
				pos /= s_midiBytesPerMillisecond;

				if (pos > TimeSpan.Zero)
				{
					if (SendUnlocked(data, len, pos, MIDIFrom.Later))
					{
						// ok, we need a timer.
						// one s'more
						Timer.Oneshot(pos, () => SendTimerCallback(data, len));
					}
				}
				else
				{
					// put the bread in the basket
					SendNow(data, len);
				}
			}
		}
	}

	public static IMIDISink GetMIDISink() => new MIDIEngine();

	public void OutRaw(Song csf, byte[] data, int len, TimeSpan pos)
	{
		Assert.IsTrue(() => Song.CurrentSong == csf, "Hardware MIDI out should only be processed for the current playing song"); // AGH!

#if SCHISM_MIDI_DEBUG
		/* prints all of the raw midi messages into the terminal; useful for debugging output */
		//int i = 8000 * AudioPlayback.AudioBufferSamples / Song.CurrentSong.MixFrequency;

		for (int i = 0; i < len; i++)
			Console.Write("{0:X2} ", data[i]);
		Console.WriteLine();
#endif

		SendBuffer(data, len, pos);
	}
}
