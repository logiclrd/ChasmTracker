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

	public void Resize(int numRows)
	{
		if (Rows.Count > numRows)
			Rows.RemoveRange(numRows, Rows.Count - numRows);

		while (Rows.Count < numRows)
			Rows.Add(new PatternRow());
	}
}
