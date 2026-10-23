param(
    [Parameter(Mandatory=$true)][ValidateSet('Compile','Edit','Play','Capture')][string]$Stage,
    [Parameter(Mandatory=$true)][string]$Output,
    [string]$TestFilter=''
)
$ErrorActionPreference='Stop'
$haloProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../tmp/component-rarity-halo-20261019')).TrimEnd('\','/')
if ($haloProject -ne 'D:\Game\tmp\component-rarity-halo-20261019') { throw 'Only the named disposable project is allowed.' }
if (-not (Test-Path -LiteralPath (Join-Path $haloProject '.rarity-halo-validation.json'))) { throw 'Run Sync.ps1 first.' }
$haloOutput=[IO.Path]::GetFullPath($Output).TrimEnd('\','/')
$haloArtifacts=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Artifacts')).TrimEnd('\','/')
if (-not $haloOutput.StartsWith($haloArtifacts+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be below Artifacts.' }
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($haloProject) }).Count) { throw 'Isolated project is occupied.' }
$haloLog=Join-Path $haloOutput ($Stage.ToLowerInvariant()+'.log')
if (Test-Path -LiteralPath $haloLog) { throw 'Preserve existing logs; use a fresh output directory.' }
[IO.Directory]::CreateDirectory($haloOutput) | Out-Null
$haloArgs=@('-batchmode','-force-d3d11','-projectPath',$haloProject,'-logFile',$haloLog)
if ($Stage -eq 'Compile') { $haloArgs += '-quit' }
elseif ($Stage -eq 'Capture') {
    $haloArgs += @('-quit','-executeMethod','ShatteredSail.Editor.ComponentRarityHaloCapture.CaptureFromCommandLine','-componentRarityHaloOutput',(Join-Path $haloOutput 'captures'))
} else {
    $haloFilter=if ($Stage -eq 'Edit') { 'PortSurfaceGroundingTests.ComponentShopOffersShowRarityHalosBehindArtworkWithoutInterceptingClicks;ComponentShopPlaqueLayoutTests;ComponentShopShowroomLayoutTests;ComponentShopDisplayGeometryTests' } else { 'ComponentShopShowroomPaginationTests;ShipyardEquipmentUiTests;ItemIcons_EquipmentAndSettlementKeepPicturesClearOfNamesAndAmounts;ComponentResaleFlowTests' }
    if ($TestFilter) { $haloFilter=$TestFilter }
    $haloArgs += @('-runTests','-testPlatform',($Stage+'Mode'),'-testFilter',$haloFilter,'-testResults',(Join-Path $haloOutput ($Stage.ToLowerInvariant()+'-results.xml')))
}
$haloQuoted=$haloArgs | ForEach-Object { '"'+$_.Replace('"','\"')+'"' }
$haloProcess=Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $haloQuoted -WorkingDirectory $haloProject -WindowStyle Hidden -PassThru
[pscustomobject]@{ Stage=$Stage; ProcessId=$haloProcess.Id; Project=$haloProject; Arguments=$haloArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $haloOutput ($Stage.ToLowerInvariant()+'-launch.json')) -Encoding utf8
Write-Output ('Started '+$Stage+', PID '+$haloProcess.Id+', log '+$haloLog+'. Wait for exit and inspect results before next run.')