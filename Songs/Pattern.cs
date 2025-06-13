using System.Collections.Generic;

namespace ChasmTracker.Songs;

public class Pattern
{
	public List<PatternRow> Rows;

	public Pattern()
	{
		Rows = new List<PatternRow>();

		for (int i = 0; i < Constants.DefaultPatternLength; i++)
			Rows.Add(new PatternRow());
	}

	public Pattern(IEnumerable<SongNote> data, int numChannels)
		: this()
	{
		int x = 0;
		int y = 0;

		foreach (var note in data)
		{
			if (x < Rows[y].Notes.Length)
				Rows[y].Notes[x] = note;

			x++;

			if (x >= numChannels)
			{
				x = 0;
				y++;

				if (y >= Rows.Count)
					break;
			}
		}
	}

	public void Resize(int numRows)
	{
		if (Rows.Count > numRows)
			Rows.RemoveRange(numRows, Rows.Count - numRows);

		while (Rows.Count < numRows)
			Rows.Add(new PatternRow());
	}
}
