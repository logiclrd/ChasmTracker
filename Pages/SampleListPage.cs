using System;

namespace ChasmTracker.Pages;

public class SampleListPage : Page
{
	public SampleListPage()
		: base(PageNumbers.SampleList, "Samples", HelpTexts.SampleList)
	{

	}

	int _currentSample;

	public int CurrentSample
	{
		get => _currentSample;
		set
		{
			int newSample = value;

			if (Status.CurrentPage is SampleListPage)
				newSample = Math.Max(1, newSample);
			else
				newSample = Math.Max(0, newSample);

			newSample = Math.Min(LastVisibleSampleNumber(), newSample);

			if (_currentSample == newSample)
				return;

			_currentSample = newSample;
			// TODO: sample_list_reposition() */

			/* update_current_instrument(); */
			if (Status.CurrentPage is SampleListPage)
				Status.Flags |= StatusFlags.NeedUpdate;
		}
	}
}
