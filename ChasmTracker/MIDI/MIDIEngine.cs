using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
	static object s_providerMutex = new object();
	static object s_portMutex = new object();

	static IEnumerable<Type> EnumerateProviders()
		=> typeof(MIDIEngine).Assembly.GetTypes().Where(t => typeof(MIDIProvider).IsAssignableFrom(t));

	static void Connect()
	{
		if (!s_connected)
		{
			foreach (var type in EnumerateProviders())
			{
				var setupMethod = type.GetMethod("SetUp", BindingFlags.Public | BindingFlags.Static);

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
		lock (s_providerMutex)
			foreach (var provider in s_providers)
				provider.Poll();
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

			foreach (var provider in s_providers)
			{
				provider.UnregisterAllPorts();

				provider.IsCancelled = true;
				provider.Wake();
			}
		}
	}

	/* ------------------------------------------------------------- */

	static MIDIEngine()
	{
		Configuration.RegisterConfigurable(new MIDIConfigurationThunk());
		Configuration.RegisterListGatherer(new GatherMIDIPortConfigurationThunk());
	}

	class MIDIConfigurationThunk : IConfigurable<MIDIConfiguration>
	{
		public void SaveConfiguration(MIDIConfiguration config) => MIDIEngine.SaveConfiguration(config);
		public void LoadConfiguration(MIDIConfiguration config) => MIDIEngine.LoadConfiguration(config);
	}

	class GatherMIDIPortConfigurationThunk : IGatherConfigurationSections<MIDIPortConfiguration>
	{
		public void GatherConfiguration(IList<MIDIPortConfiguration> list) => MIDIEngine.GatherMIDIPortConfiguration(list);
	}

	static void LoadConfiguration(MIDIConfiguration config)
		{
			Flags = config.Flags;
			PitchWheelDepth = config.PitchWheelDepth;
			Amplification = config.Amplification;
			C5Note = config.C5Note;
		}

	static void SaveConfiguration(MIDIConfiguration config)
	{
		config.Flags = Flags;
		config.PitchWheelDepth = PitchWheelDepth;
		config.Amplification = Amplification;
		config.C5Note = C5Note;
	}

	static void GatherMIDIPortConfiguration(IList<MIDIPortConfiguration> list)
	{
		/* write out only enabled midi ports */
		lock (s_mutex)
		{
			int i = 1;

			foreach (var p in s_providers)
			{
				lock (p.Sync)
				{
					foreach (var q in p.EnumeratePorts())
					{
						if (string.IsNullOrEmpty(q.Name))
							continue;

						var ss = q.Name.TrimStart();

						if (ss.Length == 0)
							continue;

						if (q.IO == MIDIIO.None)
							continue;

						while (i >= list.Count)
							list.Add(new MIDIPortConfiguration());

						var c = list[i];

						i++;

						c.Name = ss;
						c.Provider = p.Name.TrimStart();
						c.Input = q.IO.HasAllFlags(MIDIIO.Input);
						c.Output = q.IO.HasAllFlags(MIDIIO.Output);
					}
				}
			}

			//TODO: Save number of MIDI-IP ports

			/* delete other MIDI port sections */
			while (list.Count > i)
				list.RemoveAt(i);
		}
	}

	/* ------------------------------------------------------------- */
	/* PROVIDER registry */

	static List<MIDIProvider> s_providers = new List<MIDIProvider>();

	public static void RegisterProvider(MIDIProvider provider)
	{
		lock (s_providerMutex)
			s_providers.Add(provider);
	}

	/* ------------------------------------------------------------- */

	/* wankery */
	public static void SetIPPortCount(int count)
	{
		lock (s_providerMutex)
		{
			var ipProvider = s_providers.OfType<IPMIDI>().SingleOrDefault();

			if (ipProvider != null)
				ipProvider.SetPorts(count);
		}
	}

	public static int GetIPPortCount()
	{
		lock (s_providerMutex)
		{
			var ipProvider = s_providers.OfType<IPMIDI>().SingleOrDefault();

			if (ipProvider != null)
				return ipProvider.Ports.Count;

			return 0;
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

	/* midi engines list ports this way */
	public static int RegisterPort(MIDIPort p)
	{
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

		LoadMIDIPortConfiguration(p);

		p.Received += port_Received;

		return p.Number;
	}

	public static void UnregisterPort(MIDIPort p)
	{
		lock (s_portMutex)
		{
			p.Received -= port_Received;

			p.Disable();

			s_ports[p.Number] = null;
		}
	}

	static void LoadMIDIPortConfiguration(MIDIPort q)
	{
		string ss = q.Name.TrimStart();
		string? sp = q.Provider?.TrimStart();

		if (string.IsNullOrEmpty(ss))
			return;

		if (string.IsNullOrEmpty(sp))
			sp = null;

		/* look for MIDI port sections */
		for (int j = 1; j < Configuration.MIDIPorts.Count; j++)
		{
			var c = Configuration.MIDIPorts[j];

			if (!c.Name.Equals(q.Name, StringComparison.InvariantCultureIgnoreCase))
				continue;
			if (!c.Provider.Equals(q.Provider, StringComparison.InvariantCultureIgnoreCase))
				continue;

			/* okay found port */

			if (q.IOCap.HasAllFlags(MIDIIO.Input) && c.Input)
				q.IO |= MIDIIO.Input;
			if (q.IOCap.HasAllFlags(MIDIIO.Output) && c.Output)
				q.IO |= MIDIIO.Output;

			q.Enable();
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

	static void port_Received(MIDIPort src, ArraySegment<byte> data)
	{
		if (data.Count == 0)
			return;

		int len = data.Count;

		if (len < 4)
		{
			byte[] d4 = new byte[4];

			data.CopyTo(d4);
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

			data.CopyTo(Status.LastMIDIEvent.Segment(0, Status.LastMIDILength));

			Status.Flags |= StatusFlags.MIDIEventChanged;
			Status.LastMIDIPort = src;
			Status.LastMIDITick = DateTime.UtcNow;
		}

		/* pass through midi events when on midi page */
		if (Status.CurrentPageNumber == PageNumbers.MIDI)
		{
			SendNow(data);
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
					EventSysEx(data.Slice(1, len - 2));
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

	public static void EventSysEx(Span<byte> data)
	{
		var @event = new MIDISysExEvent();

		@event.Packet = data.ToArray();

		EventHub.PushEvent(@event);
	}


	public static bool NeedFlush()
	{
		if (s_recordMutex == null)
			return false;

		foreach (var ptr in s_ports.OfType<MIDIPort>())
			if (ptr.IO.HasAllFlags(MIDIIO.Output) && !ptr.CanDrain && ptr.CanSendNow)
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
				if ((ptr != null) && ptr.IO.HasAllFlags(MIDIIO.Output) && ptr.CanDrain)
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

		if (Flags.HasAllFlags(MIDIFlags.DisableRecord))
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
				if (Flags.HasAllFlags(MIDIFlags.RecordVelocity))
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

	static bool SendUnlocked(Span<byte> data, TimeSpan delay, MIDIFrom from)
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
					if ((ptr != null) && ptr.IO.HasAllFlags(MIDIIO.Output))
					{
						if (ptr.CanSendNow)
							ptr.SendNow(data, TimeSpan.Zero);
						else if (ptr.CanSendLater)
							ptr.SendLater(data, TimeSpan.Zero);
					}
				}
				break;
			case MIDIFrom.Now:
				/* only "now" plays */
				foreach (var ptr in s_ports!)
				{
					if ((ptr != null) && ptr.IO.HasAllFlags(MIDIIO.Output))
					{
						if (ptr.CanSendNow)
							ptr.SendNow(data, TimeSpan.Zero);
					}
				}
				break;
			case MIDIFrom.Later:
				/* only "later" plays */
				foreach (var ptr in s_ports!)
				{
					if ((ptr != null) && ptr.IO.HasAllFlags(MIDIIO.Output))
					{
						if (ptr.CanSendLater)
							ptr.SendLater(data, delay);
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

	public static void SendNow(Span<byte> seq)
	{
		if (s_recordMutex == null) return;

		lock (s_recordMutex)
			SendUnlocked(seq, TimeSpan.Zero, MIDIFrom.Immediate);
	}

	static void SendTimerCallback(Span<byte> msg)
	{
		// once more

		// make sure the midi system is actually still running to prevent
		// a crash on exit
		if ((s_recordMutex == null) || !s_connected) return;

		lock (s_recordMutex)
			SendUnlocked(msg, TimeSpan.Zero, MIDIFrom.Now);
	}

	void IMIDISink.OutRaw(Song csf, Span<byte> data, int samplesDelay)
	{
		Assert.IsTrue(
			csf == Song.CurrentSong,
			"csf == Song.CurrentSong",
			"Hardware MIDI out should only be processed for the current playing song"); // AGH!

#if SCHISM_MIDI_DEBUG
		/* prints all of the raw midi messages into the terminal; useful for debugging output */
		//int i = (8000*AudioPlayback.AudioBufferSamples) / AudioPlayback.MixFrequency;

		for (int i = 0; i < len; i++)
			Console.Write("{0:x2} ", data[i]);
		Console.WriteLine(); /* newline */
#endif

		SendBuffer(data, TimeSpan.FromMilliseconds(samplesDelay / s_midiBytesPerMillisecond));
	}

	// We send little chunks of data, like 4 bytes at a time. I'm hoping this will be grossly
	// more than enough to handle delayed data sends, since we can't store Span<byte>s.
	// I also don't know if multiple threads call SendBuffer, so we'll just do this
	// independently for each thread.
	[ThreadStatic]
	static byte[]? s_ringBuffer;
	[ThreadStatic]
	static int s_ringBufferNext;

	[MemberNotNull(nameof(s_ringBuffer))]
	static int AllocateFromRingBuffer(int length)
	{
		s_ringBuffer ??= new byte[65536];

		if (s_ringBufferNext + length < s_ringBuffer.Length)
			s_ringBufferNext += length;
		else
			s_ringBufferNext = 0;

		return s_ringBufferNext - length;
	}

	public static void SendBuffer(Span<byte> data, TimeSpan pos)
	{
		lock (s_recordMutex)
		{
			/* just for fun... */
			if (Status.CurrentPageNumber == PageNumbers.MIDI)
			{
				Status.LastMIDIRealLength = data.Length;
				if (data.Length > Status.LastMIDIEvent.Length)
					Status.LastMIDILength = Status.LastMIDIEvent.Length;
				else
					Status.LastMIDILength = data.Length;

				data.Slice(0, Status.LastMIDILength).CopyTo(Status.LastMIDIEvent);

				Status.LastMIDIPort = null;
				Status.LastMIDITick = DateTime.UtcNow;
				Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.MIDIEventChanged;
			}

			if (s_midiBytesPerMillisecond > 0) // should always be true but I'm paranoid
			{
				pos /= s_midiBytesPerMillisecond;

				if (pos > TimeSpan.Zero)
				{
					if (SendUnlocked(data, pos, MIDIFrom.Later))
					{
						// ok, we need a timer.
						// one s'more
						int dataLength = data.Length;
						int bufferOffset = AllocateFromRingBuffer(dataLength);

						data.CopyTo(s_ringBuffer.Slice(bufferOffset, dataLength));

						Timer.Oneshot(pos, () => SendTimerCallback(s_ringBuffer.Slice(bufferOffset, dataLength)));
					}
				}
				else
				{
					// put the bread in the basket
					SendNow(data);
				}
			}
		}
	}

	// Get the length of a MIDI event in bytes
	// FIXME: this needs to handle sysex and friends as well
	public static int GetEventLength(byte first_byte)
	{
		switch (first_byte & 0xF0)
		{
			case 0xC0:
			case 0xD0:
				return 2;
			case 0xF0:
				switch (first_byte)
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

	public static IMIDISink GetMIDISink() => new MIDIEngine();

	public void OutRaw(Song csf, Span<byte> data, TimeSpan pos)
	{
		Assert.IsTrue(
			csf == Song.CurrentSong,
			"csf == Song.CurrentSong",
			"Hardware MIDI out should only be processed for the current playing song"); // AGH!

#if SCHISM_MIDI_DEBUG
		/* prints all of the raw midi messages into the terminal; useful for debugging output */
		//int i = 8000 * AudioPlayback.AudioBufferSamples / AudioPlayback.MixFrequency;

		for (int i = 0; i < len; i++)
			Console.Write("{0:X2} ", data[i]);
		Console.WriteLine();
#endif

		SendBuffer(data, pos);
	}
}
