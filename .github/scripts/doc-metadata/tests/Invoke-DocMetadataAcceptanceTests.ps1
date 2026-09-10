#requires -Version 7.0
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\..\.."))
$ToolingSource = Join-Path $RepositoryRoot ".github\scripts\doc-metadata"
$PublicSurfaceSource = Join-Path $RepositoryRoot ".github\tools\doc-metadata"
$WorkflowPath = Join-Path $RepositoryRoot ".github\workflows\doc-metadata.yml"

$script:Passed = 0
$script:Failed = 0

function Assert-True {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) { throw $Message }
}

function Assert-Equal {
    param([object] $Expected, [object] $Actual, [string] $Message)
    if ($Expected -ne $Actual) { throw "$Message Expected '$Expected' but got '$Actual'." }
}

function Invoke-Test {
    param([string] $Name, [scriptblock] $Body)
    try {
        & $Body
        $script:Passed++
        Write-Host "PASS $Name"
    }
    catch {
        $script:Failed++
        Write-Host "FAIL $Name"
        Write-Host "  $($_.Exception.Message)"
    }
}

function Invoke-Process {
    param(
        [string] $FileName,
        [string[]] $Arguments,
        [string] $WorkingDirectory,
        [hashtable] $Environment = @{}
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) { [void] $startInfo.ArgumentList.Add($argument) }
    foreach ($key in $Environment.Keys) { $startInfo.Environment[$key] = [string] $Environment[$key] }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    [pscustomobject]@{ ExitCode = $process.ExitCode; Stdout = $stdout; Stderr = $stderr }
}

function Invoke-Git {
    param([string] $Root, [string[]] $Arguments)
    $result = Invoke-Process -FileName "git" -Arguments $Arguments -WorkingDirectory $Root
    if ($result.ExitCode -ne 0) { throw "git $($Arguments -join ' ') failed: $($result.Stderr)" }
    $result.Stdout
}

function New-TestRepository {
    $root = Join-Path ([System.IO.Path]::GetTempPath()) "doc-metadata-tests-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $root | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $root ".github\scripts") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $root ".github\tools") -Force | Out-Null
    Copy-Item -LiteralPath $ToolingSource -Destination (Join-Path $root ".github\scripts\doc-metadata") -Recurse
    Copy-Item -LiteralPath $PublicSurfaceSource -Destination (Join-Path $root ".github\tools\doc-metadata") -Recurse

    $manifestPath = Join-Path $root ".github\tools\doc-metadata\doc-metadata-manifest.json"
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 32
    $manifest.include = @("README.md", "docs/**/*", "AGENTS.*", "**/*.txt")
    $manifest.exclude = @()
    $manifest | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

    Invoke-Git -Root $root -Arguments @("init", "-q") | Out-Null
    Invoke-Git -Root $root -Arguments @("config", "user.email", "doc-tests@example.invalid") | Out-Null
    Invoke-Git -Root $root -Arguments @("config", "user.name", "Doc Metadata Tests") | Out-Null
    $root
}

function Write-Utf8File {
    param([string] $Path, [string] $Content)
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    Set-Content -LiteralPath $Path -Value $Content -NoNewline -Encoding utf8NoBOM
}

function Write-InvalidUtf8File {
    param([string] $Path)
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    [System.IO.File]::WriteAllBytes($Path, [byte[]]@(0xFF, 0xFE, 0xFD))
}

function Commit-All {
    param([string] $Root, [string] $Message = "test commit")
    Invoke-Git -Root $Root -Arguments @("add", ".") | Out-Null
    Invoke-Git -Root $Root -Arguments @("commit", "-q", "-m", $Message) | Out-Null
}

function Invoke-Tool {
    param(
        [string] $Root,
        [string] $Mode,
        [string[]] $ExtraArguments = @(),
        [hashtable] $Environment = @{}
    )

    $scriptPath = Join-Path $Root ".github\scripts\doc-metadata\update-doc-metadata.ps1"
    $arguments = @("-NoLogo", "-NoProfile", "-File", $scriptPath, "-Mode", $Mode, "-Root", $Root) + $ExtraArguments
    Invoke-Process -FileName "pwsh" -Arguments $arguments -WorkingDirectory $Root -Environment $Environment
}

function Invoke-Resolver {
    param(
        [string] $Root,
        [string[]] $ExtraArguments = @(),
        [hashtable] $Environment = @{}
    )

    $scriptPath = Join-Path $Root ".github\scripts\doc-metadata\resolve-content-change-links.ps1"
    $metadataScriptPath = Join-Path $Root ".github\scripts\doc-metadata\update-doc-metadata.ps1"
    $arguments = @("-NoLogo", "-NoProfile", "-File", $scriptPath, "-Root", $Root, "-MetadataScriptPath", $metadataScriptPath) + $ExtraArguments
    Invoke-Process -FileName "pwsh" -Arguments $arguments -WorkingDirectory $Root -Environment $Environment
}

function Read-JsonFile {
    param([string] $Path)
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 32
}

function Write-HistoryLinkMap {
    param(
        [string] $Root,
        [hashtable] $Links
    )

    $mapPath = Join-Path $Root "links.json"
    $Links | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $mapPath -Encoding utf8NoBOM
    $mapPath
}

function Get-MarkdownWithMetadata {
    param(
        [string] $Version = "1",
        [string] $Created = "2026-01-01T00:00:00+00:00",
        [string] $Updated = "2026-01-01T00:00:00+00:00",
        [string] $Author = "Doc Metadata Tests",
        [string] $Body = "# Title`n",
        [string] $CurrentChangesUrl = "",
        [string] $CurrentChangesLinkText = "View Commit",
        [string[]] $HistoryLines = @()
    )

    $currentLink = if ([string]::IsNullOrWhiteSpace($CurrentChangesUrl)) { "" } else { "[<b>$CurrentChangesLinkText</b>]($CurrentChangesUrl)`n`n" }
    $historyBlock = if ($HistoryLines.Count -gt 0) { ($HistoryLines -join "`n") + "`n`n" } else { "" }
    "---`nVersion: $Version`nCreated: $Created`nUpdated: $Updated`nAuthor: $Author`n---`n<!-- doc-metadata-presentation:start -->`n$currentLink<details>`n<summary>Change History</summary>`n`n$historyBlock</details>`n`n---`n`n<br>`n<br>`n<!-- doc-metadata-presentation:end -->`n`n$Body"
}

Invoke-Test "Bootstrap initializes Markdown with human metadata, Author, UTC, and rich presentation" {
    $root = New-TestRepository
    Write-Utf8File -Path (Join-Path $root "README.md") -Content "# Title`n"

    $result = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-ReportOutputPath", "report.json")

    Assert-Equal 0 $result.ExitCode "Bootstrap should pass."
    $content = Get-Content -LiteralPath (Join-Path $root "README.md") -Raw
    Assert-True ($content -match "Version: 1") "Version should be initialized."
    Assert-True ($content -match "Author: Doc Metadata Tests") "Author should use git config user.name."
    Assert-True ($content -match "Created: \d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\+00:00") "Created should be UTC +00:00."
    Assert-True ($content -match "<!-- doc-metadata-presentation:start -->") "Markdown presentation region should be generated."
    Assert-Equal 2 ([regex]::Matches($content, "(?m)^<br>$").Count) "Markdown spacingBreaks 2 should emit exactly two <br> lines."
    Assert-True ($content -match "<!-- doc-metadata-presentation:end -->\r?\n\r?\n# Title") "Generated Markdown should leave a blank physical line before the document heading."
    Assert-Equal 0 ([regex]::Matches($content, "(?m)^- Updated:").Count) "Metadata-only Bootstrap should not create content Change History entries."
    Assert-True ($content -notmatch "Changes: <b>Unavailable</b>") "Metadata-only Bootstrap must not generate deprecated Unavailable history entries."
    Assert-True ($content -notmatch "\[<b>View (?:Changes|Commit)</b>\]") "Metadata-only Bootstrap must not create a current-version link without reliable content context."
    $report = Read-JsonFile (Join-Path $root "report.json")
    Assert-Equal $null $report.updatedFiles[0].oldVersion "Initialized old version should be null."
    Assert-Equal "1" ([string] $report.updatedFiles[0].newVersion) "Initialized new version should be 1."
}

