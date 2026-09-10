#requires -Version 7.0
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Analyze", "Bootstrap", "Update", "Check", "ContentChanges")]
    [string] $Mode,

    [string] $Root,

    [string] $ManifestPath = ".github/tools/doc-metadata/doc-metadata-manifest.json",

    [string[]] $Include = @(),

    [string[]] $Path = @(),

    [string] $EventName,

    [string] $EventPayloadPath,

    [string] $HeadSha,

    [string] $BaseSha,

    [string] $ChangedFilesOutputPath,

    [string] $ReportOutputPath,

    [string] $HistoryLinkMapPath,

    [string] $ContentChangeOutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:RepositoryRoot = $null
$script:ManifestFullPath = $null
$script:ManagedFields = @("Version", "Created", "Updated", "Author")
$script:PresentationStartMarker = "<!-- doc-metadata-presentation:start -->"
$script:PresentationEndMarker = "<!-- doc-metadata-presentation:end -->"
$script:PlainTextSeparator = "-" * 80
$script:ManagedHistoryLinkPattern = '\[<b>(?<label>View Changes|View Commit)</b>\]\((?<url>[^)]+)\)'
$script:CurrentChangesLinkPattern = "^\s*$script:ManagedHistoryLinkPattern\s*$"
$script:TimestampPattern = "^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?[+-]\d{2}:\d{2}$"
$script:RemediationCommand = "pwsh ./.github/scripts/doc-metadata/update-doc-metadata.ps1 -Mode Update -Root ."
$script:DefaultAllowedDocumentExtensions = @(".md", ".markdown", ".txt")

function New-Report {
    param(
        [string] $ModeValue,
        [string] $RootValue,
        [string] $ManifestValue
    )

    @{
        mode = $ModeValue
        root = $RootValue
        manifestPath = $ManifestValue
        comparison = @{
            mode = $null
            baseSha = $null
            headSha = $null
            staleCheckAvailable = $false
            reason = $null
        }
        updatedFiles = [System.Collections.Generic.List[object]]::new()
        unchangedFiles = [System.Collections.Generic.List[object]]::new()
        skippedFiles = [System.Collections.Generic.List[object]]::new()
        failedFiles = [System.Collections.Generic.List[object]]::new()
        ineligibleFiles = [System.Collections.Generic.List[object]]::new()
        ignoredByEligibility = [System.Collections.Generic.List[object]]::new()
        ignoredByDeniedPath = [System.Collections.Generic.List[object]]::new()
        ignoredByDeniedExtension = [System.Collections.Generic.List[object]]::new()
        ignoredBinaryOrNonText = [System.Collections.Generic.List[object]]::new()
        staleCheckSkippedFiles = [System.Collections.Generic.List[object]]::new()
        contentChanges = [System.Collections.Generic.List[object]]::new()
        analysis = [ordered]@{
            metadataValid = $false
            repairRequired = $false
            repairSafe = $true
            unrecoverableFailure = $false
            repairableFiles = [System.Collections.Generic.List[object]]::new()
            unrecoverableFiles = [System.Collections.Generic.List[object]]::new()
            repairCategories = [ordered]@{
                initialized = [System.Collections.Generic.List[string]]::new()
                incremented = [System.Collections.Generic.List[string]]::new()
                restoredFromHistory = [System.Collections.Generic.List[string]]::new()
                repaired = [System.Collections.Generic.List[string]]::new()
                skippedManualEdit = [System.Collections.Generic.List[string]]::new()
                notSafelyRepairable = [System.Collections.Generic.List[string]]::new()
                historyTamperDetected = [System.Collections.Generic.List[string]]::new()
                historyRestoredFromTrustedPrevious = [System.Collections.Generic.List[string]]::new()
                historyTamperUnrecoverable = [System.Collections.Generic.List[string]]::new()
            }
        }
        summaryCounts = @{}
    }
}

function Add-UpdatedFile {
    param(
        [hashtable] $Report,
        [string] $Path,
        [string] $Format,
        [string] $Placement,
        [object] $OldVersion,
        [object] $NewVersion,
        [object] $OldCreated,
        [object] $NewCreated,
        [object] $OldUpdated,
        [object] $NewUpdated,
        [string] $Reason
    )

    $Report.updatedFiles.Add([ordered]@{
        path = $Path
        metadataFormat = $Format
        metadataPlacement = $Placement
        oldVersion = $OldVersion
        newVersion = $NewVersion
        oldCreated = $OldCreated
        newCreated = $NewCreated
        oldUpdated = $OldUpdated
        newUpdated = $NewUpdated
        reason = $Reason
    })
}

function Add-UnchangedFile {
    param(
        [hashtable] $Report,
        [string] $Path,
        [string] $Reason,
        [object] $OldVersion = $null,
        [object] $NewVersion = $null
    )

    $Report.unchangedFiles.Add([ordered]@{
        path = $Path
        reason = $Reason
        oldVersion = $OldVersion
        newVersion = $NewVersion
    })
}

function Add-SkippedFile {
    param(
        [hashtable] $Report,
        [string] $Path,
        [string] $Reason
    )

    $Report.skippedFiles.Add([ordered]@{
        path = $Path
        reason = $Reason
    })
}

function Add-FailedFile {
    param(
        [hashtable] $Report,
        [string] $Path,
        [string] $Rule,
        [object] $Current,
        [string] $Expected,
        [string] $Remediation = $script:RemediationCommand
    )

    $Report.failedFiles.Add([ordered]@{
        path = $Path
        rule = $Rule
        current = $Current
        expected = $Expected
        remediation = $Remediation
    })
}

function Add-StaleSkippedFile {
    param(
        [hashtable] $Report,
        [string] $Path,
        [string] $Reason
    )

    $Report.staleCheckSkippedFiles.Add([ordered]@{
        path = $Path
        reason = $Reason
    })
}

function Add-IneligibleFile {
    param(
        [hashtable] $Report,
        [string] $Path,
        [string] $Reason,
        [string] $Category,
        [object] $Current = $null,
        [string] $Expected = "eligible document text file",
        [string] $Remediation = "Update documentEligibility or convert this document to UTF-8 if it should be managed by doc-metadata."
    )

    $entry = [ordered]@{
        path = $Path
        reason = $Reason
        category = $Category
        current = $Current
        expected = $Expected
        remediation = $Remediation
    }

    $Report.ineligibleFiles.Add($entry)
    switch ($Category) {
        "ignoredByDeniedPath" { $Report.ignoredByDeniedPath.Add($entry); break }
        "ignoredByDeniedExtension" { $Report.ignoredByDeniedExtension.Add($entry); break }
        "ignoredBinaryOrNonText" { $Report.ignoredBinaryOrNonText.Add($entry); break }
        default { $Report.ignoredByEligibility.Add($entry); break }
    }
}

function Add-RepairableFile {
    param(
        [hashtable] $Report,
        [string] $Path,
        [string] $Reason,
        [string[]] $Categories = @("repaired")
    )

    $Report.analysis.repairableFiles.Add([ordered]@{
        path = $Path
        reason = $Reason
        categories = @($Categories)
    })

    foreach ($category in @($Categories)) {
        if ($Report.analysis.repairCategories.Contains($category)) {
            $Report.analysis.repairCategories[$category].Add($Path)
        }
    }
}

function Add-UnrecoverableFile {
    param(
        [hashtable] $Report,
        [string] $Path,
        [string] $Reason,
        [object] $Current = $null,
        [string] $Expected = "safely repairable metadata state"
    )

    $Report.analysis.unrecoverableFiles.Add([ordered]@{
        path = $Path
        reason = $Reason
        current = $Current
        expected = $Expected
    })
    $Report.analysis.repairCategories.notSafelyRepairable.Add($Path)
}

function Get-PropertyValue {
    param(
        [object] $Object,
        [string] $Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    $property.Value
}

function Get-ObjectPropertyNames {
    param([object] $Object)

    if ($null -eq $Object) {
        return @()
    }

    @($Object.PSObject.Properties | ForEach-Object { $_.Name })
}

function Test-ObjectPropertyExists {
    param(
        [object] $Object,
        [string] $Name
    )

    if ($null -eq $Object) {
        return $false
    }

    $null -ne $Object.PSObject.Properties[$Name]
}

function Get-JsonArrayProperty {
    param(
        [object] $Object,
        [string] $Name
    )

    $value = Get-PropertyValue -Object $Object -Name $Name
    if ($null -eq $value) {
        return @()
    }

    if ($value -is [array]) {
        foreach ($item in $value) {
            $item
        }
        return
    }

    $value
}

function Get-NonNullValues {
    param([object[]] $Values)

    @($Values | Where-Object { $null -ne $_ -and -not [string]::IsNullOrWhiteSpace([string] $_) })
}

function Resolve-RootPath {
    param([string] $RequestedRoot)

    if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
        if ([System.IO.Path]::IsPathFullyQualified($RequestedRoot)) {
            return [System.IO.Path]::GetFullPath($RequestedRoot)
        }

        return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $RequestedRoot))
    }

    try {
        $gitRoot = (& git rev-parse --show-toplevel 2>$null)
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($gitRoot)) {
            return [System.IO.Path]::GetFullPath($gitRoot.Trim())
        }
    }
    catch {
        Write-Verbose "Git root detection failed: $($_.Exception.Message)"
    }

    [System.IO.Path]::GetFullPath((Get-Location).Path)
}

function Resolve-InRootPath {
    param(
        [string] $RootPath,
        [string] $InputPath
    )

    $candidate = if ([System.IO.Path]::IsPathRooted($InputPath)) {
        [System.IO.Path]::GetFullPath($InputPath)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $RootPath $InputPath))
    }

    $rootWithSeparator = $RootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $comparison = if ($IsWindows) { [System.StringComparison]::OrdinalIgnoreCase } else { [System.StringComparison]::Ordinal }

    if ($candidate.Equals($RootPath, $comparison) -or $candidate.StartsWith($rootWithSeparator, $comparison)) {
        return $candidate
    }

    $null
}

