[CmdletBinding()]
param (
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $Value,

    [ValidateSet('User', 'System')]
    [string] $Scope = 'System',

    [ValidateNotNullOrEmpty()]
    [string] $VariableName = 'Path',

    [ValidateSet('Append', 'Overwrite')]
    [string] $UpdateMode = 'Append'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$target = if ($Scope -eq 'System') {
    [EnvironmentVariableTarget]::Machine
}
else {
    [EnvironmentVariableTarget]::User
}

$currentValue = [Environment]::GetEnvironmentVariable(
    $VariableName,
    $target)

if ([string]::IsNullOrEmpty($currentValue)) {
    # Variable doesn't exist yet -> always create it.
    $newValue = $Value
}
elseif ($UpdateMode -eq 'Overwrite') {
    $newValue = $Value
}
else {
    $separator = [IO.Path]::PathSeparator

    $newValue =
        $currentValue.TrimEnd($separator) +
        $separator +
        $Value.TrimStart($separator)
}

[Environment]::SetEnvironmentVariable(
    $VariableName,
    $newValue,
    $target)