Invoke-Test "Plain text gets compact metadata, physical blank lines, and no HTML" {
    $root = New-TestRepository
    Write-Utf8File -Path (Join-Path $root "notes.txt") -Content "Document body starts here.`n"

    $result = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-Path", "notes.txt")

    Assert-Equal 0 $result.ExitCode "Bootstrap should pass."
    $content = Get-Content -LiteralPath (Join-Path $root "notes.txt") -Raw
    Assert-True ($content -match "Version: 1") "Text metadata should use human fields."
    Assert-True ($content -match "--------------------------------------------------------------------------------") "Text separator should be generated."
    Assert-True ($content -notmatch "<br>" -and $content -notmatch "<details>") "Text files must not receive Markdown/HTML presentation."
    Assert-True ($content -match "--------------------------------------------------------------------------------\r?\n\r?\n\r?\nDocument body starts here\.") "Text spacingBreaks 2 should emit exactly two physical blank lines."
}

Invoke-Test "Body change after dotted version increments first component and refreshes Author" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "2.1.2")
    Commit-All -Root $root
    $base = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()
    Invoke-Git -Root $root -Arguments @("config", "user.name", "Content Author") | Out-Null
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "2.1.2" -Body "# Title`nChanged.`n")
    Commit-All -Root $root -Message "content change"
    $bodyCommit = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()
    $linkMap = Write-HistoryLinkMap -Root $root -Links @{
        "README.md" = @{
            path = "README.md"
            url = "https://github.com/example/repo/commit/$bodyCommit"
            linkText = "View Commit"
            context = "local:$bodyCommit"
            commitSha = $bodyCommit
            bodyChanged = $true
        }
    }

    $result = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-BaseSha", $base, "-HeadSha", $bodyCommit, "-HistoryLinkMapPath", $linkMap, "-ReportOutputPath", "report.json") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-Equal 0 $result.ExitCode "Update should pass."
    $content = Get-Content -LiteralPath $readme -Raw
    Assert-True ($content -match "Version: 3") "Automatic increment should collapse 2.1.2 to 3."
    Assert-True ($content -match "Author: Content Author") "Author should refresh on body change."
    Assert-True ($content -match "\[<b>View Commit</b>\]\(https://github.com/example/repo/commit/$bodyCommit\)") "Current View Commit should point at the proven content-change commit URL."
    Assert-Equal 1 ([regex]::Matches($content, "(?m)^- Updated:").Count) "Body change should add exactly one newest history entry."
    $report = Read-JsonFile (Join-Path $root "report.json")
    Assert-Equal "2.1.2" ([string] $report.updatedFiles[0].oldVersion) "Report should include old dotted version."
    Assert-Equal "3" ([string] $report.updatedFiles[0].newVersion) "Report should include new major version."
}

Invoke-Test "Body change without reliable content context does not create a current link or history" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1")
    Commit-All -Root $root
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -Body "# Title`nChanged without link context.`n")

    $result = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md")

    Assert-Equal 0 $result.ExitCode "Body change should still be repairable without a reliable link context."
    $content = Get-Content -LiteralPath $readme -Raw
    Assert-True ($content -match "Version: 2") "Version should increment on body change."
    Assert-True ($content -notmatch "\[<b>View (?:Changes|Commit)</b>\]") "No current managed link should be generated without reliable content context."
    Assert-Equal 0 ([regex]::Matches($content, "(?m)^- Updated:").Count) "No new history entry should be added without reliable content context."
    Assert-True ($content -notmatch "Changes: <b>Unavailable</b>") "Body changes without reliable content context must not generate Unavailable history entries."
}

Invoke-Test "Body change without reliable content context clears stale current View Commit" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1")
    Commit-All -Root $root
    $base = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -Body "# Title`nContent version with proven link.`n")
    Commit-All -Root $root -Message "content version two"
    $bodyCommit = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()
    $linkMap = Write-HistoryLinkMap -Root $root -Links @{
        "README.md" = @{
            path = "README.md"
            url = "https://github.com/example/repo/commit/$bodyCommit"
            linkText = "View Commit"
            context = "local:$bodyCommit"
            commitSha = $bodyCommit
            bodyChanged = $true
        }
    }
    $linkedUpdate = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-BaseSha", $base, "-HeadSha", $bodyCommit, "-HistoryLinkMapPath", $linkMap) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }
    Assert-Equal 0 $linkedUpdate.ExitCode "Body update with proven link should pass."
    $linkedContent = Get-Content -LiteralPath $readme -Raw
    Assert-True ($linkedContent -match "(?m)^\[<b>View Commit</b>\]\(https://github.com/example/repo/commit/$bodyCommit\)$") "Version 2 should have a proven current View Commit link."
    Commit-All -Root $root -Message "metadata version two"

    $changedBody = (Get-Content -LiteralPath $readme -Raw).Replace("Content version with proven link.", "Content version without reliable link.")
    Write-Utf8File -Path $readme -Content $changedBody
    $invalidLinkMap = Write-HistoryLinkMap -Root $root -Links @{
        "README.md" = @{
            path = "docs/other.md"
            url = "https://github.com/example/repo/commit/$bodyCommit"
            linkText = "View Commit"
            context = "local:$bodyCommit"
            commitSha = $bodyCommit
            bodyChanged = $true
        }
    }

    $result = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-HistoryLinkMapPath", $invalidLinkMap) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-Equal 0 $result.ExitCode "Body change should remain repairable when the next content-change link proof is invalid."
    $content = Get-Content -LiteralPath $readme -Raw
    Assert-True ($content -match "Version: 3") "Version should increment for the body change."
    Assert-True ($content -notmatch "(?m)^\[<b>View Commit</b>\]") "The previous version's current View Commit link must be cleared."
    Assert-True ($content -match "https://github.com/example/repo/commit/$bodyCommit") "Existing history should still preserve the older proven content-change link."
    Assert-Equal 1 ([regex]::Matches($content, "(?m)^- Updated:").Count) "No new history entry should be added without reliable content context."
    Assert-True ($content -notmatch "Changes: <b>Unavailable</b>") "Invalid replacement proof must not fall back to Unavailable history entries."
}

