#requires -Version 7.0
[CmdletBinding()]
param(
    [string] $Root = ".",

    [string] $MetadataScriptPath,

    [string[]] $Path = @(),

    [string] $PathListPath,

    [string] $EventName,

    [string] $EventPayloadPath,

    [string] $BaseSha,

    [string] $HeadSha,

    [string] $Repository = $env:GITHUB_REPOSITORY,

    [string] $ServerUrl = $env:GITHUB_SERVER_URL,

    [string] $OutputPath = "doc-metadata-links.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FromRoot {
    param(
        [string] $RootPath,
        [string] $InputPath
    )

    if ([string]::IsNullOrWhiteSpace($InputPath)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($InputPath)) {
        return [System.IO.Path]::GetFullPath($InputPath)
    }

    [System.IO.Path]::GetFullPath((Join-Path $RootPath $InputPath))
}

function Normalize-RepoPath {
    param([string] $PathValue)

    $normalized = $PathValue.Replace("\", "/")
    while ($normalized.StartsWith("./", [System.StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }

    $normalized.TrimStart("/")
}

function Invoke-GitLines {
    param(
        [string] $RootPath,
        [string[]] $Arguments,
        [bool] $AllowFailure = $false
    )

    $output = & git -C $RootPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "git $($Arguments -join ' ') failed: $($output -join "`n")"
    }

    @($output | ForEach-Object { [string] $_ })
}

function Get-CommitParents {
    param(
        [string] $RootPath,
        [string] $CommitSha
    )

    $line = (Invoke-GitLines -RootPath $RootPath -Arguments @("rev-list", "--parents", "-n", "1", $CommitSha) | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($line)) {
        return @()
    }

    $tokens = @($line -split "\s+" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($tokens.Count -le 1) {
        return @()
    }

    @($tokens | Select-Object -Skip 1)
}

function Read-EventPayload {
    param([string] $PayloadPath)

    if ([string]::IsNullOrWhiteSpace($PayloadPath) -or -not (Test-Path -LiteralPath $PayloadPath -PathType Leaf)) {
        return $null
    }

    Get-Content -LiteralPath $PayloadPath -Raw | ConvertFrom-Json -Depth 64
}

function Get-ComparisonRange {
    param(
        [string] $RootPath,
        [string] $RequestedEventName,
        [string] $RequestedEventPayloadPath,
        [string] $RequestedBaseSha,
        [string] $RequestedHeadSha
    )

    $payload = Read-EventPayload -PayloadPath $RequestedEventPayloadPath

    if ($RequestedEventName -eq "pull_request") {
        $base = if ($null -ne $payload -and $null -ne $payload.pull_request) { [string] $payload.pull_request.base.sha } else { $RequestedBaseSha }
        $head = if ($null -ne $payload -and $null -ne $payload.pull_request) { [string] $payload.pull_request.head.sha } else { $RequestedHeadSha }
        if ([string]::IsNullOrWhiteSpace($base) -or [string]::IsNullOrWhiteSpace($head)) {
            return [pscustomobject]@{ Base = $null; Head = $null; HasRange = $false; Reason = "pull_request base/head SHAs are unavailable" }
        }

        $mergeBase = (Invoke-GitLines -RootPath $RootPath -Arguments @("merge-base", $base, $head) | Select-Object -First 1).Trim()
        return [pscustomobject]@{ Base = $mergeBase; Head = $head; HasRange = $true; Reason = "pull_request merge-base..head" }
    }

    if ($RequestedEventName -eq "push") {
        $before = if ($null -ne $payload) { [string] $payload.before } else { $RequestedBaseSha }
        $after = if ($null -ne $payload -and $payload.after) { [string] $payload.after } else { $RequestedHeadSha }
        if ([string]::IsNullOrWhiteSpace($after)) {
            return [pscustomobject]@{ Base = $null; Head = $null; HasRange = $false; Reason = "push head SHA is unavailable" }
        }

        $base = if (-not [string]::IsNullOrWhiteSpace($before) -and $before -notmatch "^0{40}$") { $before } else { $null }
        return [pscustomobject]@{ Base = $base; Head = $after; HasRange = $true; Reason = "push before..after" }
    }

    if (-not [string]::IsNullOrWhiteSpace($RequestedBaseSha) -and -not [string]::IsNullOrWhiteSpace($RequestedHeadSha)) {
        return [pscustomobject]@{ Base = $RequestedBaseSha; Head = $RequestedHeadSha; HasRange = $true; Reason = "explicit base..head" }
    }

    [pscustomobject]@{ Base = $null; Head = $null; HasRange = $false; Reason = "no safe comparison context" }
}

function Test-ManagedBodyChanged {
    param(
        [string] $RootPath,
        [string] $MetadataScript,
        [string] $RepoPath,
        [string] $Base,
        [string] $Head
    )

    $outputName = "doc-metadata-content-change-$([guid]::NewGuid().ToString('N')).json"
    $outputFullPath = Join-Path $RootPath $outputName
    $arguments = @(
        "-Mode", "ContentChanges",
        "-Root", $RootPath,
        "-Path", $RepoPath,
        "-HeadSha", $Head,
        "-ContentChangeOutputPath", $outputName
    )
    if (-not [string]::IsNullOrWhiteSpace($Base)) {
        $arguments += @("-BaseSha", $Base)
    }

    try {
        $output = & pwsh -NoLogo -NoProfile -File $MetadataScript @arguments 2>&1
        $exitCode = $LASTEXITCODE
        if ($output) {
            $output | Out-Host
        }
        if ($exitCode -ne 0) {
            throw "ContentChanges failed for '$RepoPath' at '$Head'."
        }

        $result = Get-Content -LiteralPath $outputFullPath -Raw | ConvertFrom-Json -Depth 16
        $entry = @($result.contentChanges | Where-Object { $_.path -eq $RepoPath } | Select-Object -First 1)
        if ($entry.Count -eq 0) {
            return $false
        }

        [bool] $entry[0].bodyChanged
    }
    finally {
        Remove-Item -LiteralPath $outputFullPath -Force -ErrorAction SilentlyContinue
    }
}

$rootFullPath = [System.IO.Path]::GetFullPath($Root)
$metadataScriptFullPath = Resolve-FromRoot -RootPath $rootFullPath -InputPath $MetadataScriptPath
if ($null -eq $metadataScriptFullPath) {
    $metadataScriptFullPath = Join-Path $rootFullPath ".github/scripts/doc-metadata/update-doc-metadata.ps1"
}
if (-not (Test-Path -LiteralPath $metadataScriptFullPath -PathType Leaf)) {
    throw "Metadata script '$metadataScriptFullPath' was not found."
}

$outputFullPath = Resolve-FromRoot -RootPath $rootFullPath -InputPath $OutputPath
$paths = [System.Collections.Generic.List[string]]::new()
foreach ($pathValue in @($Path)) {
    if (-not [string]::IsNullOrWhiteSpace($pathValue)) {
        $paths.Add((Normalize-RepoPath $pathValue))
    }
}

$pathListFullPath = Resolve-FromRoot -RootPath $rootFullPath -InputPath $PathListPath
if ($null -ne $pathListFullPath -and (Test-Path -LiteralPath $pathListFullPath -PathType Leaf)) {
    foreach ($pathValue in @(Get-Content -LiteralPath $pathListFullPath)) {
        if (-not [string]::IsNullOrWhiteSpace($pathValue)) {
            $paths.Add((Normalize-RepoPath $pathValue))
        }
    }
}

$uniquePaths = @($paths.ToArray() | Sort-Object -Unique)
$links = [ordered]@{}
$range = Get-ComparisonRange -RootPath $rootFullPath -RequestedEventName $EventName -RequestedEventPayloadPath $EventPayloadPath -RequestedBaseSha $BaseSha -RequestedHeadSha $HeadSha
$server = if ([string]::IsNullOrWhiteSpace($ServerUrl)) { "https://github.com" } else { $ServerUrl.TrimEnd("/") }

if ($range.HasRange -and -not [string]::IsNullOrWhiteSpace($Repository)) {
    foreach ($repoPath in $uniquePaths) {
        $commits = if (-not [string]::IsNullOrWhiteSpace($range.Base)) {
            Invoke-GitLines -RootPath $rootFullPath -Arguments @("rev-list", "$($range.Base)..$($range.Head)", "--", $repoPath)
        }
        else {
            Invoke-GitLines -RootPath $rootFullPath -Arguments @("rev-list", $range.Head, "--", $repoPath)
        }

        foreach ($commit in @($commits | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
            $parents = @(Get-CommitParents -RootPath $rootFullPath -CommitSha $commit)
            if ($parents.Count -gt 1) {
                Write-Host "Skipping ambiguous merge commit '$commit' for '$repoPath'."
                continue
            }

            $baseForCommit = if ($parents.Count -eq 1) { [string] $parents[0] } else { $null }
            if (Test-ManagedBodyChanged -RootPath $rootFullPath -MetadataScript $metadataScriptFullPath -RepoPath $repoPath -Base $baseForCommit -Head $commit) {
                $links[$repoPath] = [ordered]@{
                    path = $repoPath
                    url = "$server/$Repository/commit/$commit"
                    linkText = "View Commit"
                    context = "$EventName`:$commit"
                    commitSha = $commit
                    bodyChanged = $true
                }
                break
            }
        }
    }
}

$links | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputFullPath -Encoding utf8NoBOM
