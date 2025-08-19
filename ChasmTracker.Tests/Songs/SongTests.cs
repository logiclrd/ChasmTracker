namespace ChasmTracker.Tests.Songs;

using System;
using System.Linq;
using ChasmTracker.Songs;
using FluentAssertions;
using NUnit.Framework;

[TestFixture]
public class SongTests
{
	static int[] TestPatternLength = { 32, 15, 64, 1, 64, 0, 0 };

	Song CreateSubject()
	{
		var ret = new Song();

		for (int i=0; TestPatternLength[i] != 0; i++)
			ret.GetPattern(i, create: true, rowsInNewPattern: TestPatternLength[i]);

		return ret;
	}

	void TestGetPatternOffsetWithHooks(
		int startPatternNumber, int startRowNumber, // for arrange
		Action<Song>? preAct,
		int testOffset, // for act
		Action<Song>? postAct,
		int expectedPatternNumber, int expectedRowNumber) // for assert
	{
		// Arrange
		var csf = CreateSubject();

		var pattern = csf.GetPattern(startPatternNumber);

		if (pattern == null)
			throw new InconclusiveException("Subject did not have the expected pattern");
		if (pattern.Rows.Count != TestPatternLength[startPatternNumber])
			throw new InconclusiveException("Subject did not have the expected pattern length");

		int patternNumber = startPatternNumber;
		int rowNumber = startRowNumber;

		// Hook
		preAct?.Invoke(csf);

		// Act
		var newPattern = csf.GetPatternOffset(ref patternNumber, ref rowNumber, testOffset);

		// Hook
		postAct?.Invoke(csf);

		// Assert
		if (expectedPatternNumber < 0) /* expect failure */
			newPattern.Should().BeNull();
		else
		{
			patternNumber.Should().Be(expectedPatternNumber);
			rowNumber.Should().Be(expectedRowNumber);
			newPattern.Should().Be(csf.Patterns[expectedPatternNumber]);
			newPattern.Rows.Count.Should().Be(TestPatternLength[expectedPatternNumber]);
		}
	}

	void TestGetPatternOffset(
		int startPatternNumber, int startRowNumber, // for arrange
		int testOffset, // for act
		int expectedPatternNumber, int expectedRowNumber) // for assert
	{
		TestGetPatternOffsetWithHooks(
			startPatternNumber, startRowNumber,
			preAct: null,
			testOffset,
			postAct: null,
			expectedPatternNumber, expectedRowNumber);
	}

	[Test]
	public void GetPatternOffset_0()
	{
		TestGetPatternOffset(
			0, 15,  // starting from 0:15
			0,      // advance by 0 rows
			0, 15); // expect to be at 0:15
	}

	[Test]
	public void GetPatternOffset_SamePattern_1()
	{
		TestGetPatternOffset(
			0, 15,  // starting from 0:15
			1,      // advance by 0 rows
			0, 16); // expect to be at 0:16
	}

	[Test]
	public void GetPatternOffset_SamePattern_n()
	{
		TestGetPatternOffset(
			0, 15,  // starting from
			10,     // advance by
			0, 25); // expect to be at
	}

	[Test]
	public void GetPatternOffset_SamePattern_LAST()
	{
		TestGetPatternOffset(
			0, 15,  // starting from
			16,     // advance by
			0, 31); // expect to be at
	}

	[Test]
	public void GetPatternOffset_NextPattern_FIRST()
	{
		TestGetPatternOffset(
			0, 15,  // starting from
			17,     // advance by
			1, 0); // expect to be at
	}

	[Test]
	public void GetPatternOffset_NextPattern_n()
	{
		TestGetPatternOffset(
			0, 15,  // starting from
			27,     // advance by
			1, 10); // expect to be at
	}

	[Test]
	public void GetPatternOffset_NextPattern_LAST()
	{
		TestGetPatternOffset(
			0, 15,  // starting from
			31,     // advance by
			1, 14); // expect to be at
	}

	[Test]
	public void GetPatternOffset_MoreThanTwoPatterns()
	{
		TestGetPatternOffset(
			0, 15, // starting from
			96,    // advance by
			3, 0); // expect to be at
	}

	[Test]
	public void GetPatternOffset_FromMiddle_SamePattern()
	{
		TestGetPatternOffset(
			2, 15, // starting from
			2,    // advance by
			2, 17); // expect to be at
	}

	[Test]
	public void GetPatternOffset_FromMiddle_NextPattern()
	{
		TestGetPatternOffset(
			2, 15, // starting from
			49,    // advance by
			3, 0); // expect to be at
	}

	[Test]
	public void GetPatternOffset_FromMiddle_MoreThanTwoPatterns()
	{
		TestGetPatternOffset(
			2, 16, // starting from
			49,    // advance by
			4, 0); // expect to be at
	}

	[Test]
	public void GetPatternOffset_Song_LAST()
	{
		TestGetPatternOffset(
			2, 15,  // starting from
			113,    // advance by
			4, 63); // expect to be at
	}

	[Test]
	public void GetPatternOffset_PastEndOfSong()
	{
		void VerifyEndOfSong(Song csf)
		{
			csf.GetPatternCount().Should().BeLessThanOrEqualTo(5);

			if (csf.Patterns.Count > 5)
				csf.Patterns[5].Should().BeNull();
		}

		void LatchNewPatternLength(Song csf)
		{
			TestPatternLength[5] = csf.GetPatternLength(5);
		}

		try
		{
			TestGetPatternOffsetWithHooks(
				2, 15, // starting from
				VerifyEndOfSong, // pre hook
				114,   // advance by
				LatchNewPatternLength, // post hook
				5, 0); // expect to be at
		}
		finally
		{
			// restore pattern length array
			TestPatternLength[5] = 0;
		}
	}
}