Invoke-Test "Wrong-path and unsafe history link map entries are rejected without guessed fallback" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1")
    Commit-All -Root $root
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -Body "# Title`nChanged.`n")
    $wrongPathMap = Write-HistoryLinkMap -Root $root -Links @{
        "README.md" = @{
            path = "docs/other.md"
            url = "https://github.com/example/repo/commit/3333333333333333333333333333333333333333"
            linkText = "View Commit"
        }
    }

    $result = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-HistoryLinkMapPath", $wrongPathMap) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-Equal 0 $result.ExitCode "Wrong-path map should not make the body repair fail."
    $content = Get-Content -LiteralPath $readme -Raw
    Assert-True ($content -match "Version: 2") "Version should still increment."
    Assert-True ($content -notmatch "3333333333333333333333333333333333333333") "Wrong-path URL must not be emitted."

    Commit-All -Root $root
    Write-Utf8File -Path $readme -Content ((Get-Content -LiteralPath $readme -Raw) -replace "Changed\.", "Changed again.")
    $unsafeMap = Write-HistoryLinkMap -Root $root -Links @{
        "README.md" = @{
            path = "README.md"
            url = "javascript:alert(1)"
            linkText = "View Commit"
        }
    }

    $unsafeResult = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-HistoryLinkMapPath", $unsafeMap) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-Equal 0 $unsafeResult.ExitCode "Unsafe URL should be rejected/fallback without failing metadata repair."
    $unsafeContent = Get-Content -LiteralPath $readme -Raw
    Assert-True ($unsafeContent -notmatch "javascript:") "Unsafe URL must not be emitted."
}

Invoke-Test "History link map commit proof rejects wrong or incomplete proof" {
    function New-StaleBodyChangeRepository {
        $caseRoot = New-TestRepository
        $caseReadme = Join-Path $caseRoot "README.md"
        Write-Utf8File -Path $caseReadme -Content (Get-MarkdownWithMetadata -Version "1")
        Commit-All -Root $caseRoot
        $caseBase = (Invoke-Git -Root $caseRoot -Arguments @("rev-parse", "HEAD")).Trim()
        Write-Utf8File -Path $caseReadme -Content (Get-MarkdownWithMetadata -Version "1" -Body "# Title`nChanged body.`n")
        Commit-All -Root $caseRoot -Message "readme body"
        $caseBodyCommit = (Invoke-Git -Root $caseRoot -Arguments @("rev-parse", "HEAD")).Trim()
        Write-Utf8File -Path (Join-Path $caseRoot "docs\other.md") -Content "# Other`n"
        Commit-All -Root $caseRoot -Message "other body"
        $caseHead = (Invoke-Git -Root $caseRoot -Arguments @("rev-parse", "HEAD")).Trim()
        [pscustomobject]@{ Root = $caseRoot; Readme = $caseReadme; Base = $caseBase; BodyCommit = $caseBodyCommit; Head = $caseHead }
    }

    $wrongCommit = New-StaleBodyChangeRepository
    $wrongCommitMap = Write-HistoryLinkMap -Root $wrongCommit.Root -Links @{
        "README.md" = @{
            path = "README.md"
            url = "https://github.com/example/repo/commit/$($wrongCommit.Head)"
            linkText = "View Commit"
            commitSha = $wrongCommit.Head
            bodyChanged = $true
        }
    }
    $wrongCommitResult = Invoke-Tool -Root $wrongCommit.Root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-BaseSha", $wrongCommit.Base, "-HeadSha", $wrongCommit.Head, "-HistoryLinkMapPath", $wrongCommitMap) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }
    Assert-Equal 0 $wrongCommitResult.ExitCode "Wrong commit proof should not make metadata repair fail."
    Assert-True ((Get-Content -LiteralPath $wrongCommit.Readme -Raw) -notmatch $wrongCommit.Head) "Commit that did not change README managed body must not be emitted."

    $mismatch = New-StaleBodyChangeRepository
    $mismatchMap = Write-HistoryLinkMap -Root $mismatch.Root -Links @{
        "README.md" = @{
            path = "README.md"
            url = "https://github.com/example/repo/commit/$($mismatch.BodyCommit)"
            linkText = "View Commit"
            commitSha = $mismatch.Head
            bodyChanged = $true
        }
    }
    $mismatchResult = Invoke-Tool -Root $mismatch.Root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-BaseSha", $mismatch.Base, "-HeadSha", $mismatch.Head, "-HistoryLinkMapPath", $mismatchMap) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }
    Assert-Equal 0 $mismatchResult.ExitCode "Mismatched commitSha proof should not make metadata repair fail."
    Assert-True ((Get-Content -LiteralPath $mismatch.Readme -Raw) -notmatch $mismatch.BodyCommit) "Mismatched commitSha must reject the URL."

    $missingBodyChanged = New-StaleBodyChangeRepository
    $missingMap = Write-HistoryLinkMap -Root $missingBodyChanged.Root -Links @{
        "README.md" = @{
            path = "README.md"
            url = "https://github.com/example/repo/commit/$($missingBodyChanged.BodyCommit)"
            linkText = "View Commit"
            commitSha = $missingBodyChanged.BodyCommit
        }
    }
    $missingResult = Invoke-Tool -Root $missingBodyChanged.Root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-BaseSha", $missingBodyChanged.Base, "-HeadSha", $missingBodyChanged.Head, "-HistoryLinkMapPath", $missingMap) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }
    Assert-Equal 0 $missingResult.ExitCode "Missing bodyChanged proof should not make metadata repair fail."
    Assert-True ((Get-Content -LiteralPath $missingBodyChanged.Readme -Raw) -notmatch $missingBodyChanged.BodyCommit) "Missing bodyChanged proof must reject the URL."

    $falseBodyChanged = New-StaleBodyChangeRepository
    $falseMap = Write-HistoryLinkMap -Root $falseBodyChanged.Root -Links @{
        "README.md" = @{
            path = "README.md"
            url = "https://github.com/example/repo/commit/$($falseBodyChanged.BodyCommit)"
            linkText = "View Commit"
            commitSha = $falseBodyChanged.BodyCommit
            bodyChanged = $false
        }
    }
    $falseResult = Invoke-Tool -Root $falseBodyChanged.Root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-BaseSha", $falseBodyChanged.Base, "-HeadSha", $falseBodyChanged.Head, "-HistoryLinkMapPath", $falseMap) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }
    Assert-Equal 0 $falseResult.ExitCode "False bodyChanged proof should not make metadata repair fail."
    Assert-True ((Get-Content -LiteralPath $falseBodyChanged.Readme -Raw) -notmatch $falseBodyChanged.BodyCommit) "False bodyChanged proof must reject the URL."
}

