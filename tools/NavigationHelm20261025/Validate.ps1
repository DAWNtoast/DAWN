param(
    [string]$RunName = 'run1',
    [Alias('Modes')][ValidateSet('EditMode','PlayMode')][string[]]$TestPlatform = @('PlayMode'),
    [string]$PlayFilter = 'SpeedBasedVoyage_;NavigationInputProtection_',
    [string]$EditFilter = 'NavigationDashboardTests',
    [string]$ProjectPath = 'tmp/window-fit-20261025',
    [string]$ValidationCompany = 'CodexWindowFitValidation',
    [string]$ValidationProduct = 'WindowFit20261025',
    [switch]$AllowColdImport,
    [switch]$ReportForecastTextMetrics,
    [switch]$PreserveGeneratedCache
)
$ErrorActionPreference = 'Stop'
$helmWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$helmMain = Join-Path $helmWorkspace 'UnityClient/Assets/_Project'
$helmProject = [IO.Path]::GetFullPath((Join-Path $helmWorkspace $ProjectPath)).TrimEnd('\','/')
$helmTemporaryRoot = [IO.Path]::GetFullPath((Join-Path $helmWorkspace 'tmp')).TrimEnd('\','/')
if (!$helmProject.StartsWith($helmTemporaryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation must use an existing project strictly inside the workspace tmp directory; UnityClient is forbidden.'
}
$helmOutput = Join-Path $helmWorkspace ('Artifacts/NavigationHelm20261025/' + $RunName)
$helmUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Use a simple run name.' }
if (Test-Path -LiteralPath $helmOutput) { throw 'Use a new run name to preserve evidence.' }
$helmHadLibrary = Test-Path -LiteralPath (Join-Path $helmProject 'Library')
if (!$helmHadLibrary -and !$AllowColdImport) { throw 'The shared warm validation project is required unless cold import is explicitly enabled.' }

function Assert-HelmProjectIdle([switch]$AfterOwnedProcess) {
    if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($helmProject.Replace('/','\'))
    }).Count) { throw 'The shared validation project is in use.' }
    $helmUnityLock = Join-Path $helmProject 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $helmUnityLock) {
        if (!$AfterOwnedProcess -or $null -eq $helmProcess -or !$helmProcess.HasExited) {
            throw 'The shared validation project has a lockfile.'
        }
        # Compilation failure can leave Unity's zero-byte lock after its own process exits.
        # Require exclusive access before removing only that owned stale lock.
        Assert-HelmTemporaryPath $helmUnityLock
        $helmStaleLock = [IO.File]::Open($helmUnityLock, 'Open', 'ReadWrite', 'None')
        $helmStaleLock.Dispose()
        Remove-Item -LiteralPath $helmUnityLock -Force
    }
}

function Assert-HelmTemporaryPath([string]$Path) {
    $helmAbsolute = [IO.Path]::GetFullPath($Path)
    if (!$helmAbsolute.StartsWith($helmProject + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw ('Refusing path outside the shared validation project: ' + $helmAbsolute)
    }
    for ($helmCheck = $helmAbsolute; $helmCheck -ne $helmProject; $helmCheck = Split-Path -Parent $helmCheck) {
        if ((Test-Path -LiteralPath $helmCheck) -and ((Get-Item -LiteralPath $helmCheck).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Refusing linked validation path: ' + $helmCheck)
        }
    }
}

Assert-HelmProjectIdle
foreach ($helmPath in @($helmTemporaryRoot, $helmProject, (Join-Path $helmProject 'Assets'), (Join-Path $helmProject 'Assets/_Project'))) {
    if ((Get-Item -LiteralPath $helmPath).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Validation path must not be a directory link.' }
}
$helmSettings = [IO.File]::ReadAllText((Join-Path $helmProject 'ProjectSettings/ProjectSettings.asset'))
if ($ValidationCompany -eq 'ShatteredSail' -or $ValidationProduct -eq 'Shattered Sail' -or
    $helmSettings -notmatch ('(?m)^\s+companyName: ' + [regex]::Escape($ValidationCompany) + '\s*$') -or
    $helmSettings -notmatch ('(?m)^\s+productName: ' + [regex]::Escape($ValidationProduct) + '\s*$')) {
    throw 'Validation must use separate PlayerPrefs.'
}
# Share the lease name with the existing supply validator, rather than allowing two validators to race.
$helmLeasePath = Join-Path $helmProject 'BasicSupplyLayout20261025.lock'
$helmLease = [IO.File]::Open($helmLeasePath, 'OpenOrCreate', 'ReadWrite', 'None')
$helmChanges = [Collections.Generic.List[object]]::new()
$helmSourceHashes = [Collections.Generic.List[object]]::new()
$helmFreedBytes = 0L
$helmProcess = $null
$helmFailures = [Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Path $helmOutput | Out-Null
try {
    Assert-HelmProjectIdle
    foreach ($helmPart in @('Core/Runtime','Content/Runtime','Persistence/Runtime','Presentation/Runtime','Tests/PlayMode','Tests/EditMode')) {
        foreach ($helmSource in Get-ChildItem -LiteralPath (Join-Path $helmMain $helmPart) -Filter '*.cs' -File -Recurse) {
            foreach ($helmFile in @($helmSource.FullName, ($helmSource.FullName + '.meta'))) {
                if (!(Test-Path -LiteralPath $helmFile)) { continue }
                $helmRelative = $helmFile.Substring($helmMain.Length + 1)
                $helmTarget = Join-Path $helmProject ('Assets/_Project/' + $helmRelative)
                Assert-HelmTemporaryPath $helmTarget
                $helmHash = (Get-FileHash -LiteralPath $helmFile).Hash
                $helmSourceHashes.Add([pscustomobject]@{ Path=$helmFile; SHA256=$helmHash })
                $helmBackup = $null
                if (Test-Path -LiteralPath $helmTarget) {
                    if ((Get-FileHash -LiteralPath $helmTarget).Hash -eq $helmHash) { continue }
                    $helmBackup = Join-Path $helmOutput ('OriginalValidationFiles/' + $helmRelative)
                    New-Item -ItemType Directory -Force -Path (Split-Path $helmBackup -Parent) | Out-Null
                    Copy-Item -LiteralPath $helmTarget -Destination $helmBackup
                }
                $helmChanges.Add([pscustomobject]@{ Target=$helmTarget; Backup=$helmBackup })
                $helmChanges | Export-Csv -LiteralPath (Join-Path $helmOutput 'validation-file-changes.csv') -NoTypeInformation -Encoding utf8
                Copy-Item -LiteralPath $helmFile -Destination $helmTarget -Force
            }
        }
    }
    $helmSourceHashes | Export-Csv -LiteralPath (Join-Path $helmOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
    if ($ReportForecastTextMetrics) {
        $helmMetricsTarget = Join-Path $helmProject 'Assets/_Project/Tests/EditMode/NavigationDashboardTests.cs'
        Assert-HelmTemporaryPath $helmMetricsTarget
        if (!@($helmChanges | Where-Object Target -eq $helmMetricsTarget).Count) {
            $helmMetricsBackup = Join-Path $helmOutput 'OriginalValidationFiles/Tests/EditMode/NavigationDashboardTests.cs'
            New-Item -ItemType Directory -Force -Path (Split-Path $helmMetricsBackup -Parent) | Out-Null
            Copy-Item -LiteralPath $helmMetricsTarget -Destination $helmMetricsBackup
            $helmChanges.Add([pscustomobject]@{ Target=$helmMetricsTarget; Backup=$helmMetricsBackup })
            $helmChanges | Export-Csv -LiteralPath (Join-Path $helmOutput 'validation-file-changes.csv') -NoTypeInformation -Encoding utf8
        }
        $helmMetricsSource = [IO.File]::ReadAllText($helmMetricsTarget)
        $helmMetricsMethod = 'public void CompactNavigation_KeepsControlsLegibleAndGivesMostSpaceToScenery'
        $helmMetricsStart = $helmMetricsSource.IndexOf($helmMetricsMethod, [StringComparison]::Ordinal)
        $helmMetricsAnchor = '                viewport.ResizeAndRender(width,height);'
        $helmMetricsInsert = $helmMetricsSource.IndexOf($helmMetricsAnchor, $helmMetricsStart, [StringComparison]::Ordinal)
        if ($helmMetricsStart -lt 0 -or $helmMetricsInsert -lt 0) { throw 'Expected existing compact navigation metric point was not found.' }
        $helmMetricsInsert += $helmMetricsAnchor.Length
        $helmMetricsCode = @'

                foreach (string metricName in new[] { "Forecast Time Label", "Forecast Time Value", "Forecast Speed Label", "Forecast Speed Value",
                    "Forecast Water Label", "Forecast Water Value", "Forecast Food Label", "Forecast Food Value" })
                {
                    Text metricText = Find<Text>(metricName);
                    Debug.Log("NAV_HELM_TEXT_METRIC " + width + "x" + height + " | " + metricName + " | font=" + metricText.fontSize +
                        " | boxHeight=" + metricText.rectTransform.rect.height + " | preferredHeight=" + metricText.preferredHeight +
                        " | visible=" + metricText.cachedTextGenerator.characterCountVisible + " | length=" + metricText.text.Length + " | text=" + metricText.text);
                }
'@
        [IO.File]::WriteAllText($helmMetricsTarget, $helmMetricsSource.Insert($helmMetricsInsert, $helmMetricsCode), [Text.UTF8Encoding]::new($false))
        'Only the validation copy of the existing CompactNavigation test logs forecast text metrics. The original file is restored in finally; no production test was added.' |
            Set-Content -LiteralPath (Join-Path $helmOutput 'validation-instrumentation.txt') -Encoding utf8
    }
    foreach ($helmPlatform in $TestPlatform) {
        Assert-HelmProjectIdle -AfterOwnedProcess
        $helmResult = Join-Path $helmOutput ($helmPlatform + '.xml')
        $helmFilter = if ($helmPlatform -eq 'EditMode') { $EditFilter } else { $PlayFilter }
        $helmArgs = @('-batchmode','-force-d3d11','-projectPath',$helmProject,'-runTests','-testPlatform',$helmPlatform,
            '-testFilter',$helmFilter,'-testResults',$helmResult,'-logFile',(Join-Path $helmOutput ($helmPlatform + '.log')))
        if ($helmPlatform -eq 'PlayMode') { $helmArgs += @('-navalVisualCapturePath',(Join-Path $helmOutput 'Screens')) }
        $helmProcess = Start-Process -FilePath $helmUnity -ArgumentList ($helmArgs | ForEach-Object { '"' + $_ + '"' }) -WindowStyle Hidden -PassThru
        Write-Output ($helmPlatform + ' running, PID ' + $helmProcess.Id)
        $helmProcess.WaitForExit()
        $helmProcess.Refresh()
        if (!(Test-Path -LiteralPath $helmResult)) { throw ($helmPlatform + ' produced no results; inspect log.') }
        [xml]$helmResults = Get-Content -Raw -LiteralPath $helmResult
        $helmRun = $helmResults.'test-run'
        Write-Output ($helmPlatform + ': ' + $helmRun.result + ', passed=' + $helmRun.passed + ', failed=' + $helmRun.failed)
        if ($helmProcess.ExitCode -ne 0 -or $helmRun.result -ne 'Passed' -or [int]$helmRun.passed -lt 1) { $helmFailures.Add($helmPlatform) }
    }
    if ($helmFailures.Count) { throw ('Failed test platforms: ' + ($helmFailures -join ', ')) }
} finally {
    Assert-HelmProjectIdle -AfterOwnedProcess
    foreach ($helmChange in $helmChanges) {
        Assert-HelmTemporaryPath $helmChange.Target
        if ($null -ne $helmChange.Backup) {
            Copy-Item -LiteralPath $helmChange.Backup -Destination $helmChange.Target -Force
            if ((Get-FileHash -LiteralPath $helmChange.Backup).Hash -ne (Get-FileHash -LiteralPath $helmChange.Target).Hash) { throw 'Shared source restoration failed.' }
        } elseif (Test-Path -LiteralPath $helmChange.Target) {
            $helmFreedBytes += (Get-Item -LiteralPath $helmChange.Target).Length
            Remove-Item -LiteralPath $helmChange.Target -Force
            if (Test-Path -LiteralPath $helmChange.Target) { throw 'Temporary source removal failed.' }
        }
    }
    $helmTemp = Join-Path $helmProject 'Temp'
    if (Test-Path -LiteralPath $helmTemp) {
        Assert-HelmTemporaryPath $helmTemp
        $helmTempItems = @(Get-ChildItem -LiteralPath $helmTemp -Force -Recurse)
        if (@($helmTempItems | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) {
            throw 'Refusing linked temporary cache.'
        }
        $helmFreedBytes += ($helmTempItems | Where-Object { !$_.PSIsContainer } | Measure-Object Length -Sum).Sum
        Remove-Item -LiteralPath $helmTemp -Recurse -Force
        if (Test-Path -LiteralPath $helmTemp) { throw 'Temporary cache removal failed.' }
    }
    if (!$helmHadLibrary -and !$PreserveGeneratedCache) {
        $helmCreatedLibrary = Join-Path $helmProject 'Library'
        if (Test-Path -LiteralPath $helmCreatedLibrary) {
            Assert-HelmTemporaryPath $helmCreatedLibrary
            $helmLibraryItems = @(Get-ChildItem -LiteralPath $helmCreatedLibrary -Force -Recurse)
            if (@($helmLibraryItems | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) {
                throw 'Refusing linked generated Library cache.'
            }
            $helmFreedBytes += ($helmLibraryItems | Where-Object { !$_.PSIsContainer } | Measure-Object Length -Sum).Sum
            Remove-Item -LiteralPath $helmCreatedLibrary -Recurse -Force
            if (Test-Path -LiteralPath $helmCreatedLibrary) { throw 'Generated Library cache removal failed.' }
        }
    }
    if (!$helmHadLibrary -and $PreserveGeneratedCache) {
        [pscustomobject]@{ Project=$helmProject; CreatedFor=$helmOutput; Reason='Awaiting review of this task screenshots and text metrics before final cleanup.' } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $helmOutput 'retained-generated-cache.json') -Encoding utf8
    }
    $helmLease.Dispose()
    Assert-HelmTemporaryPath $helmLeasePath
    Remove-Item -LiteralPath $helmLeasePath -Force
    $helmChangedMain = @($helmSourceHashes | Where-Object { (Get-FileHash -LiteralPath $_.Path).Hash -ne $_.SHA256 })
    $helmChangedMain | Export-Csv -LiteralPath (Join-Path $helmOutput 'main-source-drift.csv') -NoTypeInformation -Encoding utf8
    $helmTempRemaining = Test-Path -LiteralPath (Join-Path $helmProject 'Temp')
    @('Shared validation sources restored; task-only source and meta copies removed.',
        ('Existing project Assets reused; no full project copy. Pre-existing Library retained: ' + $helmHadLibrary),
        ('Task-generated Library removed: ' + (!$helmHadLibrary -and !(Test-Path -LiteralPath (Join-Path $helmProject 'Library')))),
        ('Production sources changed during validation/cleanup: ' + $helmChangedMain.Count),
        ('Temp directory remaining after Unity exit: ' + $helmTempRemaining),
        ('Temporary source/cache bytes reclaimed: ' + $helmFreedBytes),
        'Task lease removed; test results, screenshots and original source evidence retained.') |
        Set-Content -LiteralPath (Join-Path $helmOutput 'cleanup.txt') -Encoding utf8
    if ($helmChangedMain.Count) { Write-Warning 'Production sources changed during validation; inspect main-source-drift.csv.' }
}
