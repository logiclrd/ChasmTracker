function ProcessCommit($commit)
{
  Write-Host "process $commit"

  $commitMessage = (git log $commit -n 1 --pretty=format:%B)
  $trailers = (git log $commit -n 1 --format="%(trailers)")
  $author = (git show $commit --format=short)[1]

  if ($author.StartsWith("Author: "))
  {
    $author = $author.Substring(8)
  }

  if ($commitMessage.Length -eq 0)
  {
    Write-Host "empty commit message?"
    return
  }

  $coAuthors = @()

  foreach ($trailer in $trailers)
  {
    if ($trailer.StartsWith("Co-Authored-By:", [StringComparison]::InvariantCultureIgnoreCase))
    {
      $coAuthor = $trailer.Substring(14).TrimStart()

      if (!$coAuthors.Contains($coAuthor))
      {
        $coAuthors += $coAuthor
      }
    }
  }

  $otherRepoCommit = $commitMessage[0]

  $haveNewCoAuthor = $false

  if ($otherRepoCommit -match "^[0-9a-f]{40}$")
  {
    Write-Host "investigating schismtracker commit: $otherRepoCommit"

    Push-Location /code/schismtracker

    $originalAuthor = (git show $otherRepoCommit --format=short)[1]
    $originalTrailers = (git log $otherRepoCommit -n 1 --format="%(trailers)")

    if ($originalAuthor.StartsWith("Author: "))
    {
      $originalAuthor = $originalAuthor.Substring(8)
    }

    $originalAuthor = $originalAuthor.Replace("JonathanG@iQmetrix.com", "logic@deltaq.org")

    Pop-Location

    Write-Host "=> author: $originalAuthor"
    Write-Host "=> trailers: $originalTrailers"

    if (!$coAuthors.Contains($originalAuthor) -and ($originalAuthor -ne $author))
    {
      $haveNewCoAuthor = $true
      $coAuthors += $originalAuthor
    }

    foreach ($trailer in $originalTrailers)
    {
      if ($trailer.StartsWith("Co-Authored-By:", [StringComparison]::InvariantCultureIgnoreCase))
      {
        $coAuthor = $trailer.Substring(14).TrimStart()

        if (!$coAuthors.Contains($coAuthor) -and ($originalAuthor -ne $author))
        {
          $haveNewCoAuthor = $true
          $coAuthors += $coAuthor
        }
      }
    }
  }

  if ($haveNewCoAuthor)
  {
    Write-Host "update co-authors"

    $commitMessage += ""
    foreach ($coAuthor in $coAuthors)
    {
      $commitMessage += "Co-Authored-By: $coAuthor"
    }

    git cherry-pick --no-commit $commit
    git commit -m "$($commitMessage -join [Environment]::NewLine)"
  }
  else
  {
    Write-Host "propagate commit no changes"

    $env:GIT_COMMITTER_DATE = (git show -s "--format=%aD" $commit)
    $env:GIT_COMMITTER_NAME = (git show -s "--format=%aN" $commit)
    $env:GIT_COMMITTER_EMAIL = (git show -s "--format=%aE" $commit)

    git cherry-pick --keep-redundant-commits --allow-empty --no-edit $commit

    $env:GIT_COMMITTER_DATE = $null
    $env:GIT_COMMITTER_NAME = $null
    $env:GIT_COMMITTER_EMAIL = $null
  }
}

function ProcessAllCommits($startCommit)
{
  git checkout $startCommit

  $commits = (git log "$startCommit..main" --pretty=format:%H)
  [Array]::Reverse($commits)

  foreach ($commit in $commits)
  {
    ProcessCommit $commit
  }
}

ProcessAllCommits "99cdcbebf01eae1162061cf4890ef1c803f119ed"
