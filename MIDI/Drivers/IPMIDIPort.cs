using System;

namespace ChasmTracker.MIDI.Drivers;

using ChasmTracker.MIDI;

public class IPMIDIPort : MIDIPort
{
	public static bool Setup()
	{
		// TODO
		return false;
	}

	public override bool Enable()
	{
		throw new System.NotImplementedException();
		/* TODO
	int n = INT_SHAPED_PTR(p->userdata);
	mt_mutex_lock(blocker);
	if (p->io & MIDI_INPUT)
		state[n] |= 1;
	if (p->io & MIDI_OUTPUT)
		state[n] |= 2;
	mt_mutex_unlock(blocker);
	do_wake_midi();
	return 1;
		*/
	}

	public override bool Disable()
	{
		throw new System.NotImplementedException();
		/* TODO
	int n = INT_SHAPED_PTR(p->userdata);
	mt_mutex_lock(blocker);
	if (p->io & MIDI_INPUT)
		state[n] &= (~1);
	if (p->io & MIDI_OUTPUT)
		state[n] &= (~2);
	mt_mutex_unlock(blocker);
	do_wake_midi();
	return 1;
		*/
	}

	public override bool CanSendNow => true;

	public override void SendNow(byte[] seq, int len, TimeSpan delay)
	{
		throw new NotImplementedException();
//	struct sockaddr_in asin = {0};
//	unsigned char *ipcopy;
//	int n = INT_SHAPED_PTR(p->userdata);
//	int ss;
//
//	if (len == 0) return;
//	if (!(state[n] & 2)) return; /* blah... */
//
//	asin.sin_family = AF_INET;
//	ipcopy = (unsigned char *)&asin.sin_addr.s_addr;
//	ipcopy[0] = 225; ipcopy[1] = ipcopy[2] = 0; ipcopy[3] = 37;
//	asin.sin_port = htons(MIDI_IP_BASE+n);
//
//	while (len) {
//		ss = (len > MAX_DGRAM_SIZE) ?  MAX_DGRAM_SIZE : len;
//		if (sendto(out_fd, (const char*)data, ss, 0,
//				(struct sockaddr *)&asin,sizeof(asin)) < 0) {
//			state[n] &= (~2); /* turn off output */
//			break;
//		}
//		len -= ss;
//		data += ss;
//	}
	}
}