function ConvertTo-RepoRelativePath {
    param(
        [string] $RootPath,
        [string] $FullPath
    )

    [System.IO.Path]::GetRelativePath($RootPath, $FullPath).Replace("\", "/")
}

function Normalize-RepoPath {
    param([string] $PathValue)

    $normalized = $PathValue.Replace("\", "/")
    while ($normalized.StartsWith("./", [System.StringComparison]::Ordinal)) {
        $normalized = $normalized.Substring(2)
    }

    $normalized.TrimStart("/")
}

function Normalize-EligibilityExtension {
    param(
        [object] $Value,
        [string] $PathName
    )

    $text = ([string] $Value).Trim()
    if ([string]::IsNullOrWhiteSpace($text) -or $text -eq ".") {
        throw "$PathName must be a non-empty file extension."
    }
    if ($text.IndexOfAny([char[]]@("*", "?", "[", "]", "/", "\")) -ge 0) {
        throw "$PathName must be an extension only and must not contain wildcards or path separators."
    }
    if (-not $text.StartsWith(".", [System.StringComparison]::Ordinal)) {
        $text = ".$text"
    }

    $text.ToLowerInvariant()
}

function Test-RepoRelativePatternIsSafe {
    param([string] $Pattern)

    if ([string]::IsNullOrWhiteSpace($Pattern)) {
        return $false
    }
    if ($Pattern -match "^[A-Za-z]:") {
        return $false
    }
    if ($Pattern.StartsWith("/", [System.StringComparison]::Ordinal) -or $Pattern.StartsWith("\", [System.StringComparison]::Ordinal)) {
        return $false
    }
    if ($Pattern.Contains("\")) {
        return $false
    }

    $normalized = Normalize-RepoPath $Pattern
    foreach ($segment in @($normalized -split "/")) {
        if ($segment -eq "." -or $segment -eq "..") {
            return $false
        }
    }

    $true
}

function Get-DocumentEligibility {
    param([object] $Manifest)

    $eligibility = Get-PropertyValue -Object $Manifest -Name "documentEligibility"
    $allowedSource = if ($null -ne $eligibility -and (Test-ObjectPropertyExists -Object $eligibility -Name "allowedExtensions")) {
        @(Get-JsonArrayProperty -Object $eligibility -Name "allowedExtensions")
    }
    else {
        @($script:DefaultAllowedDocumentExtensions)
    }

    $allowed = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($extension in @($allowedSource)) {
        [void] $allowed.Add((Normalize-EligibilityExtension -Value $extension -PathName "documentEligibility.allowedExtensions[]"))
    }
    foreach ($extension in @(Get-JsonArrayProperty -Object $eligibility -Name "additionalAllowedExtensions")) {
        [void] $allowed.Add((Normalize-EligibilityExtension -Value $extension -PathName "documentEligibility.additionalAllowedExtensions[]"))
    }

    $denied = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($extension in @(Get-JsonArrayProperty -Object $eligibility -Name "deniedExtensions")) {
        [void] $denied.Add((Normalize-EligibilityExtension -Value $extension -PathName "documentEligibility.deniedExtensions[]"))
    }

    $deniedPaths = @(Get-JsonArrayProperty -Object $eligibility -Name "deniedPaths" | ForEach-Object { Normalize-RepoPath ([string] $_) })
    $allowExtensionless = if ($null -ne $eligibility -and (Test-ObjectPropertyExists -Object $eligibility -Name "allowExtensionless")) { [bool] (Get-PropertyValue -Object $eligibility -Name "allowExtensionless") } else { $false }
    $failOnIneligible = if ($null -ne $eligibility -and (Test-ObjectPropertyExists -Object $eligibility -Name "failOnIneligibleMatches")) { [bool] (Get-PropertyValue -Object $eligibility -Name "failOnIneligibleMatches") } else { $false }

    [pscustomobject]@{
        allowedExtensions = @($allowed)
        deniedExtensions = @($denied)
        deniedPaths = @($deniedPaths)
        allowExtensionless = $allowExtensionless
        failOnIneligibleMatches = $failOnIneligible
    }
}

function Test-DocumentEligibility {
    param(
        [object] $Eligibility,
        [string] $RepoPath
    )

    $normalizedPath = Normalize-RepoPath $RepoPath
    if (@($Eligibility.deniedPaths).Count -gt 0 -and (Test-AnyPatternMatch -Patterns @($Eligibility.deniedPaths) -PathValue $normalizedPath)) {
        return [pscustomobject]@{
            Eligible = $false
            Reason = "denied path"
            Category = "ignoredByDeniedPath"
            Current = $normalizedPath
        }
    }

    $extension = [System.IO.Path]::GetExtension($normalizedPath).ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($extension)) {
        if ($Eligibility.allowExtensionless) {
            return [pscustomobject]@{ Eligible = $true; Reason = "eligible"; Category = $null; Current = $extension }
        }

        return [pscustomobject]@{
            Eligible = $false
            Reason = "extensionless not allowed"
            Category = "ignoredByEligibility"
            Current = "extensionless"
        }
    }

    if (@($Eligibility.deniedExtensions) -contains $extension) {
        return [pscustomobject]@{
            Eligible = $false
            Reason = "denied extension"
            Category = "ignoredByDeniedExtension"
            Current = $extension
        }
    }

    if (@($Eligibility.allowedExtensions) -notcontains $extension) {
        return [pscustomobject]@{
            Eligible = $false
            Reason = "extension not allowed"
            Category = "ignoredByEligibility"
            Current = $extension
        }
    }

    [pscustomobject]@{
        Eligible = $true
        Reason = "eligible"
        Category = $null
        Current = $extension
    }
}

function Convert-GlobToRegex {
    param([string] $Pattern)

    $patternValue = Normalize-RepoPath $Pattern
    if ($patternValue.EndsWith("/", [System.StringComparison]::Ordinal)) {
        $patternValue += "**"
    }

    $builder = [System.Text.StringBuilder]::new()
    [void] $builder.Append("^")
    $index = 0
    while ($index -lt $patternValue.Length) {
        $character = $patternValue[$index]
        if ($character -eq "*") {
            if (($index + 1) -lt $patternValue.Length -and $patternValue[$index + 1] -eq "*") {
                if (($index + 2) -lt $patternValue.Length -and $patternValue[$index + 2] -eq "/") {
                    [void] $builder.Append("(?:.*/)?")
                    $index += 3
                }
                else {
                    [void] $builder.Append(".*")
                    $index += 2
                }
            }
            else {
                [void] $builder.Append("[^/]*")
                $index++
            }
        }
        elseif ($character -eq "?") {
            [void] $builder.Append("[^/]")
            $index++
        }
        else {
            [void] $builder.Append([regex]::Escape([string] $character))
            $index++
        }
    }

    [void] $builder.Append("$")
    $builder.ToString()
}

function Test-GlobMatch {
    param(
        [string] $Pattern,
        [string] $PathValue
    )

    $normalizedPattern = Normalize-RepoPath $Pattern
    $normalizedPath = Normalize-RepoPath $PathValue

    if ($normalizedPattern.IndexOfAny([char[]]@("*", "?")) -lt 0) {
        if ($normalizedPattern.EndsWith("/", [System.StringComparison]::Ordinal)) {
            return $normalizedPath.StartsWith($normalizedPattern, [System.StringComparison]::Ordinal)
        }

        return $normalizedPath.Equals($normalizedPattern, [System.StringComparison]::Ordinal)
    }

    $regex = [regex]::new((Convert-GlobToRegex $normalizedPattern), [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    $regex.IsMatch($normalizedPath)
}

function Test-AnyPatternMatch {
    param(
        [string[]] $Patterns,
        [string] $PathValue
    )

    foreach ($pattern in $Patterns) {
        if (Test-GlobMatch -Pattern $pattern -PathValue $PathValue) {
            return $true
        }
    }

    $false
}

function Get-PatternList {
    param([object[]] $Entries)

    $patterns = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in @($Entries)) {
        if ($null -eq $entry) {
            continue
        }

        if ($entry -is [string]) {
            $patterns.Add((Normalize-RepoPath $entry))
            continue
        }

        $pattern = Get-PropertyValue -Object $entry -Name "pattern"
        if ($pattern -is [string]) {
            $patterns.Add((Normalize-RepoPath $pattern))
        }
    }

    $patterns.ToArray()
}

function New-MetadataConfig {
    param(
        [object] $Defaults,
        [object] $Override = $null,
        [string] $RepoPath = ""
    )

    $metadataDefaults = Get-PropertyValue -Object $Defaults -Name "metadata"
    if ($null -eq $metadataDefaults) {
        $metadataDefaults = $Defaults
    }

    $presentationDefaults = Get-PropertyValue -Object $Defaults -Name "presentation"
    $overrideMetadata = Get-PropertyValue -Object $Override -Name "metadata"
    if ($null -eq $overrideMetadata) {
        $overrideMetadata = $Override
    }
    $overridePresentation = Get-PropertyValue -Object $Override -Name "presentation"

    $config = [ordered]@{
        metadataFormat = "yaml-front-matter"
        metadataPlacement = "top"
        versionField = "Version"
        createdField = "Created"
        updatedField = "Updated"
        authorField = "Author"
        versioningMode = "body-content-change"
        timestampFormat = "rfc3339-utc"
        commentStart = $null
        commentLinePrefix = $null
        commentEnd = $null
        presentationEnabled = $true
        historyLimit = 20
        includeSeparator = $true
        spacingBreaks = 2
        isMarkdown = $false
        isPlainText = $false
    }

    foreach ($name in @("format", "placement", "versionField", "createdField", "updatedField", "authorField", "versioningMode", "timestampFormat", "commentStart", "commentLinePrefix", "commentEnd")) {
        $value = Get-PropertyValue -Object $metadataDefaults -Name $name
        if ($null -ne $value) {
            switch ($name) {
                "format" { $config.metadataFormat = [string] $value; break }
                "placement" { $config.metadataPlacement = [string] $value; break }
                default { $config[$name] = [string] $value; break }
            }
        }
    }
    foreach ($name in @("enabled", "historyLimit", "includeSeparator", "spacingBreaks")) {
        $value = Get-PropertyValue -Object $presentationDefaults -Name $name
        if ($null -ne $value) {
            switch ($name) {
                "enabled" { $config.presentationEnabled = [bool] $value; break }
                "historyLimit" { $config.historyLimit = $value; break }
                "includeSeparator" { $config.includeSeparator = [bool] $value; break }
                "spacingBreaks" { $config.spacingBreaks = [int] $value; break }
            }
        }
    }

    $extension = [System.IO.Path]::GetExtension((Normalize-RepoPath $RepoPath)).ToLowerInvariant()
    $config.isMarkdown = $extension -in @(".md", ".markdown")
    $config.isPlainText = $extension -eq ".txt"

    if ($config.isPlainText) {
        $config.presentationEnabled = $false
        $config.historyLimit = 0
        if ($config.metadataFormat -eq "comment-block" -and [string]::IsNullOrWhiteSpace([string] $config.commentStart)) {
            $config.commentStart = "<!-- doc-metadata"
            $config.commentEnd = "-->"
        }
    }
    elseif ($config.isMarkdown) {
        $config.presentationEnabled = $true
    }

    if ($null -ne $Override) {
        foreach ($name in @("format", "placement", "versionField", "createdField", "updatedField", "authorField", "versioningMode", "timestampFormat", "commentStart", "commentLinePrefix", "commentEnd")) {
            $value = Get-PropertyValue -Object $overrideMetadata -Name $name
            if ($null -ne $value) {
                switch ($name) {
                    "format" { $config.metadataFormat = [string] $value; break }
                    "placement" { $config.metadataPlacement = [string] $value; break }
                    default { $config[$name] = [string] $value; break }
                }
            }
        }
        foreach ($name in @("enabled", "historyLimit", "includeSeparator", "spacingBreaks")) {
            $value = Get-PropertyValue -Object $overridePresentation -Name $name
            if ($null -ne $value) {
                switch ($name) {
                    "enabled" { $config.presentationEnabled = [bool] $value; break }
                    "historyLimit" { $config.historyLimit = $value; break }
                    "includeSeparator" { $config.includeSeparator = [bool] $value; break }
                    "spacingBreaks" { $config.spacingBreaks = [int] $value; break }
                }
            }
        }
    }

    [pscustomobject] $config
}

function Test-IsTimestamp {
    param([object] $Value)

    if ($Value -isnot [string]) {
        return $false
    }

    if ($Value -notmatch $script:TimestampPattern) {
        return $false
    }
    if ($Value.EndsWith("-00:00", [System.StringComparison]::Ordinal)) {
        return $false
    }

    $parsed = [System.DateTimeOffset]::MinValue
    [System.DateTimeOffset]::TryParse(
        $Value,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::None,
        [ref] $parsed)
}

function ConvertTo-UtcTimestampValue {
    param([string] $Value)

    $parsed = [System.DateTimeOffset]::Parse($Value, [System.Globalization.CultureInfo]::InvariantCulture)
    $utc = $parsed.ToUniversalTime()
    $utc.ToString("yyyy-MM-ddTHH:mm:ss", [System.Globalization.CultureInfo]::InvariantCulture) + "+00:00"
}

function Test-TimestampEquivalent {
    param(
        [object] $Left,
        [object] $Right
    )

    if (-not (Test-IsTimestamp $Left) -or -not (Test-IsTimestamp $Right)) {
        return $false
    }

    (ConvertTo-UtcTimestampValue $Left) -eq (ConvertTo-UtcTimestampValue $Right)
}

function ConvertTo-TimestampValue {
    [System.DateTimeOffset]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", [System.Globalization.CultureInfo]::InvariantCulture) + "+00:00"
}

function ConvertTo-DisplayValue {
    param([object] $Value)

    if ($null -eq $Value -or $Value -eq "") {
        return "missing"
    }

    [string] $Value
}

function Test-VersionValue {
    param([object] $Value)

    if ($Value -is [int] -or $Value -is [long]) {
        return ([int64] $Value -gt 0)
    }

    if ($Value -isnot [string]) {
        return $false
    }

    $text = ([string] $Value).Trim()
    if ($text -notmatch "^[0-9]+(?:\.[0-9]+)*$") {
        return $false
    }

    foreach ($component in @($text -split "\.")) {
        if ($component -notmatch "^[0-9]+$" -or [int64] $component -lt 0) {
            return $false
        }
    }

    [int64] (@($text -split "\.")[0]) -gt 0
}

function ConvertTo-VersionString {
    param([object] $Value)

    if (-not (Test-VersionValue $Value)) {
        return $null
    }

    ([string] $Value).Trim()
}

function Get-VersionMajor {
    param([object] $Value)

    $version = ConvertTo-VersionString $Value
    if ($null -eq $version) {
        return $null
    }

    [int64] (@($version -split "\.")[0])
}

function Compare-VersionValue {
    param(
        [object] $Left,
        [object] $Right
    )

    $leftText = ConvertTo-VersionString $Left
    $rightText = ConvertTo-VersionString $Right
    if ($null -eq $leftText -or $null -eq $rightText) {
        return $null
    }

    $leftParts = @($leftText -split "\." | ForEach-Object { [int64] $_ })
    $rightParts = @($rightText -split "\." | ForEach-Object { [int64] $_ })
    $max = [Math]::Max($leftParts.Count, $rightParts.Count)
    for ($index = 0; $index -lt $max; $index++) {
        $leftPart = if ($index -lt $leftParts.Count) { $leftParts[$index] } else { 0 }
        $rightPart = if ($index -lt $rightParts.Count) { $rightParts[$index] } else { 0 }
        if ($leftPart -gt $rightPart) {
            return 1
        }
        if ($leftPart -lt $rightPart) {
            return -1
        }
    }

    0
}

function Get-IncrementedVersion {
    param([object] $Value)

    $major = Get-VersionMajor $Value
    if ($null -eq $major) {
        return "1"
    }

    [string] ($major + 1)
}

function Split-ContentLines {
    param([string] $Content)

    $lines = [System.Collections.Generic.List[object]]::new()
    $index = 0
    while ($index -lt $Content.Length) {
        $lineStart = $index
        while ($index -lt $Content.Length -and $Content[$index] -ne "`r" -and $Content[$index] -ne "`n") {
            $index++
        }

        $text = $Content.Substring($lineStart, $index - $lineStart)
        $newLine = ""
        if ($index -lt $Content.Length) {
            if ($Content[$index] -eq "`r" -and ($index + 1) -lt $Content.Length -and $Content[$index + 1] -eq "`n") {
                $newLine = "`r`n"
                $index += 2
            }
            else {
                $newLine = [string] $Content[$index]
                $index++
            }
        }

        $lines.Add([pscustomobject]@{
            Text = $text
            NewLine = $newLine
        })
    }

    $lines.ToArray()
}

function Join-ContentLines {
    param([object[]] $Lines)

    $builder = [System.Text.StringBuilder]::new()
    foreach ($line in @($Lines)) {
        [void] $builder.Append($line.Text)
        [void] $builder.Append($line.NewLine)
    }

    $builder.ToString()
}

function ConvertTo-ComparableBody {
    param([string] $Content)

    if ($null -eq $Content) {
        return $null
    }

    $Content.Replace("`r`n", "`n").Replace("`r", "`n")
}

function Get-PreferredNewLine {
    param([string] $Content)

    $crlf = $Content.IndexOf("`r`n", [System.StringComparison]::Ordinal)
    if ($crlf -ge 0) {
        return "`r`n"
    }

    $lf = $Content.IndexOf("`n", [System.StringComparison]::Ordinal)
    if ($lf -ge 0) {
        return "`n"
    }

    $cr = $Content.IndexOf("`r", [System.StringComparison]::Ordinal)
    if ($cr -ge 0) {
        return "`r"
    }

    "`n"
}

function Read-StrictUtf8Text {
    param([string] $FullPath)

    $bytes = [System.IO.File]::ReadAllBytes($FullPath)
    if ([Array]::IndexOf($bytes, [byte] 0) -ge 0) {
        throw "File contains NUL bytes and is treated as binary/non-text."
    }

    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $offset = if ($hasBom) { 3 } else { 0 }
    $length = $bytes.Length - $offset
    $encoding = [System.Text.UTF8Encoding]::new($false, $true)
    $content = $encoding.GetString($bytes, $offset, $length)

    [pscustomobject]@{
        Content = $content
        HasBom = $hasBom
        NewLine = Get-PreferredNewLine $content
    }
}

function Convert-StrictUtf8BytesToText {
    param([byte[]] $Bytes)

    $hasBom = $Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF
    $offset = if ($hasBom) { 3 } else { 0 }
    $length = $Bytes.Length - $offset
    $encoding = [System.Text.UTF8Encoding]::new($false, $true)
    $encoding.GetString($Bytes, $offset, $length)
}

function Write-StrictUtf8Text {
    param(
        [string] $FullPath,
        [string] $Content,
        [bool] $HasBom
    )

    $encoding = [System.Text.UTF8Encoding]::new($HasBom)
    [System.IO.File]::WriteAllText($FullPath, $Content, $encoding)
}

function Invoke-GitRaw {
    param([string[]] $Arguments)

    $processStartInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $processStartInfo.FileName = "git"
    $processStartInfo.UseShellExecute = $false
    $processStartInfo.RedirectStandardOutput = $true
    $processStartInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) {
        [void] $processStartInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($processStartInfo)
    $output = [System.IO.MemoryStream]::new()
    $process.StandardOutput.BaseStream.CopyTo($output)
    $errorText = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    [pscustomobject]@{
        ExitCode = $process.ExitCode
        Bytes = $output.ToArray()
        Error = $errorText
    }
}

function Invoke-GitText {
    param([string[]] $Arguments)

    $result = Invoke-GitRaw -Arguments $Arguments
    $text = ""
    if ($result.Bytes.Length -gt 0) {
        $text = Convert-StrictUtf8BytesToText -Bytes $result.Bytes
    }

    [pscustomobject]@{
        ExitCode = $result.ExitCode
        Text = $text
        Error = $result.Error
    }
}

function Test-InGitRepository {
    $result = Invoke-GitText -Arguments @("-C", $script:RepositoryRoot, "rev-parse", "--is-inside-work-tree")
    $result.ExitCode -eq 0 -and $result.Text.Trim() -eq "true"
}

function Test-GitCommitExists {
    param([string] $Sha)

    if ([string]::IsNullOrWhiteSpace($Sha)) {
        return $false
    }

    $result = Invoke-GitText -Arguments @("-C", $script:RepositoryRoot, "cat-file", "-e", "$Sha^{commit}")
    $result.ExitCode -eq 0
}

function Get-GitFileContent {
    param(
        [string] $Revision,
        [string] $RepoPath
    )

    if ([string]::IsNullOrWhiteSpace($Revision) -or [string]::IsNullOrWhiteSpace($RepoPath)) {
        return $null
    }

    $result = Invoke-GitRaw -Arguments @("-C", $script:RepositoryRoot, "show", "$Revision`:$RepoPath")
    if ($result.ExitCode -ne 0) {
        return $null
    }

    try {
        Convert-StrictUtf8BytesToText -Bytes $result.Bytes
    }
    catch {
        $null
    }
}

function Get-GitMergeBase {
    param(
        [string] $Base,
        [string] $Head
    )

    $result = Invoke-GitText -Arguments @("-C", $script:RepositoryRoot, "merge-base", $Base, $Head)
    if ($result.ExitCode -ne 0) {
        return $null
    }

    $result.Text.Trim()
}

function Parse-GitNameStatusZ {
    param([string] $Text)

    $tokens = @($Text -split "`0" | Where-Object { $_ -ne "" })
    $records = [System.Collections.Generic.List[object]]::new()
    $index = 0
    while ($index -lt $tokens.Count) {
        $status = $tokens[$index]
        $index++
        if ($status.StartsWith("R", [System.StringComparison]::Ordinal) -or $status.StartsWith("C", [System.StringComparison]::Ordinal)) {
            if (($index + 1) -ge $tokens.Count) {
                break
            }

            $oldPath = Normalize-RepoPath $tokens[$index]
            $newPath = Normalize-RepoPath $tokens[$index + 1]
            $index += 2
            $records.Add([pscustomobject]@{
                Status = $status
                Path = $newPath
                PreviousPath = $oldPath
            })
        }
        else {
            if ($index -ge $tokens.Count) {
                break
            }

            $pathValue = Normalize-RepoPath $tokens[$index]
            $index++
            $records.Add([pscustomobject]@{
                Status = $status
                Path = $pathValue
                PreviousPath = $pathValue
            })
        }
    }

    $records.ToArray()
}

function Get-UpdateCandidateRecords {
    $records = [System.Collections.Generic.List[object]]::new()
    if (-not (Test-InGitRepository)) {
        return $records.ToArray()
    }

    $staged = Invoke-GitRaw -Arguments @("-C", $script:RepositoryRoot, "diff", "--cached", "--name-status", "--diff-filter=ACMR", "-z")
    if ($staged.ExitCode -eq 0 -and $staged.Bytes.Length -gt 0) {
        $text = Convert-StrictUtf8BytesToText -Bytes $staged.Bytes
        return @(Parse-GitNameStatusZ -Text $text)
    }

    $working = Invoke-GitRaw -Arguments @("-C", $script:RepositoryRoot, "diff", "--name-status", "--diff-filter=ACMR", "-z", "HEAD", "--")
    if ($working.ExitCode -eq 0 -and $working.Bytes.Length -gt 0) {
        $text = Convert-StrictUtf8BytesToText -Bytes $working.Bytes
        foreach ($record in @(Parse-GitNameStatusZ -Text $text)) {
            $records.Add($record)
        }
    }

    $untracked = Invoke-GitRaw -Arguments @("-C", $script:RepositoryRoot, "ls-files", "--others", "--exclude-standard", "-z")
    if ($untracked.ExitCode -eq 0 -and $untracked.Bytes.Length -gt 0) {
        $text = Convert-StrictUtf8BytesToText -Bytes $untracked.Bytes
        foreach ($pathValue in @($text -split "`0" | Where-Object { $_ -ne "" })) {
            $normalized = Normalize-RepoPath $pathValue
            $records.Add([pscustomobject]@{
                Status = "A"
                Path = $normalized
                PreviousPath = $normalized
            })
        }
    }

    $records.ToArray()
}

function Get-AllRepositoryFiles {
    if (Test-InGitRepository) {
        $files = Invoke-GitRaw -Arguments @("-C", $script:RepositoryRoot, "ls-files", "--cached", "--others", "--exclude-standard", "-z")
        if ($files.ExitCode -eq 0) {
            $text = Convert-StrictUtf8BytesToText -Bytes $files.Bytes
            return @($text -split "`0" | Where-Object { $_ -ne "" } | ForEach-Object { Normalize-RepoPath $_ } | Where-Object {
                $candidate = Join-Path $script:RepositoryRoot ($_.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
                Test-Path -LiteralPath $candidate -PathType Leaf
            })
        }
    }

    Get-ChildItem -LiteralPath $script:RepositoryRoot -Recurse -File -Force |
        ForEach-Object { ConvertTo-RepoRelativePath -RootPath $script:RepositoryRoot -FullPath $_.FullName } |
        Where-Object { -not (Test-GlobMatch -Pattern ".git/**" -PathValue $_) }
}

function Test-PathIsReparsePoint {
    param([string] $FullPath)

    $item = Get-Item -LiteralPath $FullPath -Force
    ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
}

function Validate-PatternEntries {
    param(
        [object[]] $Entries,
        [string] $PathName,
        [object] $Defaults = $null
    )

    $errors = [System.Collections.Generic.List[string]]::new()
    $index = 0
    foreach ($entry in @($Entries)) {
        if ($null -eq $entry) {
            $index++
            continue
        }

        if ($entry -is [string]) {
            if ([string]::IsNullOrWhiteSpace($entry)) {
                $errors.Add("$PathName entries must not be empty.")
            }
            $index++
            continue
        }

        foreach ($propertyName in Get-ObjectPropertyNames $entry) {
            if ($propertyName -notin @("pattern", "metadata", "presentation")) {
                $errors.Add("$PathName[$index] has unknown property '$propertyName'.")
            }
        }

        $pattern = Get-PropertyValue -Object $entry -Name "pattern"
        if ([string]::IsNullOrWhiteSpace([string] $pattern)) {
            $errors.Add("$PathName[$index] object entries must define a non-empty pattern property.")
        }

        Add-ValidationErrors -Errors $errors -AdditionalErrors (Validate-MetadataConfig -Config (Get-PropertyValue -Object $entry -Name "metadata") -PathName "$PathName[$index].metadata" -RequireAll $false)
        Add-ValidationErrors -Errors $errors -AdditionalErrors (Validate-PresentationConfig -Config (Get-PropertyValue -Object $entry -Name "presentation") -PathName "$PathName[$index].presentation" -RequireAll $false)
        if ($null -ne $Defaults) {
            Add-ValidationErrors -Errors $errors -AdditionalErrors (Validate-EffectiveMetadataConfig -Config (New-MetadataConfig -Defaults $Defaults -Override $entry -RepoPath ([string] $pattern)) -PathName "$PathName[$index] effective config")
        }
        $index++
    }

    $errors.ToArray()
}

function Add-ValidationErrors {
    param(
        [System.Collections.Generic.List[string]] $Errors,
        [object[]] $AdditionalErrors
    )

    foreach ($additionalError in @($AdditionalErrors)) {
        if ($null -ne $additionalError) {
            $Errors.Add([string] $additionalError)
        }
    }
}

function Validate-MetadataConfig {
    param(
        [object] $Config,
        [string] $PathName,
        [bool] $RequireAll
    )

    $errors = [System.Collections.Generic.List[string]]::new()
    if ($null -eq $Config) {
        if ($RequireAll) {
            $errors.Add("$PathName must be defined.")
        }
        return $errors.ToArray()
    }

    $allowedProperties = @("format", "placement", "versionField", "createdField", "updatedField", "authorField", "versioningMode", "timestampFormat", "commentStart", "commentLinePrefix", "commentEnd")
    $required = @("format", "placement", "versionField", "createdField", "updatedField", "authorField", "versioningMode", "timestampFormat")

    foreach ($propertyName in Get-ObjectPropertyNames $Config) {
        if ($propertyName -notin $allowedProperties) {
            $errors.Add("$PathName has unknown property '$propertyName'.")
        }
    }

    if ($RequireAll) {
        foreach ($requiredProperty in $required) {
            if ([string]::IsNullOrWhiteSpace([string] (Get-PropertyValue -Object $Config -Name $requiredProperty))) {
                $errors.Add("$PathName must define '$requiredProperty'.")
            }
        }
    }

    $format = Get-PropertyValue -Object $Config -Name "format"
    $placement = Get-PropertyValue -Object $Config -Name "placement"
    $versioningMode = Get-PropertyValue -Object $Config -Name "versioningMode"
    $timestampFormat = Get-PropertyValue -Object $Config -Name "timestampFormat"

    if ($null -ne $format -and $format -notin @("yaml-front-matter", "comment-block")) {
        $errors.Add("$PathName.format must be 'yaml-front-matter' or 'comment-block'.")
    }

    if ($null -ne $placement -and $placement -notin @("top", "bottom")) {
        $errors.Add("$PathName.placement must be 'top' or 'bottom'.")
    }

    if ($format -eq "yaml-front-matter" -and $placement -eq "bottom") {
        $errors.Add("$PathName cannot use placement 'bottom' with yaml-front-matter.")
    }

    if ($null -ne $versioningMode -and $versioningMode -ne "body-content-change") {
        $errors.Add("$PathName.versioningMode must be 'body-content-change'.")
    }

    if ($null -ne $timestampFormat -and $timestampFormat -ne "rfc3339-utc") {
        $errors.Add("$PathName.timestampFormat must be 'rfc3339-utc'.")
    }

    $errors.ToArray()
}

function Validate-PresentationConfig {
    param(
        [object] $Config,
        [string] $PathName,
        [bool] $RequireAll
    )

    $errors = [System.Collections.Generic.List[string]]::new()
    if ($null -eq $Config) {
        if ($RequireAll) {
            $errors.Add("$PathName must be defined.")
        }
        return $errors.ToArray()
    }

    foreach ($propertyName in Get-ObjectPropertyNames $Config) {
        if ($propertyName -notin @("enabled", "historyLimit", "includeSeparator", "spacingBreaks")) {
            $errors.Add("$PathName has unknown property '$propertyName'.")
        }
    }

    foreach ($propertyName in @("enabled", "includeSeparator")) {
        $value = Get-PropertyValue -Object $Config -Name $propertyName
        if ($null -ne $value -and $value -isnot [bool]) {
            $errors.Add("$PathName.$propertyName must be a boolean.")
        }
    }

    foreach ($propertyName in @("historyLimit", "spacingBreaks")) {
        $value = Get-PropertyValue -Object $Config -Name $propertyName
        if ($null -ne $value -and ($value -isnot [int] -and $value -isnot [long])) {
            $errors.Add("$PathName.$propertyName must be a non-negative integer or null.")
        }
        elseif ($null -ne $value -and [int64] $value -lt 0) {
            $errors.Add("$PathName.$propertyName must not be negative.")
        }
    }

    $errors.ToArray()
}

function Validate-EffectiveMetadataConfig {
    param(
        [object] $Config,
        [string] $PathName
    )

    $errors = [System.Collections.Generic.List[string]]::new()

    if ((Get-PropertyValue -Object $Config -Name "metadataFormat") -eq "comment-block") {
        if ([string]::IsNullOrWhiteSpace([string] (Get-PropertyValue -Object $Config -Name "commentStart"))) {
            $errors.Add("$PathName.commentStart is required for comment-block metadata.")
        }

        if ([string]::IsNullOrWhiteSpace([string] (Get-PropertyValue -Object $Config -Name "commentEnd"))) {
            $errors.Add("$PathName.commentEnd is required for comment-block metadata.")
        }
    }

    if ((Get-PropertyValue -Object $Config -Name "metadataFormat") -notin @("yaml-front-matter", "comment-block")) {
        $errors.Add("$PathName.format must be 'yaml-front-matter' or 'comment-block'.")
    }

    if ((Get-PropertyValue -Object $Config -Name "metadataPlacement") -notin @("top", "bottom")) {
        $errors.Add("$PathName.placement must be 'top' or 'bottom'.")
    }

    if ((Get-PropertyValue -Object $Config -Name "metadataFormat") -eq "yaml-front-matter" -and (Get-PropertyValue -Object $Config -Name "metadataPlacement") -eq "bottom") {
        $errors.Add("$PathName cannot use placement 'bottom' with yaml-front-matter.")
    }

    foreach ($fieldName in @("versionField", "createdField", "updatedField", "authorField")) {
        if ([string]::IsNullOrWhiteSpace([string] (Get-PropertyValue -Object $Config -Name $fieldName))) {
            $errors.Add("$PathName.$fieldName must be defined.")
        }
    }

    $errors.ToArray()
}

function Validate-DocumentEligibility {
    param([object] $Eligibility)

    $errors = [System.Collections.Generic.List[string]]::new()
    if ($null -eq $Eligibility) {
        return $errors.ToArray()
    }

    $allowedProperties = @("allowedExtensions", "additionalAllowedExtensions", "deniedExtensions", "deniedPaths", "allowExtensionless", "failOnIneligibleMatches")
    foreach ($propertyName in Get-ObjectPropertyNames $Eligibility) {
        if ($propertyName -notin $allowedProperties) {
            $errors.Add("documentEligibility has unknown property '$propertyName'.")
        }
    }

    foreach ($propertyName in @("allowedExtensions", "additionalAllowedExtensions", "deniedExtensions")) {
        foreach ($extension in @(Get-JsonArrayProperty -Object $Eligibility -Name $propertyName)) {
            try {
                [void] (Normalize-EligibilityExtension -Value $extension -PathName "documentEligibility.$propertyName[]")
            }
            catch {
                $errors.Add($_.Exception.Message)
            }
        }
    }

    foreach ($pathPattern in @(Get-JsonArrayProperty -Object $Eligibility -Name "deniedPaths")) {
        if (-not (Test-RepoRelativePatternIsSafe -Pattern ([string] $pathPattern))) {
            $errors.Add("documentEligibility.deniedPaths[] must be a safe repository-relative forward-slash path or glob pattern.")
        }
    }

    foreach ($propertyName in @("allowExtensionless", "failOnIneligibleMatches")) {
        $value = Get-PropertyValue -Object $Eligibility -Name $propertyName
        if ($null -ne $value -and $value -isnot [bool]) {
            $errors.Add("documentEligibility.$propertyName must be a boolean.")
        }
    }

    try {
        [void] (Get-DocumentEligibility -Manifest ([pscustomobject]@{ documentEligibility = $Eligibility }))
    }
    catch {
        $errors.Add($_.Exception.Message)
    }

    $errors.ToArray()
}

function Read-Manifest {
    param([string] $ManifestFile)

    if (-not (Test-Path -LiteralPath $ManifestFile -PathType Leaf)) {
        throw "Manifest not found at '$ManifestFile'."
    }

    $manifestText = Get-Content -LiteralPath $ManifestFile -Raw
    $manifest = $manifestText | ConvertFrom-Json -Depth 32
    $errors = [System.Collections.Generic.List[string]]::new()
    $allowedTopLevel = @('$schema', "version", "defaults", "include", "exclude", "documentEligibility")
    foreach ($propertyName in Get-ObjectPropertyNames $manifest) {
        if ($propertyName -notin $allowedTopLevel) {
            $errors.Add("Manifest has unknown top-level property '$propertyName'.")
        }
    }

    foreach ($requiredProperty in @('$schema', "version", "defaults", "include", "exclude")) {
        if (-not (Test-ObjectPropertyExists -Object $manifest -Name $requiredProperty)) {
            $errors.Add("Manifest must define '$requiredProperty'.")
        }
    }

    if ((Get-PropertyValue -Object $manifest -Name "version") -ne 1) {
        $errors.Add("Manifest version must be integer 1.")
    }

    $defaults = Get-PropertyValue -Object $manifest -Name "defaults"
    foreach ($propertyName in Get-ObjectPropertyNames $defaults) {
        if ($propertyName -notin @("metadata", "presentation")) {
            $errors.Add("defaults has unknown property '$propertyName'.")
        }
    }
    Add-ValidationErrors -Errors $errors -AdditionalErrors (Validate-MetadataConfig -Config (Get-PropertyValue -Object $defaults -Name "metadata") -PathName "defaults.metadata" -RequireAll $true)
    Add-ValidationErrors -Errors $errors -AdditionalErrors (Validate-PresentationConfig -Config (Get-PropertyValue -Object $defaults -Name "presentation") -PathName "defaults.presentation" -RequireAll $true)
    Add-ValidationErrors -Errors $errors -AdditionalErrors (Validate-EffectiveMetadataConfig -Config (New-MetadataConfig -Defaults $defaults -RepoPath "README.md") -PathName "defaults effective config")
    Add-ValidationErrors -Errors $errors -AdditionalErrors (Validate-DocumentEligibility -Eligibility (Get-PropertyValue -Object $manifest -Name "documentEligibility"))

    [object[]] $includeEntries = @(Get-JsonArrayProperty -Object $manifest -Name "include")
    [object[]] $excludeEntries = @(Get-JsonArrayProperty -Object $manifest -Name "exclude")
    Add-ValidationErrors -Errors $errors -AdditionalErrors (Validate-PatternEntries -Entries $includeEntries -PathName "include" -Defaults $defaults)
    Add-ValidationErrors -Errors $errors -AdditionalErrors (Validate-PatternEntries -Entries $excludeEntries -PathName "exclude")

    if ($errors.Count -gt 0) {
        throw "Invalid document metadata manifest:`n$($errors -join "`n")"
    }

    $manifest
}

function Resolve-GovernedFiles {
    param(
        [object] $Manifest,
        [string[]] $BootstrapIncludePatterns = @(),
        [hashtable] $Report = $null
    )

    $files = Get-AllRepositoryFiles
    $eligibility = Get-DocumentEligibility -Manifest $Manifest
    [object[]] $bootstrapIncludeValues = @(Get-NonNullValues -Values $BootstrapIncludePatterns)
    $includeEntries = if ($bootstrapIncludeValues.Length -gt 0) { $bootstrapIncludeValues } else { @(Get-JsonArrayProperty -Object $Manifest -Name "include") }
    $includePatterns = Get-PatternList -Entries $includeEntries
    $topExcludePatterns = Get-PatternList -Entries @(Get-JsonArrayProperty -Object $Manifest -Name "exclude")
    $defaults = Get-PropertyValue -Object $Manifest -Name "defaults"
    $governed = @{}

    Write-Verbose "Resolved $(@($files).Count) repository files."
    Write-Verbose "Include patterns: $($includePatterns -join ', ')"
    Write-Verbose "Default exclude patterns: $($topExcludePatterns -join ', ')"

    foreach ($repoPath in $files) {
        if (Test-AnyPatternMatch -Patterns $topExcludePatterns -PathValue $repoPath) {
            continue
        }

        $matchingConfigs = [System.Collections.Generic.List[object]]::new()
        foreach ($entry in @($includeEntries)) {
            $pattern = if ($entry -is [string]) { [string] $entry } else { [string] (Get-PropertyValue -Object $entry -Name "pattern") }
            if ([string]::IsNullOrWhiteSpace($pattern) -or -not (Test-GlobMatch -Pattern $pattern -PathValue $repoPath)) {
                continue
            }

            $entryOverride = if ($entry -is [string]) { $null } else { $entry }
            $matchingConfigs.Add((New-MetadataConfig -Defaults $defaults -Override $entryOverride -RepoPath $repoPath))
        }

        $config = $null
        if ($matchingConfigs.Count -gt 0) {
            $configJson = @($matchingConfigs | ForEach-Object { $_ | ConvertTo-Json -Depth 8 -Compress }) | Sort-Object -Unique
            if (@($configJson).Count -gt 1) {
                if ($null -ne $Report) {
                    Add-FailedFile -Report $Report -Path $repoPath -Rule "include configuration conflict" -Current "multiple include entries with different effective settings" -Expected "only identical effective metadata/presentation settings for duplicate matches" -Remediation "Adjust .github/tools/doc-metadata/doc-metadata-manifest.json so only one scoped include config applies."
                }
                continue
            }

            $config = $matchingConfigs[0]
        }

        if ($null -ne $config) {
            $eligibilityResult = Test-DocumentEligibility -Eligibility $eligibility -RepoPath $repoPath
            if (-not $eligibilityResult.Eligible) {
                if ($null -ne $Report) {
                    Add-IneligibleFile -Report $Report -Path $repoPath -Reason $eligibilityResult.Reason -Category $eligibilityResult.Category -Current $eligibilityResult.Current
                    if ($eligibility.failOnIneligibleMatches) {
                        Add-FailedFile -Report $Report -Path $repoPath -Rule "documentEligibility" -Current $eligibilityResult.Current -Expected $eligibilityResult.Reason -Remediation "Update documentEligibility, adjust manifest patterns, or convert the file to an eligible UTF-8 document."
                    }
                }
                continue
            }

            $fullPath = Join-Path $script:RepositoryRoot ($repoPath.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
            try {
                [void] (Read-StrictUtf8Text -FullPath $fullPath)
            }
            catch {
                if ($null -ne $Report) {
                    Add-IneligibleFile -Report $Report -Path $repoPath -Reason "binary/non-text" -Category "ignoredBinaryOrNonText" -Current "not strict UTF-8 text" -Expected "strict UTF-8 text document" -Remediation "Convert this document to UTF-8 if it should be managed by doc-metadata."
                    if ($eligibility.failOnIneligibleMatches) {
                        Add-FailedFile -Report $Report -Path $repoPath -Rule "documentEligibility" -Current "binary/non-text" -Expected "strict UTF-8 text document" -Remediation "Convert this document to UTF-8 if it should be managed by doc-metadata."
                    }
                }
                continue
            }

            Write-Verbose "Governed file: $repoPath"
            $governed[$repoPath] = [pscustomobject]@{
                Path = $repoPath
                FullPath = $fullPath
                Config = $config
            }
        }
    }

    $governed
}

function Test-ManifestExcludedPath {
    param(
        [object] $Manifest,
        [string] $RepoPath
    )

    $excludePatterns = Get-PatternList -Entries @(Get-JsonArrayProperty -Object $Manifest -Name "exclude")
    Test-AnyPatternMatch -Patterns $excludePatterns -PathValue $RepoPath
}

function Remove-ManagedPresentation {
    param(
        [string] $BodyContent,
        [object] $Config
    )

    $lines = Split-ContentLines $BodyContent
    $startIndex = -1
    $endIndex = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index].Text.Trim() -eq $script:PresentationStartMarker) {
            $startIndex = $index
            break
        }
    }

    if ($startIndex -ge 0) {
        for ($index = $startIndex + 1; $index -lt $lines.Count; $index++) {
            if ($lines[$index].Text.Trim() -eq $script:PresentationEndMarker) {
                $endIndex = $index
                break
            }
        }

        if ($endIndex -lt 0) {
            return [pscustomobject]@{
                Body = $BodyContent
                HasPresentation = $true
                IsPresentationMalformed = $true
                PresentationLines = @()
            }
        }

        $managedEndIndex = $endIndex
        $nextIndex = $endIndex + 1
        while ($nextIndex -lt $lines.Count -and [string]::IsNullOrWhiteSpace($lines[$nextIndex].Text)) {
            $managedEndIndex = $nextIndex
            $nextIndex++
        }

        $remaining = [System.Collections.Generic.List[object]]::new()
        for ($index = 0; $index -lt $lines.Count; $index++) {
            if ($index -ge $startIndex -and $index -le $managedEndIndex) {
                continue
            }
            $remaining.Add($lines[$index])
        }

        $presentationLines = @($lines[$startIndex..$managedEndIndex] | ForEach-Object { $_.Text })
        return [pscustomobject]@{
            Body = (Join-ContentLines $remaining.ToArray())
            HasPresentation = $true
            IsPresentationMalformed = $false
            PresentationLines = $presentationLines
        }
    }

    if (-not $Config.presentationEnabled -and $Config.includeSeparator -and $lines.Count -gt 0) {
        $separatorIndex = -1
        for ($index = 0; $index -lt $lines.Count; $index++) {
            if ([string]::IsNullOrWhiteSpace($lines[$index].Text)) {
                continue
            }
            if ($lines[$index].Text -eq $script:PlainTextSeparator) {
                $separatorIndex = $index
            }
            break
        }

        if ($separatorIndex -ge 0) {
            $bodyStart = $separatorIndex + 1
            while ($bodyStart -lt $lines.Count -and [string]::IsNullOrWhiteSpace($lines[$bodyStart].Text)) {
                $bodyStart++
            }

            $bodyLines = if ($bodyStart -lt $lines.Count) { @($lines[$bodyStart..($lines.Count - 1)]) } else { @() }
            return [pscustomobject]@{
                Body = (Join-ContentLines $bodyLines)
                HasPresentation = $true
                IsPresentationMalformed = $false
                PresentationLines = @($lines[0..($bodyStart - 1)] | ForEach-Object { $_.Text })
            }
        }
    }

    [pscustomobject]@{
        Body = $BodyContent
        HasPresentation = $false
        IsPresentationMalformed = $false
        PresentationLines = @()
    }
}

function Get-YamlMetadataInfo {
    param(
        [string] $Content,
        [object] $Config
    )

    $lines = Split-ContentLines $Content
    if ($lines.Count -eq 0 -or $lines[0].Text -ne "---") {
        return [pscustomobject]@{
            HasMetadata = $false
            IsMalformed = $false
            Fields = @{}
            Body = $Content
            MetadataLines = @()
            HasPresentation = $false
            IsPresentationMalformed = $false
            PresentationLines = @()
        }
    }

    $closingIndex = -1
    for ($index = 1; $index -lt $lines.Count; $index++) {
        if ($lines[$index].Text -eq "---") {
            $closingIndex = $index
            break
        }
    }

    if ($closingIndex -lt 0) {
        return [pscustomobject]@{
            HasMetadata = $true
            IsMalformed = $true
            Fields = @{}
            Body = $Content
            MetadataLines = @()
            HasPresentation = $false
            IsPresentationMalformed = $false
            PresentationLines = @()
        }
    }

    $metadataLines = if ($closingIndex -gt 1) { @($lines[1..($closingIndex - 1)] | ForEach-Object { $_.Text }) } else { @() }
    $bodyLines = if (($closingIndex + 1) -lt $lines.Count) { @($lines[($closingIndex + 1)..($lines.Count - 1)]) } else { @() }
    $fields = Get-SimpleMetadataFields -MetadataLines $metadataLines
    $presentation = Remove-ManagedPresentation -BodyContent (Join-ContentLines $bodyLines) -Config $Config

    [pscustomobject]@{
        HasMetadata = $true
        IsMalformed = $false
        Fields = $fields
        Body = $presentation.Body
        MetadataLines = $metadataLines
        HasPresentation = $presentation.HasPresentation
        IsPresentationMalformed = $presentation.IsPresentationMalformed
        PresentationLines = $presentation.PresentationLines
    }
}

function Get-CommentMetadataInfo {
    param(
        [string] $Content,
        [object] $Config
    )

    $lines = Split-ContentLines $Content
    $start = [string] $Config.commentStart
    $end = [string] $Config.commentEnd
    if ($lines.Count -eq 0) {
        return [pscustomobject]@{
            HasMetadata = $false
            IsMalformed = $false
            Fields = @{}
            Body = $Content
            MetadataLines = @()
            HasPresentation = $false
            IsPresentationMalformed = $false
            PresentationLines = @()
        }
    }

    if ($Config.metadataPlacement -eq "top") {
        if ($lines[0].Text.TrimEnd() -ne $start) {
            return [pscustomobject]@{
                HasMetadata = $false
                IsMalformed = $false
                Fields = @{}
                Body = $Content
                MetadataLines = @()
                HasPresentation = $false
                IsPresentationMalformed = $false
                PresentationLines = @()
            }
        }

        $closingIndex = -1
        for ($index = 1; $index -lt $lines.Count; $index++) {
            if ($lines[$index].Text.Trim() -eq $end) {
                $closingIndex = $index
                break
            }
        }

        if ($closingIndex -lt 0) {
            return [pscustomobject]@{
                HasMetadata = $true
                IsMalformed = $true
                Fields = @{}
                Body = $Content
                MetadataLines = @()
                HasPresentation = $false
                IsPresentationMalformed = $false
                PresentationLines = @()
            }
        }

        $metadataLines = if ($closingIndex -gt 1) { @($lines[1..($closingIndex - 1)] | ForEach-Object { Remove-CommentLinePrefix -Line $_.Text -Prefix $Config.commentLinePrefix }) } else { @() }
        $bodyLines = if (($closingIndex + 1) -lt $lines.Count) { @($lines[($closingIndex + 1)..($lines.Count - 1)]) } else { @() }
        $presentation = Remove-ManagedPresentation -BodyContent (Join-ContentLines $bodyLines) -Config $Config
        return [pscustomobject]@{
            HasMetadata = $true
            IsMalformed = $false
            Fields = (Get-SimpleMetadataFields -MetadataLines $metadataLines)
            Body = $presentation.Body
            MetadataLines = $metadataLines
            HasPresentation = $presentation.HasPresentation
            IsPresentationMalformed = $presentation.IsPresentationMalformed
            PresentationLines = $presentation.PresentationLines
        }
    }

    $endIndex = -1
    for ($index = $lines.Count - 1; $index -ge 0; $index--) {
        if ($lines[$index].Text.Trim() -eq $end) {
            $endIndex = $index
            break
        }
        if (-not [string]::IsNullOrWhiteSpace($lines[$index].Text)) {
            break
        }
    }

    if ($endIndex -lt 0) {
        return [pscustomobject]@{
            HasMetadata = $false
            IsMalformed = $false
            Fields = @{}
            Body = $Content
            MetadataLines = @()
            HasPresentation = $false
            IsPresentationMalformed = $false
            PresentationLines = @()
        }
    }

    $startIndex = -1
    for ($index = $endIndex - 1; $index -ge 0; $index--) {
        if ($lines[$index].Text.TrimEnd() -eq $start) {
            $startIndex = $index
            break
        }
    }

    if ($startIndex -lt 0) {
        return [pscustomobject]@{
            HasMetadata = $true
            IsMalformed = $true
            Fields = @{}
            Body = $Content
            MetadataLines = @()
            HasPresentation = $false
            IsPresentationMalformed = $false
            PresentationLines = @()
        }
    }

    $metadataLines = if (($endIndex - $startIndex) -gt 1) { @($lines[($startIndex + 1)..($endIndex - 1)] | ForEach-Object { Remove-CommentLinePrefix -Line $_.Text -Prefix $Config.commentLinePrefix }) } else { @() }
    $bodyLines = if ($startIndex -gt 0) { @($lines[0..($startIndex - 1)]) } else { @() }
    $presentation = Remove-ManagedPresentation -BodyContent (Join-ContentLines $bodyLines) -Config $Config

    [pscustomobject]@{
        HasMetadata = $true
        IsMalformed = $false
        Fields = (Get-SimpleMetadataFields -MetadataLines $metadataLines)
        Body = $presentation.Body
        MetadataLines = $metadataLines
        HasPresentation = $presentation.HasPresentation
        IsPresentationMalformed = $presentation.IsPresentationMalformed
        PresentationLines = $presentation.PresentationLines
    }
}

function Remove-CommentLinePrefix {
    param(
        [string] $Line,
        [object] $Prefix
    )

    if ($null -eq $Prefix -or [string]::IsNullOrEmpty([string] $Prefix)) {
        return $Line
    }

    $prefixValue = [string] $Prefix
    if ($Line.StartsWith($prefixValue, [System.StringComparison]::Ordinal)) {
        return $Line.Substring($prefixValue.Length)
    }

    $Line
}

function Get-SimpleMetadataFields {
    param([string[]] $MetadataLines)

    $fields = @{}
    foreach ($line in @($MetadataLines)) {
        if ($line -match "^\s*([^:#][^:]*?)\s*:\s*(.*?)\s*$") {
            $fields[$matches[1]] = $matches[2]
        }
    }

    $fields
}

function Get-MetadataInfo {
    param(
        [string] $Content,
        [object] $Config
    )

    if ($Config.metadataFormat -eq "yaml-front-matter") {
        return Get-YamlMetadataInfo -Content $Content -Config $Config
    }

    Get-CommentMetadataInfo -Content $Content -Config $Config
}

function Update-MetadataLines {
    param(
        [string[]] $MetadataLines,
        [hashtable] $Values,
        [object] $Config
    )

    $inputLines = @($MetadataLines)
    if ($inputLines.Count -eq 1 -and [string]::IsNullOrEmpty($inputLines[0])) {
        $inputLines = @()
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $inputLines) {
        if ($null -ne $line) {
            $lines.Add($line)
        }
    }

    $managedFieldNames = @($Config.versionField, $Config.createdField, $Config.updatedField, $Config.authorField)
    $customLines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in @($lines)) {
        $isManagedLine = $false
        foreach ($fieldName in $managedFieldNames) {
            if ($line -match "^\s*$([regex]::Escape($fieldName))\s*:") {
                $isManagedLine = $true
                break
            }
        }
        if (-not $isManagedLine) {
            $customLines.Add($line)
        }
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($fieldName in $managedFieldNames) {
        $lines.Add("${fieldName}: $($Values[$fieldName])")
    }
    foreach ($line in @($customLines)) {
        $lines.Add($line)
    }

    $lines.ToArray()
}

function New-MetadataBlock {
    param(
        [string[]] $Lines,
        [object] $Config,
        [string] $NewLine
    )

    if ($Config.metadataFormat -eq "yaml-front-matter") {
        return "---$NewLine$($Lines -join $NewLine)$NewLine---$NewLine"
    }

    $prefix = if ($null -ne $Config.commentLinePrefix) { [string] $Config.commentLinePrefix } else { "" }
    $prefixed = @($Lines | ForEach-Object { "$prefix$_" })
    "$($Config.commentStart)$NewLine$($prefixed -join $NewLine)$NewLine$($Config.commentEnd)$NewLine"
}

function Get-ConfiguredRepositoryParts {
    $repository = [string] $env:GITHUB_REPOSITORY
    $server = if ([string]::IsNullOrWhiteSpace($env:GITHUB_SERVER_URL)) { "https://github.com" } else { $env:GITHUB_SERVER_URL.TrimEnd("/") }

    if ([string]::IsNullOrWhiteSpace($repository) -or $repository -notmatch "^[^/]+/[^/]+$") {
        if (Test-InGitRepository) {
            $remote = Invoke-GitText -Arguments @("-C", $script:RepositoryRoot, "config", "--get", "remote.origin.url")
            if ($remote.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($remote.Text.Trim())) {
                $remoteText = $remote.Text.Trim()
                if ($remoteText -match "^(?<server>https://[^/]+)/(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$") {
                    $server = $Matches.server
                    $repository = "$($Matches.owner)/$($Matches.repo)"
                }
                elseif ($remoteText -match "^git@(?<host>[^:]+):(?<owner>[^/]+)/(?<repo>[^/]+?)(?:\.git)?$") {
                    $server = "https://$($Matches.host)"
                    $repository = "$($Matches.owner)/$($Matches.repo)"
                }
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($repository) -or $repository -notmatch "^[^/]+/[^/]+$") {
        return $null
    }

    $serverUri = $null
    if (-not [System.Uri]::TryCreate($server, [System.UriKind]::Absolute, [ref] $serverUri)) {
        return $null
    }

    $parts = $repository.Split("/", 2)
    [pscustomobject]@{
        Host = $serverUri.Host
        Owner = $parts[0]
        Name = ([regex]::Replace($parts[1], "\.git$", ""))
    }
}

function Test-ManagedHistoryUrl {
    param(
        [string] $Url,
        [string] $RepoPath = ""
    )

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return $false
    }

    $uri = $null
    if (-not [System.Uri]::TryCreate($Url, [System.UriKind]::Absolute, [ref] $uri)) {
        return $false
    }

    if ($uri.Scheme -ne "https") {
        return $false
    }

    $repository = Get-ConfiguredRepositoryParts
    if ($null -eq $repository) {
        return $false
    }

    $decodedPath = [System.Uri]::UnescapeDataString($uri.AbsolutePath.Trim("/"))
    $segments = @($decodedPath -split "/" | Where-Object { $_ -ne "" })
    if ($segments.Count -lt 3) {
        return $false
    }

    if (-not $uri.Host.Equals($repository.Host, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $segments[0].Equals($repository.Owner, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $segments[1].Equals($repository.Name, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    if ($segments.Count -eq 2) {
        return $false
    }

    $kind = $segments[2]
    if ($kind -eq "commit") {
        return $segments.Count -eq 4 -and $segments[3] -match "^[0-9a-fA-F]{40}$"
    }

    $false
}

function Get-ManagedHistoryUrlParts {
    param([string] $Url)

    $uri = $null
    if (-not [System.Uri]::TryCreate($Url, [System.UriKind]::Absolute, [ref] $uri)) {
        return $null
    }

    $segments = @([System.Uri]::UnescapeDataString($uri.AbsolutePath.Trim("/")) -split "/" | Where-Object { $_ -ne "" })
    if ($segments.Count -lt 3) {
        return $null
    }

    $commitSha = $null
    if ($segments[2] -eq "commit" -and $segments.Count -eq 4) {
        $commitSha = $segments[3]
    }

    [pscustomobject]@{
        Kind = $segments[2]
        CommitSha = $commitSha
    }
}

function Get-ManagedPresentationLinks {
    param([object] $MetadataInfo)

    $links = [System.Collections.Generic.List[object]]::new()
    if ($null -eq $MetadataInfo -or -not $MetadataInfo.HasPresentation -or $MetadataInfo.IsPresentationMalformed) {
        return $links.ToArray()
    }

    foreach ($line in @($MetadataInfo.PresentationLines)) {
        foreach ($match in [regex]::Matches($line, $script:ManagedHistoryLinkPattern)) {
            $links.Add([pscustomobject]@{
                Label = $match.Groups["label"].Value
                Url = $match.Groups["url"].Value
                Line = $line
            })
        }
    }

    $links.ToArray()
}

function Test-ManagedPresentationLink {
    param(
        [object] $Link,
        [string] $RepoPath,
        [object] $Config
    )

    if ($null -eq $Link) {
        return $false
    }

    $label = [string] (Get-PropertyValue -Object $Link -Name "Label")
    $url = [string] (Get-PropertyValue -Object $Link -Name "Url")

    if (-not $label.Equals("View Commit", [System.StringComparison]::Ordinal)) {
        return $false
    }

    if (-not (Test-ManagedHistoryUrl -Url $url -RepoPath $RepoPath)) {
        return $false
    }

    $urlParts = Get-ManagedHistoryUrlParts -Url $url
    if ($null -eq $urlParts -or $urlParts.Kind -ne "commit" -or [string]::IsNullOrWhiteSpace($urlParts.CommitSha)) {
        return $false
    }

    if ([string]::IsNullOrWhiteSpace($RepoPath) -or $null -eq $Config) {
        return $false
    }

    Test-CommitManagedBodyChanged -RepoPath $RepoPath -Config $Config -CommitSha $urlParts.CommitSha
}

function Test-ManagedPresentationUrls {
    param(
        [object] $MetadataInfo,
        [string] $RepoPath,
        [object] $Config
    )

    foreach ($link in @(Get-ManagedPresentationLinks -MetadataInfo $MetadataInfo)) {
        if (-not (Test-ManagedPresentationLink -Link $link -RepoPath $RepoPath -Config $Config)) {
            return $false
        }
    }

    $true
}

function Test-ManagedHistoryLineIsUnproven {
    param(
        [string] $Line,
        [string] $RepoPath = "",
        [object] $Config = $null
    )

    if ([string]::IsNullOrWhiteSpace($Line)) {
        return $false
    }

    if ($Line -match "Changes:\s*<b>Unavailable</b>") {
        return $true
    }

    $matches = [regex]::Matches($Line, $script:ManagedHistoryLinkPattern)
    if ($matches.Count -eq 0) {
        return $false
    }

    foreach ($match in $matches) {
        $link = [pscustomobject]@{
            Label = $match.Groups["label"].Value
            Url = $match.Groups["url"].Value
            Line = $Line
        }
        if (-not (Test-ManagedPresentationLink -Link $link -RepoPath $RepoPath -Config $Config)) {
            return $true
        }
    }

    $false
}

function Get-HistoryEntryLines {
    param([object] $MetadataInfo)

    if ($null -eq $MetadataInfo -or -not $MetadataInfo.HasPresentation -or $MetadataInfo.IsPresentationMalformed) {
        return @()
    }

    @($MetadataInfo.PresentationLines | Where-Object { $_ -match "^\s*-\s+Updated:" })
}

function Get-CurrentChangesLine {
    param([object] $MetadataInfo)

    if ($null -eq $MetadataInfo -or -not $MetadataInfo.HasPresentation -or $MetadataInfo.IsPresentationMalformed) {
        return $null
    }

    foreach ($line in @($MetadataInfo.PresentationLines)) {
        if ($line.Trim() -eq $script:PresentationStartMarker) {
            continue
        }
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        if ($line.Trim() -eq "<details>") {
            return $null
        }
        if ($line -match $script:CurrentChangesLinkPattern) {
            return $line
        }
        return $null
    }

    $null
}

function Test-PresentationEquivalent {
    param(
        [object] $Left,
        [object] $Right
    )

    if ($null -eq $Left -or $null -eq $Right) {
        return $false
    }
    if (-not $Left.HasPresentation -or -not $Right.HasPresentation -or $Left.IsPresentationMalformed -or $Right.IsPresentationMalformed) {
        return $false
    }

    (@($Left.PresentationLines) -join "`n") -eq (@($Right.PresentationLines) -join "`n")
}

function Test-ManagedHistoryEquivalent {
    param(
        [object] $Left,
        [object] $Right,
        [string] $RepoPath = "",
        [object] $Config = $null
    )

    if ($null -eq $Left -or $null -eq $Right) {
        return $false
    }
    if (-not $Left.HasPresentation -or -not $Right.HasPresentation -or $Left.IsPresentationMalformed -or $Right.IsPresentationMalformed) {
        return $false
    }

    $leftCurrent = Get-CurrentChangesLine -MetadataInfo $Left
    $rightCurrent = Get-CurrentChangesLine -MetadataInfo $Right
    if ($leftCurrent -ne $rightCurrent) {
        if ([string]::IsNullOrWhiteSpace($leftCurrent) -and (Test-ManagedHistoryLineIsUnproven -Line $rightCurrent -RepoPath $RepoPath -Config $Config)) {
            $leftHistory = @(Get-HistoryEntryLines -MetadataInfo $Left)
            $rightHistory = @(Get-HistoryEntryLines -MetadataInfo $Right | Where-Object { -not (Test-ManagedHistoryLineIsUnproven -Line $_ -RepoPath $RepoPath -Config $Config) })
            return (($leftHistory -join "`n") -eq ($rightHistory -join "`n"))
        }

        return $false
    }

    $leftHistoryLines = @(Get-HistoryEntryLines -MetadataInfo $Left)
    $rightHistoryLines = @(Get-HistoryEntryLines -MetadataInfo $Right)
    if (($leftHistoryLines -join "`n") -eq ($rightHistoryLines -join "`n")) {
        return $true
    }

    if ([string]::IsNullOrWhiteSpace($RepoPath)) {
        return $false
    }

    $rightProvenHistoryLines = @($rightHistoryLines | Where-Object { -not (Test-ManagedHistoryLineIsUnproven -Line $_ -RepoPath $RepoPath -Config $Config) })
    ($leftHistoryLines -join "`n") -eq ($rightProvenHistoryLines -join "`n")
}

function Test-TrueValue {
    param([object] $Value)

    if ($Value -is [bool]) {
        return [bool] $Value
    }

    if ($Value -is [string]) {
        return ([string] $Value).Equals("true", [System.StringComparison]::OrdinalIgnoreCase)
    }

    $false
}

function Test-CommitManagedBodyChanged {
    param(
        [string] $RepoPath,
        [object] $Config,
        [string] $CommitSha
    )

    if ([string]::IsNullOrWhiteSpace($CommitSha) -or $CommitSha -notmatch "^[0-9a-fA-F]{40}$" -or -not (Test-GitCommitExists $CommitSha)) {
        return $false
    }

    $parentsLine = Invoke-GitText -Arguments @("-C", $script:RepositoryRoot, "rev-list", "--parents", "-n", "1", $CommitSha)
    if ($parentsLine.ExitCode -ne 0) {
        return $false
    }

    $tokens = @($parentsLine.Text.Trim() -split "\s+" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $parentCount = [Math]::Max(0, $tokens.Count - 1)
    if ($parentCount -gt 1) {
        return $false
    }

    $baseSha = if ($parentCount -eq 1) { [string] $tokens[1] } else { $null }
    $record = [pscustomobject]@{
        Path = Normalize-RepoPath $RepoPath
        PreviousPath = Normalize-RepoPath $RepoPath
        Config = $Config
    }

    $changeResult = Get-ManagedBodyChangeResult -Record $record -RequestedBaseSha $baseSha -RequestedHeadSha $CommitSha
    [bool] $changeResult.bodyChanged
}

function New-MarkdownPresentation {
    param(
        [object] $Config,
        [hashtable] $Values,
        [string] $NewLine,
        [object] $MetadataInfo
    )

    $historyLines = [System.Collections.Generic.List[string]]::new()
    $limit = if ($null -eq $Config.historyLimit) { $null } else { [int] $Config.historyLimit }
    $sourceMetadataInfo = if ($Values.ContainsKey("__sourcePresentationInfo")) { $Values["__sourcePresentationInfo"] } else { $MetadataInfo }
    $addHistoryEntry = if ($Values.ContainsKey("__addHistoryEntry")) { [bool] $Values["__addHistoryEntry"] } else { $false }
    $currentChangesLineMode = if ($Values.ContainsKey("__currentChangesLineMode")) { [string] $Values["__currentChangesLineMode"] } else { "preserve" }
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $currentChangesLine = $null
    if ($null -eq $limit -or $limit -gt 0) {
        $updated = $Values[$Config.updatedField]
        $author = $Values[$Config.authorField]
        $historyUrl = if ($Values.ContainsKey("__historyUrl")) { [string] $Values["__historyUrl"] } else { "" }
        $historyLinkText = if ($Values.ContainsKey("__historyLinkText")) { [string] $Values["__historyLinkText"] } else { "View Commit" }
        switch ($currentChangesLineMode) {
            "preserve" {
                $currentChangesLine = Get-CurrentChangesLine -MetadataInfo $sourceMetadataInfo
            }
            "replace" {
                if (-not [string]::IsNullOrWhiteSpace($historyUrl) -and $historyLinkText -eq "View Commit") {
                    $currentChangesLine = "[<b>$historyLinkText</b>]($historyUrl)"
                }
            }
            "clear" {
                $currentChangesLine = $null
            }
            default {
                $currentChangesLine = Get-CurrentChangesLine -MetadataInfo $sourceMetadataInfo
            }
        }
        if ($addHistoryEntry -and -not [string]::IsNullOrWhiteSpace($historyUrl) -and $historyLinkText -eq "View Commit") {
            $newEntry = "- Updated: <b>$updated</b> | Author: <b>$author</b> | Changes: [<b>$historyLinkText</b>]($historyUrl)"

            [void] $seen.Add($newEntry)
            $historyLines.Add($newEntry)
        }
        foreach ($line in (Get-HistoryEntryLines -MetadataInfo $sourceMetadataInfo)) {
            if ($seen.Add($line)) {
                $historyLines.Add($line)
            }
        }

        if ($null -ne $limit -and $historyLines.Count -gt $limit) {
            $trimmedHistoryLines = [System.Collections.Generic.List[string]]::new()
            foreach ($line in @($historyLines | Select-Object -First $limit)) {
                $trimmedHistoryLines.Add($line)
            }
            $historyLines = $trimmedHistoryLines
        }
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add($script:PresentationStartMarker)
    if (-not [string]::IsNullOrWhiteSpace($currentChangesLine)) {
        $lines.Add($currentChangesLine)
        $lines.Add("")
    }
    $lines.Add("<details>")
    $lines.Add("<summary>Change History</summary>")
    $lines.Add("")
    foreach ($line in @($historyLines)) {
        $lines.Add($line)
    }
    $lines.Add("")
    $lines.Add("</details>")
    $lines.Add("")
    if ($Config.includeSeparator) {
        $lines.Add("---")
        $lines.Add("")
    }
    for ($index = 0; $index -lt [int] $Config.spacingBreaks; $index++) {
        $lines.Add("<br>")
    }
    $lines.Add($script:PresentationEndMarker)
    $lines.Add("")
    ($lines -join $NewLine) + $NewLine
}

function New-PlainTextPresentation {
    param(
        [object] $Config,
        [string] $NewLine
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    if ($Config.includeSeparator) {
        $lines.Add($script:PlainTextSeparator)
    }
    for ($index = 0; $index -lt [int] $Config.spacingBreaks; $index++) {
        $lines.Add("")
    }

    if ($lines.Count -eq 0) {
        return ""
    }

    ($lines -join $NewLine) + $NewLine
}

function New-ManagedPresentation {
    param(
        [object] $Config,
        [hashtable] $Values,
        [string] $NewLine,
        [object] $MetadataInfo
    )

    if ($Config.presentationEnabled -and $Config.isMarkdown) {
        return New-MarkdownPresentation -Config $Config -Values $Values -NewLine $NewLine -MetadataInfo $MetadataInfo
    }

    if (-not $Config.presentationEnabled) {
        return New-PlainTextPresentation -Config $Config -NewLine $NewLine
    }

    ""
}

function Set-MetadataContent {
    param(
        [string] $Content,
        [object] $Config,
        [hashtable] $Values,
        [string] $NewLine
    )

    $info = Get-MetadataInfo -Content $Content -Config $Config
    $metadataLines = [string[]]::new(0)
    if ($info.HasMetadata -and -not $info.IsMalformed) {
        $metadataLines = [string[]] @($info.MetadataLines)
    }
    $updatedLines = Update-MetadataLines -MetadataLines $metadataLines -Values $Values -Config $Config
    $metadataBlock = New-MetadataBlock -Lines $updatedLines -Config $Config -NewLine $NewLine
    $presentationBlock = New-ManagedPresentation -Config $Config -Values $Values -NewLine $NewLine -MetadataInfo $info

    if ($Config.metadataFormat -eq "yaml-front-matter" -or $Config.metadataPlacement -eq "top") {
        return $metadataBlock + $presentationBlock + $info.Body
    }

    $body = $info.Body
    if ($body.Length -gt 0 -and -not ($body.EndsWith("`n", [System.StringComparison]::Ordinal) -or $body.EndsWith("`r", [System.StringComparison]::Ordinal))) {
        $body += $NewLine
    }

    $body + $presentationBlock + $metadataBlock
}

function Validate-FileMetadata {
    param(
        [object] $MetadataInfo,
        [object] $Config,
        [string] $RepoPath = ""
    )

    $errors = [System.Collections.Generic.List[object]]::new()
    if (-not $MetadataInfo.HasMetadata) {
        $errors.Add([pscustomobject]@{
            Rule = "metadata block exists"
            Current = "missing"
            Expected = "managed metadata block must be present"
        })
        return $errors.ToArray()
    }

    if ($MetadataInfo.IsMalformed) {
        $errors.Add([pscustomobject]@{
            Rule = "metadata block format"
            Current = "malformed"
            Expected = "metadata block must match manifest metadataFormat"
        })
        return $errors.ToArray()
    }

    if ($MetadataInfo.IsPresentationMalformed) {
        $errors.Add([pscustomobject]@{
            Rule = "managed metadata presentation"
            Current = "malformed"
            Expected = "managed presentation region must be complete and restorable"
        })
    }
    $expectsPresentation = ($Config.presentationEnabled -and $Config.isMarkdown) -or ((-not $Config.presentationEnabled) -and $Config.includeSeparator)
    if ($expectsPresentation -and -not $MetadataInfo.HasPresentation) {
        $errors.Add([pscustomobject]@{
            Rule = "managed metadata presentation"
            Current = "missing"
            Expected = "managed metadata presentation/separator must be present"
        })
    }
    if ($Config.presentationEnabled -and $Config.isMarkdown -and $MetadataInfo.HasPresentation -and -not $MetadataInfo.IsPresentationMalformed) {
        $presentationLines = @($MetadataInfo.PresentationLines)
        $presentationText = $presentationLines -join "`n"
        if ($presentationText -notmatch '(?s)^<!-- doc-metadata-presentation:start -->.*?<details>\n<summary>Change History</summary>.*?</details>.*?<!-- doc-metadata-presentation:end -->') {
            $errors.Add([pscustomobject]@{
                Rule = "managed metadata presentation"
                Current = "malformed"
                Expected = "managed presentation must contain complete Change History details"
            })
        }
        if ($presentationLines.Count -eq 0 -or -not [string]::IsNullOrWhiteSpace($presentationLines[$presentationLines.Count - 1])) {
            $errors.Add([pscustomobject]@{
                Rule = "managed metadata presentation"
                Current = "missing trailing blank line"
                Expected = "blank physical line after doc-metadata-presentation end marker"
            })
        }
        foreach ($link in @(Get-ManagedPresentationLinks -MetadataInfo $MetadataInfo)) {
            if (-not (Test-ManagedPresentationLink -Link $link -RepoPath $RepoPath -Config $Config)) {
                $errors.Add([pscustomobject]@{
                    Rule = "managed history URL"
                    Current = "invalid link: $($link.Label) $($link.Url)"
                    Expected = "proven View Commit link to a commit that changed this file's managed body"
                })
            }
        }
    }

    $versionValue = if ($MetadataInfo.Fields.ContainsKey($Config.versionField)) { $MetadataInfo.Fields[$Config.versionField] } else { $null }
    $createdValue = if ($MetadataInfo.Fields.ContainsKey($Config.createdField)) { $MetadataInfo.Fields[$Config.createdField] } else { $null }
    $updatedValue = if ($MetadataInfo.Fields.ContainsKey($Config.updatedField)) { $MetadataInfo.Fields[$Config.updatedField] } else { $null }
    $authorValue = if ($MetadataInfo.Fields.ContainsKey($Config.authorField)) { $MetadataInfo.Fields[$Config.authorField] } else { $null }

    if (-not (Test-VersionValue $versionValue)) {
        $errors.Add([pscustomobject]@{
            Rule = $Config.versionField
            Current = $versionValue
            Expected = "positive integer or numeric dotted document revision"
        })
    }

    if (-not (Test-IsTimestamp $createdValue)) {
        $errors.Add([pscustomobject]@{
            Rule = $Config.createdField
            Current = $createdValue
            Expected = "ISO-8601 timestamp with timezone offset"
        })
    }

    if (-not (Test-IsTimestamp $updatedValue)) {
        $errors.Add([pscustomobject]@{
            Rule = $Config.updatedField
            Current = $updatedValue
            Expected = "ISO-8601 timestamp with timezone offset"
        })
    }

    if ($authorValue -isnot [string] -or [string]::IsNullOrWhiteSpace($authorValue) -or ([string] $authorValue).IndexOfAny([char[]]@("`r", "`n")) -ge 0) {
        $errors.Add([pscustomobject]@{
            Rule = $Config.authorField
            Current = $authorValue
            Expected = "non-empty plain scalar author"
        })
    }

    $errors.ToArray()
}

function Get-MetadataSnapshot {
    param(
        [object] $MetadataInfo,
        [object] $Config
    )

    if (-not $MetadataInfo.HasMetadata -or $MetadataInfo.IsMalformed) {
        return [pscustomobject]@{
            Version = $null
            Created = $null
            Updated = $null
            Author = $null
        }
    }

    [pscustomobject]@{
        Version = if ($MetadataInfo.Fields.ContainsKey($Config.versionField)) { ConvertTo-VersionString $MetadataInfo.Fields[$Config.versionField] } else { $null }
        Created = if ($MetadataInfo.Fields.ContainsKey($Config.createdField)) { $MetadataInfo.Fields[$Config.createdField] } else { $null }
        Updated = if ($MetadataInfo.Fields.ContainsKey($Config.updatedField)) { $MetadataInfo.Fields[$Config.updatedField] } else { $null }
        Author = if ($MetadataInfo.Fields.ContainsKey($Config.authorField)) { $MetadataInfo.Fields[$Config.authorField] } else { $null }
    }
}

function Get-ComparisonInfo {
    param(
        [string] $RequestedEventName,
        [string] $RequestedEventPayloadPath,
        [string] $RequestedHeadSha,
        [string] $RequestedBaseSha
    )

    $comparison = @{
        mode = "format-only fallback"
        baseSha = $null
        headSha = $null
        staleCheckAvailable = $false
        reason = "No reliable comparison base is available."
    }

    if (-not (Test-InGitRepository)) {
        $comparison.reason = "Not inside a Git repository."
        return $comparison
    }

    if ([string]::IsNullOrWhiteSpace($RequestedEventName) -and -not [string]::IsNullOrWhiteSpace($RequestedBaseSha) -and -not [string]::IsNullOrWhiteSpace($RequestedHeadSha)) {
        if (-not (Test-GitCommitExists $RequestedBaseSha)) {
            throw "Base SHA '$RequestedBaseSha' is not fetchable in the local checkout."
        }
        if (-not (Test-GitCommitExists $RequestedHeadSha)) {
            throw "Head SHA '$RequestedHeadSha' is not fetchable in the local checkout."
        }

        $comparison.mode = "local"
        $comparison.baseSha = $RequestedBaseSha
        $comparison.headSha = $RequestedHeadSha
        $comparison.staleCheckAvailable = $true
        $comparison.reason = "Comparing explicit base/head SHAs."
        return $comparison
    }

    if (-not [string]::IsNullOrWhiteSpace($RequestedEventName)) {
        if ([string]::IsNullOrWhiteSpace($RequestedEventPayloadPath) -or -not (Test-Path -LiteralPath $RequestedEventPayloadPath -PathType Leaf)) {
            throw "Event data was provided for '$RequestedEventName', but EventPayloadPath is missing or unreadable."
        }

        $payload = Get-Content -LiteralPath $RequestedEventPayloadPath -Raw | ConvertFrom-Json -Depth 64
        if ($RequestedEventName -eq "pull_request") {
            $payloadBase = Get-PropertyValue -Object (Get-PropertyValue -Object (Get-PropertyValue -Object $payload -Name "pull_request") -Name "base") -Name "sha"
            $payloadHead = Get-PropertyValue -Object (Get-PropertyValue -Object (Get-PropertyValue -Object $payload -Name "pull_request") -Name "head") -Name "sha"
            $base = if (-not [string]::IsNullOrWhiteSpace($RequestedBaseSha)) { $RequestedBaseSha } else { $payloadBase }
            $head = $payloadHead

            if ([string]::IsNullOrWhiteSpace($base) -or [string]::IsNullOrWhiteSpace($head)) {
                throw "pull_request event payload does not contain fetchable base/head SHAs."
            }
            if (-not (Test-GitCommitExists $base)) {
                throw "pull_request base SHA '$base' is not fetchable in the local checkout."
            }
            if (-not (Test-GitCommitExists $head)) {
                throw "pull_request head SHA '$head' is not fetchable in the local checkout."
            }

            $mergeBase = Get-GitMergeBase -Base $base -Head $head
            if ([string]::IsNullOrWhiteSpace($mergeBase)) {
                throw "Unable to compute git merge-base for pull_request base '$base' and head '$head'."
            }

            $comparison.mode = "pull_request"
            $comparison.baseSha = $mergeBase
            $comparison.headSha = $head
            $comparison.staleCheckAvailable = $true
            $comparison.reason = "Comparing pull request merge-base to PR head."
            return $comparison
        }

        if ($RequestedEventName -eq "push") {
            $before = Get-PropertyValue -Object $payload -Name "before"
            $after = Get-PropertyValue -Object $payload -Name "after"
            $head = if (-not [string]::IsNullOrWhiteSpace($RequestedHeadSha)) { $RequestedHeadSha } else { $after }
            if ([string]::IsNullOrWhiteSpace($head)) {
                throw "push event did not provide a head SHA."
            }
            if (-not (Test-GitCommitExists $head)) {
                throw "push head SHA '$head' is not fetchable in the local checkout."
            }

            $comparison.mode = "push"
            $comparison.headSha = $head
            if ($before -match "^0{40}$") {
                $comparison.baseSha = $null
                $comparison.staleCheckAvailable = $false
                $comparison.reason = "Push before SHA is all zero; governed files are treated as new for stale checks."
                return $comparison
            }

            if ([string]::IsNullOrWhiteSpace($before) -or -not (Test-GitCommitExists $before)) {
                throw "push before SHA '$before' is missing or not fetchable in the local checkout."
            }

            $comparison.baseSha = $before
            $comparison.staleCheckAvailable = $true
            $comparison.reason = "Comparing push before SHA to head SHA."
            return $comparison
        }

        $comparison.mode = "format-only fallback"
        $comparison.headSha = $RequestedHeadSha
        $comparison.reason = "Event '$RequestedEventName' has no reliable comparison base; stale checks skipped."
        return $comparison
    }

    $headResult = Invoke-GitText -Arguments @("-C", $script:RepositoryRoot, "rev-parse", "HEAD")
    $baseResult = Invoke-GitText -Arguments @("-C", $script:RepositoryRoot, "rev-parse", "HEAD^")
    if ($headResult.ExitCode -eq 0 -and $baseResult.ExitCode -eq 0) {
        $comparison.mode = "local"
        $comparison.baseSha = $baseResult.Text.Trim()
        $comparison.headSha = $headResult.Text.Trim()
        $comparison.staleCheckAvailable = $true
        $comparison.reason = "Comparing local HEAD^ to HEAD."
        return $comparison
    }

    $comparison
}

function Get-HistoryLinkInfo {
    param(
        [string] $RepoPath,
        [hashtable] $Comparison = $null,
        [object] $Config = $null
    )

    $map = $null
    if (-not [string]::IsNullOrWhiteSpace($HistoryLinkMapPath)) {
        $mapPath = Resolve-InRootPath -RootPath $script:RepositoryRoot -InputPath $HistoryLinkMapPath
        if ($null -ne $mapPath -and (Test-Path -LiteralPath $mapPath -PathType Leaf)) {
            $map = Get-Content -LiteralPath $mapPath -Raw | ConvertFrom-Json -Depth 32
        }
    }

    $mapEntry = if ($null -ne $map) { Get-PropertyValue -Object $map -Name $RepoPath } else { $null }
    if ($null -ne $mapEntry) {
        $entryPath = Get-PropertyValue -Object $mapEntry -Name "path"
        if ($null -eq $entryPath -or (Normalize-RepoPath ([string] $entryPath)) -ne (Normalize-RepoPath $RepoPath)) {
            return @{
                Url = ""
                LinkText = "View Commit"
                HasReliableContext = $false
                Reason = "link map path does not match governed file path"
            }
        }

        $url = [string] (Get-PropertyValue -Object $mapEntry -Name "url")
        if (-not (Test-ManagedHistoryUrl -Url $url -RepoPath $RepoPath)) {
            return @{
                Url = ""
                LinkText = "View Commit"
                HasReliableContext = $false
                Reason = "link map URL is not a safe managed history URL"
            }
        }

        $urlParts = Get-ManagedHistoryUrlParts -Url $url
        if ($null -eq $urlParts -or $urlParts.Kind -ne "commit") {
            return @{
                Url = ""
                LinkText = "View Commit"
                HasReliableContext = $false
                Reason = "link map URL is not a proven commit fallback URL"
            }
        }

        $commitSha = [string] (Get-PropertyValue -Object $mapEntry -Name "commitSha")
        if ([string]::IsNullOrWhiteSpace($commitSha) -or $commitSha -notmatch "^[0-9a-fA-F]{40}$" -or -not $commitSha.Equals($urlParts.CommitSha, [System.StringComparison]::OrdinalIgnoreCase)) {
            return @{
                Url = ""
                LinkText = "View Commit"
                HasReliableContext = $false
                Reason = "link map commitSha does not match commit URL"
            }
        }

        if (-not (Test-TrueValue (Get-PropertyValue -Object $mapEntry -Name "bodyChanged"))) {
            return @{
                Url = ""
                LinkText = "View Commit"
                HasReliableContext = $false
                Reason = "link map entry does not prove a managed body change"
            }
        }

        if ($null -eq $Config -or -not (Test-CommitManagedBodyChanged -RepoPath $RepoPath -Config $Config -CommitSha $commitSha)) {
            return @{
                Url = ""
                LinkText = "View Commit"
                HasReliableContext = $false
                Reason = "link map commit does not change the governed file's managed body"
            }
        }

        return @{
            Url = $url
            LinkText = "View Commit"
            HasReliableContext = $true
            Reason = "validated link map entry"
        }
    }

    @{
        Url = ""
        LinkText = "View Commit"
        HasReliableContext = $false
        Reason = "no validated content-change link map entry"
    }
}

function Resolve-DocumentAuthor {
    param(
        [string] $RepoPath,
        [hashtable] $Comparison = $null
    )

    if ($null -ne $Comparison -and $Comparison.staleCheckAvailable -and -not [string]::IsNullOrWhiteSpace($Comparison.baseSha) -and -not [string]::IsNullOrWhiteSpace($Comparison.headSha)) {
        $logResult = Invoke-GitText -Arguments @("-C", $script:RepositoryRoot, "log", "--format=%an", "$($Comparison.baseSha)..$($Comparison.headSha)", "--", $RepoPath)
        if ($logResult.ExitCode -eq 0) {
            $authors = @($logResult.Text -split "\r?\n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            $nonBot = @($authors | Where-Object { $_ -ne "github-actions[bot]" } | Select-Object -First 1)
            if ($nonBot.Count -gt 0) {
                return [string] $nonBot[0]
            }
            if ($authors.Count -gt 0) {
                return [string] $authors[0]
            }
        }
    }

    $gitName = Invoke-GitText -Arguments @("-C", $script:RepositoryRoot, "config", "user.name")
    if ($gitName.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($gitName.Text.Trim())) {
        return $gitName.Text.Trim()
    }

    foreach ($candidate in @($env:GIT_AUTHOR_NAME, $env:GITHUB_ACTOR, $env:USERNAME, $env:USER, [Environment]::UserName)) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            return [string] $candidate
        }
    }

    "Unknown"
}

function Convert-ExplicitPathsToRecords {
    param(
        [string[]] $RequestedPaths,
        [hashtable] $Report
    )

    $records = [System.Collections.Generic.List[object]]::new()
    foreach ($requestedPath in @($RequestedPaths)) {
        $fullPath = Resolve-InRootPath -RootPath $script:RepositoryRoot -InputPath $requestedPath
        if ($null -eq $fullPath) {
            Add-SkippedFile -Report $Report -Path (Normalize-RepoPath $requestedPath) -Reason "outside repository root"
            continue
        }

        $repoPath = ConvertTo-RepoRelativePath -RootPath $script:RepositoryRoot -FullPath $fullPath
        $records.Add([pscustomobject]@{
            Status = "explicit"
            Path = $repoPath
            PreviousPath = $repoPath
        })
    }

    $records.ToArray()
}

function Get-SelectedGovernedRecords {
    param(
        [object] $Manifest,
        [hashtable] $GovernedFiles,
        [hashtable] $Report,
        [string] $ModeValue
    )

    [object[]] $requestedPathValues = @(Get-NonNullValues -Values $Path)
    if ($requestedPathValues.Length -gt 0) {
        $candidateRecords = Convert-ExplicitPathsToRecords -RequestedPaths $Path -Report $Report
    }
    elseif ($ModeValue -eq "Update") {
        $candidateRecords = Get-UpdateCandidateRecords
    }
    else {
        $candidateRecords = @($GovernedFiles.Values | ForEach-Object {
            [pscustomobject]@{
                Status = "governed"
                Path = $_.Path
                PreviousPath = $_.Path
            }
        })
    }

    $selected = [System.Collections.Generic.List[object]]::new()
    foreach ($record in @($candidateRecords)) {
        $repoPath = Normalize-RepoPath $record.Path
        if (-not $GovernedFiles.ContainsKey($repoPath)) {
            $ineligible = @($Report.ineligibleFiles | Where-Object { $_.path -eq $repoPath } | Select-Object -First 1)
            $reason = if ($ineligible.Count -gt 0) { "ineligible by documentEligibility: $($ineligible[0].reason)" } elseif (Test-ManifestExcludedPath -Manifest $Manifest -RepoPath $repoPath) { "excluded by manifest" } else { "not governed by manifest" }
            Add-SkippedFile -Report $Report -Path $repoPath -Reason $reason
            continue
        }

        $governed = $GovernedFiles[$repoPath]
        $selected.Add([pscustomobject]@{
            Path = $repoPath
            PreviousPath = Normalize-RepoPath $record.PreviousPath
            FullPath = $governed.FullPath
            Config = $governed.Config
        })
    }

    @($selected.ToArray() | Sort-Object -Property Path -Unique)
}

function Initialize-OrUpdateFile {
    param(
        [object] $Record,
        [hashtable] $Report,
        [string] $ModeValue,
        [hashtable] $Comparison = $null
    )

    $repoPath = $Record.Path
    $config = $Record.Config

    if (-not (Test-Path -LiteralPath $Record.FullPath -PathType Leaf)) {
        Add-SkippedFile -Report $Report -Path $repoPath -Reason "deleted file"
        return
    }

    if (Test-PathIsReparsePoint -FullPath $Record.FullPath) {
        Add-SkippedFile -Report $Report -Path $repoPath -Reason "reparse point / symlink write blocked"
        return
    }

    try {
        $textFile = Read-StrictUtf8Text -FullPath $Record.FullPath
    }
    catch {
        Add-FailedFile -Report $Report -Path $repoPath -Rule "UTF-8" -Current "invalid UTF-8" -Expected "valid UTF-8 text" -Remediation "Convert the file to UTF-8 or exclude it from .github/tools/doc-metadata/doc-metadata-manifest.json."
        return
    }

    $currentInfo = Get-MetadataInfo -Content $textFile.Content -Config $config
    $currentSnapshot = Get-MetadataSnapshot -MetadataInfo $currentInfo -Config $config
    $previousRevision = if ($null -ne $Comparison -and $Comparison.staleCheckAvailable) { $Comparison.baseSha } else { "HEAD" }
    $previousContent = Get-GitFileContent -Revision $previousRevision -RepoPath $Record.PreviousPath
    $previousInfo = if ($null -ne $previousContent) { Get-MetadataInfo -Content $previousContent -Config $config } else { $null }
    $previousSnapshot = if ($null -ne $previousInfo) { Get-MetadataSnapshot -MetadataInfo $previousInfo -Config $config } else { $null }

    $bodyChanged = if ($null -ne $previousInfo) { (ConvertTo-ComparableBody $currentInfo.Body) -ne (ConvertTo-ComparableBody $previousInfo.Body) } else { $false }
    $now = ConvertTo-TimestampValue

    if ($currentInfo.HasMetadata -and -not $currentInfo.IsMalformed) {
        [object[]] $metadataValidationErrors = @(Validate-FileMetadata -MetadataInfo $currentInfo -Config $config -RepoPath $repoPath)
        [object[]] $invalidVersionErrors = @($metadataValidationErrors | Where-Object { $_.Rule -eq $config.versionField -and $null -ne $_.Current })
        if ($invalidVersionErrors.Count -gt 0) {
            foreach ($error in $invalidVersionErrors) {
                Add-FailedFile -Report $Report -Path $repoPath -Rule $error.Rule -Current $error.Current -Expected $error.Expected
            }
            return
        }
    }

    if ($null -ne $previousSnapshot -and $null -ne $previousSnapshot.Version -and $null -ne $currentSnapshot.Version -and (Compare-VersionValue $currentSnapshot.Version $previousSnapshot.Version) -lt 0) {
        Add-FailedFile -Report $Report -Path $repoPath -Rule "Version must not decrease without explicit rebaseline approval" -Current $currentSnapshot.Version -Expected "greater than or equal to previous committed Version $($previousSnapshot.Version)"
        return
    }

    if ($currentInfo.IsMalformed) {
        Add-FailedFile -Report $Report -Path $repoPath -Rule "metadata block format" -Current "malformed" -Expected "metadata block intent can be determined safely"
        return
    }

    if (-not $currentInfo.HasMetadata) {
        $initialVersion = 1
        $initialCreated = $now
        $initialUpdated = $now
        $reason = "missing metadata initialized"

        if ($null -ne $previousSnapshot -and $null -ne $previousSnapshot.Version -and (Test-IsTimestamp $previousSnapshot.Created) -and (Test-IsTimestamp $previousSnapshot.Updated)) {
            $initialVersion = if ($bodyChanged) { Get-IncrementedVersion $previousSnapshot.Version } else { $previousSnapshot.Version }
            $initialCreated = $previousSnapshot.Created
            $initialUpdated = if ($bodyChanged) { $now } else { $previousSnapshot.Updated }
            $reason = if ($bodyChanged) { "body changed; metadata restored from history" } else { "metadata restored from history" }
        }
        if (Test-IsTimestamp $initialCreated) {
            $initialCreated = ConvertTo-UtcTimestampValue $initialCreated
        }
        if (Test-IsTimestamp $initialUpdated) {
            $initialUpdated = ConvertTo-UtcTimestampValue $initialUpdated
        }
        $author = Resolve-DocumentAuthor -RepoPath $repoPath -Comparison $Comparison
        $linkInfo = Get-HistoryLinkInfo -RepoPath $repoPath -Comparison $Comparison -Config $config
        $hasNewContentReference = $null -eq $previousContent -and $null -ne $Comparison -and $Comparison.staleCheckAvailable -and $linkInfo.HasReliableContext
        $addHistoryEntry = ($bodyChanged -or $hasNewContentReference) -and $linkInfo.HasReliableContext
        $currentChangesLineMode = if ($addHistoryEntry) { "replace" } else { "clear" }

        $values = @{
            $config.versionField = $initialVersion
            $config.createdField = $initialCreated
            $config.updatedField = $initialUpdated
            $config.authorField = $author
            "__historyUrl" = $linkInfo.Url
            "__historyLinkText" = $linkInfo.LinkText
            "__addHistoryEntry" = $addHistoryEntry
            "__currentChangesLineMode" = $currentChangesLineMode
        }
        $newContent = Set-MetadataContent -Content $textFile.Content -Config $config -Values $values -NewLine $textFile.NewLine
        if ($PSCmdlet.ShouldProcess($repoPath, "Initialize document metadata")) {
            Write-StrictUtf8Text -FullPath $Record.FullPath -Content $newContent -HasBom $textFile.HasBom
        }
        Add-UpdatedFile -Report $Report -Path $repoPath -Format $config.metadataFormat -Placement $config.metadataPlacement -OldVersion $null -NewVersion $initialVersion -OldCreated $null -NewCreated $initialCreated -OldUpdated $null -NewUpdated $initialUpdated -Reason $reason
        return
    }

    [object[]] $validationErrors = @(Validate-FileMetadata -MetadataInfo $currentInfo -Config $config -RepoPath $repoPath)
    [object[]] $presentationErrors = @($validationErrors | Where-Object { $_.Rule -eq "managed metadata presentation" })
    if (@($presentationErrors | Where-Object { $_.Current -eq "malformed" }).Count -gt 0 -and ($null -eq $previousInfo -or -not $previousInfo.HasPresentation -or $previousInfo.IsPresentationMalformed)) {
        Add-FailedFile -Report $Report -Path $repoPath -Rule "managed metadata presentation" -Current "malformed" -Expected "trusted previous generated presentation for safe restoration"
        return
    }
    [object[]] $nonRepairableVersionErrors = @($validationErrors | Where-Object { $_.Rule -eq $config.versionField -and $null -ne $_.Current })
    [object[]] $repairableErrors = @($validationErrors | Where-Object { -not ($_.Rule -eq $config.versionField -and $null -ne $_.Current) })
    if ($ModeValue -eq "Bootstrap" -and $validationErrors.Length -eq 0) {
        Add-UnchangedFile -Report $Report -Path $repoPath -Reason "metadata valid" -OldVersion $currentSnapshot.Version -NewVersion $currentSnapshot.Version
        return
    }

    if ($nonRepairableVersionErrors.Count -gt 0) {
        foreach ($error in $nonRepairableVersionErrors) {
            Add-FailedFile -Report $Report -Path $repoPath -Rule $error.Rule -Current $error.Current -Expected $error.Expected
        }
        return
    }

    $newVersion = $currentSnapshot.Version
    $newCreated = $currentSnapshot.Created
    $newUpdated = $currentSnapshot.Updated
    $newAuthor = $currentSnapshot.Author
    $reason = $null
    $shouldWrite = $false
    [object[]] $timestampValidationErrors = @($validationErrors | Where-Object { $_.Rule -in @($config.createdField, $config.updatedField) })
    [object[]] $urlValidationErrors = @($validationErrors | Where-Object { $_.Rule -eq "managed history URL" })
    $hasManualVersionIncrease = $null -ne $previousSnapshot -and $null -ne $previousSnapshot.Version -and $null -ne $currentSnapshot.Version -and (Compare-VersionValue $currentSnapshot.Version $previousSnapshot.Version) -gt 0
    $hasValidPreviousTimestamps = $null -ne $previousSnapshot -and (Test-IsTimestamp $previousSnapshot.Created) -and (Test-IsTimestamp $previousSnapshot.Updated)
    $hasValidPreviousMetadata = $hasValidPreviousTimestamps -and $null -ne $previousSnapshot.Version -and -not [string]::IsNullOrWhiteSpace([string] $previousSnapshot.Author)
    $hasTimestampDriftFromPrevious = $null -ne $previousSnapshot -and ((-not (Test-TimestampEquivalent $currentSnapshot.Created $previousSnapshot.Created)) -or (-not (Test-TimestampEquivalent $currentSnapshot.Updated $previousSnapshot.Updated)))
    $hasAuthorDriftFromPrevious = $null -ne $previousSnapshot -and $null -ne $previousSnapshot.Author -and $currentSnapshot.Author -ne $previousSnapshot.Author
    $hasPresentationDriftFromPrevious = $null -ne $previousInfo -and $currentInfo.HasPresentation -and $previousInfo.HasPresentation -and -not (Test-ManagedHistoryEquivalent -Left $currentInfo -Right $previousInfo -RepoPath $repoPath -Config $config)
    $canRestoreMissingPresentation = $null -ne $previousInfo -and -not $currentInfo.HasPresentation -and $previousInfo.HasPresentation -and -not $previousInfo.IsPresentationMalformed
    $canRestoreUrlFromPrevious = $null -ne $previousInfo -and $previousInfo.HasPresentation -and -not $previousInfo.IsPresentationMalformed -and (Test-ManagedPresentationUrls -MetadataInfo $previousInfo -RepoPath $repoPath -Config $config)
    [object[]] $metadataFieldValidationErrors = @($validationErrors | Where-Object { $_.Rule -in @($config.versionField, $config.createdField, $config.updatedField, $config.authorField) })
    [object[]] $presentationValidationErrors = @($validationErrors | Where-Object { $_.Rule -eq "managed metadata presentation" })

    if ($urlValidationErrors.Count -gt 0 -and -not $canRestoreUrlFromPrevious) {
        foreach ($error in $urlValidationErrors) {
            Add-FailedFile -Report $Report -Path $repoPath -Rule $error.Rule -Current $error.Current -Expected $error.Expected
        }
        return
    }

    if ($ModeValue -eq "Update" -and -not $bodyChanged -and ($timestampValidationErrors.Count -gt 0 -or $hasTimestampDriftFromPrevious -or $hasAuthorDriftFromPrevious -or $hasPresentationDriftFromPrevious)) {
        if (-not $hasValidPreviousMetadata) {
            Add-FailedFile -Report $Report -Path $repoPath -Rule "created/updated timestamp drift could not be safely restored" -Current "created=$($currentSnapshot.Created); updated=$($currentSnapshot.Updated)" -Expected "valid previous committed created and updated timestamps"
            return
        }

        $newCreated = $previousSnapshot.Created
        $newUpdated = $previousSnapshot.Updated
        $newAuthor = $previousSnapshot.Author
        $reasonParts = [System.Collections.Generic.List[string]]::new()
        $reasonParts.Add("metadata repaired")
        if ($hasManualVersionIncrease) { $reasonParts.Add("manual version rebaseline") }
        if ($hasPresentationDriftFromPrevious) { $reasonParts.Add("historyTamperDetected"); $reasonParts.Add("historyRestoredFromTrustedPrevious") }
        $reason = $reasonParts -join "; "
        $shouldWrite = $true
    }
    elseif ($bodyChanged) {
        $baselineVersion = if ($null -ne $currentSnapshot.Version) { $currentSnapshot.Version } elseif ($null -ne $previousSnapshot -and $null -ne $previousSnapshot.Version) { $previousSnapshot.Version } else { $null }
        if ($null -eq $baselineVersion) {
            Add-FailedFile -Report $Report -Path $repoPath -Rule $config.versionField -Current $currentSnapshot.Version -Expected "valid Version or safely restorable previous Version"
            return
        }
        if (-not (Test-IsTimestamp $newCreated)) {
            if ($hasValidPreviousTimestamps) {
                $newCreated = $previousSnapshot.Created
            }
            else {
                Add-FailedFile -Report $Report -Path $repoPath -Rule $config.createdField -Current $currentSnapshot.Created -Expected "valid created timestamp or safely restorable previous created timestamp"
                return
            }
        }

        $newVersion = Get-IncrementedVersion $baselineVersion
        $newUpdated = $now
        $newAuthor = Resolve-DocumentAuthor -RepoPath $repoPath -Comparison $Comparison
        $reason = if ($validationErrors.Count -gt 0 -or $hasPresentationDriftFromPrevious) { "body changed; metadata repaired" } else { "body changed" }
        if ($hasPresentationDriftFromPrevious) {
            $reason = "$reason; historyTamperDetected; historyRestoredFromTrustedPrevious"
        }
        $shouldWrite = $true
    }
    elseif ($validationErrors.Count -gt 0) {
        if ($metadataFieldValidationErrors.Count -gt 0) {
            if (-not $hasValidPreviousMetadata) {
                Add-FailedFile -Report $Report -Path $repoPath -Rule "managed metadata fields" -Current "invalid or incomplete" -Expected "valid current fields or trusted previous metadata for safe restoration"
                return
            }

            $newVersion = $previousSnapshot.Version
            $newCreated = $previousSnapshot.Created
            $newUpdated = $previousSnapshot.Updated
            $newAuthor = $previousSnapshot.Author
            $reason = "metadata repaired from trusted previous metadata"
            $shouldWrite = $true
        }
        elseif ($presentationValidationErrors.Count -gt 0) {
            $reason = if ($canRestoreMissingPresentation) { "metadata presentation restored from trusted previous" } else { "metadata presentation repaired" }
            $shouldWrite = $true
        }
        else {
            Add-FailedFile -Report $Report -Path $repoPath -Rule "managed metadata" -Current "invalid" -Expected "safely repairable metadata state"
            return
        }
    }
    elseif ($hasManualVersionIncrease) {
        Add-UnchangedFile -Report $Report -Path $repoPath -Reason "manual version rebaseline" -OldVersion $previousSnapshot.Version -NewVersion $currentSnapshot.Version
        return
    }
    else {
        $reason = if ($null -ne $previousInfo -and (($currentInfo.MetadataLines -join "`n") -ne ($previousInfo.MetadataLines -join "`n"))) { "metadata-only change" } else { "no body change" }
        $oldVersionForReport = if ($null -ne $previousSnapshot) { $previousSnapshot.Version } else { $null }
        Add-UnchangedFile -Report $Report -Path $repoPath -Reason $reason -OldVersion $oldVersionForReport -NewVersion $currentSnapshot.Version
        return
    }

    if ($shouldWrite) {
        if ([string]::IsNullOrWhiteSpace([string] $newAuthor)) {
            $newAuthor = Resolve-DocumentAuthor -RepoPath $repoPath -Comparison $Comparison
        }
        if (Test-IsTimestamp $newCreated) {
            $newCreated = ConvertTo-UtcTimestampValue $newCreated
        }
        if (Test-IsTimestamp $newUpdated) {
            $newUpdated = ConvertTo-UtcTimestampValue $newUpdated
        }
        $linkInfo = Get-HistoryLinkInfo -RepoPath $repoPath -Comparison $Comparison -Config $config
        $currentChangesLineMode = if ($bodyChanged) {
            if ($linkInfo.HasReliableContext) { "replace" } else { "clear" }
        }
        else {
            "preserve"
        }
        $values = @{
            $config.versionField = $newVersion
            $config.createdField = $newCreated
            $config.updatedField = $newUpdated
            $config.authorField = $newAuthor
            "__historyUrl" = $linkInfo.Url
            "__historyLinkText" = $linkInfo.LinkText
            "__sourcePresentationInfo" = if ($hasPresentationDriftFromPrevious -or $canRestoreMissingPresentation) { $previousInfo } else { $currentInfo }
            "__addHistoryEntry" = ($bodyChanged -and $linkInfo.HasReliableContext)
            "__currentChangesLineMode" = $currentChangesLineMode
        }
        $newContent = Set-MetadataContent -Content $textFile.Content -Config $config -Values $values -NewLine $textFile.NewLine
        if ($PSCmdlet.ShouldProcess($repoPath, "Update document metadata")) {
            Write-StrictUtf8Text -FullPath $Record.FullPath -Content $newContent -HasBom $textFile.HasBom
        }
        Add-UpdatedFile -Report $Report -Path $repoPath -Format $config.metadataFormat -Placement $config.metadataPlacement -OldVersion $currentSnapshot.Version -NewVersion $newVersion -OldCreated $currentSnapshot.Created -NewCreated $newCreated -OldUpdated $currentSnapshot.Updated -NewUpdated $newUpdated -Reason $reason
    }
}

function Analyze-GovernedFile {
    param(
        [object] $Record,
        [hashtable] $Report,
        [hashtable] $Comparison
    )

    $repoPath = $Record.Path
    $config = $Record.Config
    if (-not (Test-Path -LiteralPath $Record.FullPath -PathType Leaf)) {
        Add-SkippedFile -Report $Report -Path $repoPath -Reason "deleted file"
        return
    }

    try {
        $textFile = Read-StrictUtf8Text -FullPath $Record.FullPath
    }
    catch {
        Add-IneligibleFile -Report $Report -Path $repoPath -Reason "binary/non-text" -Category "ignoredBinaryOrNonText" -Current "not strict UTF-8 text" -Expected "strict UTF-8 text document" -Remediation "Convert this document to UTF-8 if it should be managed by doc-metadata."
        return
    }

    $currentInfo = Get-MetadataInfo -Content $textFile.Content -Config $config
    $currentSnapshot = Get-MetadataSnapshot -MetadataInfo $currentInfo -Config $config
    $previousContent = if ($Comparison.staleCheckAvailable) { Get-GitFileContent -Revision $Comparison.baseSha -RepoPath $repoPath } else { $null }
    $previousInfo = if ($null -ne $previousContent) { Get-MetadataInfo -Content $previousContent -Config $config } else { $null }
    $previousSnapshot = if ($null -ne $previousInfo) { Get-MetadataSnapshot -MetadataInfo $previousInfo -Config $config } else { $null }
    $bodyChanged = if ($null -ne $previousInfo) { (ConvertTo-ComparableBody $currentInfo.Body) -ne (ConvertTo-ComparableBody $previousInfo.Body) } else { $false }
    $hasValidPreviousMetadata = $null -ne $previousSnapshot -and $null -ne $previousSnapshot.Version -and (Test-IsTimestamp $previousSnapshot.Created) -and (Test-IsTimestamp $previousSnapshot.Updated) -and -not [string]::IsNullOrWhiteSpace([string] $previousSnapshot.Author)

    if (-not $Comparison.staleCheckAvailable) {
        Add-StaleSkippedFile -Report $Report -Path $repoPath -Reason $Comparison.reason
    }

    if ($currentInfo.IsMalformed) {
        Add-UnrecoverableFile -Report $Report -Path $repoPath -Reason "malformed metadata cannot be safely repaired" -Current "malformed" -Expected "metadata block matching manifest format"
        Add-FailedFile -Report $Report -Path $repoPath -Rule "metadata block format" -Current "malformed" -Expected "metadata block intent can be determined safely"
        return
    }

    if (-not $currentInfo.HasMetadata) {
        $categories = @("initialized")
        $reason = "missing metadata can be initialized"
        if ($hasValidPreviousMetadata) {
            $categories += "restoredFromHistory"
            $reason = "missing metadata can be restored from history"
        }
        if ($bodyChanged) {
            $categories += "incremented"
            $reason = "$reason; body changed"
        }
        Add-RepairableFile -Report $Report -Path $repoPath -Reason $reason -Categories $categories
        return
    }

    [object[]] $validationErrors = @(Validate-FileMetadata -MetadataInfo $currentInfo -Config $config -RepoPath $repoPath)
    [object[]] $presentationErrors = @($validationErrors | Where-Object { $_.Rule -eq "managed metadata presentation" })
    if (@($presentationErrors | Where-Object { $_.Current -eq "malformed" }).Count -gt 0 -and ($null -eq $previousInfo -or -not $previousInfo.HasPresentation -or $previousInfo.IsPresentationMalformed)) {
        Add-UnrecoverableFile -Report $Report -Path $repoPath -Reason "malformed managed presentation cannot be safely restored" -Current "malformed" -Expected "trusted previous generated presentation"
        Add-FailedFile -Report $Report -Path $repoPath -Rule "managed metadata presentation" -Current "malformed" -Expected "trusted previous generated presentation for safe restoration"
        return
    }
    [object[]] $versionErrors = @($validationErrors | Where-Object { $_.Rule -eq $config.versionField -and $null -ne $_.Current })
    if ($versionErrors.Count -gt 0) {
        foreach ($error in $versionErrors) {
            Add-UnrecoverableFile -Report $Report -Path $repoPath -Reason "invalid Version is not safely repairable" -Current $error.Current -Expected $error.Expected
            Add-FailedFile -Report $Report -Path $repoPath -Rule $error.Rule -Current $error.Current -Expected $error.Expected
        }
        return
    }

    if ($null -ne $previousSnapshot -and $null -ne $previousSnapshot.Version -and $null -ne $currentSnapshot.Version -and (Compare-VersionValue $currentSnapshot.Version $previousSnapshot.Version) -lt 0) {
        $rule = "Version must not decrease without explicit rebaseline approval"
        Add-UnrecoverableFile -Report $Report -Path $repoPath -Reason $rule -Current $currentSnapshot.Version -Expected "greater than or equal to previous committed Version $($previousSnapshot.Version)"
        Add-FailedFile -Report $Report -Path $repoPath -Rule $rule -Current $currentSnapshot.Version -Expected "greater than or equal to previous committed Version $($previousSnapshot.Version)"
        return
    }

    [object[]] $timestampErrors = @($validationErrors | Where-Object { $_.Rule -in @($config.createdField, $config.updatedField) })
    [object[]] $metadataFieldValidationErrors = @($validationErrors | Where-Object { $_.Rule -in @($config.versionField, $config.createdField, $config.updatedField, $config.authorField) })
    [object[]] $presentationValidationErrors = @($validationErrors | Where-Object { $_.Rule -eq "managed metadata presentation" })
    [object[]] $urlValidationErrors = @($validationErrors | Where-Object { $_.Rule -eq "managed history URL" })
    $hasManualVersionIncrease = $null -ne $previousSnapshot -and $null -ne $previousSnapshot.Version -and $null -ne $currentSnapshot.Version -and (Compare-VersionValue $currentSnapshot.Version $previousSnapshot.Version) -gt 0
    $hasTimestampDriftFromPrevious = $null -ne $previousSnapshot -and ((-not (Test-TimestampEquivalent $currentSnapshot.Created $previousSnapshot.Created)) -or (-not (Test-TimestampEquivalent $currentSnapshot.Updated $previousSnapshot.Updated)))
    $hasAuthorDriftFromPrevious = $null -ne $previousSnapshot -and $null -ne $previousSnapshot.Author -and $currentSnapshot.Author -ne $previousSnapshot.Author
    $hasPresentationDriftFromPrevious = $null -ne $previousInfo -and $currentInfo.HasPresentation -and $previousInfo.HasPresentation -and -not (Test-ManagedHistoryEquivalent -Left $currentInfo -Right $previousInfo -RepoPath $repoPath -Config $config)
    $canRestoreMissingPresentation = $null -ne $previousInfo -and -not $currentInfo.HasPresentation -and $previousInfo.HasPresentation -and -not $previousInfo.IsPresentationMalformed
    $canRestoreUrlFromPrevious = $null -ne $previousInfo -and $previousInfo.HasPresentation -and -not $previousInfo.IsPresentationMalformed -and (Test-ManagedPresentationUrls -MetadataInfo $previousInfo -RepoPath $repoPath -Config $config)

    if ($urlValidationErrors.Count -gt 0 -and -not $canRestoreUrlFromPrevious) {
        foreach ($error in $urlValidationErrors) {
            Add-UnrecoverableFile -Report $Report -Path $repoPath -Reason "managed history URL is not safely repairable" -Current $error.Current -Expected $error.Expected
            Add-FailedFile -Report $Report -Path $repoPath -Rule $error.Rule -Current $error.Current -Expected $error.Expected
        }
        return
    }

    if ($bodyChanged) {
        if ($null -eq $currentSnapshot.Version -and ($null -eq $previousSnapshot -or $null -eq $previousSnapshot.Version)) {
            Add-UnrecoverableFile -Report $Report -Path $repoPath -Reason "body changed but Version cannot be safely restored" -Current $currentSnapshot.Version -Expected "valid Version or valid previous metadata"
            Add-FailedFile -Report $Report -Path $repoPath -Rule $config.versionField -Current $currentSnapshot.Version -Expected "valid Version or safely restorable previous Version"
            return
        }
        if ($timestampErrors.Count -gt 0 -and -not $hasValidPreviousMetadata -and -not (Test-IsTimestamp $currentSnapshot.Created)) {
            Add-UnrecoverableFile -Report $Report -Path $repoPath -Reason "body changed but created timestamp cannot be safely restored" -Current $currentSnapshot.Created -Expected "valid created timestamp or valid previous metadata"
            Add-FailedFile -Report $Report -Path $repoPath -Rule $config.createdField -Current $currentSnapshot.Created -Expected "valid created timestamp or safely restorable previous created timestamp"
            return
        }

        $categories = @("incremented")
        $reason = "body changed"
        if ($timestampErrors.Count -gt 0) {
            $categories += "repaired"
            $reason = "body changed; metadata repaired"
        }
        Add-RepairableFile -Report $Report -Path $repoPath -Reason $reason -Categories $categories
        return
    }

    if ($timestampErrors.Count -gt 0 -or $hasTimestampDriftFromPrevious -or $hasAuthorDriftFromPrevious -or $hasPresentationDriftFromPrevious) {
        if ($hasValidPreviousMetadata) {
            $reason = if ($hasManualVersionIncrease) { "metadata-only drift can be restored; manual version rebaseline preserved" } else { "metadata-only drift can be restored" }
            $categories = @("restoredFromHistory", "repaired")
            if ($hasPresentationDriftFromPrevious) {
                $reason = "$reason; historyTamperDetected; historyRestoredFromTrustedPrevious"
                $categories += @("historyTamperDetected", "historyRestoredFromTrustedPrevious")
            }
            Add-RepairableFile -Report $Report -Path $repoPath -Reason $reason -Categories $categories
            return
        }

        $rule = "created/updated timestamp drift could not be safely restored"
        Add-UnrecoverableFile -Report $Report -Path $repoPath -Reason $rule -Current "created=$($currentSnapshot.Created); updated=$($currentSnapshot.Updated)" -Expected "valid previous committed created and updated timestamps"
        Add-FailedFile -Report $Report -Path $repoPath -Rule $rule -Current "created=$($currentSnapshot.Created); updated=$($currentSnapshot.Updated)" -Expected "valid previous committed created and updated timestamps"
        return
    }

    if ($metadataFieldValidationErrors.Count -gt 0) {
        if ($hasValidPreviousMetadata) {
            Add-RepairableFile -Report $Report -Path $repoPath -Reason "metadata fields can be restored from trusted previous metadata" -Categories @("restoredFromHistory", "repaired")
            return
        }

        Add-UnrecoverableFile -Report $Report -Path $repoPath -Reason "metadata fields cannot be safely repaired" -Current "invalid or incomplete" -Expected "valid current fields or trusted previous metadata"
        Add-FailedFile -Report $Report -Path $repoPath -Rule "managed metadata fields" -Current "invalid or incomplete" -Expected "valid current fields or trusted previous metadata for safe restoration"
        return
    }

    if ($presentationValidationErrors.Count -gt 0) {
        if ($canRestoreMissingPresentation) {
            Add-RepairableFile -Report $Report -Path $repoPath -Reason "metadata presentation can be restored from trusted previous" -Categories @("restoredFromHistory", "repaired")
        }
        else {
            Add-RepairableFile -Report $Report -Path $repoPath -Reason "metadata presentation can be repaired" -Categories @("repaired")
        }
        return
    }

    if ($hasManualVersionIncrease) {
        Add-UnchangedFile -Report $Report -Path $repoPath -Reason "manual version rebaseline" -OldVersion $previousSnapshot.Version -NewVersion $currentSnapshot.Version
        return
    }

    Add-UnchangedFile -Report $Report -Path $repoPath -Reason "metadata valid"
}

function Test-GovernedFile {
    param(
        [object] $Record,
        [hashtable] $Report,
        [hashtable] $Comparison
    )

    $repoPath = $Record.Path
    $config = $Record.Config
    if (-not (Test-Path -LiteralPath $Record.FullPath -PathType Leaf)) {
        Add-SkippedFile -Report $Report -Path $repoPath -Reason "deleted file"
        return
    }

    try {
        $textFile = Read-StrictUtf8Text -FullPath $Record.FullPath
    }
    catch {
        Add-FailedFile -Report $Report -Path $repoPath -Rule "UTF-8" -Current "invalid UTF-8" -Expected "valid UTF-8 text" -Remediation "Convert the file to UTF-8 or exclude it from .github/tools/doc-metadata/doc-metadata-manifest.json."
        return
    }

    $currentInfo = Get-MetadataInfo -Content $textFile.Content -Config $config
    [object[]] $errors = @(Validate-FileMetadata -MetadataInfo $currentInfo -Config $config -RepoPath $repoPath)
    foreach ($error in $errors) {
        Add-FailedFile -Report $Report -Path $repoPath -Rule $error.Rule -Current $error.Current -Expected $error.Expected
    }

    if ($errors.Length -gt 0) {
        return
    }

    $currentSnapshot = Get-MetadataSnapshot -MetadataInfo $currentInfo -Config $config
    if (-not $Comparison.staleCheckAvailable) {
        Add-StaleSkippedFile -Report $Report -Path $repoPath -Reason $Comparison.reason
        Add-UnchangedFile -Report $Report -Path $repoPath -Reason "metadata valid"
        return
    }

    $previousContent = Get-GitFileContent -Revision $Comparison.baseSha -RepoPath $repoPath
    if ($null -eq $previousContent) {
        Add-UnchangedFile -Report $Report -Path $repoPath -Reason "metadata valid; new governed file"
        return
    }

    $previousInfo = Get-MetadataInfo -Content $previousContent -Config $config
    $previousSnapshot = Get-MetadataSnapshot -MetadataInfo $previousInfo -Config $config
    if ($null -ne $previousSnapshot.Version -and $null -ne $currentSnapshot.Version -and (Compare-VersionValue $currentSnapshot.Version $previousSnapshot.Version) -lt 0) {
        Add-FailedFile -Report $Report -Path $repoPath -Rule "Version must not decrease without explicit rebaseline approval" -Current $currentSnapshot.Version -Expected "greater than or equal to previous committed Version $($previousSnapshot.Version)"
        return
    }

    $bodyChanged = (ConvertTo-ComparableBody $currentInfo.Body) -ne (ConvertTo-ComparableBody $previousInfo.Body)
    if (-not $bodyChanged) {
        Add-StaleSkippedFile -Report $Report -Path $repoPath -Reason "no body change"
        $previousHadMetadata = $previousInfo.HasMetadata -and -not $previousInfo.IsMalformed
        if ($previousHadMetadata -and $null -ne $previousSnapshot.Created -and -not (Test-TimestampEquivalent $currentSnapshot.Created $previousSnapshot.Created)) {
            Add-FailedFile -Report $Report -Path $repoPath -Rule $config.createdField -Current $currentSnapshot.Created -Expected "unchanged when body content did not change"
            return
        }
        if ($previousHadMetadata -and $null -ne $previousSnapshot.Updated -and -not (Test-TimestampEquivalent $currentSnapshot.Updated $previousSnapshot.Updated)) {
            Add-FailedFile -Report $Report -Path $repoPath -Rule $config.updatedField -Current $currentSnapshot.Updated -Expected "unchanged when body content did not change"
            return
        }
        if ($previousHadMetadata -and $null -ne $previousSnapshot.Author -and $currentSnapshot.Author -ne $previousSnapshot.Author) {
            Add-FailedFile -Report $Report -Path $repoPath -Rule $config.authorField -Current $currentSnapshot.Author -Expected "unchanged when body content did not change"
            return
        }
        if ($previousHadMetadata -and $currentInfo.HasPresentation -and $previousInfo.HasPresentation -and -not (Test-ManagedHistoryEquivalent -Left $currentInfo -Right $previousInfo -RepoPath $repoPath -Config $config)) {
            Add-FailedFile -Report $Report -Path $repoPath -Rule "managed history integrity" -Current "generated presentation changed" -Expected "unchanged generated presentation when body content did not change"
            return
        }

        Add-UnchangedFile -Report $Report -Path $repoPath -Reason "metadata valid"
        return
    }

    if ($null -ne $previousSnapshot.Version -and $null -ne $currentSnapshot.Version -and (Compare-VersionValue $currentSnapshot.Version $previousSnapshot.Version) -le 0) {
        Add-FailedFile -Report $Report -Path $repoPath -Rule $config.versionField -Current $currentSnapshot.Version -Expected "greater than previous Version $($previousSnapshot.Version) because body content changed"
        return
    }

    if ($null -ne $previousSnapshot.Updated) {
        $currentUpdated = [System.DateTimeOffset]::Parse($currentSnapshot.Updated, [System.Globalization.CultureInfo]::InvariantCulture)
        $previousUpdated = [System.DateTimeOffset]::Parse($previousSnapshot.Updated, [System.Globalization.CultureInfo]::InvariantCulture)
        if ($currentUpdated -le $previousUpdated) {
            Add-FailedFile -Report $Report -Path $repoPath -Rule $config.updatedField -Current $currentSnapshot.Updated -Expected "later than previous updated timestamp because body content changed"
            return
        }
    }

    Add-UnchangedFile -Report $Report -Path $repoPath -Reason "metadata valid"
}

function Get-ManagedBodyAtRevision {
    param(
        [string] $Revision,
        [string] $RepoPath,
        [object] $Config
    )

    $content = Get-GitFileContent -Revision $Revision -RepoPath $RepoPath
    if ($null -eq $content) {
        return [pscustomobject]@{
            Exists = $false
            Body = $null
        }
    }

    $info = Get-MetadataInfo -Content $content -Config $Config
    [pscustomobject]@{
        Exists = $true
        Body = ConvertTo-ComparableBody $info.Body
    }
}

function Get-ManagedBodyChangeResult {
    param(
        [object] $Record,
        [string] $RequestedBaseSha,
        [string] $RequestedHeadSha
    )

    $repoPath = $Record.Path
    if ([string]::IsNullOrWhiteSpace($RequestedHeadSha) -or -not (Test-GitCommitExists $RequestedHeadSha)) {
        throw "Head SHA '$RequestedHeadSha' is not fetchable in the local checkout."
    }
    if (-not [string]::IsNullOrWhiteSpace($RequestedBaseSha) -and -not (Test-GitCommitExists $RequestedBaseSha)) {
        throw "Base SHA '$RequestedBaseSha' is not fetchable in the local checkout."
    }

    $headState = Get-ManagedBodyAtRevision -Revision $RequestedHeadSha -RepoPath $repoPath -Config $Record.Config
    $baseState = if ([string]::IsNullOrWhiteSpace($RequestedBaseSha)) {
        [pscustomobject]@{ Exists = $false; Body = $null }
    }
    else {
        Get-ManagedBodyAtRevision -Revision $RequestedBaseSha -RepoPath $Record.PreviousPath -Config $Record.Config
    }

    $bodyChanged = $false
    if ($headState.Exists -and -not $baseState.Exists) {
        $bodyChanged = $true
    }
    elseif ($headState.Exists -and $baseState.Exists) {
        $bodyChanged = $headState.Body -ne $baseState.Body
    }

    [ordered]@{
        path = $repoPath
        baseSha = if ([string]::IsNullOrWhiteSpace($RequestedBaseSha)) { $null } else { $RequestedBaseSha }
        headSha = $RequestedHeadSha
        baseExists = $baseState.Exists
        headExists = $headState.Exists
        newFile = ($headState.Exists -and -not $baseState.Exists)
        bodyChanged = $bodyChanged
    }
}

function Complete-Report {
    param(
        [hashtable] $Report,
        [int] $TotalGovernedConsidered,
        [int] $TotalGovernedValidated = 0
    )

    $uniqueFailedFiles = @($Report.failedFiles | ForEach-Object { $_.path } | Sort-Object -Unique)
    $Report.analysis.repairRequired = $Report.analysis.repairableFiles.Count -gt 0
    $Report.analysis.unrecoverableFailure = $Report.analysis.unrecoverableFiles.Count -gt 0 -or $uniqueFailedFiles.Count -gt 0
    $Report.analysis.repairSafe = -not $Report.analysis.unrecoverableFailure
    $Report.analysis.metadataValid = -not $Report.analysis.repairRequired -and -not $Report.analysis.unrecoverableFailure
    $Report.summaryCounts = @{
        totalGovernedFilesConsidered = $TotalGovernedConsidered
        totalGovernedFilesValidated = $TotalGovernedValidated
        filesUpdated = $Report.updatedFiles.Count
        filesUnchanged = $Report.unchangedFiles.Count
        filesSkipped = $Report.skippedFiles.Count
        filesFailed = $uniqueFailedFiles.Count
        filesIneligible = $Report.ineligibleFiles.Count
        ignoredByEligibility = $Report.ignoredByEligibility.Count
        ignoredByDeniedPath = $Report.ignoredByDeniedPath.Count
        ignoredByDeniedExtension = $Report.ignoredByDeniedExtension.Count
        ignoredBinaryOrNonText = $Report.ignoredBinaryOrNonText.Count
        repairableFiles = $Report.analysis.repairableFiles.Count
        unrecoverableFiles = $Report.analysis.unrecoverableFiles.Count
        staleCheckFilesConsidered = if ($Report.comparison.staleCheckAvailable) { $TotalGovernedValidated - $Report.staleCheckSkippedFiles.Count } else { 0 }
        staleCheckFilesSkipped = $Report.staleCheckSkippedFiles.Count
    }
}

function Write-ReportTable {
    param(
        [string] $Title,
        [object[]] $Rows
    )

    if ($Rows.Count -eq 0) {
        return
    }

    Write-Host ""
    Write-Host $Title
    Write-Host ("-" * $Title.Length)
    ($Rows | Format-Table -AutoSize | Out-String -Width 240).TrimEnd() | Write-Host
}

function Write-HumanReport {
    param([hashtable] $Report)

    Write-Host ""
    Write-Host "Document metadata report"
    Write-Host "Mode: $($Report.mode)"
    Write-Host "Repository root: $($Report.root)"
    Write-Host "Manifest path: $($Report.manifestPath)"
    if ($Report.mode -in @("Analyze", "Check")) {
        Write-Host "Comparison mode: $($Report.comparison.mode)"
        if ($null -ne $Report.comparison.baseSha) {
            Write-Host "Base SHA: $($Report.comparison.baseSha)"
        }
        if ($null -ne $Report.comparison.headSha) {
            Write-Host "Head SHA: $($Report.comparison.headSha)"
        }
        Write-Host "Comparison note: $($Report.comparison.reason)"
    }

    if ($Report.mode -eq "Analyze") {
        Write-Host "Analysis: metadataValid=$($Report.analysis.metadataValid), repairRequired=$($Report.analysis.repairRequired), repairSafe=$($Report.analysis.repairSafe), unrecoverableFailure=$($Report.analysis.unrecoverableFailure)"
    }

    Write-Host "Summary: governed considered=$($Report.summaryCounts.totalGovernedFilesConsidered), validated=$($Report.summaryCounts.totalGovernedFilesValidated), updated=$($Report.summaryCounts.filesUpdated), unchanged=$($Report.summaryCounts.filesUnchanged), skipped=$($Report.summaryCounts.filesSkipped), failed=$($Report.summaryCounts.filesFailed), ineligible=$($Report.summaryCounts.filesIneligible), repairable=$($Report.summaryCounts.repairableFiles), unrecoverable=$($Report.summaryCounts.unrecoverableFiles), stale considered=$($Report.summaryCounts.staleCheckFilesConsidered), stale skipped=$($Report.summaryCounts.staleCheckFilesSkipped)"

    $updatedRows = @($Report.updatedFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Format = $_.metadataFormat
            Placement = $_.metadataPlacement
            OldVersion = ConvertTo-DisplayValue $_.oldVersion
            NewVersion = ConvertTo-DisplayValue $_.newVersion
            OldUpdated = ConvertTo-DisplayValue $_.oldUpdated
            NewUpdated = ConvertTo-DisplayValue $_.newUpdated
            Reason = $_.reason
        }
    })
    Write-ReportTable -Title "Updated files" -Rows $updatedRows

    $unchangedRows = @($Report.unchangedFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Reason = $_.reason
        }
    })
    Write-ReportTable -Title "Unchanged files" -Rows $unchangedRows

    $skippedRows = @($Report.skippedFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Reason = $_.reason
        }
    })
    Write-ReportTable -Title "Skipped files" -Rows $skippedRows

    $ineligibleRows = @($Report.ineligibleFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Reason = $_.reason
            Category = $_.category
            Current = ConvertTo-DisplayValue $_.current
            Remediation = $_.remediation
        }
    })
    Write-ReportTable -Title "Ineligible manifest matches" -Rows $ineligibleRows

    $repairableRows = @($Report.analysis.repairableFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Reason = $_.reason
            Categories = (@($_.categories) -join ", ")
        }
    })
    Write-ReportTable -Title "Repairable files" -Rows $repairableRows

    $unrecoverableRows = @($Report.analysis.unrecoverableFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Reason = $_.reason
            Current = ConvertTo-DisplayValue $_.current
            Expected = $_.expected
        }
    })
    Write-ReportTable -Title "Not safely repairable files" -Rows $unrecoverableRows

    $failureRows = @($Report.failedFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Rule = $_.rule
            Current = ConvertTo-DisplayValue $_.current
            Expected = $_.expected
            Remediation = $_.remediation
        }
    })
    Write-ReportTable -Title "Validation failures" -Rows $failureRows

    $staleSkippedRows = @($Report.staleCheckSkippedFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Reason = $_.reason
        }
    })
    Write-ReportTable -Title "Stale-check skipped files" -Rows $staleSkippedRows
}