Invoke-Test "Managed presentation URL validation rejects unrelated and generic repository URLs" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -CurrentChangesUrl "javascript:alert")

    $unsafeResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($unsafeResult.ExitCode -ne 0) "Unsafe URL schemes in managed presentation should fail validation."
    Assert-True ($unsafeResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -CurrentChangesUrl "../commit/4444444444444444444444444444444444444444")
    $relativeResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($relativeResult.ExitCode -ne 0) "Relative URLs in managed presentation should fail validation."
    Assert-True ($relativeResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -CurrentChangesUrl "https://github.com/example/repo/commit/4444444444444444444444444444444444444444")
    $missingIdentityResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md") -Environment @{ GITHUB_REPOSITORY = ""; GITHUB_SERVER_URL = "" }

    Assert-True ($missingIdentityResult.ExitCode -ne 0) "Managed history URLs should fail when repository identity cannot be resolved."
    Assert-True ($missingIdentityResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -CurrentChangesUrl "https://example.github.io/example/repo/commit/4444444444444444444444444444444444444444")
    $githubIoResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($githubIoResult.ExitCode -ne 0) "github.io URLs should fail managed history URL validation."
    Assert-True ($githubIoResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -CurrentChangesUrl "https://github.com/example/repo/tree/4444444444444444444444444444444444444444/README.md")
    $treeResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($treeResult.ExitCode -ne 0) "Tree URLs should fail because they are not file-specific changes URLs."
    Assert-True ($treeResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -CurrentChangesUrl "https://github.com/example/repo/blob/4444444444444444444444444444444444444444/README.md")
    $blobResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($blobResult.ExitCode -ne 0) "Blob URLs should fail because history entries must link to changes, not file-at-version views."
    Assert-True ($blobResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -CurrentChangesUrl "https://github.com/example/repo/compare/1111111111111111111111111111111111111111...2222222222222222222222222222222222222222" -CurrentChangesLinkText "View Changes")
    $compareResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($compareResult.ExitCode -ne 0) "Current View Changes compare URLs should fail until verified file-specific changes support exists."
    Assert-True ($compareResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."

    $historyCompare = Get-MarkdownWithMetadata -Version "1" -HistoryLines @("- Updated: <b>2026-01-01T00:00:00+00:00</b> | Author: <b>Doc Metadata Tests</b> | Changes: [<b>View Changes</b>](https://github.com/example/repo/compare/1111111111111111111111111111111111111111...2222222222222222222222222222222222222222)")
    Write-Utf8File -Path $readme -Content $historyCompare
    $historyCompareResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($historyCompareResult.ExitCode -ne 0) "History View Changes compare URLs should fail until verified file-specific changes support exists."
    Assert-True ($historyCompareResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -CurrentChangesUrl "https://github.com/other/repo/commit/4444444444444444444444444444444444444444")

    $result = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($result.ExitCode -ne 0) "Unrelated repository URLs in managed presentation should fail validation."
    Assert-True ($result.Stdout -match "managed history URL") "Failure should identify managed history URL validation."

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -CurrentChangesUrl "https://github.com/example/repo")
    $genericResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($genericResult.ExitCode -ne 0) "Generic repository home URLs should fail validation."
    Assert-True ($genericResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."
}

Invoke-Test "Managed presentation links require View Commit and body-change proof" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1")
    Commit-All -Root $root
    $base = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -Body "# Title`nBody changed for link proof.`n")
    Commit-All -Root $root -Message "readme body proof"
    $bodyCommit = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "2" -Updated "2026-01-02T00:00:00+00:00" -Body "# Title`nBody changed for link proof.`n" -CurrentChangesUrl "https://github.com/example/repo/commit/$bodyCommit" -CurrentChangesLinkText "View Commit")
    $validResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md", "-BaseSha", $base, "-HeadSha", $bodyCommit) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-Equal 0 $validResult.ExitCode "Proven View Commit link should pass managed presentation URL validation."

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "2" -Updated "2026-01-02T00:00:00+00:00" -Body "# Title`nBody changed for link proof.`n" -CurrentChangesUrl "https://github.com/example/repo/commit/$bodyCommit" -CurrentChangesLinkText "View Changes")
    $viewChangesResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md", "-BaseSha", $base, "-HeadSha", $bodyCommit) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($viewChangesResult.ExitCode -ne 0) "Commit URLs labeled View Changes should fail until verified file-specific changes support exists."
    Assert-True ($viewChangesResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."

    Write-Utf8File -Path (Join-Path $root "docs\other.md") -Content "# Other`n"
    Commit-All -Root $root -Message "other document"
    $otherCommit = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "2" -Updated "2026-01-02T00:00:00+00:00" -Body "# Title`nBody changed for link proof.`n" -CurrentChangesUrl "https://github.com/example/repo/commit/$otherCommit" -CurrentChangesLinkText "View Commit")
    $unprovenCommitResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md", "-BaseSha", $base, "-HeadSha", $bodyCommit) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($unprovenCommitResult.ExitCode -ne 0) "Same-repo commit URLs should fail when the commit did not change the governed file body."
    Assert-True ($unprovenCommitResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."

    $historyViewChanges = Get-MarkdownWithMetadata -Version "2" -Updated "2026-01-02T00:00:00+00:00" -Body "# Title`nBody changed for link proof.`n" -HistoryLines @("- Updated: <b>2026-01-02T00:00:00+00:00</b> | Author: <b>Doc Metadata Tests</b> | Changes: [<b>View Changes</b>](https://github.com/example/repo/commit/$bodyCommit)")
    Write-Utf8File -Path $readme -Content $historyViewChanges
    $historyResult = Invoke-Tool -Root $root -Mode "Check" -ExtraArguments @("-Path", "README.md", "-BaseSha", $base, "-HeadSha", $bodyCommit) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-True ($historyResult.ExitCode -ne 0) "History entries labeled View Changes should fail until verified file-specific changes support exists."
    Assert-True ($historyResult.Stdout -match "managed history URL") "Failure should identify managed history URL validation."
}

Invoke-Test "ContentChanges mode uses managed-body semantics for multi-commit ranges" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1")
    Commit-All -Root $root
    $base = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -Body "# Title`nBody change in earlier commit.`n")
    Commit-All -Root $root -Message "body change"
    $bodyCommit = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()

    Write-Utf8File -Path (Join-Path $root "docs\other.md") -Content "# Other`n"
    Commit-All -Root $root -Message "unrelated head"
    $headCommit = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()

    $bodyResult = Invoke-Tool -Root $root -Mode "ContentChanges" -ExtraArguments @("-Path", "README.md", "-BaseSha", $base, "-HeadSha", $bodyCommit, "-ContentChangeOutputPath", "body-change.json")
    $headResult = Invoke-Tool -Root $root -Mode "ContentChanges" -ExtraArguments @("-Path", "README.md", "-BaseSha", $bodyCommit, "-HeadSha", $headCommit, "-ContentChangeOutputPath", "head-change.json")

    Assert-Equal 0 $bodyResult.ExitCode "ContentChanges should classify the body-changing commit."
    Assert-Equal 0 $headResult.ExitCode "ContentChanges should classify the non-body-changing head commit."
    $bodyJson = Read-JsonFile (Join-Path $root "body-change.json")
    $headJson = Read-JsonFile (Join-Path $root "head-change.json")
    Assert-True ([bool] $bodyJson.contentChanges[0].bodyChanged) "Earlier content-changing commit should be detected."
    Assert-True (-not [bool] $headJson.contentChanges[0].bodyChanged) "Later unrelated head commit should not be treated as a body change."
}

Invoke-Test "ContentChanges mode treats root new-file commits as introduced content" {
    $root = New-TestRepository
    Write-Utf8File -Path (Join-Path $root "README.md") -Content "# Root file`n"
    Commit-All -Root $root -Message "root file"
    $rootCommit = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()

    $result = Invoke-Tool -Root $root -Mode "ContentChanges" -ExtraArguments @("-Path", "README.md", "-HeadSha", $rootCommit, "-ContentChangeOutputPath", "root-change.json")

    Assert-Equal 0 $result.ExitCode "Root commit content classification should pass."
    $json = Read-JsonFile (Join-Path $root "root-change.json")
    Assert-True ([bool] $json.contentChanges[0].newFile) "Root commit should report the file as newly introduced."
    Assert-True ([bool] $json.contentChanges[0].bodyChanged) "Root commit should report introduced content as a body change."
}

