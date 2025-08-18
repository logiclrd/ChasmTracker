using System;
using System.Linq;

namespace ChasmTracker.Dialogs.Samples;

using ChasmTracker.Input;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class AdLibConfigDialog : Dialog
{
	enum ParameterType
	{
		Boolean,
		Number,
	}

	class ParameterInfo
	{
		public bool IsHeading;
		public string Label;
		public Point LabelPosition;
		public bool LabelOnly;
		public int ControlColumnNumber;
		public int ControlRow;
		public ParameterType Type;
		public int ByteNumber;
		public int FirstBit;
		public int NumBits;
		public bool InvertInterpretation;

		public ParameterInfo(string label, Point labelPosition)
		{
			Label = label;
			LabelPosition = labelPosition;
			LabelOnly = true;
		}

		public ParameterInfo(string label, Point labelPosition, int controlColumnNumber, int controlRow, ParameterType type, int byteNumber, int firstBit, int numBits, bool invertInterpretation = false)
		{
			Label = label;
			LabelPosition = labelPosition;
			ControlColumnNumber = controlColumnNumber;
			ControlRow = controlRow;
			Type = type;
			ByteNumber = byteNumber;
			FirstBit = firstBit;
			NumBits = numBits;
			InvertInterpretation = invertInterpretation;
		}
	}

	static string[] YNToggle = ["n", "y"];
	static int[] ColumnXPos = [26, 30, 58, 62, 39];

	Shared<int>[] _columnCursorPos = ColumnXPos.Select(_ => new Shared<int>()).ToArray();

	static ParameterInfo[] Parameters =
		[
			new ParameterInfo(
				"Adlib Melodic Instrument Parameters", new Point(19, 1)) { IsHeading = true },

			new ParameterInfo(
				"Additive Synthesis:", new Point(19, 3),
				4,  3, ParameterType.Boolean,10, 0, 1),
			new ParameterInfo(
				"Modulation Feedback:", new Point(18, 4),
				4,  4, ParameterType.Number,10, 1, 3),

			new ParameterInfo(
				"Car Mod", new Point(26, 6)),
			new ParameterInfo(
				"Car Mod", new Point(56, 6)),

			new ParameterInfo(
				"Attack", new Point(19, 7),
				0,  7, ParameterType.Number, 5, 4, 4),
			new ParameterInfo(
				"Decay", new Point(20, 8),
				0,  8, ParameterType.Number, 5, 0, 4),
			new ParameterInfo(
				"Sustain Sound", new Point(18, 9),
				0,  9, ParameterType.Number, 7, 4, 4, invertInterpretation: true),
			new ParameterInfo(
				"Release", new Point(18, 10),
				0, 10, ParameterType.Number, 7, 0, 4),
			new ParameterInfo(
				"Sustain Sound", new Point(12, 11),
				0, 11, ParameterType.Boolean, 1, 5, 1),
			new ParameterInfo(
				"Volume", new Point(19, 12),
				0, 12, ParameterType.Number, 3, 0, 6, invertInterpretation: true),

			new ParameterInfo(
				"", new Point(), // Modulator Attack
				1,  7, ParameterType.Number, 4, 4, 4),
			new ParameterInfo(
				"", new Point(), // Modulator Decay
				1,  8, ParameterType.Number, 4, 0, 4),
			new ParameterInfo(
				"", new Point(), // Modulator Sustain
				1,  9, ParameterType.Number, 6, 4, 4, invertInterpretation: true),
			new ParameterInfo(
				"", new Point(), // Modulator Release
				1, 10, ParameterType.Number, 6, 0, 4),
			new ParameterInfo(
				"", new Point(), // Modulator Sustain Enabled
				1, 11, ParameterType.Boolean, 0, 5, 1),
			new ParameterInfo(
				"", new Point(), // Modulator Volume
				1, 12, ParameterType.Number, 2, 0, 6, invertInterpretation: true),

			new ParameterInfo(
				"Scale Envelope", new Point(43, 7),
				2,  7, ParameterType.Boolean, 1, 4, 1),
			new ParameterInfo(
				"Level Scaling", new Point(44, 8), // This is actually reversed bits...
				2,  8, ParameterType.Number, 3, 6, 2),
			new ParameterInfo(
				"Frequency Multiplier", new Point(37, 9),
				2,  9, ParameterType.Number, 1, 0, 4),
			new ParameterInfo(
				"Wave Select", new Point(46, 10),
				2, 10, ParameterType.Number, 9, 0, 3),
			new ParameterInfo(
				"Pitch Vibrato", new Point(44, 11),
				2, 11, ParameterType.Boolean, 1, 6, 1),
			new ParameterInfo(
				"Volume Vibrato", new Point(43, 12),
				2, 12, ParameterType.Boolean, 1, 7, 1),

			new ParameterInfo(
				"", new Point(), // Modulator Scale Envelope
				3,  7, ParameterType.Boolean, 0, 4, 1),
			new ParameterInfo(
				"", new Point(), // Modulator Level Scaling
				3,  8, ParameterType.Number, 2, 6, 2),
			new ParameterInfo(
				"", new Point(), // Modulator Frequency Multiplier
				3,  9, ParameterType.Number, 0, 0, 4),
			new ParameterInfo(
				"", new Point(), // Modulator Wave Select
				3, 10, ParameterType.Number, 8, 0, 3),
			new ParameterInfo(
				"", new Point(), // Modulator Pitch Vibrato
				3, 11, ParameterType.Boolean, 0, 6, 1),
			new ParameterInfo(
				"", new Point(), // Modulator Volume Vibrato
				3, 12, ParameterType.Boolean, 0, 7, 1),
		];

	SongSample _sample;
	VGAMemOverlay _sampleImage;
	byte[] _adlibBytes;

	public SongSample Sample => _sample;
	public byte[] AdLibBytes => _adlibBytes;

	public AdLibConfigDialog(int sampleNumber, VGAMemOverlay sampleImage)
		: base(new Point(9, 30), new Size(61, 15))
	{
		_sample = Song.CurrentSong.EnsureSample(sampleNumber);
		_sampleImage = sampleImage;
		_adlibBytes = _sample.AdLibBytes?.ToArray() ?? new byte[12];
	}

	protected override void Initialize()
	{
		//page.HelpIndex = HelpPages.AdlibSample;
		// Eh, what page? Where am I supposed to get a reference to page?
		// How do I make this work? -Bisqwit

		foreach (var parameter in Parameters)
		{
			if (parameter.LabelOnly)
				continue;

			int srcValue = _adlibBytes[parameter.ByteNumber];

			int minValue = 0;
			int maxValue = (1 << parameter.NumBits) - 1;

			srcValue >>= parameter.FirstBit;
			srcValue &= maxValue;

			if (parameter.InvertInterpretation)
				srcValue = maxValue - srcValue; // reverse the semantics

			Widget widget;

			switch (parameter.Type)
			{
				case ParameterType.Boolean:
				{
					var menuToggleWidget = new MenuToggleWidget(
						new Point(ColumnXPos[parameter.ControlColumnNumber], parameter.ControlRow + 30),
						YNToggle);

					foreach (var choice in menuToggleWidget.Choices)
						choice.ActivationKey = choice.Label[0];
					menuToggleWidget.State = srcValue;
					menuToggleWidget.Changed += () => UpdateConfiguration(parameter, menuToggleWidget.State);

					widget = menuToggleWidget;

					break;
				}
				case ParameterType.Number:
				{
					var numberEntryWidget = new NumberEntryWidget(
						new Point(ColumnXPos[parameter.ControlColumnNumber], parameter.ControlRow + 30),
						parameter.NumBits < 4 ? 1 : 2,
						minValue, maxValue,
						_columnCursorPos[parameter.ControlColumnNumber]);

					numberEntryWidget.Value = srcValue;
					numberEntryWidget.Changed += () => UpdateConfiguration(parameter, numberEntryWidget.Value);

					widget = numberEntryWidget;
					break;
				}
				default: throw new Exception("Internal error");
			}

			AddWidget(widget);
		}
	}

	void UpdateConfiguration(ParameterInfo parameter, int value)
	{
		VGAMem.DrawSampleData(_sampleImage, _sample);

		int srcValue = value;
		int maskValue = 0xFFFF;
		int maxValue = (1 << parameter.NumBits) - 1;

		if (parameter.InvertInterpretation)
			srcValue = maxValue - srcValue; // reverse the semantics

		srcValue  &= maxValue; srcValue  <<= parameter.FirstBit;
		maskValue &= maxValue; maskValue <<= parameter.FirstBit;

		_adlibBytes[parameter.ByteNumber] = (byte)
			((_adlibBytes[parameter.ByteNumber] & ~maskValue) | srcValue);
	}

	public override void DrawConst()
	{
		// 39 33
		VGAMem.DrawBox(new Point(38, 2 + 30), new Point(40, 5 + 30), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawFillCharacters(new Point(25, 6 + 30), new Point(32, 13 + 30), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(25, 6 + 30), new Point(28, 13 + 30), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(29, 6 + 30), new Point(32, 13 + 30), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawFillCharacters(new Point(57, 6 + 30), new Point(64, 13 + 30), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(57, 6 + 30), new Point(60, 13 + 30), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(61, 6 + 30), new Point(64, 13 + 30), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		foreach (var parameter in Parameters)
			if (!string.IsNullOrEmpty(parameter.Label))
				VGAMem.DrawText(parameter.Label, parameter.LabelPosition.Advance(0, 30), parameter.IsHeading ? (0, 2) : (3, 2));
	}

	public event Action? F1Pressed;

	public override bool HandleKey(KeyEvent kk)
	{
		if (kk.Sym == KeySym.F1)
		{
			if (kk.State == KeyState.Press)
				return true;

			F1Pressed?.Invoke();

			return true;
		}
		return false;
	}
}
