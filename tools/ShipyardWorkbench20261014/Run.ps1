param(
    [Parameter(Mandatory=$true)][ValidateSet('Compile','Edit','Play','Capture')][string]$Stage,
    [Parameter(Mandatory=$true)][string]$Output,
    [string]$ProjectPath = 'D:/Game/tmp/shipyard-detail-clean-20261011',
    [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$workbenchProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$workbenchExpected = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../tmp/shipyard-detail-clean-20261011')).TrimEnd('\','/')
if ($workbenchProject -ne $workbenchExpected) { throw 'Only the named isolated validation project is allowed.' }
$workbenchOutput = [IO.Path]::GetFullPath($Output).TrimEnd('\','/')
$workbenchArtifacts = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Artifacts')).TrimEnd('\','/')
if (-not $workbenchOutput.StartsWith($workbenchArtifacts + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use an output directory below Artifacts.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($workbenchProject) }).Count) { throw 'Isolated project is occupied.' }
$workbenchLog = Join-Path $workbenchOutput ($Stage.ToLowerInvariant() + '.log')
if (Test-Path -LiteralPath $workbenchLog) { throw 'Use a fresh output directory; existing logs are preserved.' }
[IO.Directory]::CreateDirectory($workbenchOutput) | Out-Null
$workbenchUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
$workbenchArgs = @('-batchmode','-force-d3d11','-projectPath',$workbenchProject,'-logFile',$workbenchLog)
if ($Stage -eq 'Compile') { $workbenchArgs += '-quit' }
elseif ($Stage -eq 'Capture') {
    $workbenchArgs += @('-quit','-executeMethod','ShatteredSail.Editor.ShipyardWorkbenchCapture.CaptureFromCommandLine',
        '-shipyardWorkbenchCapturePath',(Join-Path $workbenchOutput 'captures'))
} else {
    $workbenchFilter = if ($Stage -eq 'Edit') { 'ShipyardGalleryTests;ShipyardComponentComparisonTests' } else { 'ShipyardEquipment_;ItemIcons_EquipmentAndSettlementKeepPicturesClearOfNamesAndAmounts' }
    if ($TestFilter) { $workbenchFilter = $TestFilter }
    $workbenchArgs += @('-runTests','-testPlatform',($Stage + 'Mode'),'-testFilter',$workbenchFilter,
        '-testResults',(Join-Path $workbenchOutput ($Stage.ToLowerInvariant() + '-results.xml')))
    if ($Stage -eq 'Play') { $workbenchArgs += @('-navalVisualCapturePath',(Join-Path $workbenchOutput 'interactions')) }
}
$workbenchQuoted = $workbenchArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
$workbenchProcess = Start-Process -FilePath $workbenchUnity -ArgumentList $workbenchQuoted -WorkingDirectory $workbenchProject -WindowStyle Hidden -PassThru
[pscustomobject]@{ Stage=$Stage; ProcessId=$workbenchProcess.Id; Project=$workbenchProject; Arguments=$workbenchArgs } |
    ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $workbenchOutput ($Stage.ToLowerInvariant() + '-launch.json')) -Encoding utf8
Write-Output ('Started ' + $Stage + ', PID ' + $workbenchProcess.Id + ', log ' + $workbenchLog + '. Wait for exit and inspect results before the next run.')