Invoke-Test "Resolver maps each file to newest body-changing commit, not unrelated head" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1")
    Commit-All -Root $root
    $base = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -Body "# Title`nBody change in commit A.`n")
    Commit-All -Root $root -Message "commit A readme"
    $commitA = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()
    Write-Utf8File -Path (Join-Path $root "docs\other.md") -Content "# Other`n"
    Commit-All -Root $root -Message "commit B other"
    $commitB = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()
    "README.md" | Set-Content -LiteralPath (Join-Path $root "paths.txt") -Encoding utf8NoBOM
    @{ before = $base; after = $commitB } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root "event.json") -Encoding utf8NoBOM

    $result = Invoke-Resolver -Root $root -ExtraArguments @("-PathListPath", "paths.txt", "-EventName", "push", "-EventPayloadPath", "event.json", "-Repository", "example/repo", "-ServerUrl", "https://github.com", "-OutputPath", "links.json")

    Assert-Equal 0 $result.ExitCode "Resolver should pass."
    $links = Read-JsonFile (Join-Path $root "links.json")
    $entry = $links.'README.md'
    Assert-Equal $commitA $entry.commitSha "Resolver should choose commit A for README, not unrelated commit B."
    Assert-True ([bool] $entry.bodyChanged) "Resolver should emit bodyChanged proof."
    Assert-Equal "View Commit" $entry.linkText "Resolver should label commit fallback links as View Commit."
}

Invoke-Test "Resolver handles root introductions and skips ambiguous merge commits" {
    $root = New-TestRepository
    Write-Utf8File -Path (Join-Path $root "README.md") -Content "# Root file`n"
    Commit-All -Root $root -Message "root readme"
    $rootCommit = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()
    "README.md" | Set-Content -LiteralPath (Join-Path $root "paths.txt") -Encoding utf8NoBOM
    @{ before = "0000000000000000000000000000000000000000"; after = $rootCommit } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root "event.json") -Encoding utf8NoBOM

    $rootResult = Invoke-Resolver -Root $root -ExtraArguments @("-PathListPath", "paths.txt", "-EventName", "push", "-EventPayloadPath", "event.json", "-Repository", "example/repo", "-ServerUrl", "https://github.com", "-OutputPath", "links.json")

    Assert-Equal 0 $rootResult.ExitCode "Resolver should pass for root introduction."
    $rootLinks = Read-JsonFile (Join-Path $root "links.json")
    Assert-Equal $rootCommit $rootLinks.'README.md'.commitSha "Resolver should map a root file introduction to the root commit."

    $mergeRoot = New-TestRepository
    $mergeReadme = Join-Path $mergeRoot "README.md"
    Write-Utf8File -Path $mergeReadme -Content (Get-MarkdownWithMetadata -Version "1")
    Commit-All -Root $mergeRoot -Message "base"
    $mergeBase = (Invoke-Git -Root $mergeRoot -Arguments @("rev-parse", "HEAD")).Trim()
    Invoke-Git -Root $mergeRoot -Arguments @("checkout", "-q", "-b", "side") | Out-Null
    Write-Utf8File -Path (Join-Path $mergeRoot "docs\side.md") -Content "# Side`n"
    Commit-All -Root $mergeRoot -Message "side only"
    Invoke-Git -Root $mergeRoot -Arguments @("checkout", "-q", "master") | Out-Null
    Write-Utf8File -Path (Join-Path $mergeRoot "docs\main.md") -Content "# Main`n"
    Commit-All -Root $mergeRoot -Message "main only"
    Invoke-Git -Root $mergeRoot -Arguments @("merge", "--no-ff", "--no-commit", "side") | Out-Null
    Write-Utf8File -Path $mergeReadme -Content (Get-MarkdownWithMetadata -Version "1" -Body "# Title`nMerge-only body change.`n")
    Commit-All -Root $mergeRoot -Message "ambiguous merge"
    $mergeCommit = (Invoke-Git -Root $mergeRoot -Arguments @("rev-parse", "HEAD")).Trim()
    "README.md" | Set-Content -LiteralPath (Join-Path $mergeRoot "paths.txt") -Encoding utf8NoBOM
    @{ before = $mergeBase; after = $mergeCommit } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $mergeRoot "event.json") -Encoding utf8NoBOM

    $mergeResult = Invoke-Resolver -Root $mergeRoot -ExtraArguments @("-PathListPath", "paths.txt", "-EventName", "push", "-EventPayloadPath", "event.json", "-Repository", "example/repo", "-ServerUrl", "https://github.com", "-OutputPath", "links.json")

    Assert-Equal 0 $mergeResult.ExitCode "Resolver should not fail on ambiguous merge commits."
    $mergeLinksRaw = Get-Content -LiteralPath (Join-Path $mergeRoot "links.json") -Raw
    Assert-True ($mergeLinksRaw -eq "{}" -or $mergeLinksRaw -notmatch "README.md") "Ambiguous merge-only body changes must not produce guessed link-map proof."
}

Invoke-Test "Manual dotted version rebaseline is no-write when body and metadata are otherwise stable" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "2")
    Commit-All -Root $root
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "2.1")

    $result = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-ChangedFilesOutputPath", "changed.json")

    Assert-Equal 0 $result.ExitCode "Manual dotted rebaseline should pass."
    Assert-True ($result.Stdout -match "manual version rebaseline") "Report should mention manual rebaseline."
    $changed = Read-JsonFile (Join-Path $root "changed.json")
    Assert-Equal 0 @($changed.changedFiles).Count "Changed files should stay empty for no-write rebaseline."
}

Invoke-Test "Full manifest Bootstrap initializes all eligible governed files" {
    $root = New-TestRepository
    Write-Utf8File -Path (Join-Path $root "README.md") -Content "# Readme`n"
    Write-Utf8File -Path (Join-Path $root "docs\guide.md") -Content "# Guide`n"
    Write-Utf8File -Path (Join-Path $root ".github\tools\sync-config\documentation\sync-manifest.md") -Content "# Sync Manifest`n"
    Write-Utf8File -Path (Join-Path $root ".github\tools\sync-config\documentation\types\manifest-document.md") -Content "# ManifestDocument`n"

    $manifestPath = Join-Path $root ".github\tools\doc-metadata\doc-metadata-manifest.json"
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 32
    $manifest.include = @("README.md", "docs/**/*.md", ".github/tools/sync-config/documentation/**/*.md")
    $manifest.exclude = @()
    $manifest | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

    $result = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-ReportOutputPath", "report.json")

    Assert-Equal 0 $result.ExitCode "Bootstrap should pass."
    $report = Read-JsonFile (Join-Path $root "report.json")
    foreach ($path in @("README.md", "docs/guide.md", ".github/tools/sync-config/documentation/sync-manifest.md", ".github/tools/sync-config/documentation/types/manifest-document.md")) {
        Assert-True (@($report.updatedFiles.path) -contains $path) "$path should be initialized."
        $content = Get-Content -LiteralPath (Join-Path $root ($path.Replace("/", [System.IO.Path]::DirectorySeparatorChar))) -Raw
        Assert-True ($content.StartsWith("---`nVersion: 1`nCreated:", [System.StringComparison]::Ordinal)) "$path should start with stable managed metadata."
    }
}

