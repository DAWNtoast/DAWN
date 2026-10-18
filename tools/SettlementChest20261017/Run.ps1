param(
    [Parameter(Mandatory=$true)][ValidateSet('Sync','EditMode','PlayMode','Capture')][string]$Stage,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Name = 'pass01'
)
$ErrorActionPreference = 'Stop'
$settleSource = 'D:\Game\UnityClient'
$settleProject = [IO.Path]::GetFullPath('D:\Game\tmp\settlement-alignment-20261011')
$settleArtifacts = 'D:\Game\Artifacts\SettlementChest20261017'
$settleOutput = Join-Path $settleArtifacts $Name
$settleUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if (-not $settleProject.StartsWith('D:\Game\tmp\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected scratch project.' }
$settleEditors = @(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.ExecutablePath -match '[/\\]Editor[/\\]Unity\.exe$' })
if ($settleEditors.Count -gt 0) { throw 'Unity Editor slot is occupied. Wait for its owner.' }
[IO.Directory]::CreateDirectory($settleArtifacts) | Out-Null
if ($Stage -eq 'Sync') {
    $settleLog = Join-Path $settleArtifacts ('sync-' + $Name + '.log')
    foreach ($settlePart in @('Assets','Packages','ProjectSettings')) {
        & robocopy (Join-Path $settleSource $settlePart) (Join-Path $settleProject $settlePart) /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS /XD UIPreviewsForMe /XF UIPreviewsForMe.meta ('/LOG+:'+$settleLog) | Out-Null
        if ($LASTEXITCODE -ge 8) { throw ('Source sync failed: '+$settlePart) }
    }
    $settleScratchAssets = [IO.Path]::GetFullPath((Join-Path $settleProject 'Assets\_Project'))
    $settleSourceAssets = Join-Path $settleSource 'Assets\_Project'
    $settleRemoved = @()
    foreach ($settleOld in @(Get-ChildItem -LiteralPath $settleScratchAssets -Filter '*.cs' -File -Recurse)) {
        $settleRelative = $settleOld.FullName.Substring($settleScratchAssets.Length + 1)
        if (Test-Path -LiteralPath (Join-Path $settleSourceAssets $settleRelative)) { continue }
        $settleResolved = [IO.Path]::GetFullPath($settleOld.FullName)
        if (-not $settleResolved.StartsWith($settleScratchAssets+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source outside scratch assets.' }
        Remove-Item -LiteralPath $settleResolved -Force
        if (Test-Path -LiteralPath ($settleResolved+'.meta')) { Remove-Item -LiteralPath ($settleResolved+'.meta') -Force }
        $settleRemoved += $settleRelative
    }
    $settleRemoved | Set-Content -LiteralPath (Join-Path $settleArtifacts ('stale-sources-'+$Name+'.txt')) -Encoding utf8
    $settleSettingsPath = Join-Path $settleProject 'ProjectSettings\ProjectSettings.asset'
    $settleSettings = [IO.File]::ReadAllText($settleSettingsPath)
    $settleSettings = [regex]::Replace($settleSettings,'(?m)^  companyName:.*$','  companyName: ShatteredSailSettlementChest20261017')
    $settleSettings = [regex]::Replace($settleSettings,'(?m)^  productName:.*$','  productName: Shattered Sail Settlement Chest 20261017')
    [IO.File]::WriteAllText($settleSettingsPath,$settleSettings,[Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SettlementChestCapture.cs') -Destination (Join-Path $settleProject 'Assets\_Project\Editor\SettlementChestCapture.cs') -Force
    [pscustomobject]@{Source=$settleSource; Project=$settleProject; SyncedUtc=[DateTime]::UtcNow.ToString('o'); RemovedStaleSources=$settleRemoved.Count; RetiredPreviewExcluded=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $settleArtifacts ('sync-'+$Name+'.json')) -Encoding utf8
    Write-Output 'Source synchronized into isolated warm project; ForMe is excluded and no scene generation is invoked.'
    exit 0
}
if (Test-Path -LiteralPath $settleOutput) { throw 'Use a fresh evidence directory.' }
[IO.Directory]::CreateDirectory($settleOutput) | Out-Null
$settleArgs = @('-batchmode','-force-d3d11','-projectPath',$settleProject,'-logFile',(Join-Path $settleOutput 'unity.log'))
if ($Stage -eq 'Capture') {
    $settleArgs += @('-quit','-executeMethod','ShatteredSail.Editor.SettlementChestCapture.CaptureFromCommandLine','-uiAuditPath',(Join-Path $settleOutput 'captures'))
} else {
    $settleFilter = if ($Stage -eq 'EditMode') { 'SettlementChestRewardPlanTests;SettlementChestPresentationTests;SettlementPresentationTests;SettlementTextPixelAlignmentTests' } else { 'SettlementChestSequencePlayModeTests;SettlementChestFlow_;SettlementRevealPlayModeTests;SettlementWarehouse_;SupplySettlementOptimization_' }
    $settleArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$settleFilter,'-testResults',(Join-Path $settleOutput 'results.xml'))
    if ($Stage -eq 'PlayMode') { $settleArgs += @('-navalVisualCapturePath',(Join-Path $settleOutput 'interactions')) }
}
$settleQuoted = foreach ($settleArg in $settleArgs) { '"' + $settleArg.Replace('"','\"') + '"' }
$settleProcess = Start-Process -FilePath $settleUnity -ArgumentList $settleQuoted -WorkingDirectory $settleProject -WindowStyle Hidden -PassThru
[pscustomobject]@{Stage=$Stage; PID=$settleProcess.Id; Arguments=$settleArgs; StartedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $settleOutput 'launch.json') -Encoding utf8
Write-Output ('Started '+$Stage+' PID '+$settleProcess.Id+'; inspect unity.log/results.xml before proceeding.')