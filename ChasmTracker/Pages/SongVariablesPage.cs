using System.IO;

namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class SongVariablesPage : Page
{
	TextEntryWidget textEntrySongName;
	ThumbBarWidget thumbBarInitialTempo;
	ThumbBarWidget thumbBarInitialSpeed;
	ThumbBarWidget thumbBarGlobalVolume;
	ThumbBarWidget thumbBarMixingVolume;
	ThumbBarWidget thumbBarSeparation;
	ToggleWidget toggleITOldEffects;
	ToggleWidget toggleCompatibleGXX;
	ToggleButtonWidget toggleButtonControlInstruments;
	ToggleButtonWidget toggleButtonControlSamples;
	ToggleButtonWidget toggleButtonPlaybackStereo;
	ToggleButtonWidget toggleButtonPlaybackMono;
	ToggleButtonWidget toggleButtonPitchSlideLinear;
	ToggleButtonWidget toggleButtonPitchSlideAmiga;
	TextEntryWidget textEntryModulesDirectory;
	TextEntryWidget textEntrySamplesDirectory;
	TextEntryWidget textEntryInstrumentsDirectory;
	ButtonWidget buttonSaveAllPreferences;

	const int ControlGroup = 1;
	const int PlaybackGroup = 2;
	const int PitchSlideGroup = 3;

	public SongVariablesPage()
		: base(PageNumbers.SongVariables, "Song Variables & Directory Configuration (F12)", HelpTexts.Global)
	{
		textEntrySongName = new TextEntryWidget(new Point(17, 16), 26, Song.CurrentSong.Title, 25);

		thumbBarInitialTempo = new ThumbBarWidget(new Point(17, 20), 33, 31, 255);
		thumbBarInitialSpeed = new ThumbBarWidget(new Point(17, 20), 33, 1, 255);

		thumbBarGlobalVolume = new ThumbBarWidget(new Point(17, 23), 17, 0, 128);
		thumbBarMixingVolume = new ThumbBarWidget(new Point(17, 24), 17, 0, 128);
		thumbBarSeparation = new ThumbBarWidget(new Point(17, 25), 17, 0, 128);

		toggleITOldEffects = new ToggleWidget(new Point(17, 26));
		toggleCompatibleGXX = new ToggleWidget(new Point(17, 27));

		toggleButtonControlInstruments = new ToggleButtonWidget(new Point(17, 30), 11, "Instruments", 1, ControlGroup);
		toggleButtonControlSamples = new ToggleButtonWidget(new Point(32, 30), 11, "Samples", 1, ControlGroup);

		toggleButtonPlaybackStereo = new ToggleButtonWidget(new Point(17, 33), 11, "Stereo", 1, PlaybackGroup);
		toggleButtonPlaybackMono = new ToggleButtonWidget(new Point(32, 33), 11, "Mono", 1, PlaybackGroup);

		toggleButtonPitchSlideLinear = new ToggleButtonWidget(new Point(17, 33), 11, "Linear", 1, PitchSlideGroup);
		toggleButtonPitchSlideAmiga = new ToggleButtonWidget(new Point(32, 33), 11, Amiga, 1, PitchSlideGroup);

		textEntryModulesDirectory = new TextEntryWidget(new Point(13, 42), 65, Configuration.Directories.ModulesDirectory, Constants.MaxPathLength);
		textEntrySamplesDirectory = new TextEntryWidget(new Point(13, 43), 65, Configuration.Directories.SamplesDirectory, Constants.MaxPathLength);
		textEntryInstrumentsDirectory = new TextEntryWidget(new Point(13, 44), 65, Configuration.Directories.InstrumentsDirectory, Constants.MaxPathLength);

		buttonSaveAllPreferences = new ButtonWidget(new Point(28, 47), 22, "Save all Preferences", 2);

		toggleButtonControlInstruments.Next.BackTab = toggleButtonControlSamples;

		textEntrySongName.Changed += UpdateSongTitle;
		thumbBarInitialTempo.Changed += UpdateValuesInSong;
		thumbBarInitialSpeed.Changed += UpdateValuesInSong;
		thumbBarGlobalVolume.Changed += UpdateValuesInSong;
		thumbBarMixingVolume.Changed += UpdateValuesInSong;
		thumbBarSeparation.Changed += UpdateValuesInSong;
		toggleITOldEffects.Changed += UpdateValuesInSong;
		toggleITOldEffects.Changed += UpdateValuesInSong;
		toggleButtonControlInstruments.Changed += MaybeInitializeInstruments;
		toggleButtonControlSamples.Changed += UpdateValuesInSong;
		toggleButtonPlaybackStereo.Changed += UpdateValuesInSong;
		toggleButtonPlaybackMono.Changed += UpdateValuesInSong;
		toggleButtonPitchSlideLinear.Changed += UpdateValuesInSong;
		toggleButtonPitchSlideAmiga.Changed += UpdateValuesInSong;
		textEntryModulesDirectory.Changed += ModulesDirectoryChanged;
		textEntrySamplesDirectory.Changed += SamplesDirectoryChanged;
		textEntryInstrumentsDirectory.Changed += InstrumentsDirectoryChanged;
		buttonSaveAllPreferences.Clicked += Configuration.Save;

		AddWidget(textEntrySongName);
		AddWidget(thumbBarInitialTempo);
		AddWidget(thumbBarInitialSpeed);
		AddWidget(thumbBarGlobalVolume);
		AddWidget(thumbBarMixingVolume);
		AddWidget(thumbBarSeparation);
		AddWidget(toggleITOldEffects);
		AddWidget(toggleCompatibleGXX);
		AddWidget(toggleButtonControlInstruments);
		AddWidget(toggleButtonControlSamples);
		AddWidget(toggleButtonPlaybackStereo);
		AddWidget(toggleButtonPlaybackMono);
		AddWidget(toggleButtonPitchSlideLinear);
		AddWidget(toggleButtonPitchSlideAmiga);
		AddWidget(textEntryModulesDirectory);
		AddWidget(textEntrySamplesDirectory);
		AddWidget(textEntryInstrumentsDirectory);
		AddWidget(buttonSaveAllPreferences);
	}

	public override void DrawConst()
	{
		VGAMem.DrawBox(new Point(16, 15), new Point(43, 17), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(16, 18), new Point(50, 21), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(16, 22), new Point(34, 28), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(12, 41), new Point(78, 45), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawFillCharacters(new Point(20, 26), new Point(33, 27), (VGAMem.DefaultForeground, 0));

		VGAMem.DrawText("Song Variables", new Point(33, 13), (3, 2));
		VGAMem.DrawText("Song Name", new Point(7, 16), (0, 2));
		VGAMem.DrawText("Initial Tempo", new Point(3, 19), (0, 2));
		VGAMem.DrawText("Initial Speed", new Point(3, 20), (0, 2));
		VGAMem.DrawText("Global Volume", new Point(3, 23), (0, 2));
		VGAMem.DrawText("Mixing Volume", new Point(3, 24), (0, 2));
		VGAMem.DrawText("Separation", new Point(6, 25), (0, 2));
		VGAMem.DrawText("Old Effects", new Point(5, 26), (0, 2));
		VGAMem.DrawText("Compatible Gxx", new Point(2, 27), (0, 2));
		VGAMem.DrawText("Control", new Point(9, 30), (0, 2));
		VGAMem.DrawText("Playback", new Point(8, 33), (0, 2));
		VGAMem.DrawText("Pitch Slides", new Point(4, 36), (0, 2));
		VGAMem.DrawText("Directories", new Point(34, 40), (3, 2));
		VGAMem.DrawText("Module", new Point(6, 42), (0, 2));
		VGAMem.DrawText("Sample", new Point(6, 43), (0, 2));
		VGAMem.DrawText("Instrument", new Point(2, 44), (0, 2));

		for (int n = 1; n < 79; n++)
			VGAMem.DrawCharacter(129, new Point(n, 39), (1, 2));
	}

	public override void NotifySongChanged()
	{
		textEntrySongName.Text = Song.CurrentSong.Title;
		textEntrySongName.CursorPosition = Song.CurrentSong.Title.Length;

		thumbBarInitialTempo.Value = Song.CurrentSong.InitialTempo;
		thumbBarInitialSpeed.Value = Song.CurrentSong.InitialSpeed;
		thumbBarSeparation.Value = Song.CurrentSong.PanSeparation;

		toggleITOldEffects.State = Song.CurrentSong.Flags.HasAllFlags(SongFlags.ITOldEffects);
		toggleCompatibleGXX.State = Song.CurrentSong.Flags.HasAllFlags(SongFlags.CompatibleGXX);

		if (Song.CurrentSong.IsInstrumentMode)
			toggleButtonControlInstruments.InitializeState(true);
		else
			toggleButtonControlSamples.InitializeState(true);

		if (Song.CurrentSong.IsStereo)
			toggleButtonPlaybackStereo.InitializeState(true);
		else
			toggleButtonPlaybackMono.InitializeState(true);

		if (Song.CurrentSong.Flags.HasAllFlags(SongFlags.LinearSlides))
			toggleButtonPitchSlideLinear.InitializeState(true);
		else
			toggleButtonPitchSlideAmiga.InitializeState(true);

		// Storlek's AINOR easter egg
		if (Path.GetFileName(Song.CurrentSong.FileName).Contains("Ainor", System.StringComparison.InvariantCultureIgnoreCase))
			Amiga = "AINOR";
	}

	static string Amiga = "Amiga";

	/* --------------------------------------------------------------------- */

	public void SyncStereo(bool isStereo)
	{
		toggleButtonPlaybackStereo.SetState(isStereo);
	}

	void UpdateSongTitle()
	{
		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	void UpdateValuesInSong()
	{
		Song.CurrentSong.InitialTempo = thumbBarInitialTempo.Value;
		Song.CurrentSong.InitialSpeed = thumbBarInitialSpeed.Value;
		Song.CurrentSong.InitialGlobalVolume = thumbBarGlobalVolume.Value;
		Song.CurrentSong.MixingVolume = thumbBarMixingVolume.Value;
		Song.CurrentSong.PanSeparation = thumbBarSeparation.Value;

		Song.CurrentSong.ITOldEffects = toggleITOldEffects.State;
		Song.CurrentSong.CompatibleGXX = toggleCompatibleGXX.State;
		Song.CurrentSong.SetInstrumentMode(toggleButtonControlInstruments.State);
		Song.CurrentSong.IsStereo = toggleButtonPlaybackStereo.State;
		Song.CurrentSong.LinearPitchSlides = toggleButtonPitchSlideLinear.State;

		Status.Flags = StatusFlags.SongNeedsSave;
	}

	void MaybeInitializeInstruments()
	{
		/* XXX actually, in IT the buttons on this dialog say OK/No for whatever reason */
		Song.CurrentSong.SetInstrumentMode(true);

		MessageBox.Show(MessageBoxTypes.YesNo, "Initialise instruments?", accept: () => Song.CurrentSong.InitializeInstruments());

		Status.Flags |= StatusFlags.SongNeedsSave;
	}

	/* bleh */
	void ModulesDirectoryChanged()
	{
		Status.Flags |= StatusFlags.ModulesDirectoryChanged;
	}

	void SamplesDirectoryChanged()
	{
		Status.Flags |= StatusFlags.SamplesDirectoryChanged;
	}

	void InstrumentsDirectoryChanged()
	{
		Status.Flags |= StatusFlags.InstrumentsDirectoryChanged;
	}
}