Invoke-Test "Existing file onboarding does not create content history for metadata-only initialization" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content "# Title`r`nExisting body.`r`n"
    Commit-All -Root $root

    $result = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-Path", "README.md", "-ReportOutputPath", "report.json")

    Assert-Equal 0 $result.ExitCode "Bootstrap should pass."
    $content = Get-Content -LiteralPath $readme -Raw
    Assert-True ($content -match "Version: 1") "Existing file should be initialized at Version 1."
    Assert-Equal 0 ([regex]::Matches($content, "(?m)^- Updated:").Count) "Existing metadata-only onboarding should not create a content history entry."
    Assert-True ($content -notmatch "Changes: <b>Unavailable</b>") "Existing metadata-only onboarding must not generate Unavailable history entries."
    Assert-True ($content -match "<!-- doc-metadata-presentation:end -->\r?\n\r?\n# Title") "Onboarding should preserve a clean Markdown body boundary."
}

Invoke-Test "Body repair does not strip unrelated governed metadata" {
    $root = New-TestRepository
    $one = Join-Path $root "docs\one.md"
    $two = Join-Path $root "docs\two.md"
    Write-Utf8File -Path $one -Content "# One`n"
    Write-Utf8File -Path $two -Content "# Two`n"
    $bootstrap = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-ReportOutputPath", "bootstrap-report.json")
    Assert-Equal 0 $bootstrap.ExitCode "Bootstrap should pass."
    Commit-All -Root $root
    $twoBefore = Get-Content -LiteralPath $two -Raw

    $oneContent = Get-Content -LiteralPath $one -Raw
    Write-Utf8File -Path $one -Content ($oneContent -replace "# One", "# One Changed")
    $result = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "docs/one.md", "-ChangedFilesOutputPath", "changed.json", "-ReportOutputPath", "repair-report.json")

    Assert-Equal 0 $result.ExitCode "Repair should pass."
    Assert-True ((Get-Content -LiteralPath $one -Raw) -match "Version: 2") "Changed file should increment."
    Assert-Equal $twoBefore (Get-Content -LiteralPath $two -Raw) "Unrelated governed metadata should be retained unchanged."
    $changed = Read-JsonFile (Join-Path $root "changed.json")
    Assert-Equal 1 @($changed.changedFiles).Count "Only the repaired body-changed file should be reported changed."
    Assert-Equal "docs/one.md" $changed.changedFiles[0] "ChangedFilesOutputPath should name only docs/one.md."
}

Invoke-Test "Metadata-only presentation repair preserves metadata and current View Commit" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1")
    Commit-All -Root $root
    $base = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -Body "# Title`nContent version with proven link.`n")
    Commit-All -Root $root -Message "content version"
    $bodyCommit = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()
    $linkMap = Write-HistoryLinkMap -Root $root -Links @{
        "README.md" = @{
            path = "README.md"
            url = "https://github.com/example/repo/commit/$bodyCommit"
            linkText = "View Commit"
            context = "local:$bodyCommit"
            commitSha = $bodyCommit
            bodyChanged = $true
        }
    }
    $contentUpdate = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-BaseSha", $base, "-HeadSha", $bodyCommit, "-HistoryLinkMapPath", $linkMap) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }
    Assert-Equal 0 $contentUpdate.ExitCode "Body update with proven link should pass before metadata-only repair."
    Commit-All -Root $root -Message "metadata update"

    $content = (Get-Content -LiteralPath $readme -Raw) -replace "<!-- doc-metadata-presentation:end -->\r?\n\r?\n", "<!-- doc-metadata-presentation:end -->`n"
    Write-Utf8File -Path $readme -Content $content

    $result = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-ChangedFilesOutputPath", "changed.json", "-ReportOutputPath", "report.json") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-Equal 0 $result.ExitCode "Presentation repair should pass."
    $after = Get-Content -LiteralPath $readme -Raw
    Assert-True ($after -match "Version: 2") "Metadata-only repair must not change Version."
    Assert-True ($after -match "Author: Doc Metadata Tests") "Metadata-only repair must not change Author."
    Assert-True ($after -match "\[<b>View Commit</b>\]\(https://github.com/example/repo/commit/$bodyCommit\)") "Metadata-only repair should preserve the current proven View Commit link."
    Assert-Equal 1 ([regex]::Matches($after, "(?m)^- Updated:").Count) "Metadata-only repair must not add a history entry."
    Assert-True ($after -notmatch "Changes: <b>Unavailable</b>") "Metadata-only repair must not generate Unavailable history entries."
    Assert-True ($after -match "<!-- doc-metadata-presentation:end -->\r?\n\r?\n# Title") "Presentation repair should restore the managed trailing blank line."
}

Invoke-Test "Version decrease is rejected" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "2.1")
    Commit-All -Root $root
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "2")

    $result = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md")

    Assert-True ($result.ExitCode -ne 0) "Version decrease should fail."
    Assert-True ($result.Stdout -match "Version must not decrease") "Failure should name Version decrease."
}

Invoke-Test "Generated history tamper is restored from trusted previous presentation" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1")
    Commit-All -Root $root
    $base = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()

    Write-Utf8File -Path $readme -Content (Get-MarkdownWithMetadata -Version "1" -Body "# Title`nContent version with proven history.`n")
    Commit-All -Root $root -Message "content version with history"
    $bodyCommit = (Invoke-Git -Root $root -Arguments @("rev-parse", "HEAD")).Trim()
    $linkMap = Write-HistoryLinkMap -Root $root -Links @{
        "README.md" = @{
            path = "README.md"
            url = "https://github.com/example/repo/commit/$bodyCommit"
            linkText = "View Commit"
            context = "local:$bodyCommit"
            commitSha = $bodyCommit
            bodyChanged = $true
        }
    }
    $contentUpdate = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-BaseSha", $base, "-HeadSha", $bodyCommit, "-HistoryLinkMapPath", $linkMap) -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }
    Assert-Equal 0 $contentUpdate.ExitCode "Body update with proven link should pass before tamper."
    Commit-All -Root $root -Message "metadata with proven history"

    $tampered = (Get-Content -LiteralPath $readme -Raw).Replace("https://github.com/example/repo/commit/$bodyCommit", "https://github.com/example/example")
    Write-Utf8File -Path $readme -Content $tampered

    $result = Invoke-Tool -Root $root -Mode "Update" -ExtraArguments @("-Path", "README.md", "-ReportOutputPath", "report.json") -Environment @{ GITHUB_REPOSITORY = "example/repo"; GITHUB_SERVER_URL = "https://github.com" }

    Assert-Equal 0 $result.ExitCode "Safe history tamper restore should pass."
    $content = Get-Content -LiteralPath $readme -Raw
    Assert-True ($content -match "Changes: \[<b>View Commit</b>\]\(https://github.com/example/repo/commit/$bodyCommit\)") "Generated history should restore the previous proven View Commit entry."
    Assert-True ($content -notmatch "Changes: <b>Unavailable</b>") "History tamper restoration must not restore deprecated Unavailable history entries."
    Assert-True ($content -notmatch "github.com/example/example") "Tampered URL should be removed."
    Assert-Equal 1 ([regex]::Matches($content, "(?m)^- Updated:").Count) "Metadata-only tamper restoration must not add a new history entry."
    $report = Read-JsonFile (Join-Path $root "report.json")
    Assert-True ($report.updatedFiles[0].reason -match "historyTamperDetected" -and $report.updatedFiles[0].reason -match "historyRestoredFromTrustedPrevious") "Report should include history tamper categories."
}