function ConvertTo-MarkdownTable {
    param(
        [string[]] $Headers,
        [object[]] $Rows,
        [int] $Limit = 100
    )

    if ($Rows.Count -eq 0) {
        return "_None_`n"
    }

    $builder = [System.Text.StringBuilder]::new()
    [void] $builder.AppendLine("| $($Headers -join " | ") |")
    [void] $builder.AppendLine("| $((@($Headers | ForEach-Object { "---" })) -join " | ") |")
    foreach ($row in @($Rows | Select-Object -First $Limit)) {
        $values = foreach ($header in $Headers) {
            $value = Get-PropertyValue -Object $row -Name $header
            ([string] (ConvertTo-DisplayValue $value)).Replace("|", "\|")
        }
        [void] $builder.AppendLine("| $($values -join " | ") |")
    }

    if ($Rows.Count -gt $Limit) {
        [void] $builder.AppendLine()
        [void] $builder.AppendLine("_truncated; see console log or JSON report_")
    }

    $builder.ToString()
}

function Write-GitHubSummary {
    param([hashtable] $Report)

    if ([string]::IsNullOrWhiteSpace($env:GITHUB_STEP_SUMMARY)) {
        return
    }

    $updatedRows = @($Report.updatedFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Format = $_.metadataFormat
            Placement = $_.metadataPlacement
            OldVersion = ConvertTo-DisplayValue $_.oldVersion
            NewVersion = ConvertTo-DisplayValue $_.newVersion
            OldUpdated = ConvertTo-DisplayValue $_.oldUpdated
            NewUpdated = ConvertTo-DisplayValue $_.newUpdated
            Reason = $_.reason
        }
    })
    $skippedRows = @($Report.skippedFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Reason = $_.reason
        }
    })
    $failureRows = @($Report.failedFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Rule = $_.rule
            Current = ConvertTo-DisplayValue $_.current
            Expected = $_.expected
            Remediation = $_.remediation
        }
    })
    $ineligibleGroupedRows = @($Report.ineligibleFiles | Group-Object -Property reason | ForEach-Object {
        [pscustomobject]@{
            Reason = $_.Name
            Count = $_.Count
        }
    })
    $repairableRows = @($Report.analysis.repairableFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Reason = $_.reason
            Categories = (@($_.categories) -join ", ")
        }
    })
    $unrecoverableRows = @($Report.analysis.unrecoverableFiles | ForEach-Object {
        [pscustomobject]@{
            Path = $_.path
            Reason = $_.reason
            Current = ConvertTo-DisplayValue $_.current
            Expected = $_.expected
        }
    })

    $summary = [System.Text.StringBuilder]::new()
    [void] $summary.AppendLine("## Document metadata")
    [void] $summary.AppendLine()
    [void] $summary.AppendLine("- Mode: $($Report.mode)")
    [void] $summary.AppendLine("- Governed considered: $($Report.summaryCounts.totalGovernedFilesConsidered)")
    [void] $summary.AppendLine("- Updated: $($Report.summaryCounts.filesUpdated)")
    [void] $summary.AppendLine("- Skipped: $($Report.summaryCounts.filesSkipped)")
    [void] $summary.AppendLine("- Failed: $($Report.summaryCounts.filesFailed)")
    [void] $summary.AppendLine("- Ineligible manifest matches: $($Report.summaryCounts.filesIneligible)")
    if ($Report.mode -eq "Analyze") {
        [void] $summary.AppendLine("- Metadata valid: $($Report.analysis.metadataValid)")
        [void] $summary.AppendLine("- Repair required: $($Report.analysis.repairRequired)")
        [void] $summary.AppendLine("- Repair safe: $($Report.analysis.repairSafe)")
        [void] $summary.AppendLine("- Unrecoverable failure: $($Report.analysis.unrecoverableFailure)")
    }
    [void] $summary.AppendLine("- Remediation: ``$script:RemediationCommand``")
    [void] $summary.AppendLine()
    [void] $summary.AppendLine("### Updated files")
    [void] $summary.AppendLine((ConvertTo-MarkdownTable -Headers @("Path", "Format", "Placement", "OldVersion", "NewVersion", "OldUpdated", "NewUpdated", "Reason") -Rows $updatedRows))
    [void] $summary.AppendLine("### Skipped files")
    [void] $summary.AppendLine((ConvertTo-MarkdownTable -Headers @("Path", "Reason") -Rows $skippedRows))
    [void] $summary.AppendLine("### Ineligible manifest matches by reason")
    [void] $summary.AppendLine((ConvertTo-MarkdownTable -Headers @("Reason", "Count") -Rows $ineligibleGroupedRows))
    [void] $summary.AppendLine("### Repairable files")
    [void] $summary.AppendLine((ConvertTo-MarkdownTable -Headers @("Path", "Reason", "Categories") -Rows $repairableRows))
    [void] $summary.AppendLine("### Not safely repairable files")
    [void] $summary.AppendLine((ConvertTo-MarkdownTable -Headers @("Path", "Reason", "Current", "Expected") -Rows $unrecoverableRows))
    [void] $summary.AppendLine("### Failures")
    [void] $summary.AppendLine((ConvertTo-MarkdownTable -Headers @("Path", "Rule", "Current", "Expected", "Remediation") -Rows $failureRows))

    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary.ToString()
}

