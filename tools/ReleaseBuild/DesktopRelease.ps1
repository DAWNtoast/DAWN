param(
    [Parameter(Mandatory=$true)][ValidateSet('Snapshot','Windows','Smoke','Package','Seal')][string]$Stage,
    [Parameter(Mandatory=$true)][string]$ReleaseDirectory,
    [Parameter(Mandatory=$true)][string]$EvidenceDirectory,
    [Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory=$true)][string]$ZipFileName,
    [Parameter(Mandatory=$true)][string]$ValidationSummary,
    [string]$ProjectPath = 'D:/Game/tmp/shipyard-detail-clean-20261011',
    [string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe',
    [string]$SourceProjectPath = ''
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$source = if ($SourceProjectPath) { [IO.Path]::GetFullPath($SourceProjectPath).TrimEnd('\','/') } else { Join-Path $workspace 'UnityClient' }
$project = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$release = [IO.Path]::GetFullPath($ReleaseDirectory).TrimEnd('\','/')
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory).TrimEnd('\','/')
function Assert-ChildPath([string]$Path, [string]$Parent) {
    if (-not $Path.StartsWith($Parent.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw "Path escaped its workspace directory: $Path" }
}
Assert-ChildPath $project (Join-Path $workspace 'tmp')
Assert-ChildPath $source $workspace
if ($source -eq $project -or
    $source.StartsWith($project + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $project.StartsWith($source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Source and build project directories must be separate and must not contain each other.'
}
Assert-ChildPath $release (Join-Path $workspace 'Builds')
Assert-ChildPath $evidence (Join-Path $workspace 'Artifacts')
if ([IO.Path]::GetFileName($ZipFileName) -ne $ZipFileName -or $ZipFileName -notlike '*.zip') { throw 'Use a ZIP file name without directories.' }
if (-not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) { throw 'An existing isolated Unity project is required.' }
$playerDirectory = Join-Path $release 'Windows'
$player = Join-Path $playerDirectory '第七港口.exe'
$archivePath = Join-Path $release $ZipFileName
function Assert-EditorAvailable {
    if (@(Get-CimInstance Win32_Process | Where-Object { $_.Name -ieq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($project.Replace('/','\')) }).Count) { throw 'The isolated project is occupied by Unity.' }
    foreach ($root in @($source,$project)) {
        $retired = Join-Path $root 'Assets/_Project/Scenes/UIPreviewsForMe'
        if ((Test-Path -LiteralPath $retired) -and @(Get-ChildItem -LiteralPath $retired -Force).Count) { throw 'Retired previews must remain empty.' }
    }
}
function Assert-Report {
    $report = [IO.File]::ReadAllText((Join-Path $playerDirectory 'build-report.txt'))
    if ($report -notmatch '(?m)^Result: Succeeded\r?$' -or $report -notmatch '(?m)^Errors: 0\r?$' -or $report -notmatch ('(?m)^Version: ' + [regex]::Escape($Version) + '\r?$')) { throw 'Build result or version failed.' }
}
function Assert-Validation {
    $validation = Get-Content -LiteralPath $ValidationSummary -Raw | ConvertFrom-Json
    if ($validation.Result -ne 'PASS' -or $validation.Failed -ne 0 -or $validation.Passed -le 0) { throw 'Regression checks must pass before building.' }
    $snapshotHash = (Get-FileHash -LiteralPath (Join-Path $evidence 'source-manifest.csv')).Hash
    $projectSnapshotHash = (Get-FileHash -LiteralPath (Join-Path $evidence 'project-input-manifest.csv')).Hash
    if ($validation.SourceManifestSHA256 -ne $snapshotHash -or
        $validation.ProjectInputManifestSHA256 -ne $projectSnapshotHash) { throw 'Validation belongs to a different source or isolated project snapshot.' }
}
function Assert-ManifestFiles([string]$Root, [string]$ManifestPath) {
    $manifest = @(Import-Csv -LiteralPath $ManifestPath)
    if (-not $manifest.Count) { throw 'An input manifest must not be empty.' }
    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $manifest) {
        $relative = $file.Path.Replace('\','/')
        if ($relative -notmatch '^(Assets|Packages|ProjectSettings)/' -or $relative -match '(^|/)\.\.?(/|$)' -or
            $file.SHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or -not $expected.Add($relative)) { throw ('Invalid or duplicate manifest entry: ' + $relative) }
        $target = [IO.Path]::GetFullPath((Join-Path $Root $relative))
        Assert-ChildPath $target $Root
        if (-not (Test-Path -LiteralPath $target -PathType Leaf) -or (Get-FileHash -LiteralPath $target).Hash -ne $file.SHA256) {
            throw ('Input changed after snapshot: ' + $target)
        }
    }
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $Root $part) -File -Recurse -Force) {
            $relative = $file.FullName.Substring($Root.Length+1).Replace('\','/')
            if ($relative -match '(^|/)UIPreviewsForMe(?:/|\.|$)') { continue }
            if (-not $expected.Contains($relative)) { throw ('Unexpected source/build input after snapshot: ' + $file.FullName) }
        }
    }
}
function Assert-Snapshot {
    $sourceManifest = Join-Path $evidence 'source-manifest.csv'
    $projectManifest = Join-Path $evidence 'project-input-manifest.csv'
    Assert-ManifestFiles $source $sourceManifest
    if (Test-Path -LiteralPath $projectManifest) {
        $snapshot = Get-Content -LiteralPath (Join-Path $evidence 'snapshot.json') -Raw | ConvertFrom-Json
        if ($snapshot.SourceManifestSHA256 -ne (Get-FileHash -LiteralPath $sourceManifest).Hash -or
            $snapshot.ProjectInputManifestSHA256 -ne (Get-FileHash -LiteralPath $projectManifest).Hash) { throw 'Snapshot manifest changed.' }
        Assert-ManifestFiles $project $projectManifest
    } else {
        if ($Stage -ne 'Snapshot') { throw 'The complete isolated project input manifest is required.' }
        # During Snapshot, verify the exact copy before applying the two storage
        # identity overrides and the editor-only validator. Afterwards the full
        # isolated inputs, including those overrides, have their own locked hash.
        Assert-ManifestFiles $project $sourceManifest
    }
}
function Quote-Arguments($Values) {
    $quoted = foreach ($value in $Values) { $escaped = [regex]::Replace($value,'(\\*)"','$1$1\"'); '"' + [regex]::Replace($escaped,'(\\+)$','$1$1') + '"' }
    return ($quoted -join ' ')
}
if ($Stage -eq 'Snapshot') {
    Assert-EditorAvailable
    & (Join-Path $PSScriptRoot 'AssertMetaGuids.ps1') -AssetsPath (Join-Path $source 'Assets')
    if ((Test-Path -LiteralPath $release) -or (Test-Path -LiteralPath $evidence)) { throw 'Use new release and evidence directories.' }
    [IO.Directory]::CreateDirectory($evidence) | Out-Null
    $files = @(foreach ($part in @('Assets','Packages','ProjectSettings')) { Get-ChildItem -LiteralPath (Join-Path $source $part) -File -Recurse -Force | Where-Object { $_.FullName -notmatch '[\\/]UIPreviewsForMe(?:[\\/.]|$)' } })
    $manifest = @(foreach ($file in $files) { [pscustomobject]@{ Path=$file.FullName.Substring($source.Length+1); Bytes=$file.Length; SHA256=(Get-FileHash -LiteralPath $file.FullName).Hash } })
    $manifest | Export-Csv -LiteralPath (Join-Path $evidence 'source-manifest.csv') -NoTypeInformation -Encoding utf8
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $manifest) { [void]$paths.Add($file.Path) }
    $excluded = [Collections.Generic.List[string]]::new()
    $quarantine = Join-Path $evidence 'ExcludedValidationFiles'
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        foreach ($old in @(Get-ChildItem -LiteralPath (Join-Path $project $part) -File -Recurse -Force)) {
            $relative = $old.FullName.Substring($project.Length+1)
            if ($relative -match '[\\/]UIPreviewsForMe(?:[\\/.]|$)' -or $paths.Contains($relative)) { continue }
            $oldPath = [IO.Path]::GetFullPath($old.FullName)
            $destination = [IO.Path]::GetFullPath((Join-Path $quarantine $relative))
            Assert-ChildPath $oldPath $project
            Assert-ChildPath $destination $quarantine
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            Move-Item -LiteralPath $oldPath -Destination $destination
            $excluded.Add($relative)
        }
    }
    foreach ($file in $manifest) {
        $origin = Join-Path $source $file.Path
        $target = Join-Path $project $file.Path
        if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne $file.SHA256) {
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            Copy-Item -LiteralPath $origin -Destination $target -Force
        }
    }
    Assert-Snapshot
    $settingsPath = Join-Path $project 'ProjectSettings/ProjectSettings.asset'
    $settings = [IO.File]::ReadAllText($settingsPath)
    $settings = [regex]::Replace($settings,'(?m)^  companyName:.*$','  companyName: ShatteredSailValidation')
    $settings = [regex]::Replace($settings,'(?m)^  productName:.*$','  productName: CodeStructureReleaseValidation')
    [IO.File]::WriteAllText($settingsPath,$settings,[Text.UTF8Encoding]::new($false))
    $helper = Join-Path $project 'Assets/_Project/Editor/ReleaseAssetValidation.cs'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ValidateAndBuild.cs') -Destination $helper
    [IO.File]::WriteAllText(($helper + '.meta'),"fileFormatVersion: 2`nguid: f3c9c3e5bd834512a56585a047584ce8`n",[Text.UTF8Encoding]::new($false))
    $projectFiles = @(foreach ($part in @('Assets','Packages','ProjectSettings')) { Get-ChildItem -LiteralPath (Join-Path $project $part) -File -Recurse -Force | Where-Object { $_.FullName -notmatch '[\\/]UIPreviewsForMe(?:[\\/.]|$)' } })
    $projectManifest = @(foreach ($file in $projectFiles) { [pscustomobject]@{ Path=$file.FullName.Substring($project.Length+1); Bytes=$file.Length; SHA256=(Get-FileHash -LiteralPath $file.FullName).Hash } })
    $projectManifest | Export-Csv -LiteralPath (Join-Path $evidence 'project-input-manifest.csv') -NoTypeInformation -Encoding utf8
    $excluded | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'excluded-validation-files.json') -Encoding utf8
    [pscustomobject]@{ Result='PASS'; Version=$Version; Files=$manifest.Count; ProjectFiles=$projectManifest.Count; Excluded=$excluded.Count; Project=$project; Overrides=@('Validation storage identity','Editor-only resource import validator'); SourceManifestSHA256=(Get-FileHash -LiteralPath (Join-Path $evidence 'source-manifest.csv')).Hash; ProjectInputManifestSHA256=(Get-FileHash -LiteralPath (Join-Path $evidence 'project-input-manifest.csv')).Hash } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidence 'snapshot.json') -Encoding utf8
    Assert-Snapshot
    Get-Content -LiteralPath (Join-Path $evidence 'snapshot.json')
    exit 0
}
if (-not (Test-Path -LiteralPath (Join-Path $evidence 'snapshot.json'))) { throw 'Snapshot must complete first.' }
if ($Stage -in @('Windows','Smoke')) {
    Assert-EditorAvailable
    $run = Join-Path $evidence $Stage
    if (Test-Path -LiteralPath $run) { throw 'Preserve previous run evidence.' }
    if ($Stage -eq 'Windows') {
        Assert-Validation
        Assert-Snapshot
        $program = $UnityEditor
        $arguments = @('-batchmode','-nographics','-quit','-projectPath',$project,'-executeMethod','ShatteredSail.Editor.ReleaseAssetValidation.Build','-releaseOutput',$player,'-releaseVersion',$Version,'-releaseEvidence',$evidence,'-logFile',(Join-Path $run 'unity.log'))
        $timeout = 2400
    } else {
        Assert-Report
        $program = $player
        $arguments = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-shatteredSailSmokeTest',(Join-Path $run 'Report'),'-logFile',(Join-Path $run 'player.log'))
        $timeout = 180
    }
    [IO.Directory]::CreateDirectory($run) | Out-Null
    $started = [DateTime]::UtcNow
    $process = Start-Process -FilePath $program -ArgumentList (Quote-Arguments $arguments) -WorkingDirectory $project -WindowStyle Hidden -PassThru
    $null = $process.Handle
    [pscustomobject]@{ PID=$process.Id; Program=$program; Arguments=$arguments; StartedUtc=$started.ToString('o') } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $run 'launch.json') -Encoding utf8
    Write-Output ($Stage + ' started, PID ' + $process.Id)
    while (-not $process.WaitForExit(15000)) {
        Write-Output ($Stage + ' running: ' + [int]([DateTime]::UtcNow-$started).TotalSeconds + 's')
        if (([DateTime]::UtcNow-$started).TotalSeconds -gt $timeout) { $process.Kill(); throw 'Owned release process timed out; evidence retained.' }
    }
    [pscustomobject]@{ ExitCode=$process.ExitCode; Seconds=([DateTime]::UtcNow-$started).TotalSeconds } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    if ($process.ExitCode -ne 0) { throw ($Stage + ' failed; inspect its log.') }
    if ($Stage -eq 'Windows') {
        Assert-Report
        $assetLog = [IO.File]::ReadAllText((Join-Path $run 'unity.log')).Replace('\','/')
        $images = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/_Project/Resources') -File -Recurse | Where-Object { $_.Extension -in @('.png','.jpg','.jpeg') })
        $missing = @(foreach ($file in $images) { $relative = $file.FullName.Substring($project.Length+1).Replace('\','/'); if (-not $assetLog.Contains($relative)) { $relative } })
        [pscustomobject]@{ Result=$(if ($missing.Count) {'FAIL'} else {'PASS'}); Images=$images.Count; Missing=$missing } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $run 'packaged-images.json') -Encoding utf8
        if ($missing.Count) { throw 'Resource images missing from actual build assets.' }
        Get-Content -LiteralPath (Join-Path $playerDirectory 'build-report.txt')
    } else {
        $smoke = [IO.File]::ReadAllText((Join-Path $run 'Report/player-smoke-report.txt'))
        if ($smoke -notmatch '^PASS\r?\n' -or $smoke -notmatch ('(?m)^Version: '+[regex]::Escape($Version)+'\r?$') -or $smoke -notmatch '(?m)^Company: ShatteredSail\r?$' -or $smoke -notmatch '(?m)^Product: Shattered Sail\r?$') { throw 'Smoke identity or result failed.' }
        Write-Output $smoke
    }
    exit 0
}
Assert-Report
if ($Stage -eq 'Package') {
    $smoke = [IO.File]::ReadAllText((Join-Path $evidence 'Smoke/Report/player-smoke-report.txt'))
    if ($smoke -notmatch '^PASS\r?\n') { throw 'Actual player smoke must pass before packaging.' }
    & (Join-Path $PSScriptRoot 'Package.ps1') -BuildDirectory $playerDirectory -ZipPath $archivePath -ExecutableName '第七港口.exe' | Tee-Object -FilePath (Join-Path $evidence 'package.json')
    exit 0
}
Assert-Validation
Assert-Snapshot
$package = Get-Content -LiteralPath (Join-Path $evidence 'package.json') -Raw | ConvertFrom-Json
$smokeResult = Get-Content -LiteralPath (Join-Path $evidence 'Smoke/result.json') -Raw | ConvertFrom-Json
$imageResult = Get-Content -LiteralPath (Join-Path $evidence 'Windows/packaged-images.json') -Raw | ConvertFrom-Json
if ($package.Result -ne 'PASS' -or $smokeResult.ExitCode -ne 0 -or $imageResult.Result -ne 'PASS' -or (Get-FileHash -LiteralPath $archivePath).Hash -ne $package.ZipSHA256) { throw 'Release verification incomplete.' }
[pscustomobject]@{ Result='PASS'; Version=$Version; Platform='Windows x86_64 / Mono'; Executable=$player; Package=$package; SmokePassed=$true; ImagesReferencedInBuildLog=$imageResult.Images; Validation=$ValidationSummary; Evidence=$evidence; SourceManifestSHA256=(Get-FileHash -LiteralPath (Join-Path $evidence 'source-manifest.csv')).Hash; ProjectInputManifestSHA256=(Get-FileHash -LiteralPath (Join-Path $evidence 'project-input-manifest.csv')).Hash } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $release 'release-manifest.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $release 'release-manifest.json')