Invoke-Test "Custom front matter fields are preserved and ignored" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-Utf8File -Path $readme -Content "---`nVersion: 1`nCreated: 2026-01-01T00:00:00+00:00`nUpdated: 2026-01-01T00:00:00+00:00`nAuthor: Doc Metadata Tests`nTool: Visual Studio Code`nReviewState: Draft`n---`n# Title`n"

    $result = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-Path", "README.md")

    Assert-Equal 0 $result.ExitCode "Bootstrap should pass."
    $content = Get-Content -LiteralPath $readme -Raw
    Assert-True ($content -match "Tool: Visual Studio Code" -and $content -match "ReviewState: Draft") "Custom fields should be preserved."
    Assert-True ($content -match "(?s)^---\r?\nVersion: 1\r?\nCreated: 2026-01-01T00:00:00\+00:00\r?\nUpdated: 2026-01-01T00:00:00\+00:00\r?\nAuthor: Doc Metadata Tests\r?\nTool: Visual Studio Code\r?\nReviewState: Draft") "Managed field order should be stable before custom fields."

    $second = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-Path", "README.md", "-ChangedFilesOutputPath", "changed.json")
    Assert-Equal 0 $second.ExitCode "Second Bootstrap should pass."
    $changed = Read-JsonFile (Join-Path $root "changed.json")
    Assert-Equal 0 @($changed.changedFiles).Count "Repeated run should be idempotent."
}

Invoke-Test "Manifest removes overrides and uses include object scoped configuration" {
    $root = New-TestRepository
    $manifestPath = Join-Path $root ".github\tools\doc-metadata\doc-metadata-manifest.json"
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 32
    $manifest | Add-Member -NotePropertyName overrides -NotePropertyValue @()
    $manifest | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

    $invalid = Invoke-Tool -Root $root -Mode "Check"
    Assert-True ($invalid.ExitCode -ne 0) "overrides should be rejected as unknown."

    $manifest = Get-Content -LiteralPath (Join-Path $PublicSurfaceSource "doc-metadata-manifest.json") -Raw | ConvertFrom-Json -Depth 32
    $manifest.include = @(
        [pscustomobject]@{
            pattern = "README.md"
            presentation = [pscustomobject]@{ includeSeparator = $false; spacingBreaks = 1 }
        }
    )
    $manifest.exclude = @()
    $manifest | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
    Write-Utf8File -Path (Join-Path $root "README.md") -Content "# Title`n"

    $valid = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-Path", "README.md")
    Assert-Equal 0 $valid.ExitCode "Include object scoped presentation should pass."
    $content = Get-Content -LiteralPath (Join-Path $root "README.md") -Raw
    Assert-Equal 1 ([regex]::Matches($content, "(?m)^<br>$").Count) "Scoped spacingBreaks override should apply."
    Assert-True ($content -notmatch "(?m)^---\r?$`r?`n<br>") "Scoped includeSeparator false should remove presentation separator."
}

Invoke-Test "Conflicting multiple include entries fail" {
    $root = New-TestRepository
    $manifestPath = Join-Path $root ".github\tools\doc-metadata\doc-metadata-manifest.json"
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 32
    $manifest.include = @(
        "README.md",
        [pscustomobject]@{ pattern = "README.md"; presentation = [pscustomobject]@{ spacingBreaks = 1 } }
    )
    $manifest.exclude = @()
    $manifest | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
    Write-Utf8File -Path (Join-Path $root "README.md") -Content "# Title`n"

    $result = Invoke-Tool -Root $root -Mode "Bootstrap"

    Assert-True ($result.ExitCode -ne 0) "Conflicting include configs should fail."
    Assert-True ($result.Stdout -match "include configuration conflict") "Conflict should be reported."
}

Invoke-Test "Default exclude is empty and broad include plus exclude works" {
    $manifest = Read-JsonFile (Join-Path $PublicSurfaceSource "doc-metadata-manifest.json")
    Assert-Equal 0 @($manifest.exclude).Count "Default manifest exclude should be empty."

    $root = New-TestRepository
    Write-Utf8File -Path (Join-Path $root "docs\keep.md") -Content "# Keep`n"
    Write-Utf8File -Path (Join-Path $root "docs\skip.md") -Content "# Skip`n"
    $manifestPath = Join-Path $root ".github\tools\doc-metadata\doc-metadata-manifest.json"
    $json = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 32
    $json.include = @("docs/**/*.md")
    $json.exclude = @("docs/skip.md")
    $json | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

    $result = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-ReportOutputPath", "report.json")

    Assert-Equal 0 $result.ExitCode "Bootstrap should pass."
    $updated = @((Read-JsonFile (Join-Path $root "report.json")).updatedFiles.path)
    Assert-True ($updated -contains "docs/keep.md") "Included file should be updated."
    Assert-True ($updated -notcontains "docs/skip.md") "Excluded file should not be updated."
}

Invoke-Test "Eligibility processes only eligible files from AGENTS glob" {
    $root = New-TestRepository
    Write-Utf8File -Path (Join-Path $root "AGENTS.md") -Content "# Agents`n"
    Write-Utf8File -Path (Join-Path $root "AGENTS.cs") -Content "class Agents { }`n"

    $result = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-ReportOutputPath", "report.json")

    Assert-Equal 0 $result.ExitCode "Bootstrap should pass with ineligible report."
    $report = Read-JsonFile (Join-Path $root "report.json")
    Assert-True (@($report.updatedFiles.path) -contains "AGENTS.md") "AGENTS.md should be governed."
    Assert-True (@($report.updatedFiles.path) -notcontains "AGENTS.cs") "AGENTS.cs must not be modified."
    Assert-True (@($report.ineligibleFiles.path) -contains "AGENTS.cs") "AGENTS.cs should be reported ineligible."
}

Invoke-Test "Invalid UTF-8 is reported as binary non-text and never rewritten" {
    $root = New-TestRepository
    $readme = Join-Path $root "README.md"
    Write-InvalidUtf8File -Path $readme
    $before = [Convert]::ToHexString([System.IO.File]::ReadAllBytes($readme))

    $result = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-ReportOutputPath", "report.json")

    Assert-Equal 0 $result.ExitCode "Invalid UTF-8 should be ignored by default."
    $after = [Convert]::ToHexString([System.IO.File]::ReadAllBytes($readme))
    Assert-Equal $before $after "Invalid UTF-8 file should not be rewritten."
    $report = Read-JsonFile (Join-Path $root "report.json")
    Assert-Equal 1 $report.ignoredBinaryOrNonText.Count "Invalid UTF-8 should be classified as binary/non-text."
}

Invoke-Test "GitHub summary and changed-files output contracts remain stable" {
    $root = New-TestRepository
    Write-Utf8File -Path (Join-Path $root "README.md") -Content "# Title`n"
    $summaryPath = Join-Path $root "summary.md"

    $result = Invoke-Tool -Root $root -Mode "Bootstrap" -ExtraArguments @("-ChangedFilesOutputPath", "changed.json", "-ReportOutputPath", "report.json") -Environment @{ GITHUB_STEP_SUMMARY = $summaryPath }

    Assert-Equal 0 $result.ExitCode "Bootstrap should pass."
    Assert-True (Test-Path -LiteralPath $summaryPath) "GitHub summary should be written."
    $changed = Read-JsonFile (Join-Path $root "changed.json")
    Assert-Equal "README.md" $changed.changedFiles[0] "ChangedFilesOutputPath should contain only updated paths."
}