function Write-MachineReports {
    param([hashtable] $Report)

    if (-not [string]::IsNullOrWhiteSpace($ReportOutputPath)) {
        $reportFullPath = Resolve-InRootPath -RootPath $script:RepositoryRoot -InputPath $ReportOutputPath
        if ($null -eq $reportFullPath) {
            throw "ReportOutputPath must be inside the repository root."
        }
        $Report | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $reportFullPath -Encoding utf8NoBOM
    }

    if (-not [string]::IsNullOrWhiteSpace($ChangedFilesOutputPath)) {
        $changedFullPath = Resolve-InRootPath -RootPath $script:RepositoryRoot -InputPath $ChangedFilesOutputPath
        if ($null -eq $changedFullPath) {
            throw "ChangedFilesOutputPath must be inside the repository root."
        }
        [ordered]@{
            changedFiles = @($Report.updatedFiles | ForEach-Object { $_.path })
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $changedFullPath -Encoding utf8NoBOM
    }

    if (-not [string]::IsNullOrWhiteSpace($ContentChangeOutputPath)) {
        $contentChangeFullPath = Resolve-InRootPath -RootPath $script:RepositoryRoot -InputPath $ContentChangeOutputPath
        if ($null -eq $contentChangeFullPath) {
            throw "ContentChangeOutputPath must be inside the repository root."
        }
        [ordered]@{
            contentChanges = @($Report.contentChanges)
        } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $contentChangeFullPath -Encoding utf8NoBOM
    }
}

function Invoke-Main {
    $script:RepositoryRoot = Resolve-RootPath -RequestedRoot $Root
    $script:ManifestFullPath = Resolve-InRootPath -RootPath $script:RepositoryRoot -InputPath $ManifestPath
    if ($null -eq $script:ManifestFullPath) {
        throw "ManifestPath must resolve inside the repository root."
    }

    $manifestReportPath = ConvertTo-RepoRelativePath -RootPath $script:RepositoryRoot -FullPath $script:ManifestFullPath
    $report = New-Report -ModeValue $Mode -RootValue $script:RepositoryRoot -ManifestValue $manifestReportPath
    $manifest = $null

    try {
        $manifest = Read-Manifest -ManifestFile $script:ManifestFullPath
        [object[]] $includeValues = @(Get-NonNullValues -Values $Include)
        $bootstrapPatterns = if ($Mode -eq "Bootstrap" -and $includeValues.Length -gt 0) { $includeValues } else { @() }
        $governedFiles = Resolve-GovernedFiles -Manifest $manifest -BootstrapIncludePatterns $bootstrapPatterns -Report $report

        if ($Mode -eq "ContentChanges") {
            $report.comparison = @{
                mode = "content-change"
                baseSha = if ([string]::IsNullOrWhiteSpace($BaseSha)) { $null } else { $BaseSha }
                headSha = $HeadSha
                staleCheckAvailable = -not [string]::IsNullOrWhiteSpace($HeadSha)
                reason = "Classifying managed body changes for explicit content-change references."
            }
            $selected = @(Get-SelectedGovernedRecords -Manifest $manifest -GovernedFiles $governedFiles -Report $report -ModeValue $Mode)
            foreach ($record in $selected) {
                $result = Get-ManagedBodyChangeResult -Record $record -RequestedBaseSha $BaseSha -RequestedHeadSha $HeadSha
                $report.contentChanges.Add($result)
                Add-UnchangedFile -Report $report -Path $record.Path -Reason "content body changed=$($result.bodyChanged)"
            }
            Complete-Report -Report $report -TotalGovernedConsidered $selected.Count -TotalGovernedValidated $selected.Count
        }
        elseif ($Mode -eq "Analyze") {
            $comparison = Get-ComparisonInfo -RequestedEventName $EventName -RequestedEventPayloadPath $EventPayloadPath -RequestedHeadSha $HeadSha -RequestedBaseSha $BaseSha
            $report.comparison = $comparison
            $selected = @($governedFiles.Values | Sort-Object -Property Path)
            foreach ($record in $selected) {
                Analyze-GovernedFile -Record $record -Report $report -Comparison $comparison
            }
            Complete-Report -Report $report -TotalGovernedConsidered $selected.Count -TotalGovernedValidated $selected.Count
        }
        elseif ($Mode -eq "Check") {
            $comparison = Get-ComparisonInfo -RequestedEventName $EventName -RequestedEventPayloadPath $EventPayloadPath -RequestedHeadSha $HeadSha -RequestedBaseSha $BaseSha
            $report.comparison = $comparison
            $selected = @($governedFiles.Values | Sort-Object -Property Path)
            foreach ($record in $selected) {
                Test-GovernedFile -Record $record -Report $report -Comparison $comparison
            }
            Complete-Report -Report $report -TotalGovernedConsidered $selected.Count -TotalGovernedValidated $selected.Count
        }
        else {
            $repairComparison = $null
            if ($Mode -eq "Update" -and ((-not [string]::IsNullOrWhiteSpace($EventName)) -or ((-not [string]::IsNullOrWhiteSpace($BaseSha)) -and (-not [string]::IsNullOrWhiteSpace($HeadSha))))) {
                $repairComparison = Get-ComparisonInfo -RequestedEventName $EventName -RequestedEventPayloadPath $EventPayloadPath -RequestedHeadSha $HeadSha -RequestedBaseSha $BaseSha
                $report.comparison = $repairComparison
            }
            $selected = @(Get-SelectedGovernedRecords -Manifest $manifest -GovernedFiles $governedFiles -Report $report -ModeValue $Mode)
            foreach ($record in $selected) {
                Initialize-OrUpdateFile -Record $record -Report $report -ModeValue $Mode -Comparison $repairComparison
            }
            Complete-Report -Report $report -TotalGovernedConsidered $selected.Count
        }
    }
    catch {
        if ($null -eq $manifest) {
            Add-FailedFile -Report $report -Path $manifestReportPath -Rule "manifest validation" -Current $_.Exception.Message -Expected "valid document metadata manifest" -Remediation "Fix .github/tools/doc-metadata/doc-metadata-manifest.json and rerun the command."
        }
        else {
            Add-FailedFile -Report $report -Path "." -Rule "execution" -Current $_.Exception.Message -Expected "document metadata command completes successfully" -Remediation "Review the reported error, verify comparison inputs, and rerun the command."
        }
        Complete-Report -Report $report -TotalGovernedConsidered 0
    }
    finally {
        Write-HumanReport -Report $report
        Write-GitHubSummary -Report $report
        Write-MachineReports -Report $report
    }

    if ($Mode -eq "Analyze") {
        exit 0
    }

    if ($report.failedFiles.Count -gt 0) {
        exit 1
    }

    exit 0
}

Invoke-Main
