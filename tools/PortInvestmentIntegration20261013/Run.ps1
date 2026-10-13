param(
    [ValidateSet('Prepare','Sync','Compile','EditMode','PlayMode','Capture','SyncSaved','CaptureSaved','CopyBack')]
    [string]$Stage = 'Prepare',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunName = 'run1',
    [string]$TestFilter = '',
    [string]$Report = '',
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
if (@('SyncSaved','CaptureSaved','CopyBack') -contains $Stage) { throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.' }
$investmentSource = [IO.Path]::GetFullPath('D:\Game\UnityClient').TrimEnd('\')
$investmentProject = [IO.Path]::GetFullPath('D:\Game\tmp\port-investment-layout-20261011').TrimEnd('\')
$investmentOutput = [IO.Path]::GetFullPath('D:\Game\Artifacts\PortInvestmentRedesign20261013\validation')
$investmentUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$investmentMarker = Join-Path $investmentProject '.port-investment-scene-validation.json'
$investmentSceneRoot = 'Assets/_Project/Scenes/UIPreviewsForMe/'
$investmentScenes = @() # Retired UIPreviewsForMe targets; Prepare/Sync snapshot only active source.
if ($investmentProject -ne 'D:\Game\tmp\port-investment-layout-20261011' -or $investmentProject -eq $investmentSource) { throw 'Invalid dedicated scratch path.' }
if (-not (Test-Path -LiteralPath (Join-Path $investmentProject 'Library'))) { throw 'Expected dedicated warm cache is missing.' }
function Get-InvestmentHash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Assert-InvestmentPath([string]$Path,[string]$Root) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($Root.TrimEnd('\') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw "Path escaped expected root: $resolved" }
    return $resolved
}
function Assert-InvestmentStopped {
    if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'").Count -gt 0) { throw 'Another Unity Editor is running. Coordinate before launching; do not terminate it.' }
    $lockPath = Join-Path $investmentProject 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $lockPath) {
        try { $lockStream = [IO.File]::Open($lockPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None); $lockStream.Dispose() }
        catch { throw 'Dedicated scratch Unity lock is held.' }
    }
    foreach ($container in @('D:\Game\tmp',$investmentProject)) {
        if ((Get-Item -LiteralPath $container).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Scratch container is a reparse point: $container" }
    }
    foreach ($part in @('Assets','Packages','ProjectSettings','Library')) {
        $partPath = Assert-InvestmentPath (Join-Path $investmentProject $part) $investmentProject
        if ((Get-Item -LiteralPath $partPath).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Scratch target is a reparse point: $part" }
    }
}
function Assert-InvestmentMarker {
    if (-not (Test-Path -LiteralPath $investmentMarker)) { throw 'Run Prepare to register the dedicated cache first.' }
    $markerData = Get-Content -LiteralPath $investmentMarker -Raw | ConvertFrom-Json
    if ($markerData.project -ne $investmentProject -or $markerData.source -ne $investmentSource -or $markerData.task -ne 'PortInvestmentScene20261013') { throw 'Dedicated marker does not match this task.' }
    return $markerData
}
function Install-InvestmentHelpers {
    foreach ($helper in @(@('InvestmentPreviewSync.cs','InvestmentPreviewSync.cs'),@('Capture.cs','InvestmentSceneCapture.cs'))) {
        $origin = Join-Path $PSScriptRoot $helper[0]
        if (Test-Path -LiteralPath $origin) { Copy-Item -LiteralPath $origin -Destination (Join-Path $investmentProject ('Assets\_Project\Editor\' + $helper[1])) -Force }
    }
}
[IO.Directory]::CreateDirectory($investmentOutput) | Out-Null
Assert-InvestmentStopped

if ($Stage -eq 'CopyBack') {
    $null = Assert-InvestmentMarker
    if (-not $Report) { throw 'CopyBack requires -Report <copy-manifest.json>.' }
    $reportPath = Assert-InvestmentPath $Report $investmentOutput
    $manifest = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (-not $manifest.passed -or $manifest.project -ne $investmentProject.Replace('\','/') -or $manifest.source -ne $investmentSource.Replace('\','/')) { throw 'The sync manifest did not pass or uses another project.' }
    if (@($manifest.scenes).Count -ne 4) { throw 'Expected exactly four scoped scene records.' }
    $seenScenes = @{}
    foreach ($scene in $manifest.scenes) {
        if ($investmentScenes -notcontains $scene.path -or $seenScenes.ContainsKey($scene.path)) { throw 'Unexpected or duplicate scene in manifest.' }
        $seenScenes[$scene.path] = $true
        $sceneChecks = @(
            [pscustomobject]@{Path=$scene.path;Hash=$scene.beforeSha256}
            [pscustomobject]@{Path=($scene.path + '.meta');Hash=$scene.metaSha256}
        )
        foreach ($check in $sceneChecks) {
            $destination = Assert-InvestmentPath (Join-Path $investmentSource $check.Path) $investmentSource
            if ((Get-InvestmentHash $destination) -ne $check.Hash) { throw "Concurrent production scene/meta edit: $($check.Path)" }
        }
    }
    foreach ($dependency in $manifest.existingDependencies) {
        $destination = Assert-InvestmentPath (Join-Path $investmentSource $dependency.path) $investmentSource
        if (-not (Test-Path -LiteralPath $destination) -or (Get-InvestmentHash $destination) -ne $dependency.sha256) { throw "Dependency changed since scene sync: $($dependency.path)" }
    }
    $packageRoot = Split-Path -Parent $reportPath
    $copies = @()
    $seenFiles = @{}
    foreach ($file in $manifest.files) {
        if ($seenFiles.ContainsKey($file.path) -or $file.packagePath -ne ('files/' + $file.path)) { throw 'Duplicate file or unexpected package location.' }
        $seenFiles[$file.path] = $true
        $allowed = $investmentScenes -contains $file.path
        $allowed = $allowed -or $file.path -eq ($manifest.visualFolder + '.meta') -or $file.path.StartsWith($manifest.visualFolder + '/', [StringComparison]::Ordinal)
        if (-not $allowed -or -not $manifest.visualFolder.StartsWith('Assets/_Project/Presentation/EditablePreviewAssets/PortInvestmentScene20261013_', [StringComparison]::Ordinal)) { throw "Unscoped copy manifest path: $($file.path)" }
        $origin = Assert-InvestmentPath (Join-Path $packageRoot $file.packagePath) $packageRoot
        $destination = Assert-InvestmentPath (Join-Path $investmentSource $file.path) $investmentSource
        if ((Get-InvestmentHash $origin) -ne $file.sha256) { throw "Packaged file changed: $($file.path)" }
        if ($investmentScenes -notcontains $file.path -and (Test-Path -LiteralPath $destination)) { throw "New visual dependency already exists: $($file.path)" }
        $copies += [pscustomobject]@{Source=$origin;Destination=$destination;Hash=$file.sha256;Path=$file.path}
    }
    foreach ($scene in $manifest.scenes) {
        $copy = @($copies | Where-Object { $_.Path -eq $scene.path })
        if ($copy.Count -ne 1 -or $copy[0].Hash -ne $scene.afterSha256) { throw 'Packaged scene does not match the verified sync record.' }
    }
    if ($Apply) {
        $backupRoot = Join-Path $investmentOutput ('copyback-' + $RunName)
        if (Test-Path -LiteralPath $backupRoot) { throw 'Use a fresh RunName for copyback backups.' }
        foreach ($scene in $investmentScenes) {
            $backup = Join-Path $backupRoot $scene
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup)) | Out-Null
            Copy-Item -LiteralPath (Join-Path $investmentSource $scene) -Destination $backup
            Copy-Item -LiteralPath (Join-Path $investmentSource ($scene + '.meta')) -Destination ($backup + '.meta')
        }
        # All checks complete before any production write; dependencies precede scenes.
        foreach ($copy in ($copies | Sort-Object { if ($investmentScenes -contains $_.Path) { 1 } else { 0 } })) {
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($copy.Destination)) | Out-Null
            Copy-Item -LiteralPath $copy.Source -Destination $copy.Destination -Force
            if ((Get-InvestmentHash $copy.Destination) -ne $copy.Hash) { throw 'Copyback verification failed.' }
        }
        Write-Output "Copied $($copies.Count) verified scene/dependency files; backup=$backupRoot"
    } else { Write-Output "Ready to copy $($copies.Count) verified files; add -Apply after reviewing saved screenshots." }
    exit 0
}

if ($Stage -in @('Prepare','Sync')) {
    if (Test-Path -LiteralPath $investmentMarker) { $null = Assert-InvestmentMarker }
    elseif ($Stage -ne 'Prepare') { throw 'Run Prepare before Sync.' }
    else {
        $oldSettings = Get-Content -LiteralPath (Join-Path $investmentProject 'ProjectSettings\ProjectSettings.asset') -Raw
        if ($oldSettings -notmatch '(?m)^  companyName: PortInvestmentLayoutValidation\r?$' -or $oldSettings -notmatch '(?m)^  productName: PortInvestmentLayout20261011\r?$') { throw 'Unmarked cache does not have the expected prior independent investment identity.' }
        [pscustomobject]@{task='PortInvestmentScene20261013';project=$investmentProject;source=$investmentSource;snapshot=''} | ConvertTo-Json | Set-Content -LiteralPath $investmentMarker -Encoding utf8
    }
    $snapshotDirectory = Join-Path $investmentOutput ('snapshot-' + $RunName)
    if (Test-Path -LiteralPath $snapshotDirectory) { throw 'Use a fresh RunName for the source snapshot.' }
    [IO.Directory]::CreateDirectory($snapshotDirectory) | Out-Null
    $snapshotFiles = @()
    foreach ($scene in $investmentScenes) {
        foreach ($relative in @($scene,($scene + '.meta'))) {
            $origin = Join-Path $investmentSource $relative
            $backup = Join-Path $snapshotDirectory $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup)) | Out-Null
            Copy-Item -LiteralPath $origin -Destination $backup
            $snapshotFiles += [pscustomobject]@{path=$relative;sha256=(Get-InvestmentHash $backup)}
        }
    }
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        $destination = Assert-InvestmentPath (Join-Path $investmentProject $part) $investmentProject
        # /MIR deletes only within this verified dedicated scratch target.
        & robocopy (Join-Path $investmentSource $part) $destination /MIR /XJ /R:1 /W:1 /NFL /NDL /NP /NJH /NJS ('/LOG+:' + (Join-Path $snapshotDirectory 'mirror.log')) | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Snapshot mirror failed: $part" }
    }
    $settingsPath = Join-Path $investmentProject 'ProjectSettings\ProjectSettings.asset'
    $settings = [IO.File]::ReadAllText($settingsPath)
    $settings = [regex]::Replace($settings,'(?m)^  companyName:.*$','  companyName: PortInvestmentSceneValidation')
    $settings = [regex]::Replace($settings,'(?m)^  productName:.*$','  productName: PortInvestmentSceneValidation20261013')
    $settings = [regex]::Replace($settings,'(?m)^    Standalone: com\.shatteredsail\.shattered-sail\r?$','    Standalone: com.validation.portinvestmentscene20261013')
    [IO.File]::WriteAllText($settingsPath,$settings,[Text.UTF8Encoding]::new($false))
    foreach ($file in $snapshotFiles) {
        if ((Get-InvestmentHash (Join-Path $investmentProject $file.path)) -ne $file.sha256 -or (Get-InvestmentHash (Join-Path $investmentSource $file.path)) -ne $file.sha256) { throw "Scene changed during source sync: $($file.path)" }
    }
    $snapshotPath = Join-Path $snapshotDirectory 'baseline.json'
    [pscustomobject]@{source=$investmentSource;project=$investmentProject;utc=[DateTime]::UtcNow.ToString('o');files=$snapshotFiles} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $snapshotPath -Encoding utf8
    [pscustomobject]@{task='PortInvestmentScene20261013';project=$investmentProject;source=$investmentSource;snapshot=$snapshotPath} | ConvertTo-Json | Set-Content -LiteralPath $investmentMarker -Encoding utf8
    Install-InvestmentHelpers
    Write-Output "Synchronized dedicated warm cache; baseline=$snapshotPath. Unity was not started."
    exit 0
}
$markerData = Assert-InvestmentMarker
$settings = Get-Content -LiteralPath (Join-Path $investmentProject 'ProjectSettings\ProjectSettings.asset') -Raw
if ($settings -notmatch '(?m)^  companyName: PortInvestmentSceneValidation\r?$' -or $settings -notmatch '(?m)^  productName: PortInvestmentSceneValidation20261013\r?$') { throw 'Scratch storage identity is no longer isolated.' }
Install-InvestmentHelpers
$runDirectory = Join-Path $investmentOutput ($Stage.ToLowerInvariant() + '-' + $RunName)
if (Test-Path -LiteralPath $runDirectory) { throw 'Use a fresh RunName for this stage.' }
[IO.Directory]::CreateDirectory($runDirectory) | Out-Null
$unityArgs = @('-batchmode','-force-d3d11','-projectPath',$investmentProject,'-logFile',(Join-Path $runDirectory 'Unity.log'))
switch ($Stage) {
    'Compile' { $unityArgs += '-quit' }
    'EditMode' {
        if (-not $TestFilter) { $TestFilter = 'CorePortInvestmentTests;PortInvestmentCatalogTests;MaintenanceEditorPreviewTests;MaintenanceGroundingLayoutTests;RegionalPortFacilityArtCatalogTests;DesktopBackgroundCompressionTests;Investment_;InvestmentScene_' }
        $unityArgs += @('-runTests','-testPlatform','EditMode','-testFilter',$TestFilter,'-testResults',(Join-Path $runDirectory 'results.xml'))
    }
    'PlayMode' {
        if (-not $TestFilter) { $TestFilter = 'PortInvestment_;PortWarehouse_TwoDeliveryFixtures;MonthlyCalendar_Investment' }
        $unityArgs += @('-runTests','-testPlatform','PlayMode','-testFilter',$TestFilter,'-testResults',(Join-Path $runDirectory 'results.xml'),'-navalVisualCapturePath',(Join-Path $runDirectory 'LiveScreens'))
    }
    'Capture' { $unityArgs += @('-quit','-executeMethod','ShatteredSail.Editor.InvestmentSceneCapture.Run','-investmentCapturePath',(Join-Path $runDirectory 'screens')) }
    'CaptureSaved' { $unityArgs += @('-quit','-executeMethod','ShatteredSail.Editor.InvestmentPreviewSync.CaptureSaved','-investmentCapturePath',(Join-Path $runDirectory 'screens')) }
    'SyncSaved' { $unityArgs += @('-quit','-executeMethod','ShatteredSail.Editor.InvestmentPreviewSync.Run','-investmentSyncPath',(Join-Path $runDirectory 'package')) }
}
$quotedArgs = ($unityArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }) -join ' '
$process = Start-Process -FilePath $investmentUnity -ArgumentList $quotedArgs -WindowStyle Hidden -PassThru
[pscustomobject]@{stage=$Stage;pid=$process.Id;project=$investmentProject;utc=[DateTime]::UtcNow.ToString('o');arguments=$unityArgs;baseline=$markerData.snapshot} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runDirectory 'launch.json') -Encoding utf8
Write-Output "Started hidden Unity PID $($process.Id); stage=$Stage; output=$runDirectory"
Write-Output 'Asynchronous launch. Wait for this PID to exit; inspect Unity.log and require a nonempty Passed results.xml before proceeding.'
