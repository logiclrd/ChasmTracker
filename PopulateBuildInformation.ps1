$timestamp = "built $([System.DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))"
$commit = $(. git log --pretty=format:'%H' -n 1 2> $null)

. git status --porcelain=v1 2> $null | grep -q .

$pristine = ($LASTEXITCODE -ne 0)

$info = (Get-Content BuildInformation.cs.template)

$info = $info.Replace("TIMESTAMP", $timestamp)
$info = $info.Replace("COMMIT", $commit)
$info = $info.Replace("PRISTINE", $pristine)

Set-Content BuildInformation.cs $info

Write-Output "Updated BuildInformation.cs"