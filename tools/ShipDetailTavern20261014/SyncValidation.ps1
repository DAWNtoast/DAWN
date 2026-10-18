param([string]$IsolatedProject = 'D:/Game/tmp/shipyard-detail-clean-20261011')
$ErrorActionPreference = 'Stop'
$detailWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$detailSource = [IO.Path]::GetFullPath((Join-Path $detailWorkspace 'UnityClient')).TrimEnd('\','/')
$detailTarget = [IO.Path]::GetFullPath($IsolatedProject).TrimEnd('\','/')
$detailExpected = [IO.Path]::GetFullPath((Join-Path $detailWorkspace 'tmp/shipyard-detail-clean-20261011')).TrimEnd('\','/')
if ($detailTarget -ne $detailExpected -or $detailTarget -eq $detailSource) { throw 'Only the named isolated validation project is allowed.' }
if (-not (Test-Path -LiteralPath (Join-Path $detailTarget 'ProjectSettings/ProjectVersion.txt'))) { throw 'Isolated Unity project missing.' }
$detailRunning = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($detailTarget.Replace('\','/')) })
if ($detailRunning.Count -gt 0) { throw 'Isolated project is occupied by Unity.' }
$detailCopied = 0
function Copy-DetailFile([string]$source, [string]$relative) {
    $destination = [IO.Path]::GetFullPath((Join-Path $detailTarget $relative))
    if (-not $destination.StartsWith($detailTarget + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $destination.Contains('UIPreviewsForMe')) { throw ('Unsafe synchronization target: ' + $destination) }
    if (Test-Path -LiteralPath $destination) {
        if ((Get-Item -LiteralPath $source).Length -eq (Get-Item -LiteralPath $destination).Length -and (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) { return }
    }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
    $script:detailCopied++
}
foreach ($folder in @('Core','Content','Persistence','Presentation/Runtime','Editor','Tests','Resources')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $detailSource ('Assets/_Project/' + $folder)) -File -Recurse) {
        if ($folder -ne 'Resources' -and $file.Extension -notin @('.cs','.asmdef','.meta')) { continue }
        $relative = $file.FullName.Substring($detailSource.Length + 1)
        if ($relative.Contains('UIPreviewsForMe')) { continue }
        Copy-DetailFile $file.FullName $relative
    }
}
Copy-DetailFile (Join-Path $PSScriptRoot 'ShipDetailTavernCapture.cs') 'Assets/_Project/Editor/ShipDetailTavernCapture.cs'
$detailSettingsPath = Join-Path $detailTarget 'ProjectSettings/ProjectSettings.asset'
$detailSettings = [IO.File]::ReadAllText($detailSettingsPath)
$detailSettings = [regex]::Replace($detailSettings, '(?m)^  companyName:.*$', '  companyName: CodexShipDetailValidation')
$detailSettings = [regex]::Replace($detailSettings, '(?m)^  productName:.*$', '  productName: ShipDetailTavern20261014')
[IO.File]::WriteAllText($detailSettingsPath, $detailSettings, [Text.UTF8Encoding]::new($false))
Write-Output ("Ship detail validation: {0} files copied into {1}; production scenes and profiles untouched." -f $detailCopied,$detailTarget)
