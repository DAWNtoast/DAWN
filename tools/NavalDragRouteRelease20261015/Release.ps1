param(
    [Parameter(Mandatory=$true)][ValidateSet('Snapshot','Windows','Smoke','Package','Seal')][string]$Stage,
    [string]$ReleaseDirectory = 'D:/Game/Builds/第七港口-拖动航线与新手引导20261015',
    [string]$EvidenceDirectory = 'D:/Game/Artifacts/NavalDragRouteRelease20261015',
    [string]$Version = '0.3.20261015.6',
    [string]$ZipFileName = '第七港口-Windows-20261015-拖动航线与新手引导.zip',
    [string]$Validation = 'See Artifacts/NavalDragRoute20261015 and Artifacts/NavalTactics20261015/TutorialCourse'
)
$ErrorActionPreference = 'Stop'
$releaseSource = [IO.Path]::GetFullPath('D:/Game/UnityClient')
$releaseProject = [IO.Path]::GetFullPath('D:/Game/tmp/shipyard-detail-clean-20261011')
$releaseRoot = [IO.Path]::GetFullPath($ReleaseDirectory)
$releaseEvidence = [IO.Path]::GetFullPath($EvidenceDirectory)
$releaseVersion = $Version
if (-not $releaseRoot.StartsWith('D:\Game\Builds\',[StringComparison]::OrdinalIgnoreCase) -or
    -not $releaseEvidence.StartsWith('D:\Game\Artifacts\',[StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($ZipFileName) -ne $ZipFileName -or $ZipFileName -notlike '*.zip' -or
    $releaseVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Invalid release destination, archive name or version.' }
$releaseWindows = Join-Path $releaseRoot 'Windows'
$releaseExe = Join-Path $releaseWindows '第七港口.exe'
$releaseZip = Join-Path $releaseRoot $ZipFileName
function Assert-EditorAvailable {
    if (@(Get-CimInstance Win32_Process | Where-Object { $_.Name -ieq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($releaseProject.Replace('/','\')) }).Count) { throw 'The isolated project is occupied by Unity.' }
}
function Assert-Report {
    $report = [IO.File]::ReadAllText((Join-Path $releaseWindows 'build-report.txt'))
    if ($report -notmatch '(?m)^Result: Succeeded\r?$' -or $report -notmatch '(?m)^Errors: 0\r?$' -or $report -notmatch ('(?m)^Version: ' + [regex]::Escape($releaseVersion) + '\r?$')) { throw 'Build result or version failed.' }
}
function Quote-Arguments($Values) {
    $quoted = foreach ($value in $Values) { $escaped = [regex]::Replace($value,'(\\*)"','$1$1\"'); '"' + [regex]::Replace($escaped,'(\\+)$','$1$1') + '"' }
    return ($quoted -join ' ')
}
if ($Stage -eq 'Snapshot') {
    Assert-EditorAvailable
    if ((Test-Path -LiteralPath $releaseRoot) -or (Test-Path -LiteralPath $releaseEvidence)) { throw 'Use new release and evidence directories.' }
    foreach ($project in @($releaseSource,$releaseProject)) {
        $retired = Join-Path $project 'Assets/_Project/Scenes/UIPreviewsForMe'
        if ((Test-Path -LiteralPath $retired) -and @(Get-ChildItem -LiteralPath $retired -Force).Count) { throw 'Retired previews must remain empty.' }
    }
    [IO.Directory]::CreateDirectory($releaseEvidence) | Out-Null
    $files = @(foreach ($part in @('Assets','Packages','ProjectSettings')) { Get-ChildItem -LiteralPath (Join-Path $releaseSource $part) -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/]UIPreviewsForMe(?:[\\/.]|$)' } })
    $manifest = @(foreach ($file in $files) { [pscustomobject]@{ Path=$file.FullName.Substring($releaseSource.Length+1); Bytes=$file.Length; SHA256=(Get-FileHash -LiteralPath $file.FullName).Hash } })
    $manifest | Export-Csv -LiteralPath (Join-Path $releaseEvidence 'source-manifest.csv') -NoTypeInformation -Encoding utf8
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $manifest) { [void]$paths.Add($file.Path) }
    $excluded = [Collections.Generic.List[string]]::new()
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        foreach ($old in @(Get-ChildItem -LiteralPath (Join-Path $releaseProject $part) -File -Recurse)) {
            $relative = $old.FullName.Substring($releaseProject.Length+1)
            if ($relative -match '[\\/]UIPreviewsForMe(?:[\\/.]|$)' -or $paths.Contains($relative)) { continue }
            $oldPath = [IO.Path]::GetFullPath($old.FullName)
            $quarantine = [IO.Path]::GetFullPath((Join-Path $releaseEvidence 'ExcludedValidationFiles'))
            $destination = [IO.Path]::GetFullPath((Join-Path $quarantine $relative))
            if (-not $oldPath.StartsWith($releaseProject + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or -not $destination.StartsWith($quarantine + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Quarantine escaped the designated directories.' }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            Move-Item -LiteralPath $oldPath -Destination $destination
            $excluded.Add($relative)
        }
    }
    $changed = [Collections.Generic.List[string]]::new()
    foreach ($file in $manifest) {
        $source = Join-Path $releaseSource $file.Path
        $target = Join-Path $releaseProject $file.Path
        if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne $file.SHA256) {
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            Copy-Item -LiteralPath $source -Destination $target -Force
            $changed.Add($file.Path)
        }
        if ((Get-FileHash -LiteralPath $source).Hash -ne $file.SHA256 -or (Get-FileHash -LiteralPath $target).Hash -ne $file.SHA256) { throw ('Snapshot changed while copying: ' + $file.Path) }
    }
    $excluded | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseEvidence 'excluded-validation-files.json') -Encoding utf8
    $changed | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseEvidence 'synchronized-files.json') -Encoding utf8
    $invalid = @(Get-ChildItem -LiteralPath (Join-Path $releaseProject 'Assets') -Filter '*.meta' -File -Recurse | Where-Object { [IO.File]::ReadAllText($_.FullName) -notmatch '(?m)^guid: [a-fA-F0-9]{32}\r?$' })
    if ($invalid.Count) { throw ('Invalid metadata: ' + ($invalid.FullName -join ', ')) }
    $helper = Join-Path $releaseProject 'Assets/_Project/Editor/NavalDragRouteRelease.cs'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ValidateAndBuild.cs') -Destination $helper
    [IO.File]::WriteAllText(($helper + '.meta'),"fileFormatVersion: 2`nguid: f3c9c3e5bd834512a56585a047584ce8`n",[Text.UTF8Encoding]::new($false))
    [pscustomobject]@{ Result='PASS'; Version=$releaseVersion; Files=$manifest.Count; Changed=$changed.Count; Excluded=$excluded.Count; Project=$releaseProject; OnlyReleaseOverrides=@('Editor/NavalDragRouteRelease asset validation helper'); ReleaseVersionArgument=$releaseVersion } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $releaseEvidence 'snapshot.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $releaseEvidence 'snapshot.json')
    exit 0
}
if (-not (Test-Path -LiteralPath (Join-Path $releaseEvidence 'snapshot.json'))) { throw 'Snapshot must complete first.' }
if ($Stage -in @('Windows','Smoke')) {
    Assert-EditorAvailable
    $run = Join-Path $releaseEvidence $Stage
    if (Test-Path -LiteralPath $run) { throw 'Preserve previous run evidence.' }
    [IO.Directory]::CreateDirectory($run) | Out-Null
    if ($Stage -eq 'Windows') {
        $program = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
        $arguments = @('-batchmode','-nographics','-quit','-projectPath',$releaseProject,'-executeMethod','ShatteredSail.Editor.NavalDragRouteRelease.Build','-releaseOutput',$releaseExe,'-releaseVersion',$releaseVersion,'-releaseEvidence',$releaseEvidence,'-logFile',(Join-Path $run 'unity.log'))
        $timeout = 2400
    } else {
        Assert-Report
        $program = $releaseExe
        $arguments = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-shatteredSailSmokeTest',(Join-Path $run 'Report'),'-logFile',(Join-Path $run 'player.log'))
        $timeout = 180
    }
    $started = [DateTime]::UtcNow
    $process = Start-Process -FilePath $program -ArgumentList (Quote-Arguments $arguments) -WorkingDirectory $releaseProject -WindowStyle Hidden -PassThru
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
        $imageFiles = @(Get-ChildItem -LiteralPath (Join-Path $releaseProject 'Assets/_Project/Resources') -File -Recurse | Where-Object { $_.Extension -in @('.png','.jpg','.jpeg') })
        $missing = @(foreach ($file in $imageFiles) { $relative = $file.FullName.Substring($releaseProject.Length+1).Replace('\','/'); if (-not $assetLog.Contains($relative)) { $relative } })
        [pscustomobject]@{ Result=$(if ($missing.Count) {'FAIL'} else {'PASS'}); Images=$imageFiles.Count; Missing=$missing } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $run 'packaged-images.json') -Encoding utf8
        if ($missing.Count) { throw 'Resource images missing from actual build assets.' }
        Get-Content -LiteralPath (Join-Path $releaseWindows 'build-report.txt')
    } else {
        $smoke = [IO.File]::ReadAllText((Join-Path $run 'Report/player-smoke-report.txt'))
        if ($smoke -notmatch '^PASS\r?\n' -or $smoke -notmatch ('(?m)^Version: '+[regex]::Escape($releaseVersion)+'\r?$') -or $smoke -notmatch '(?m)^Company: ShatteredSail\r?$' -or $smoke -notmatch '(?m)^Product: Shattered Sail\r?$') { throw 'Smoke identity or result failed.' }
        Write-Output $smoke
    }
    exit 0
}
if ($Stage -eq 'Package') {
    Assert-Report
    $smoke = [IO.File]::ReadAllText((Join-Path $releaseEvidence 'Smoke/Report/player-smoke-report.txt'))
    if ($smoke -notmatch '^PASS\r?\n') { throw 'Actual player smoke must pass before packaging.' }
    & 'D:/Game/tools/ReleaseBuild/Package.ps1' -BuildDirectory $releaseWindows -ZipPath $releaseZip -ExecutableName '第七港口.exe' | Tee-Object -FilePath (Join-Path $releaseEvidence 'package.json')
    exit 0
}
if ($Stage -eq 'Seal') {
    Assert-Report
    $package = Get-Content -LiteralPath (Join-Path $releaseEvidence 'package.json') -Raw | ConvertFrom-Json
    $smoke = Get-Content -LiteralPath (Join-Path $releaseEvidence 'Smoke/result.json') -Raw | ConvertFrom-Json
    $images = Get-Content -LiteralPath (Join-Path $releaseEvidence 'Windows/packaged-images.json') -Raw | ConvertFrom-Json
    if ($package.Result -ne 'PASS' -or $smoke.ExitCode -ne 0 -or $images.Result -ne 'PASS' -or (Get-FileHash -LiteralPath $releaseZip).Hash -ne $package.ZipSHA256) { throw 'Release verification incomplete.' }
    [pscustomobject]@{ Result='PASS'; Version=$releaseVersion; Platform='Windows x86_64 / Mono'; Executable=$releaseExe; Package=$package; SmokePassed=$true; ImagesReferencedInBuildLog=$images.Images; Validation=$Validation; Evidence=$releaseEvidence } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $releaseRoot 'release-manifest.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $releaseRoot 'release-manifest.json')
}
