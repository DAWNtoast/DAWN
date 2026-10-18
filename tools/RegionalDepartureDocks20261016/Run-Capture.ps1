$ErrorActionPreference = 'Stop'
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Leave the running Unity editor untouched.' }
$dockCaptureOutput = 'D:/Game/Artifacts/RegionalDepartureDocks20261016/validation'
[IO.Directory]::CreateDirectory($dockCaptureOutput) | Out-Null
$dockCaptureArguments = @('-batchmode', '-quit', '-projectPath', 'D:/Game/tmp/departure-chart-inline-20261014',
    '-executeMethod', 'ShatteredSail.Editor.RegionalDepartureDockCapture.Capture',
    '-logFile', (Join-Path $dockCaptureOutput 'capture-editor.log'))
$dockCaptureProcess = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $dockCaptureArguments -WindowStyle Hidden -PassThru
$dockCaptureProcess.WaitForExit()
if ($dockCaptureProcess.ExitCode -ne 0) { throw 'Regional capture failed; inspect capture-editor.log.' }
Write-Output 'Six production UI captures saved under validation/screenshots.'
