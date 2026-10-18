param([Parameter(Mandatory=$true)][ValidateSet('Snapshot','Windows','Mac','Smoke','PackageWindows','PackageMac','Seal')][string]$Stage)
$ErrorActionPreference='Stop'
$releaseSource=[IO.Path]::GetFullPath('D:/Game/UnityClient').TrimEnd('\','/')
$releaseProject=[IO.Path]::GetFullPath('D:/Game/tmp/runtime-memory-release-20261014').TrimEnd('\','/')
$releaseRoot='D:/Game/Builds/第七港口-探索与航行更新20261015'
$releaseEvidence='D:/Game/Artifacts/ExplorationRelease20261015'
$releaseVersion='0.3.20261015.3'
$releaseUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
$releaseWindows=Join-Path $releaseRoot 'Windows'
$releaseMac=Join-Path $releaseRoot 'Mac/第七港口.app'
$releaseExe=Join-Path $releaseWindows '第七港口.exe'

function Quote-ReleaseArguments($Values) {
    $quoted=foreach($value in $Values){$escaped=[regex]::Replace($value,'(\\*)"','$1$1\"');'"'+[regex]::Replace($escaped,'(\\+)$','$1$1')+'"'}
    return ($quoted -join ' ')
}
function Assert-ReleaseReport([string]$Path) {
    $report=Get-Content -LiteralPath $Path -Raw
    if($report -notmatch '(?m)^Result: Succeeded\r?$' -or $report -notmatch ('(?m)^Version: '+[regex]::Escape($releaseVersion)+'\r?$') -or $report -notmatch '(?m)^Errors: 0\r?$') {throw ('Invalid release report: '+$Path)}
}
function Assert-ReleaseEditorAvailable {
    # Unity Hub also runs a unity.exe service; it does not own the editor project.
    $editors=@(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -ieq 'Unity.exe' -and (($_.ExecutablePath -and $_.ExecutablePath -match '[\\/]Editor[\\/]Unity\.exe$') -or
            ($_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($releaseProject)))
    })
    if($editors.Count){throw 'Unity Editor is running; coordinate ownership before this release stage.'}
}
if($Stage -eq 'Snapshot') {
    if(Test-Path -LiteralPath $releaseEvidence){throw 'Use fresh evidence; preserve the previous snapshot.'}
    if(Test-Path -LiteralPath $releaseRoot){throw 'Use a fresh release directory.'}
    if(-not(Test-Path -LiteralPath (Join-Path $releaseProject '.runtime-memory-release-validation.json'))){throw 'Expected isolated release project.'}
    Assert-ReleaseEditorAvailable
    $lock=Join-Path $releaseProject 'Temp/UnityLockfile'
    if(Test-Path -LiteralPath $lock){$stream=[IO.File]::Open($lock,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None);$stream.Dispose()}
    [IO.Directory]::CreateDirectory($releaseEvidence)|Out-Null
    $files=@(foreach($part in @('Assets','Packages','ProjectSettings')){Get-ChildItem -LiteralPath (Join-Path $releaseSource $part) -File -Recurse | Where-Object {$_.FullName -notmatch '[\\/]UIPreviewsForMe(?:[\\/.]|$)'}})
    $manifest=@(foreach($file in $files){[pscustomobject]@{Path=$file.FullName.Substring($releaseSource.Length+1);Bytes=$file.Length;SHA256=(Get-FileHash -LiteralPath $file.FullName).Hash}})
    $manifest|Export-Csv -LiteralPath (Join-Path $releaseEvidence 'source-manifest.csv') -NoTypeInformation -Encoding utf8
    $paths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($file in $manifest){[void]$paths.Add($file.Path)}
    $removed=[Collections.Generic.List[string]]::new()
    foreach($part in @('Assets','Packages','ProjectSettings')) {
        $destination=Join-Path $releaseProject $part
        foreach($old in @(Get-ChildItem -LiteralPath $destination -File -Recurse)) {
            $relative=$old.FullName.Substring($releaseProject.Length+1)
            if($relative -match '[\\/]UIPreviewsForMe(?:[\\/.]|$)' -or $paths.Contains($relative)){continue}
            $target=[IO.Path]::GetFullPath($old.FullName)
            if(-not $target.StartsWith($releaseProject+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Stale file escaped the isolated project.'}
            Remove-Item -LiteralPath $target -Force
            $removed.Add($relative)
        }
        $copyArgs=@((Join-Path $releaseSource $part),$destination,'/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
        if($part -eq 'Assets'){$copyArgs+=@('/XD',(Join-Path $releaseSource 'Assets/_Project/Scenes/UIPreviewsForMe'),'/XF','UIPreviewsForMe.meta')}
        & robocopy @copyArgs | Out-Null
        if($LASTEXITCODE -ge 8){throw ('Source copy failed: '+$part)}
    }
    $removed|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $releaseEvidence 'removed-stale-cache-files.json') -Encoding utf8
    $mismatches=@(foreach($file in $manifest){$target=Join-Path $releaseProject $file.Path;if(-not(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne $file.SHA256){$file.Path}})
    if($mismatches.Count){$mismatches|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $releaseEvidence 'snapshot-mismatches.json');throw 'Snapshot mismatch.'}
    $invalidMeta=@(Get-ChildItem -LiteralPath (Join-Path $releaseProject 'Assets') -Filter '*.meta' -File -Recurse | Where-Object {[IO.File]::ReadAllText($_.FullName) -notmatch '(?m)^guid: [a-fA-F0-9]{32}\r?$'})
    if($invalidMeta.Count){$invalidMeta.FullName|Set-Content -LiteralPath (Join-Path $releaseEvidence 'invalid-metadata.txt');throw 'Invalid metadata found before build.'}
    $diagnostics=@(Get-ChildItem -LiteralPath (Join-Path $releaseProject 'Assets') -File -Recurse | Where-Object {$_.Name -match 'RuntimeMemoryAudit|StartupMemoryAudit|PlayerPerformanceAudit|MemoryAudit'})
    if($diagnostics.Count){throw 'Diagnostic source found in the release project.'}
    [pscustomobject]@{Result='PASS';Version=$releaseVersion;Source=$releaseSource;Project=$releaseProject;Files=$manifest.Count;Bytes=($manifest|Measure-Object Bytes -Sum).Sum;RetiredPreviewCopied=0;DiagnosticFiles=0;Utc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $releaseEvidence 'snapshot-result.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $releaseEvidence 'snapshot-result.json')
    exit 0
}
if(-not(Test-Path -LiteralPath (Join-Path $releaseEvidence 'snapshot-result.json'))){throw 'Create and verify the release snapshot first.'}
if($Stage -in @('Windows','Mac','Smoke')) {
    Assert-ReleaseEditorAvailable
    $output=Join-Path $releaseEvidence $Stage
    if(Test-Path -LiteralPath $output){throw 'Preserve earlier execution evidence.'}
    [IO.Directory]::CreateDirectory($output)|Out-Null
    if($Stage -eq 'Smoke') {
        Assert-ReleaseReport (Join-Path $releaseWindows 'build-report.txt')
        $program=$releaseExe
        $arguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-shatteredSailSmokeTest',(Join-Path $output 'Report'),'-logFile',(Join-Path $output 'player.log'))
    } else {
        $program=$releaseUnity
        $buildOutput=if($Stage -eq 'Windows'){$releaseExe}else{$releaseMac}
        $arguments=@('-batchmode','-nographics','-quit','-projectPath',$releaseProject,'-executeMethod',('ShatteredSail.Editor.'+$Stage+'ReleaseBuilder.BuildFromCommandLine'),'-releaseOutput',$buildOutput,'-logFile',(Join-Path $output 'unity.log'))
    }
    $started=[DateTime]::UtcNow
    $process=Start-Process -FilePath $program -ArgumentList (Quote-ReleaseArguments $arguments) -WindowStyle Hidden -PassThru
    $null=$process.Handle
    [pscustomobject]@{PID=$process.Id;Stage=$Stage;Program=$program;Arguments=$arguments;StartedUtc=$started.ToString('o')}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $output 'launch.json') -Encoding utf8
    Write-Output ($Stage+' process started, PID '+$process.Id)
    $timeoutSeconds=if($Stage -eq 'Smoke'){180}else{1800}
    while(-not $process.WaitForExit(15000)) {
        $process.Refresh()
        $elapsed=([DateTime]::UtcNow-$started).TotalSeconds
        $logPath=Join-Path $output $(if($Stage -eq 'Smoke'){'player.log'}else{'unity.log'})
        $tail=if(Test-Path -LiteralPath $logPath){[string](Get-Content -LiteralPath $logPath -Tail 1)}else{'Waiting for log.'}
        if($tail.Length -gt 240){$tail=$tail.Substring(0,240)}
        Write-Output ($Stage+' ['+[int]$elapsed+'s] CPU='+[int]$process.TotalProcessorTime.TotalSeconds+'s; '+$tail)
        if($elapsed -gt $timeoutSeconds){$process.Kill();$null=$process.WaitForExit(10000);throw ('Owned '+$Stage+' process timed out; evidence retained.')}
    }
    [pscustomobject]@{ExitCode=$process.ExitCode;Seconds=([DateTime]::UtcNow-$started).TotalSeconds;CompletedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
    if($process.ExitCode -ne 0){throw ($Stage+' failed; inspect its log.')}
    if($Stage -eq 'Smoke') {
        $report=Get-Content -LiteralPath (Join-Path $output 'Report/player-smoke-report.txt') -Raw
        if($report -notmatch '^PASS\r?\n' -or $report -notmatch ('(?m)^Version: '+[regex]::Escape($releaseVersion)+'\r?$')){throw 'Smoke result or version mismatch.'}
        Write-Output $report
    } else {Assert-ReleaseReport (Join-Path (Join-Path $releaseRoot $Stage) 'build-report.txt');Get-Content -LiteralPath (Join-Path (Join-Path $releaseRoot $Stage) 'build-report.txt')}
    exit 0
}
if($Stage -eq 'PackageWindows') {
    Assert-ReleaseReport (Join-Path $releaseWindows 'build-report.txt')
    & 'D:/Game/tools/ReleaseBuild/Package.ps1' -BuildDirectory $releaseWindows -ZipPath (Join-Path $releaseRoot '第七港口-Windows-20261015-探索航行更新.zip') -ExecutableName '第七港口.exe' | Tee-Object -FilePath (Join-Path $releaseEvidence 'windows-package.json')
    exit 0
}
if($Stage -eq 'PackageMac') {
    Assert-ReleaseReport (Join-Path $releaseRoot 'Mac/build-report.txt')
    & 'C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' 'D:/Game/tools/ReleaseBuild/PackageMac.py' --app-directory $releaseMac --zip-path (Join-Path $releaseRoot '第七港口-macOS-20261015-探索航行更新.zip') | Set-Content -LiteralPath (Join-Path $releaseEvidence 'mac-package.json') -Encoding utf8
    if($LASTEXITCODE -ne 0){throw 'macOS package failed.'}
    $package=Get-Content -LiteralPath (Join-Path $releaseEvidence 'mac-package.json') -Raw|ConvertFrom-Json
    if($package.result -ne 'PASS' -or $package.bundle.shortVersion -ne $releaseVersion){throw 'macOS bundle validation failed.'}
    Write-Output 'macOS Universal package validated.'
    exit 0
}
if($Stage -eq 'Seal') {
    Assert-ReleaseReport (Join-Path $releaseWindows 'build-report.txt')
    Assert-ReleaseReport (Join-Path $releaseRoot 'Mac/build-report.txt')
    $smokeResult=Get-Content -LiteralPath (Join-Path $releaseEvidence 'Smoke/result.json') -Raw|ConvertFrom-Json
    $smokeReport=Get-Content -LiteralPath (Join-Path $releaseEvidence 'Smoke/Report/player-smoke-report.txt') -Raw
    if($smokeResult.ExitCode -ne 0 -or $smokeReport -notmatch '^PASS\r?\n' -or $smokeReport -notmatch ('(?m)^Version: '+[regex]::Escape($releaseVersion)+'\r?$') -or $smokeReport -notmatch '(?m)^Company: ShatteredSail\r?$' -or $smokeReport -notmatch '(?m)^Product: Shattered Sail\r?$'){throw 'Windows smoke acceptance is incomplete.'}
    $windowsPackage=Get-Content -LiteralPath (Join-Path $releaseEvidence 'windows-package.json') -Raw|ConvertFrom-Json
    $macPackage=Get-Content -LiteralPath (Join-Path $releaseEvidence 'mac-package.json') -Raw|ConvertFrom-Json
    if($windowsPackage.Result -ne 'PASS' -or $macPackage.result -ne 'PASS' -or $macPackage.bundle.shortVersion -ne $releaseVersion){throw 'Package validation is incomplete.'}
    foreach($package in @(@{Path=$windowsPackage.Zip;SHA256=$windowsPackage.ZipSHA256;Name='第七港口-Windows-20261015-探索航行更新.zip'},@{Path=$macPackage.zipPath;SHA256=$macPackage.zipSHA256;Name='第七港口-macOS-20261015-探索航行更新.zip'})) {
        if([IO.Path]::GetFullPath($package.Path) -ne [IO.Path]::GetFullPath((Join-Path $releaseRoot $package.Name)) -or (Get-FileHash -LiteralPath $package.Path).Hash -ne $package.SHA256){throw 'Validated package identity or SHA256 mismatch.'}
    }
    $unexpected=@(Get-ChildItem -LiteralPath $releaseRoot -File -Recurse | Where-Object {$_.Name -match 'RuntimeMemoryAudit|StartupMemoryAudit|PlayerPerformanceAudit|MemoryAudit'})
    if($unexpected.Count){throw 'Diagnostic artifact found in release.'}
    $assetManifest=@(Import-Csv -LiteralPath (Join-Path $releaseEvidence 'source-manifest.csv')|Where-Object {$_.Path -like 'Assets*'})
    $assetPaths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($file in $assetManifest){[void]$assetPaths.Add($file.Path)}
    $changes=@(foreach($file in $assetManifest){$path=Join-Path $releaseProject $file.Path;if((Get-FileHash -LiteralPath $path).Hash -ne $file.SHA256){$file.Path}})
    $changes+=@(foreach($file in Get-ChildItem -LiteralPath (Join-Path $releaseProject 'Assets') -File -Recurse){$relative=$file.FullName.Substring($releaseProject.Length+1);if($relative -notmatch '[\\/]UIPreviewsForMe(?:[\\/.]|$)' -and -not $assetPaths.Contains($relative)){$relative}})
    $changes|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $releaseEvidence 'post-build-asset-changes.json') -Encoding utf8
    # Existing desktop importer preserves NPOT mipmaps by normalizing only
    # these authored overrides from BC7/HQ to RGBA32/uncompressed. Verify the
    # exact two-line change against the frozen source, never accept arbitrary drift.
    $normalizable=@('Assets\_Project\Resources\PortFacilities\DepartureCharts4K\chart-golden-trade.png.meta',
        'Assets\_Project\Resources\PortFacilities\DepartureCharts4K\chart-moon-ruins.png.meta',
        'Assets\_Project\Resources\PortFacilities\DepartureCharts4K\chart-shattered-reef.png.meta',
        'Assets\_Project\Resources\UISurfaces\journal_parchment_v1.png.meta',
        'Assets\_Project\Resources\UISurfaces\nautical_canvas_v1.png.meta')
    $normalized=@(foreach($relative in $changes) {
        if($relative -notin $normalizable){throw ('Unreviewed build asset change: '+$relative)}
        $sourcePath=Join-Path $releaseSource $relative
        $builtPath=Join-Path $releaseProject $relative
        $frozen=@($assetManifest|Where-Object Path -eq $relative)
        if($frozen.Count -ne 1 -or (Get-FileHash -LiteralPath $sourcePath).Hash -ne $frozen[0].SHA256){throw 'Original metadata no longer matches the frozen snapshot.'}
        $original=[IO.File]::ReadAllText($sourcePath).Replace("`r`n","`n")
        $imported=[IO.File]::ReadAllText($builtPath).Replace("`r`n","`n")
        $oldLines="    textureFormat: 25`n    textureCompression: 2"
        $newLines="    textureFormat: 4`n    textureCompression: 0"
        if([regex]::Matches($original,[regex]::Escape($oldLines)).Count -ne 1 -or $original.Replace($oldLines,$newLines) -cne $imported){throw ('Unexpected importer metadata change: '+$relative)}
        [pscustomobject]@{Path=$relative;BeforeSHA256=$frozen[0].SHA256;AfterSHA256=(Get-FileHash -LiteralPath $builtPath).Hash;Reason='Existing DesktopBackgroundCompressionPolicy NPOT mipmap fallback: BC7/HQ to RGBA32/uncompressed; source PNG and all other metadata unchanged.'}
    })
    $normalized|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $releaseEvidence 'verified-importer-normalizations.json') -Encoding utf8
    $packages=@(foreach($zip in Get-ChildItem -LiteralPath $releaseRoot -Filter '*.zip' -File){[pscustomobject]@{Name=$zip.Name;Bytes=$zip.Length;SHA256=(Get-FileHash -LiteralPath $zip.FullName).Hash}})
    if($packages.Count -ne 2){throw 'Two platform packages are required.'}
    [pscustomobject]@{Version=$releaseVersion;Result='PASS';SourceAssetsUnchanged=($normalized.Count -eq 0);GameplaySourcesUnchanged=$true;SourceArtUnchanged=$true;VerifiedImporterMetadataChanges=$normalized;DiagnosticFiles=0;WindowsSmoke=$smokeResult;MacExecution='Not run on macOS; Universal Mach-O, bundle and ZIP permissions validated on Windows.';Packages=$packages}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $releaseRoot 'release-manifest.json') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $releaseRoot 'release-manifest.json')
}
