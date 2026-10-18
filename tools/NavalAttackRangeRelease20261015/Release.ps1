param([Parameter(Mandatory=$true)][ValidateSet('Snapshot','Windows','Smoke','Package','Seal')][string]$Stage)
$ErrorActionPreference = 'Stop'
$navalSource = [IO.Path]::GetFullPath('D:/Game/UnityClient')
$navalProject = [IO.Path]::GetFullPath('D:/Game/tmp/shipyard-detail-clean-20261011')
$navalRoot = 'D:/Game/Builds/第七港口-海战攻击范围20261015'
$navalEvidence = 'D:/Game/Artifacts/NavalAttackRangeRelease20261015'
$navalVersion = '0.3.20261015.5'
$navalWindows = Join-Path $navalRoot 'Windows'
$navalExe = Join-Path $navalWindows '第七港口.exe'
$navalZip = Join-Path $navalRoot '第七港口-Windows-20261015-海战攻击范围.zip'
function Assert-EditorAvailable {
    if (@(Get-CimInstance Win32_Process | Where-Object { $_.Name -ieq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($navalProject.Replace('/','\')) }).Count) { throw 'The isolated project is occupied by Unity.' }
}
function Assert-Report {
    $report = [IO.File]::ReadAllText((Join-Path $navalWindows 'build-report.txt'))
    if ($report -notmatch '(?m)^Result: Succeeded\r?$' -or $report -notmatch '(?m)^Errors: 0\r?$' -or $report -notmatch ('(?m)^Version: ' + [regex]::Escape($navalVersion) + '\r?$')) { throw 'Build report failed or has the wrong version.' }
}
function Quote-Arguments($Values) {
    $quoted = foreach ($value in $Values) { $escaped = [regex]::Replace($value,'(\\*)"','$1$1\"'); '"' + [regex]::Replace($escaped,'(\\+)$','$1$1') + '"' }
    return ($quoted -join ' ')
}
if ($Stage -eq 'Snapshot') {
    Assert-EditorAvailable
    if (Test-Path -LiteralPath $navalRoot) { throw 'Use a new release output directory.' }
    if (Test-Path -LiteralPath $navalEvidence) { throw 'Use a new evidence directory.' }
    foreach ($test in @('D:/Game/Artifacts/NavalAttackRange20261015/UnityFinal/edit-results.xml','D:/Game/Artifacts/NavalAttackRange20261015/UnityVerified/play-results.xml')) {
        [xml]$result = Get-Content -LiteralPath $test -Raw
        if ([int]$result.'test-run'.failed -ne 0 -or [int]$result.'test-run'.passed -lt 1) { throw 'Required attack range regression is not passing.' }
    }
    foreach ($project in @($navalSource,$navalProject)) {
        $retired = Join-Path $project 'Assets/_Project/Scenes/UIPreviewsForMe'
        if ((Test-Path -LiteralPath $retired) -and @(Get-ChildItem -LiteralPath $retired -Force).Count) { throw 'Retired preview contents must not enter this release.' }
    }
    [IO.Directory]::CreateDirectory($navalEvidence) | Out-Null
    $files = @(foreach ($part in @('Assets','Packages','ProjectSettings')) { Get-ChildItem -LiteralPath (Join-Path $navalSource $part) -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/]UIPreviewsForMe(?:[\\/.]|$)' } })
    $manifest = @(foreach ($file in $files) { [pscustomobject]@{ Path=$file.FullName.Substring($navalSource.Length+1); Bytes=$file.Length; SHA256=(Get-FileHash -LiteralPath $file.FullName).Hash } })
    $manifest | Export-Csv -LiteralPath (Join-Path $navalEvidence 'source-manifest.csv') -NoTypeInformation -Encoding utf8
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $manifest) { [void]$paths.Add($file.Path) }
    $excluded = [Collections.Generic.List[string]]::new()
    $changed = [Collections.Generic.List[string]]::new()
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        foreach ($old in @(Get-ChildItem -LiteralPath (Join-Path $navalProject $part) -File -Recurse)) {
            $relative = $old.FullName.Substring($navalProject.Length+1)
            if ($relative -match '[\\/]UIPreviewsForMe(?:[\\/.]|$)' -or $paths.Contains($relative)) { continue }
            $oldPath = [IO.Path]::GetFullPath($old.FullName)
            $quarantineRoot = [IO.Path]::GetFullPath((Join-Path $navalEvidence 'ExcludedValidationFiles'))
            $destination = [IO.Path]::GetFullPath((Join-Path $quarantineRoot $relative))
            if (-not $oldPath.StartsWith($navalProject + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not $destination.StartsWith($quarantineRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Quarantine path escaped its workspace.' }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            Move-Item -LiteralPath $oldPath -Destination $destination
            $excluded.Add($relative)
        }
    }
    foreach ($file in $manifest) {
        $source = Join-Path $navalSource $file.Path
        $target = Join-Path $navalProject $file.Path
        if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne $file.SHA256) {
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            Copy-Item -LiteralPath $source -Destination $target -Force
            if ((Get-FileHash -LiteralPath $target).Hash -ne $file.SHA256) { throw ('Source changed during snapshot: ' + $file.Path) }
            $changed.Add($file.Path)
        }
    }
    $changed | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $navalEvidence 'synchronized-files.json') -Encoding utf8
    $excluded | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $navalEvidence 'excluded-validation-files.json') -Encoding utf8
    $invalid = @(Get-ChildItem -LiteralPath (Join-Path $navalProject 'Assets') -Filter '*.meta' -File -Recurse | Where-Object { [IO.File]::ReadAllText($_.FullName) -notmatch '(?m)^guid: [a-fA-F0-9]{32}\r?$' })
    if ($invalid.Count) { $invalid.FullName | Set-Content -LiteralPath (Join-Path $navalEvidence 'invalid-metadata.txt'); throw 'Invalid metadata in release snapshot.' }
    $diagnostics = @(Get-ChildItem -LiteralPath (Join-Path $navalProject 'Assets') -File -Recurse | Where-Object { $_.Name -match 'RuntimeMemoryAudit|StartupMemoryAudit|PlayerPerformanceAudit|MemoryAudit' })
    if ($diagnostics.Count) { throw 'Diagnostic injection found in release source.' }
    $builder = Join-Path $navalProject 'Assets/_Project/Editor/WindowsReleaseBuilder.cs'
    Copy-Item -LiteralPath $builder -Destination (Join-Path $navalEvidence 'WindowsReleaseBuilder.before.cs')
    $builderText = [IO.File]::ReadAllText($builder)
    $oldVersion = 'PlayerSettings.bundleVersion = "0.3.20261015.3";'
    if ([regex]::Matches($builderText,[regex]::Escape($oldVersion)).Count -ne 1) { throw 'Unexpected builder version.' }
    [IO.File]::WriteAllText($builder,$builderText.Replace($oldVersion,('PlayerSettings.bundleVersion = "' + $navalVersion + '";')),[Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath 'D:/Game/tools/ReleaseBuild/Package.ps1' -Destination (Join-Path $navalEvidence 'Package.ps1')
    [pscustomobject]@{ Result='PASS'; Version=$navalVersion; Files=$manifest.Count; Bytes=($manifest | Measure-Object Bytes -Sum).Sum; ChangedCode=@($changed | Where-Object { $_ -like '*.cs' }); Synchronized=$changed.Count; Excluded=$excluded.Count; Source=$navalSource; Project=$navalProject } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $navalEvidence 'snapshot.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $navalEvidence 'snapshot.json')
    exit 0
}
if (-not (Test-Path -LiteralPath (Join-Path $navalEvidence 'snapshot.json'))) { throw 'Snapshot must complete first.' }
if ($Stage -in @('Windows','Smoke')) {
    Assert-EditorAvailable
    $run = Join-Path $navalEvidence $Stage
    if (Test-Path -LiteralPath $run) { throw 'Preserve previous run evidence.' }
    [IO.Directory]::CreateDirectory($run) | Out-Null
    if ($Stage -eq 'Windows') {
        $imageImports = [IO.File]::ReadAllText((Join-Path $navalEvidence 'ImageRepair/import-report.txt'))
        if ($imageImports -notmatch '^PASS\r?\n' -or $imageImports -notmatch '(?m)^Images: 875\r?$' -or $imageImports -notmatch '(?m)^Failed: 0\r?$') { throw 'Resource image imports must all pass before the final build.' }
        $program = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
        $arguments = @('-batchmode','-nographics','-quit','-projectPath',$navalProject,'-executeMethod','ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine','-releaseOutput',$navalExe,'-logFile',(Join-Path $run 'unity.log'))
        $timeout = 2400
    } else {
        Assert-Report
        $program = $navalExe
        $arguments = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-shatteredSailSmokeTest',(Join-Path $run 'Report'),'-logFile',(Join-Path $run 'player.log'))
        $timeout = 180
    }
    $started = [DateTime]::UtcNow
    $process = Start-Process -FilePath $program -ArgumentList (Quote-Arguments $arguments) -WorkingDirectory $navalProject -WindowStyle Hidden -PassThru
    $null = $process.Handle
    [pscustomobject]@{ PID=$process.Id; Program=$program; Arguments=$arguments; StartedUtc=$started.ToString('o') } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $run 'launch.json') -Encoding utf8
    Write-Output ($Stage + ' started, PID ' + $process.Id)
    while (-not $process.WaitForExit(15000)) {
        $seconds = ([DateTime]::UtcNow-$started).TotalSeconds
        Write-Output ($Stage + ' running: ' + [int]$seconds + 's')
        if ($seconds -gt $timeout) { $process.Kill(); throw 'Owned release process timed out; evidence retained.' }
    }
    [pscustomobject]@{ ExitCode=$process.ExitCode; Seconds=([DateTime]::UtcNow-$started).TotalSeconds } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    if ($process.ExitCode -ne 0) { throw ($Stage + ' failed; inspect its log.') }
    if ($Stage -eq 'Windows') {
        Assert-Report
        $assetLog = [IO.File]::ReadAllText((Join-Path $run 'unity.log')).Replace('\','/')
        $imageFiles = @(Get-ChildItem -LiteralPath (Join-Path $navalProject 'Assets/_Project/Resources') -File -Recurse | Where-Object { $_.Extension -in @('.png','.jpg','.jpeg') })
        $missingImages = @(foreach ($file in $imageFiles) { $relative = $file.FullName.Substring($navalProject.Length+1).Replace('\','/'); if (-not $assetLog.Contains($relative)) { $relative } })
        [pscustomobject]@{ Result=$(if ($missingImages.Count) {'FAIL'} else {'PASS'}); Images=$imageFiles.Count; Missing=$missingImages } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $run 'packaged-images.json') -Encoding utf8
        if ($missingImages.Count) { throw 'Resource images are missing from the actual build asset list.' }
        Get-Content -LiteralPath (Join-Path $navalWindows 'build-report.txt')
    }
    else {
        $smoke = [IO.File]::ReadAllText((Join-Path $run 'Report/player-smoke-report.txt'))
        if ($smoke -notmatch '^PASS\r?\n' -or $smoke -notmatch ('(?m)^Version: '+[regex]::Escape($navalVersion)+'\r?$') -or $smoke -notmatch '(?m)^Company: ShatteredSail\r?$' -or $smoke -notmatch '(?m)^Product: Shattered Sail\r?$') { throw 'Smoke identity or result failed.' }
        Write-Output $smoke
    }
    exit 0
}
if ($Stage -eq 'Package') {
    Assert-Report
    $smokeReport = [IO.File]::ReadAllText((Join-Path $navalEvidence 'Smoke/Report/player-smoke-report.txt'))
    if ($smokeReport -notmatch '^PASS\r?\n' -or $smokeReport -notmatch ('(?m)^Version: '+[regex]::Escape($navalVersion)+'\r?$')) { throw 'Actual player smoke must pass before packaging.' }
    & (Join-Path $navalEvidence 'Package.ps1') -BuildDirectory $navalWindows -ZipPath $navalZip -ExecutableName '第七港口.exe' | Tee-Object -FilePath (Join-Path $navalEvidence 'package.json')
    exit 0
}
if ($Stage -eq 'Seal') {
    Assert-Report
    $smoke = Get-Content -LiteralPath (Join-Path $navalEvidence 'Smoke/result.json') -Raw | ConvertFrom-Json
    $package = Get-Content -LiteralPath (Join-Path $navalEvidence 'package.json') -Raw | ConvertFrom-Json
    if ($smoke.ExitCode -ne 0 -or $package.Result -ne 'PASS' -or (Get-FileHash -LiteralPath $navalZip).Hash -ne $package.ZipSHA256) { throw 'Release verification incomplete.' }
    [xml]$artTests = Get-Content -LiteralPath (Join-Path $navalEvidence 'ArtRepair/edit-results.xml') -Raw
    if ([int]$artTests.'test-run'.failed -ne 0 -or [int]$artTests.'test-run'.passed -ne 5) { throw 'Card artwork reference repair tests are incomplete.' }
    [xml]$frameTests = Get-Content -LiteralPath (Join-Path $navalEvidence 'FrameRepair/edit-results.xml') -Raw
    if ([int]$frameTests.'test-run'.failed -ne 0 -or [int]$frameTests.'test-run'.passed -ne 3) { throw 'Complete card artwork and frame checks are incomplete.' }
    $images = Get-Content -LiteralPath (Join-Path $navalEvidence 'Windows/packaged-images.json') -Raw | ConvertFrom-Json
    if ($images.Result -ne 'PASS' -or $images.Images -ne 875) { throw 'Packaged image completeness check failed.' }
    [pscustomobject]@{ Result='PASS'; Version=$navalVersion; Platform='Windows x86_64 / Mono'; Executable=$navalExe; Package=$package; SmokePassed=$true; Validation='123 Core; 70 Unity EditMode; 7 Unity PlayMode; 5 card artwork reference checks; 3 complete card artwork and frame checks; 875 packaged images'; Evidence=$navalEvidence } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $navalRoot 'release-manifest.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $navalRoot 'release-manifest.json')
}
