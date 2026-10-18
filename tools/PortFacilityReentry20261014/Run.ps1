param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$ProjectPath = 'D:\Game\tmp\port-facility-reentry-20261014',
    [string]$TestFilter = '',
    [string]$WarmLibraryPath = ''
)
$ErrorActionPreference = 'Stop'
$reentrySource = 'D:\Game\UnityClient'
$reentryProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\')
$reentryRoot = 'D:\Game\Artifacts\PortFacilityReentry20261014\validation'
$reentryUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$reentryIdentity = 'PortFacilityReentryValidation20261014'
$reentryMarker = Join-Path $reentryProject 'PortFacilityReentry20261014.lock'
if (-not $reentryProject.StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use an isolated project below D:\Game\tmp.' }
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if (-not (Test-Path -LiteralPath $reentryUnity)) { throw 'The required Unity editor is unavailable.' }
if (($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') -and [string]::IsNullOrWhiteSpace($TestFilter)) { throw 'Pass an explicit TestFilter for this verification run.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($reentryProject, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count) { throw 'Isolated project is already running.' }
if (-not (Test-Path -LiteralPath $reentryProject)) {
    if ($Stage -ne 'Prepare') { throw 'Run Prepare before launching verification.' }
    [IO.Directory]::CreateDirectory($reentryProject) | Out-Null
} elseif (-not (Test-Path -LiteralPath $reentryMarker)) {
    if (@(Get-ChildItem -LiteralPath $reentryProject -Force).Count) { throw 'Choose an empty directory or this task''s previously prepared project.' }
}
$reentryUnityLock = Join-Path $reentryProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $reentryUnityLock) {
    try { $reentryCheck = [IO.File]::Open($reentryUnityLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $reentryCheck.Dispose() }
    catch { throw 'The isolated Unity project is already open.' }
}
$reentryLease = $null
try {
    $reentryLease = [IO.File]::Open($reentryMarker, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $reentryRetired = Join-Path $reentryProject 'Assets\_Project\Scenes\UIPreviewsForMe'
    $reentrySettingsPath = Join-Path $reentryProject 'ProjectSettings\ProjectSettings.asset'
    if (Test-Path -LiteralPath $reentryRetired) { throw 'Choose an isolated project without retired preview assets.' }
    if ($Stage -eq 'Prepare') {
        foreach ($reentryPart in @('Assets','Packages','ProjectSettings')) {
            $reentryCopy = @((Join-Path $reentrySource $reentryPart), (Join-Path $reentryProject $reentryPart), '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
            if ($reentryPart -eq 'Assets') {
                $reentryCopy += @('/XD', (Join-Path $reentrySource 'Assets\_Project\Scenes\UIPreviewsForMe'), '/XF', 'UIPreviewsForMe.meta')
            }
            & robocopy @reentryCopy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $reentryPart) }
        }
        $reentrySettings = [IO.File]::ReadAllText($reentrySettingsPath)
        $reentrySettings = [regex]::Replace($reentrySettings, '(?m)^  companyName:.*$', ('  companyName: ' + $reentryIdentity))
        $reentrySettings = [regex]::Replace($reentrySettings, '(?m)^  productName:.*$', ('  productName: ' + $reentryIdentity))
        [IO.File]::WriteAllText($reentrySettingsPath, $reentrySettings, [Text.UTF8Encoding]::new($false))
        # An optional seed avoids re-importing art, but never copies build outputs or user state.
        if ($WarmLibraryPath -and -not (Test-Path -LiteralPath (Join-Path $reentryProject 'Library'))) {
            $reentryWarmLibrary = [IO.Path]::GetFullPath($WarmLibraryPath).TrimEnd('\')
            $reentryWarmProject = Split-Path -Parent $reentryWarmLibrary
            if (-not $reentryWarmLibrary.StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $reentryWarmLibrary) -ne 'Library') { throw 'WarmLibraryPath must identify a Library in an isolated project below D:\Game\tmp.' }
            if (-not (Test-Path -LiteralPath $reentryWarmLibrary -PathType Container)) { throw 'Warm Library does not exist.' }
            if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($reentryWarmProject, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count) { throw 'Warm Library belongs to a running Unity editor.' }
            $reentryWarmLock = Join-Path $reentryWarmProject 'Temp\UnityLockfile'
            if (Test-Path -LiteralPath $reentryWarmLock) {
                try { $reentryCheck = [IO.File]::Open($reentryWarmLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $reentryCheck.Dispose() }
                catch { throw 'Warm Library is in use.' }
            }
            $reentryWarmParts = @('Artifacts','PackageCache','ShaderCache')
            $reentryWarmFiles = @('ArtifactDB','SourceAssetDB','ShaderCache.db')
            $reentryWarmBytes = 0L
            foreach ($reentryPart in $reentryWarmParts) {
                $reentryPartPath = Join-Path $reentryWarmLibrary $reentryPart
                if (Test-Path -LiteralPath $reentryPartPath) { $reentryWarmBytes += [long](Get-ChildItem -LiteralPath $reentryPartPath -File -Recurse | Measure-Object -Property Length -Sum).Sum }
            }
            foreach ($reentryFile in $reentryWarmFiles) {
                $reentryFilePath = Join-Path $reentryWarmLibrary $reentryFile
                if (Test-Path -LiteralPath $reentryFilePath) { $reentryWarmBytes += (Get-Item -LiteralPath $reentryFilePath).Length }
            }
            if ($reentryWarmBytes -gt 8GB) { throw 'Warm Library exceeds the 8 GiB seed limit; omit WarmLibraryPath for a fresh import.' }
            if (([IO.DriveInfo]::new([IO.Path]::GetPathRoot($reentryProject))).AvailableFreeSpace -lt ($reentryWarmBytes + 5GB)) { throw 'Insufficient free space for optional cache seed.' }
            $reentryLibrary = Join-Path $reentryProject 'Library'
            [IO.Directory]::CreateDirectory($reentryLibrary) | Out-Null
            foreach ($reentryPart in $reentryWarmParts) {
                $reentryPartPath = Join-Path $reentryWarmLibrary $reentryPart
                if (-not (Test-Path -LiteralPath $reentryPartPath)) { continue }
                & robocopy $reentryPartPath (Join-Path $reentryLibrary $reentryPart) /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
                if ($LASTEXITCODE -ge 8) { throw ('Cache copy failed: ' + $reentryPart) }
            }
            foreach ($reentryFile in $reentryWarmFiles) {
                $reentryFilePath = Join-Path $reentryWarmLibrary $reentryFile
                if (Test-Path -LiteralPath $reentryFilePath) { Copy-Item -LiteralPath $reentryFilePath -Destination (Join-Path $reentryLibrary $reentryFile) }
            }
            Write-Output ('Seeded ' + [Math]::Round($reentryWarmBytes / 1GB, 2) + ' GiB of import/package cache.')
        }
        if (Test-Path -LiteralPath $reentryRetired) { throw 'Retired preview assets must remain absent.' }
        Write-Output ('Prepared current source with separate PlayerPrefs identity: ' + $reentryProject + '. No scenes generated.')
        exit 0
    }
    $reentrySettings = [IO.File]::ReadAllText($reentrySettingsPath)
    foreach ($reentrySetting in @('companyName','productName')) {
        if ($reentrySettings -notmatch ('(?m)^  ' + $reentrySetting + ': ' + [regex]::Escape($reentryIdentity) + '\s*$')) { throw 'Run Prepare to isolate PlayerPrefs before launching Unity.' }
    }
    $reentryOutput = Join-Path $reentryRoot $RunName
    if (Test-Path -LiteralPath $reentryOutput) { throw 'Use a fresh RunName to preserve existing output.' }
    [IO.Directory]::CreateDirectory($reentryOutput) | Out-Null
    $reentryArgs = @('-batchmode','-force-d3d11','-projectPath',$reentryProject,'-logFile',(Join-Path $reentryOutput ($Stage + '.log')))
    if ($Stage -eq 'Compile') {
        $reentryArgs += '-quit'
    } else {
        $reentryArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $reentryOutput 'results.xml'))
    }
    $reentryQuoted = $reentryArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $reentryProcess = Start-Process -FilePath $reentryUnity -WindowStyle Hidden -ArgumentList $reentryQuoted -PassThru
    [pscustomobject]@{ Stage=$Stage; ProcessId=$reentryProcess.Id; Project=$reentryProject; Arguments=$reentryArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $reentryOutput 'launch.json') -Encoding utf8
    Write-Output ('Unity ' + $Stage + ' PID ' + $reentryProcess.Id + '; output=' + $reentryOutput)
    $reentryProcess.WaitForExit()
    $reentryProcess.Refresh()
    if (Test-Path -LiteralPath $reentryRetired) { throw ('Unity unexpectedly created retired preview assets; inspect ' + $reentryOutput) }
    if ($reentryProcess.ExitCode -ne 0) { throw ('Unity failed with exit ' + $reentryProcess.ExitCode + '; inspect ' + $reentryOutput) }
    if ($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') {
        [xml]$reentryResults = Get-Content -Raw -LiteralPath (Join-Path $reentryOutput 'results.xml')
        if ($reentryResults.'test-run'.result -ne 'Passed' -or [int]$reentryResults.'test-run'.passed -lt 1) { throw ('Tests failed or no tests matched; inspect ' + $reentryOutput) }
        Write-Output ('Passed ' + $reentryResults.'test-run'.passed + ' tests.')
    }
    Write-Output ('Completed ' + $Stage + ': ' + $reentryOutput)
} finally {
    if ($reentryLease) { $reentryLease.Dispose() }
}