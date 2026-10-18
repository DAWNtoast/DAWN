param(
    [string]$ProjectPath = 'D:/Game/tmp/shipyard-detail-clean-20261011',
    [ValidateSet('Code','Resources','All')][string]$Scope = 'All'
)
$ErrorActionPreference = 'Stop'
$navalWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$navalSource = Join-Path $navalWorkspace 'UnityClient'
$navalTarget = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$navalAllowed = Join-Path $navalWorkspace 'tmp'
if (-not $navalTarget.StartsWith($navalAllowed + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $navalTarget.Contains('UIPreviewsForMe')) {
    throw 'Validation must use an isolated project under this workspace/tmp.'
}
if (-not (Test-Path -LiteralPath (Join-Path $navalTarget 'ProjectSettings/ProjectVersion.txt'))) { throw 'An existing isolated Unity project is required.' }
$navalRetired = Join-Path $navalTarget 'Assets/_Project/Scenes/UIPreviewsForMe'
if ((Test-Path -LiteralPath $navalRetired) -and @(Get-ChildItem -LiteralPath $navalRetired -Force).Count) { throw 'Quarantine pre-existing isolated retired previews outside Assets before validation. This tool will never import or copy them.' }
$navalRunning = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($navalTarget.Replace('\','/'))
})
if ($navalRunning.Count) { throw 'The isolated project is occupied by Unity.' }
$script:navalCopied = 0
function Copy-NavalFile([string]$relative) {
    if ($relative -match '(^|[/\\])UIPreviewsForMe([/\\]|$)') { throw 'Retired previews are excluded.' }
    $source = [IO.Path]::GetFullPath((Join-Path $navalSource $relative))
    $target = [IO.Path]::GetFullPath((Join-Path $navalTarget $relative))
    if (-not $source.StartsWith($navalSource + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not $target.StartsWith($navalTarget + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe synchronization path.' }
    if (-not (Test-Path -LiteralPath $source)) { return }
    if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $source).Hash -eq (Get-FileHash -LiteralPath $target).Hash) { return }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    $script:navalCopied++
}
if ($Scope -in @('Code','All')) {
    foreach ($folder in @('Core','Content','Persistence','Presentation/Runtime','Editor','Tests')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $navalSource ('Assets/_Project/' + $folder)) -File -Recurse) {
            if ($file.Extension -in @('.cs','.asmdef','.meta')) { Copy-NavalFile $file.FullName.Substring($navalSource.Length + 1) }
        }
    }
    foreach ($folder in @('Packages','ProjectSettings')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $navalSource $folder) -File) { Copy-NavalFile $file.FullName.Substring($navalSource.Length + 1) }
    }
    foreach ($scene in @('MainMenu','CoreGameplayDemo','TutorialVoyage','UIPreviews/UI_Battle')) {
        Copy-NavalFile ('Assets/_Project/Scenes/' + $scene + '.unity')
        Copy-NavalFile ('Assets/_Project/Scenes/' + $scene + '.unity.meta')
    }
    $settingsPath = Join-Path $navalTarget 'ProjectSettings/ProjectSettings.asset'
    $settings = [IO.File]::ReadAllText($settingsPath)
    $settings = [regex]::Replace($settings, '(?m)^  companyName:.*$', '  companyName: CodexNavalValidation')
    $settings = [regex]::Replace($settings, '(?m)^  productName:.*$', '  productName: NavalTacticsValidation20261015')
    [IO.File]::WriteAllText($settingsPath, $settings, [Text.UTF8Encoding]::new($false))
}
if ($Scope -in @('Resources','All')) {
    foreach ($folder in @('Resources','Presentation/Prefabs')) {
        $resourceRoot = Join-Path $navalSource ('Assets/_Project/' + $folder)
        if (-not (Test-Path -LiteralPath $resourceRoot)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $resourceRoot -File -Recurse) { Copy-NavalFile $file.FullName.Substring($navalSource.Length + 1) }
    }
}
Write-Output ("Naval validation {0}: copied {1} files into {2}. No retired previews copied or generated." -f $Scope,$navalCopied,$navalTarget)
