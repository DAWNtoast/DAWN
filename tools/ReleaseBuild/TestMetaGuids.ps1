param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditAllowedRoot = Join-Path $auditWorkspace 'tmp'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $auditAllowedRoot ('MetaGuidAudit-' + [Guid]::NewGuid().ToString('N')) }
$auditRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $auditRoot.StartsWith($auditAllowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a new test directory under workspace tmp.' }
if (Test-Path -LiteralPath $auditRoot) { throw "Refusing existing test output: $auditRoot" }
[IO.Directory]::CreateDirectory($auditRoot) | Out-Null
$auditScript = Join-Path $PSScriptRoot 'AssertMetaGuids.ps1'
$auditResults = [Collections.Generic.List[object]]::new()
$guid = '1234567890abcdef1234567890abcdef'

function New-MetaFixture([string]$Name, [hashtable]$Files) {
    $assets = Join-Path (Join-Path $auditRoot $Name) 'Assets'
    [IO.Directory]::CreateDirectory($assets) | Out-Null
    foreach ($name in $Files.Keys) {
        $path = Join-Path $assets $name
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
        [IO.File]::WriteAllText($path, $Files[$name], [Text.UTF8Encoding]::new($false))
    }
    return $assets
}
function Get-FixtureHashes([string]$Assets) {
    return (@(Get-ChildItem -LiteralPath $Assets -File -Recurse -Force | Sort-Object FullName | ForEach-Object {
        $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }) -join "`n")
}
function Assert-MetaResult([string]$Name, [string]$Assets, [string[]]$ExpectedErrors = @()) {
    $before = Get-FixtureHashes $Assets
    $failure = ''
    try { if (@(& $auditScript -AssetsPath $Assets).Count) { throw 'A successful precheck must not alter pipeline output.' } }
    catch { $failure = $_.Exception.Message }
    if (-not $ExpectedErrors.Count -and $failure) { throw "$Name failed: $failure" }
    foreach ($expected in $ExpectedErrors) {
        if (-not $failure.Contains($expected)) { throw "$Name did not report '$expected': $failure" }
    }
    if ((Get-FixtureHashes $Assets) -cne $before) { throw "$Name modified its input files." }
    $auditResults.Add([ordered]@{ name=$Name; passed=$true; message=$failure })
    Write-Output "PASS $Name"
}

$assets = New-MetaFixture 'valid' @{ 'First.cs.meta'="fileFormatVersion: 2`r`nguid: $guid`r`n"; 'Nested/Second.cs.meta'="guid: ABCDEF1234567890ABCDEF1234567890`n" }
Assert-MetaResult 'valid' $assets
$assets = New-MetaFixture 'guid-33' @{ 'Bad.cs.meta'="guid: ${guid}1`n" }
Assert-MetaResult 'guid-33' $assets @((Join-Path $assets 'Bad.cs.meta'), '32 hexadecimal')
$assets = New-MetaFixture 'duplicate' @{ 'First.cs.meta'="guid: $guid`n"; 'Nested/Second.cs.meta'="guid: $($guid.ToUpperInvariant())`n" }
Assert-MetaResult 'duplicate' $assets @((Join-Path $assets 'First.cs.meta'), (Join-Path $assets 'Nested/Second.cs.meta'), 'duplicate guid')
$assets = New-MetaFixture 'missing-guid' @{ 'Bad.cs.meta'="fileFormatVersion: 2`n" }
Assert-MetaResult 'missing-guid' $assets @((Join-Path $assets 'Bad.cs.meta'), 'found 0')
$assets = New-MetaFixture 'multiple-guid' @{ 'Bad.cs.meta'="guid: $guid`n  guid: ABCDEF1234567890ABCDEF1234567890`n" }
Assert-MetaResult 'multiple-guid' $assets @((Join-Path $assets 'Bad.cs.meta'), 'found 2')

# Exercise both real entry points in a synthetic workspace. A nonexistent editor
# also ensures a missing guard cannot accidentally start Unity during this audit.
$harness = Join-Path $auditRoot 'EntryPoints'
$scripts = Join-Path $harness 'tools/ReleaseBuild'
[IO.Directory]::CreateDirectory($scripts) | Out-Null
foreach ($name in @('AssertMetaGuids.ps1','DesktopRelease.ps1','RunTests.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $scripts $name)
}
$sourceAssets = New-MetaFixture 'EntryPoints/UnityClient' @{ 'Bad.cs.meta'="guid: ${guid}1`n" }
$projectAssets = New-MetaFixture 'EntryPoints/tmp/Project' @{ 'Bad.cs.meta'="guid: ${guid}1`n" }
$project = Split-Path $projectAssets -Parent
$settings = Join-Path $project 'ProjectSettings'
[IO.Directory]::CreateDirectory($settings) | Out-Null
[IO.File]::WriteAllText((Join-Path $settings 'ProjectVersion.txt'), 'fixture')
[IO.File]::WriteAllText((Join-Path $settings 'ProjectSettings.asset'), "  companyName: MetaGuidValidation`n")
foreach ($entry in @('Snapshot','RunTests')) {
    $destination = Join-Path $harness ('Artifacts/' + $entry)
    $failure = ''
    try {
        if ($entry -eq 'Snapshot') {
            & (Join-Path $scripts 'DesktopRelease.ps1') -Stage Snapshot -ProjectPath $project -ReleaseDirectory (Join-Path $harness 'Builds/Release') -EvidenceDirectory $destination -Version '0.0.0.1' -ZipFileName 'fixture.zip' -ValidationSummary (Join-Path $harness 'unused.json') -UnityEditor (Join-Path $harness 'nonexistent.exe')
        } else {
            & (Join-Path $scripts 'RunTests.ps1') -Platform EditMode -ProjectPath $project -Output $destination -UnityEditor (Join-Path $harness 'nonexistent.exe')
        }
    } catch { $failure = $_.Exception.Message }
    $expectedAssets = if ($entry -eq 'Snapshot') { $sourceAssets } else { $projectAssets }
    if (-not $failure.Contains('Asset meta GUID validation failed:') -or -not $failure.Contains((Join-Path $expectedAssets 'Bad.cs.meta'))) { throw "$entry did not reject its invalid asset: $failure" }
    if (Test-Path -LiteralPath $destination) { throw "$entry wrote output before rejecting invalid meta." }
    $auditResults.Add([ordered]@{ name=$entry; passed=$true; message=$failure })
    Write-Output "PASS $entry"
}
[ordered]@{ passed=$auditResults.Count; failed=0; cases=$auditResults.ToArray() } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $auditRoot 'summary.json') -Encoding utf8
Write-Output "Meta GUID audit: $($auditResults.Count) passed. Evidence: $auditRoot"
