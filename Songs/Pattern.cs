using System.Collections.Generic;

namespace ChasmTracker.Songs;

public class Pattern
{
	public List<PatternRow> Rows;

	public Pattern() : this(Constants.DefaultPatternLength) { }

	public Pattern(int length)
	{
		Rows = new List<PatternRow>();

		for (int i = 0; i < length; i++)
			Rows.Add(new PatternRow());
	}

	public bool IsEmpty
	{
		get
		{
			if (Rows.Count != 64)
				return false;

			foreach (var row in Rows)
				if (!row.IsEmpty)
					return false;

			return true;
		}
	}

	public Pattern Clone()
	{
		var clone = new Pattern();

		clone.Rows.Clear();

		foreach (var row in Rows)
			clone.Rows.Add(row.Clone());

		return clone;
	}

	public PatternRow this[int row] => Rows[row];

	public static readonly Pattern Empty = new Pattern();

	public Pattern(IEnumerable<SongNote> data, int numChannels)
		: this()
	{
		int x = 0;
		int y = 0;

		foreach (var note in data)
		{
			Rows[y][x] = note;

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
