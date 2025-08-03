using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ChasmTracker.Events;
using ChasmTracker.MIDI;
using ChasmTracker.Utility;

namespace ChasmTracker.MIDI.Drivers;

public class IPMIDI : MIDIProvider<IPMIDIPort>
{
	public const int DefaultIPPortCount = 5;
	public const int MIDIIPBase = 21928;
	public const int MaxDGramSize = 1280;

	Socket[] _wakeUp = new Socket[2];
	Socket? _outFD = null;
	volatile int _desiredNumPorts = 0;

	void DoWakeMain()
	{
		EventHub.PushEvent(new UpdateIPMIDIEvent());
	}

	byte[] _buffer = new byte[1];

	public void DoWakeMIDI()
	{
		_buffer[0] = 1;
		_wakeUp[1].Send(_buffer);
	}

	public void SetPorts(int n)
	{
		if (_outFD == null) return;

		if (Status.Flags.HasFlag(StatusFlags.NoNetwork)) return;

		_desiredNumPorts = n;

		DoWakeMIDI();
	}

	void ThreadProc()
	{
		byte[] buffer = new byte[4096];

		while (!IsCancelled)
		{
			var checkRead = new List<Socket>();

			lock (Sync)
				checkRead.AddRange(Ports.Select(p => p.Socket));

			checkRead.Add(_wakeUp[0]);

			Socket.Select(checkRead, checkWrite: null, checkError: null, Timeout.Infinite);

			if (IsCancelled)
				return;

			if (checkRead.Any())
			{
				bool hasOthers;

				if (checkRead.Contains(_wakeUp[0]))
				{
					_wakeUp[0].Receive(_buffer);
					hasOthers = checkRead.Count > 1;
				}
				else
					hasOthers = checkRead.Count > 0;

				if (hasOthers)
				{
					var doRead = new List<IPMIDIPort>();

					lock (Sync)
					{
						foreach (var port in Ports)
							if (port.ActiveIO.HasFlag(MIDIIO.Input)
							 && checkRead.Contains(port.Socket))
								doRead.Add(port);
					}

					doRead.ForEach(port => port.ReadIn());
				}
			}
		}
	}

	public override void Poll()
	{
		bool wake = false;

		lock (Sync)
		{
			if (_desiredNumPorts > Ports.Count)
			{
				for (int i = Ports.Count; i < _desiredNumPorts; i++)
				{
					IPMIDIPort? newPort;

					try
					{
						newPort = new IPMIDIPort(this, i);

						MIDIEngine.RegisterPort(newPort);

						Ports.Add(newPort);
					}
					catch
					{
						break;
					}
				}

				wake = true;
			}
			else if (_desiredNumPorts < Ports.Count)
			{
				for (int i = _desiredNumPorts; i < Ports.Count; i++)
				{
					MIDIEngine.UnregisterPort(Ports[i]);
					Ports[i].Dispose();
				}

				Ports.RemoveRange(_desiredNumPorts, Ports.Count - _desiredNumPorts);

				wake = true;
			}
		}

		if (wake)
		{
			DoWakeMain();
			DoWakeMIDI();
		}
	}

	public override void Wake()
	{
		DoWakeMIDI();
	}

	public override bool SetUp()
	{
		if (Status.Flags.HasFlag(StatusFlags.NoNetwork)) return false;

		_wakeUp[0] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		_wakeUp[1] = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

		_wakeUp[1].Blocking = false;

		IPEndPoint sin = new IPEndPoint(IPAddress.Loopback, port: 0);

		_wakeUp[0].Bind(sin);
		_wakeUp[0].Listen();

		sin = (IPEndPoint)_wakeUp[0].LocalEndPoint!;

		try
		{
			_wakeUp[1].Connect(sin);
		}
		catch (SocketException se) when (se.SocketErrorCode == SocketError.WouldBlock)
		{
		}

		var clientFD = _wakeUp[0].Accept();

		_wakeUp[0].Dispose();
		_wakeUp[0] = clientFD;

		_wakeUp[0].Blocking = false;

		// _wakeUp[0] is all good to go, make sure wakeup[1] got the message too
		{
			var checkRead = new List<Socket>();

			checkRead.Add(_wakeUp[1]);

			Socket.Select(checkRead, null, null, TimeSpan.FromSeconds(1));

			if (!checkRead.Contains(_wakeUp[1]))
				throw new Exception("Creation of wake-up loopback connection failed");
		}

		_outFD = IPMIDIPort.CreateSocket(-1, true);

		//TODO: Save number of MIDI-IP ports
		SetPorts(DefaultIPPortCount);

		MIDIEngine.RegisterProvider(this);

		return true;
	}

	public override void UnregisterAllPorts()
	{
		lock (Sync)
			foreach (var port in Ports)
				MIDIEngine.UnregisterPort(port);
	}
}