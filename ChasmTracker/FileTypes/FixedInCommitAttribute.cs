using System;

namespace ChasmTracker.FileTypes;

public class FixedInCommitAttribute : Attribute
{
	public readonly string Repository;
	public readonly string CommitHash;

	public FixedInCommitAttribute(string repository, string commitHash)
	{
		Repository = repository;
		CommitHash = commitHash;
	}
}