Invoke-Test "Workflow preserves repair design and deterministic branch hash" {
    $workflow = Get-Content -LiteralPath $WorkflowPath -Raw
    $resolver = Get-Content -LiteralPath (Join-Path $ToolingSource "resolve-content-change-links.ps1") -Raw
    $manifest = Read-JsonFile (Join-Path $PublicSurfaceSource "doc-metadata-manifest.json")

    Assert-True ($workflow -match "analyze-document-metadata") "Workflow should include analyze job."
    Assert-True ($workflow -match "repair-document-metadata") "Workflow should include repair job."
    Assert-True ($workflow -match "final-document-metadata-status") "Workflow should include final status job."
    Assert-True ($workflow -match "(?ms)^on:\s*\r?\n\s+workflow_call:[\s\S]*?\r?\n\s+workflow_dispatch:\s*$") "Workflow should be reusable and directly dispatchable."
    Assert-True ($workflow -notmatch "(?m)^\s{2}(pull_request|push|schedule):") "Workflow should not own standalone repository-maintenance triggers."
    Assert-True ($workflow -match "concurrency:\s*\r?\n\s+group:\s*\$\{\{ github\.workflow \}\}-\$\{\{ \(inputs\.pull_request_number != '' && inputs\.pull_request_number\) \|\| github\.event\.pull_request\.number \|\| \(\(inputs\.ref != '' && inputs\.ref\) \|\| github\.ref\) \}\}") "Workflow should cancel stale runs using reusable PR/ref context."
    Assert-True ($workflow -match "concurrency:[\s\S]*?cancel-in-progress:\s*true") "Workflow should cancel stale runs for the same PR/ref context."
    Assert-True ($workflow -notmatch "(?m)^\s+paths:\s*$") "Workflow must not use narrow paths filters that can miss manifest-governed files."
    Assert-True ($workflow -notmatch '"README\.md"|\"docs/\*\*\"|\"specs/\*\*\"') "Workflow should not duplicate manifest include patterns as trigger filters."
    Assert-True ($workflow -match "analyze-document-metadata:[\s\S]*?permissions:\s*\r?\n\s+contents: read") "Analyze job should use contents: read."
    Assert-True ($workflow -match "repair-document-metadata:[\s\S]*?permissions:\s*\r?\n\s+contents: write\s*\r?\n\s+pull-requests: write") "Repair job should have write permissions only in repair job."
    Assert-True ($workflow -notmatch "pull_request_target") "Workflow must not use pull_request_target."
    Assert-True ($workflow -match "path: trusted" -and $workflow -match "path: work") "Workflow should use trusted/work checkout layout."
    Assert-True ($workflow -match "startsWith\(\(inputs\.head_ref != '' && inputs\.head_ref \|\| github\.head_ref\), 'codex/doc-metadata-repair/'\)" -and $workflow -match "startsWith\(\(inputs\.ref_name != '' && inputs\.ref_name \|\| github\.ref_name\), 'codex/doc-metadata-repair/'\)") "Workflow should guard recursive doc-metadata repair branches."
    Assert-True ($workflow -match "Repair publishing skipped because this run is already on a doc-metadata repair branch") "Workflow should report repair-branch publishing skips."
    Assert-True ($workflow -match 'codex/doc-metadata-repair/\$safeTarget-\$hash') "Workflow should use the required repair branch prefix and hash suffix."
    Assert-True ($workflow -match "SHA256" -and $workflow -match "ToHexString" -and $workflow -match "Substring\(0, 12\)") "Workflow should construct a stable SHA-256 branch hash."
    Assert-True ($workflow -notmatch 'doc-metadata/repair/\$safeTarget') "Workflow must not use the old repair branch prefix."
    Assert-True ($workflow -match "Post-repair Check") "Workflow should run mandatory post-repair Check."
    Assert-True ($workflow -match "doc-metadata-links.json") "Workflow should pass stable history links to the trusted script."
    Assert-True ($workflow -match "Resolve content-change links") "Workflow should resolve per-file content-change links before repair."
    Assert-True ($workflow -match "resolve-content-change-links.ps1") "Workflow should call the trusted resolver script."
    Assert-True ($workflow -match "update-doc-metadata.ps1") "Workflow should pass the trusted metadata script to the resolver."
    Assert-True (-not $workflow.Contains('${{ github.event.pull_request.head.sha || github.sha }}')) "Workflow must not assign one event head commit URL to every file."
    Assert-True ($workflow -match "doc-metadata-post-check-report.json") "Workflow should write a post-repair Check report."
    Assert-True ($workflow -match "Remaining invalid files" -and $workflow -match "Remaining unrecoverable files") "Final status should list remaining post-repair failures."
    Assert-True ($workflow.Contains('GITHUB_RUN_ID_VALUE: ${{ github.run_id }}') -and $workflow.Contains('actions/runs/$($env:GITHUB_RUN_ID_VALUE)') -and $workflow.Contains('Run ID: $env:GITHUB_RUN_ID_VALUE')) "Repair PR body should include workflow run traceability."
    Assert-True ($workflow -match "Repaired files" -and $workflow -match "Initialized files" -and $workflow -match "Skipped files" -and $workflow -match "Remaining failed files" -and $workflow -match "Remaining unrecoverable files") "Repair PR body should include repaired, skipped, failed, and unrecoverable report sections."
    Assert-True ($workflow -match "doc-metadata-repair-summary\.md" -and $workflow -match 'Get-Content -LiteralPath \$bodyPath -Raw \| Add-Content -LiteralPath \$env:GITHUB_STEP_SUMMARY' -and $workflow -match '--body-file", \$bodyPath') "Workflow summary and PR body should use the same generated report file."
    Assert-True ($workflow -match 'rev-list", "--count", "origin/\$targetBranch\.\.HEAD"' -and $workflow -match "skipping repair branch push and PR creation") "Bot PR publishing should skip clean repair branches with no commits."
    Assert-True ($workflow -match '--force-with-lease=refs/heads/\$\{repairBranch\}:\$remoteSha') "Existing repair branch pushes should use an explicit force-with-lease tied to the observed remote SHA."
    Assert-True ($workflow -match 'Invoke-Native -FileName "git" -Arguments @\("push", \$remoteUrl, "HEAD:refs/heads/\$repairBranch"\)') "New repair branch pushes should use a normal push."
    $pushIndex = $workflow.IndexOf('Invoke-Native -FileName "git" -Arguments @("push"')
    $ghCreateIndex = $workflow.IndexOf('Invoke-Native -FileName "gh" -Arguments @("pr", "create"')
    Assert-True ($pushIndex -ge 0 -and $ghCreateIndex -gt $pushIndex -and $workflow -match 'throw "\$FileName \$\(\$Arguments -join') "PR creation should be unreachable after a failed guarded native push."
    Assert-True (@($manifest.include) -contains ".github/tools/sync-config/documentation/**/*.md") "This patch must not remove sync-config documentation from doc-metadata governance."
    Assert-True ($workflow -notmatch "sync-managed-files") "Doc-metadata workflow patch should not alter sync-managed-files behavior."
    Assert-True ($resolver -match '"-Mode", "ContentChanges"' -or $resolver -match "-Mode ContentChanges") "Resolver should use ContentChanges mode as its source of managed-body truth."
    Assert-True ($resolver -match "parents.Count -gt 1" -and $resolver -match "Skipping ambiguous merge commit") "Resolver should not guess for ambiguous merge-parent commits."
}

Write-Host ""
Write-Host "Doc metadata acceptance tests: $script:Passed passed, $script:Failed failed"

if ($script:Failed -gt 0) {
    exit 1
}
