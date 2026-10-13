param(
    [int]$Seeds = 4,
    [string]$Output = 'D:/Game/UnityClient/Artifacts/CardCrewIndependent20261012/BaselineAfter',
    [string]$CoreSource = '',
    [ValidateSet('Baseline','Pilot','Manifest','Search','Holdout','Sources','Trace','Endurance','EnduranceSources')][string]$Mode = 'Baseline',
    [int]$SeedOffset = 0,
    [string]$Selection = '',
    [string]$SchoolFilter = '',
    [int]$Shards = 8,
    [ValidateRange(1,2)][int]$MaxParallel = 2,
    [string]$MergeParts = '',
    [switch]$ManifestOnly,
    [switch]$CompileOnly,
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$dummyStarted = [DateTime]::UtcNow
$dummyClock = [Diagnostics.Stopwatch]::StartNew()
if ($Seeds -lt 1 -or $Shards -lt 1) { throw 'Seeds and Shards must be positive.' }
$dummyWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$Output = [IO.Path]::GetFullPath($Output)
foreach ($dummyOldAudit in @('CardDeckSupplyAudit20261012','CardBalanceReview20261012','CardSchoolRebalance20261012','CardDefenseReduction20261012')) {
    if ($Output -match ('[\\/]' + [regex]::Escape($dummyOldAudit) + '([\\/]|$)')) { throw 'Previous audit outputs are immutable; choose a new CardCrewIndependent20261012 output.' }
}
if ((Test-Path -LiteralPath (Join-Path $Output 'battles.csv')) -or (Test-Path -LiteralPath (Join-Path $Output 'streaks.csv'))) { throw 'Output already has persisted trials. Choose a new directory.' }
if ([string]::IsNullOrWhiteSpace($CoreSource)) { $CoreSource = Join-Path $dummyWorkspace 'UnityClient/Assets/_Project/Core/Runtime' }
if ($Mode -eq 'Holdout' -and ([string]::IsNullOrWhiteSpace($Selection) -or $SeedOffset -lt 1000)) { throw 'Holdout requires an existing selection and independent seed offset >=1000.' }
if ($ManifestOnly) { $Mode = 'Manifest' }
$dummyBuild = Join-Path $dummyWorkspace ('tmp/card-dummy-four-supplies-' + [Guid]::NewGuid().ToString('N'))
$dummyCoreSnapshot = Join-Path $dummyBuild 'Core'
New-Item -ItemType Directory -Force -Path $dummyCoreSnapshot | Out-Null
$dummyCoreFiles = @(Get-ChildItem -LiteralPath $CoreSource -Filter '*.cs')
if ($dummyCoreFiles.Count -eq 0) { throw 'CoreSource has no C# files.' }
foreach ($dummyFile in $dummyCoreFiles) { Copy-Item -LiteralPath $dummyFile.FullName -Destination $dummyCoreSnapshot }
$dummyToolSnapshot = Join-Path $dummyBuild 'DummyDeckBenchmark.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DummyDeckBenchmark.cs') -Destination $dummyToolSnapshot
$dummyEnduranceSnapshot = Join-Path $dummyBuild 'DummyDeckEndurance.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DummyDeckEndurance.cs') -Destination $dummyEnduranceSnapshot
$dummySelectionSnapshot = ''
if (-not [string]::IsNullOrWhiteSpace($Selection)) { $dummySelectionSnapshot = Join-Path $dummyBuild 'selection.tsv'; Copy-Item -LiteralPath $Selection -Destination $dummySelectionSnapshot }
$dummyExe = Join-Path $dummyBuild 'DummyDeckBenchmark.exe'
$dummyCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$dummyDotnet = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$dummyMono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$dummyFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$dummyArguments = @($dummyCompiler,'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$dummyExe")
foreach ($dummyAssembly in @('mscorlib.dll','System.dll','System.Core.dll')) { $dummyArguments += '-r:' + (Join-Path $dummyFramework $dummyAssembly) }
$dummyArguments += @(Get-ChildItem -LiteralPath $dummyCoreSnapshot -Filter '*.cs' | ForEach-Object FullName)
$dummyArguments += $dummyToolSnapshot
$dummyArguments += $dummyEnduranceSnapshot
& $dummyDotnet @dummyArguments
if ($LASTEXITCODE -ne 0) { throw 'Dummy benchmark compilation failed.' }
New-Item -ItemType Directory -Force -Path $Output | Out-Null
Get-ChildItem -LiteralPath $dummyCoreSnapshot -Filter '*.cs' | Sort-Object Name | ForEach-Object { "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $($_.Name)" } | Set-Content -LiteralPath (Join-Path $Output 'core-source-sha256.txt') -Encoding UTF8
@("$((Get-FileHash -LiteralPath $dummyToolSnapshot -Algorithm SHA256).Hash)  DummyDeckBenchmark.cs", "$((Get-FileHash -LiteralPath $dummyEnduranceSnapshot -Algorithm SHA256).Hash)  DummyDeckEndurance.cs", "$((Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash)  RunDummyDeckBenchmark.ps1") | Set-Content -LiteralPath (Join-Path $Output 'benchmark-source-sha256.txt') -Encoding UTF8
if ($dummySelectionSnapshot) { "$((Get-FileHash -LiteralPath $dummySelectionSnapshot -Algorithm SHA256).Hash)  selection.tsv" | Set-Content -LiteralPath (Join-Path $Output 'selection-sha256.txt') -Encoding UTF8 }
if ($CompileOnly) { Write-Output ('Compiled frozen executable: ' + $dummyExe); exit 0 }

function ConvertTo-DummyProcessArgument([string]$value) {
    # Win32 argv quoting. No shell parses these arguments.
    return '"' + [regex]::Replace([regex]::Replace($value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}

$dummyMode = $Mode.ToLowerInvariant()
$dummyResultName = if ($Mode -eq 'Endurance' -or $Mode -eq 'EnduranceSources') { 'streaks.csv' } else { 'battles.csv' }
if ($Mode -eq 'Manifest' -or $Mode -eq 'Trace') {
    & $dummyMono $dummyExe $Seeds $Output $dummyMode $SeedOffset $dummySelectionSnapshot
    if ($LASTEXITCODE -ne 0) { throw 'Manifest/trace validation failed.' }
} else {
    $dummyParts = if ($MergeParts) { [IO.Path]::GetFullPath($MergeParts) } else { Join-Path $Output 'parts' }
    if (-not $MergeParts) {
        New-Item -ItemType Directory -Force -Path $dummyParts | Out-Null
        $dummyPending = 0; $dummyRunning = @()
        try {
            while ($dummyPending -lt $Shards -or $dummyRunning.Count -gt 0) {
                while ($dummyPending -lt $Shards -and $dummyRunning.Count -lt $MaxParallel) {
                    $dummyPart = Join-Path $dummyParts ('part-' + $dummyPending.ToString('D3'))
                    if (Test-Path -LiteralPath $dummyPart) { throw 'A shard directory already exists; choose a new output or explicitly merge completed parts.' }
                    New-Item -ItemType Directory -Path $dummyPart | Out-Null
                    foreach ($dummyHashFile in @('core-source-sha256.txt','benchmark-source-sha256.txt')) { Copy-Item -LiteralPath (Join-Path $Output $dummyHashFile) -Destination $dummyPart }
                    $dummyArgv = @($dummyExe,[string]$Seeds,$dummyPart,$dummyMode,[string]$SeedOffset,$dummySelectionSnapshot,[string]$dummyPending,[string]$Shards,$SchoolFilter)
                    $dummyArgumentLine = ($dummyArgv | ForEach-Object { ConvertTo-DummyProcessArgument $_ }) -join ' '
                    $dummyProcess = Start-Process -FilePath $dummyMono -ArgumentList $dummyArgumentLine -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $dummyPart 'stdout.log') -RedirectStandardError (Join-Path $dummyPart 'stderr.log')
                    $dummyRunning += [pscustomobject]@{Process=$dummyProcess;Directory=$dummyPart;Index=$dummyPending;Watch=[Diagnostics.Stopwatch]::StartNew()}
                    $dummyPending++
                }
                foreach ($dummyJob in @($dummyRunning)) {
                    if (-not $dummyJob.Process.HasExited) { continue }
                    $dummyJob.Process.WaitForExit()
                    if ($dummyJob.Process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $dummyJob.Directory $dummyResultName))) { throw ('Shard failed: ' + $dummyJob.Directory + '; inspect stderr.log. No partial in-memory trials count.') }
                    $dummyJob.Watch.Stop()
                    @{status='completed';mode=$dummyMode;seeds=$Seeds;seedOffset=$SeedOffset;schoolFilter=$SchoolFilter;shardIndex=$dummyJob.Index;shardCount=$Shards;exe=$dummyExe;elapsedSeconds=$dummyJob.Watch.Elapsed.TotalSeconds} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dummyJob.Directory 'run-metadata.json') -Encoding UTF8
                    'Complete; original frozen process exited successfully and persisted all CSVs.' | Set-Content -LiteralPath (Join-Path $dummyJob.Directory 'completed.txt') -Encoding UTF8
                    Write-Output ('Completed shard '+$dummyJob.Index+'/'+$Shards+' in '+$dummyJob.Watch.Elapsed.TotalSeconds+' s')
                    $dummyRunning = @($dummyRunning | Where-Object { $_.Index -ne $dummyJob.Index })
                }
                if ($dummyRunning.Count -gt 0) { Start-Sleep -Milliseconds 250 }
            }
        } finally {
            foreach ($dummyJob in $dummyRunning) { if (-not $dummyJob.Process.HasExited) { Stop-Process -Id $dummyJob.Process.Id -ErrorAction SilentlyContinue } }
        }
    }
    $dummyFoundParts = @(Get-ChildItem -LiteralPath $dummyParts -Directory)
    if ($dummyFoundParts.Count -ne $Shards) { throw 'Missing or extra shards.' }
    $dummyIndices = @()
    foreach ($dummyPart in $dummyFoundParts) {
        $dummyMeta = Get-Content -LiteralPath (Join-Path $dummyPart.FullName 'run-metadata.json') -Raw | ConvertFrom-Json
        if ($dummyMeta.status -ne 'completed' -or $dummyMeta.shardCount -ne $Shards -or $dummyMeta.seeds -ne $Seeds -or $dummyMeta.seedOffset -ne $SeedOffset -or $dummyMeta.mode -ne $dummyMode -or $dummyMeta.schoolFilter -ne $SchoolFilter) { throw 'Inconsistent shard metadata.' }
        $dummyIndices += [int]$dummyMeta.shardIndex
        foreach ($dummyHashFile in @('core-source-sha256.txt','benchmark-source-sha256.txt')) { if ((Get-Content -LiteralPath (Join-Path $Output $dummyHashFile) -Raw) -ne (Get-Content -LiteralPath (Join-Path $dummyPart.FullName $dummyHashFile) -Raw)) { throw 'Merge compiler/runner differs from the frozen shards.' } }
    }
    if (($dummyIndices|Sort-Object -Unique).Count -ne $Shards -or ($dummyIndices|Measure-Object -Minimum).Minimum -ne 0 -or ($dummyIndices|Measure-Object -Maximum).Maximum -ne $Shards-1) { throw 'Incomplete shard index set.' }
    & $dummyMono $dummyExe $Seeds $Output ('merge-'+$dummyMode) $SeedOffset $dummySelectionSnapshot $dummyParts $SchoolFilter
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $Output 'merge-checks.txt'))) { throw 'Shard merge/replay validation failed.' }
}
$dummyClock.Stop()
@{status='completed';mode=$dummyMode;seeds=$Seeds;seedOffset=$SeedOffset;schoolFilter=$SchoolFilter;shards=$Shards;maxParallel=$MaxParallel;coreSource=$CoreSource;coreSnapshot=$dummyCoreSnapshot;toolSource=$dummyToolSnapshot;selection=$dummySelectionSnapshot;exe=$dummyExe;startedUtc=$dummyStarted.ToString('o');completedUtc=[DateTime]::UtcNow.ToString('o');elapsedSeconds=$dummyClock.Elapsed.TotalSeconds} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'run-metadata.json') -Encoding UTF8
