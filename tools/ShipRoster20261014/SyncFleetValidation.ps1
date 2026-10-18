param([ValidateSet('Code','Resources')][string]$Scope = 'Code',
    [string]$IsolatedProject = 'D:/Game/tmp/shipyard-detail-clean-20261011')
$ErrorActionPreference = 'Stop'
$fleetWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fleetSource = [IO.Path]::GetFullPath((Join-Path $fleetWorkspace 'UnityClient'))
$fleetTarget = [IO.Path]::GetFullPath($IsolatedProject).TrimEnd('\','/')
$fleetExpected = [IO.Path]::GetFullPath((Join-Path $fleetWorkspace 'tmp/shipyard-detail-clean-20261011')).TrimEnd('\','/')
if ($fleetTarget -ne $fleetExpected -or $fleetTarget -eq $fleetSource) { throw 'Only the named isolated validation project is allowed.' }
if (-not (Test-Path -LiteralPath (Join-Path $fleetTarget 'ProjectSettings/ProjectVersion.txt'))) { throw 'Isolated Unity project missing.' }
$fleetRunning = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($fleetTarget.Replace('\','/')) })
if ($fleetRunning.Count -gt 0) { throw 'Isolated project is occupied by Unity.' }
function Get-FleetTarget([string]$relative) {
    $result = [IO.Path]::GetFullPath((Join-Path $fleetTarget $relative))
    if (-not $result.StartsWith($fleetTarget + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $result.Contains('UIPreviewsForMe')) { throw ('Unsafe synchronization target: ' + $result) }
    return $result
}
function Copy-FleetFile([string]$source, [string]$relative) {
    $destination = Get-FleetTarget $relative
    $sourceInfo = Get-Item -LiteralPath $source
    if (Test-Path -LiteralPath $destination) {
        $targetInfo = Get-Item -LiteralPath $destination
        if ($sourceInfo.Length -eq $targetInfo.Length -and (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) { return }
    }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
    $script:fleetCopied++
}
$fleetCopied = 0
if ($Scope -eq 'Code') {
    foreach ($folder in @('Core','Content','Persistence','Presentation/Runtime','Editor','Tests')) {
        $sourceFolder = Join-Path $fleetSource ('Assets/_Project/' + $folder)
        foreach ($file in Get-ChildItem -LiteralPath $sourceFolder -File -Recurse) {
            if ($file.Extension -notin @('.cs','.asmdef','.meta')) { continue }
            $relative = $file.FullName.Substring($fleetSource.Length + 1)
            if ($relative.Contains('UIPreviewsForMe')) { continue }
            Copy-FleetFile $file.FullName $relative
        }
    }
    Copy-FleetFile (Join-Path $PSScriptRoot 'FleetRedrawCapture.cs') 'Assets/_Project/Editor/FleetRedrawCapture.cs'
    # Separate registry and persistent-data namespaces for test fixtures.
    $settingsPath = Get-FleetTarget 'ProjectSettings/ProjectSettings.asset'
    $settings = [IO.File]::ReadAllText($settingsPath)
    $settings = [regex]::Replace($settings, '(?m)^  companyName:.*$', '  companyName: CodexFleetValidation')
    $settings = [regex]::Replace($settings, '(?m)^  productName:.*$', '  productName: FleetRedesign20261014')
    [IO.File]::WriteAllText($settingsPath, $settings, [Text.UTF8Encoding]::new($false))
} else {
    # Only production Resources are copied; no scenes or editable-preview outputs.
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $fleetSource 'Assets/_Project/Resources') -File -Recurse) {
        Copy-FleetFile $file.FullName $file.FullName.Substring($fleetSource.Length + 1)
    }
}
Write-Output ("Fleet validation {0} synchronization: {1} files copied into {2}" -f $Scope,$fleetCopied,$fleetTarget)
