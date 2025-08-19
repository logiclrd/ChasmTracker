using System;

using ChasmTracker.FileTypes;

/* quick n dirty copy of the quirks openmpt has defined for schism
 * there are likely many more than this, but let's start by not
 * reinventing the wheel
 *
 * the default playback behavior is that all of these are on. */
public enum SchismQuirks
{
	[FixedInVersion(2015,  1, 29), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "671b30311082a0e7df041fca25f989b5d2478f69")]
	PeriodsAreHertz = 0,
	[FixedInVersion(2016,  5, 13), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "e7b1461fe751554309fd403713c2a1ef322105ca")]
	ShortSampleRetrigger = 1,
	[FixedInVersion(2021,  5,  2), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "a34ec86dc819915debc9e06f4727b77bf2dd29ee")]
	DoNotOverrideChannelPan = 2,
	[FixedInVersion(2021,  5,  2), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "648f5116f984815c69e11d018b32dfec53c6b97a")]
	PanningReset = 3,
	[FixedInVersion(2021, 11,  1), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "6e9f1207015cae0fe1b829fff7bb867e02ec6dea")]
	PitchPanSeparation = 4,
	[FixedInVersion(2022,  4, 30), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "1b2f7d5522fcb971f134a6664182ca569f7c8008")]
	EmptyNoteMapSlot = 5,
	[FixedInVersion(2022,  4, 30), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "1b2f7d5522fcb971f134a6664182ca569f7c8008")]
	PortamentoSwapResetsPosition = 6,
	[FixedInVersion(2022,  4, 30), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "1b2f7d5522fcb971f134a6664182ca569f7c8008")]
	MultiSampleInstrumentNumberPart1 = 999,
	[FixedInVersion(2023,  3,  9), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "73e9d60676c2b48c8e94e582373e29517105b2b1")]
	InitialNoteMemory = 7,
	[FixedInVersion(2023, 10, 17), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "31d36dc00013fc5ab0efa20c782af18e8b006e07")]
	DuplicateCheckTypeBehaviour = 8,
	[FixedInVersion(2023, 10, 19), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "411ec16b190ba1a486d8b0907ad8d74f8fdc2840")]
	SampleAndHoldPanbrello = 9,
	[FixedInVersion(2023, 10, 19), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "8ff0a86a715efb50c89770fb9095d4c4089ff187")]
	PortamentoNoNote = 10,
	[FixedInVersion(2023, 10, 22), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "b9609e4f827e1b6ce9ebe6573b85e69388ca0ea0")]
	FirstTickHandling = 11,
	[FixedInVersion(2023, 10, 22), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "a9e5df533ab52c35190fcc1cbfed4f0347b660bb")]
	MultiSampleInstrumentNumber = 12,
	[FixedInVersion(2024,  3,  9), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "ebdebaa8c8a735a7bf49df55debded1b7aac3605")]
	PanbrelloHold = 13,
	[FixedInVersion(2024,  5, 12), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "6f68f2855a7e5e4ffe825869244e631e15741037")]
	NoSustainOnPortamento = 14,
	[FixedInVersion(2024,  5, 12), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "aa84148e019a65f3d52ecd33fd84bfecfdb87bf4")]
	EmptyNoteMapSlotIgnoreCell = 15,
	[FixedInVersion(2024,  5, 27), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "9237960d45079a54ad73f87bacfe5dd8ae82e273")]
	OffsetWithInstrumentNumber = 16,
	[FixedInVersion(2024, 10, 13), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/", "223e327d9448561931b8cac8a55180286b17276c")]
	DoublePortamentoSlides = 17,
	[FixedInVersion(2025,  1,  8), FixedInCommit("https://github.com/schismtracker/schismtracker/commit/ff7a817df327c8f13d97b8c6546a9329f59edff8", "}")]
	CarryAfterNoteOff = 18,
}
