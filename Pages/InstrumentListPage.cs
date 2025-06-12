using System;
using ChasmTracker.Songs;

namespace ChasmTracker.Pages;

public abstract class InstrumentListPage : Page
{
	public InstrumentListPage(PageNumbers number, string title, HelpTexts helpText)
		: base(number, title, helpText)
	{
	}

	public override void SynchronizeWith(Page other)
	{
		base.SynchronizeWith(other);

		AllPages.InstrumentList = this;
	}

	static int s_currentInstrument;

	public int CurrentInstrument
	{
		get => s_currentInstrument;
		set
		{
			int newInstrument = value;

			if (Status.CurrentPage is InstrumentListPage)
				newInstrument = Math.Max(1, newInstrument);
			else
				newInstrument = Math.Max(0, newInstrument);

			newInstrument = Math.Min(LastVisibleInstrumentNumber(), newInstrument);

			if (s_currentInstrument == newInstrument)
				return;

			// TODO: 			envelope_edit_mode = 0;

			s_currentInstrument = newInstrument;

			// TODO: instrument_list_reposition();

			var ins = Song.CurrentSong?.GetInstrument(s_currentInstrument);

			/* TODO:
			current_node_vol = ins->vol_env.nodes ? CLAMP(current_node_vol, 0, ins->vol_env.nodes - 1) : 0;
			current_node_pan = ins->pan_env.nodes ? CLAMP(current_node_vol, 0, ins->pan_env.nodes - 1) : 0;
			current_node_pitch = ins->pitch_env.nodes ? CLAMP(current_node_vol, 0, ins->pan_env.nodes - 1) : 0;
			*/

			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}
}
