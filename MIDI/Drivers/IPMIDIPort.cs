using System;

namespace ChasmTracker.MIDI.Drivers;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using ChasmTracker.MIDI;
using ChasmTracker.Utility;
using Mono.Unix;

public class IPMIDIPort : MIDIPort, IDisposable
{
	IPMIDI _owner;

	public IPMIDIPort(IPMIDI owner, int number)
	{
		_owner = owner;

		Number = number;

		AllocateSocket();
	}

	public override string Name => "Multicast/IP MIDI " + (Number + 1);
	public override string Provider => "IP";

	static readonly IPAddress s_ipMIDIGroupAddress = IPAddress.Parse("225.0.0.37");

	public Socket Socket;

	public static Socket CreateSocket(int n, bool isOut)
	{
		var fd = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

		try
		{
			var mreq = new MulticastOption(s_ipMIDIGroupAddress);

			fd.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			/* don't loop back what we generate */
			fd.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, !isOut);
			fd.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 31);
			fd.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, mreq);
			fd.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

			var asin = new IPEndPoint(IPAddress.Any, IPMIDI.MIDIIPBase + n);

			fd.Bind(asin);

			return fd;
		}
		catch
		{
			fd.Dispose();

			throw;
		}
	}

	[MemberNotNull(nameof(Socket))]
	void AllocateSocket()
	{
		Socket = CreateSocket(Number, false);
	}

	public void Dispose()
	{
		Socket.Dispose();
	}

	public override bool Enable()
	{
		base.Enable();
		_owner.DoWakeMIDI();
		return true;
	}

	public override bool Disable()
	{
		base.Disable();
		_owner.DoWakeMIDI();
		return true;
	}

	[ThreadStatic]
	static byte[]? s_receiveBuffer;

	public void ReadIn()
	{
		s_receiveBuffer ??= new byte[65536];

		EndPoint asin = new IPEndPoint(IPAddress.Any, 0);

		int r = Socket.ReceiveFrom(s_receiveBuffer, ref asin);

		if (r > 0)
			OnReceived(s_receiveBuffer.Segment(0, r));
	}

	public override bool CanSendNow => true;

	public override void SendNow(Span<byte> seq, TimeSpan delay)
	{
		if (seq.Length == 0) return;
		if (!ActiveIO.HasAllFlags(MIDIIO.Output)) return; /* blah... */

		var asin = new IPEndPoint(s_ipMIDIGroupAddress, IPMIDI.MIDIIPBase + Number);

		while (seq.Length > 0)
		{
			int ss = Math.Min(seq.Length, IPMIDI.MaxDGramSize);

			try
			{
				if (Socket.SendTo(seq.Slice(0, ss), asin) < 0)
					throw new SocketException();
			}
			catch
			{
				ActiveIO &= ~MIDIIO.Output; /* turn off output */
				break;
			}

			seq = seq.Slice(ss);
		}
	}
}

