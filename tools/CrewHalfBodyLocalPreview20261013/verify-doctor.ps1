$ErrorActionPreference = 'Stop'
$cardProject = 'D:\Game\tmp\card-clarity-20261013'
$cardOutput = 'D:\Game\Artifacts\CardDoctorAnatomy20261013\verification'
$cardLog = 'D:\Game\Artifacts\CardDoctorAnatomy20261013\capture.log'
if (Test-Path -LiteralPath (Join-Path $cardProject 'Temp\UnityLockfile')) { throw 'Isolated validation project is locked.' }
if (Test-Path -LiteralPath $cardOutput) { throw 'Preserve existing verification output.' }
$cardEditors = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'")
if (@($cardEditors | Where-Object { [string]::IsNullOrWhiteSpace($_.CommandLine) -or $_.CommandLine.Replace('/','\').IndexOf($cardProject,[StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count -gt 0) { throw 'Validation project is already open.' }
$cardArgs = @('-batchmode','-quit','-force-d3d11','-buildTarget','Win64','-projectPath',$cardProject,'-logFile',$cardLog,
 '-executeMethod','ShatteredSail.Editor.CardClarityCapture.CaptureFromCommandLine',
 '-cardClarityOutput',$cardOutput,'-cardClarityVariant','doctor-anatomy-fix',
 '-cardClarityCards','card_doctor_sutures','-cardClaritySkipCatalog')
$cardQuoted = $cardArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
$cardProcess = Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -ArgumentList $cardQuoted -WindowStyle Hidden -PassThru
Write-Output ('Single card verification PID=' + $cardProcess.Id)
$cardProcess.WaitForExit(); $cardProcess.Refresh()
if ($cardProcess.ExitCode -ne 0) { throw ('Unity failed: '+$cardProcess.ExitCode) }
$cardReport = Get-Content -Raw -LiteralPath (Join-Path $cardOutput 'report.json') | ConvertFrom-Json
if ($cardReport.status -ne 'passed' -or $cardReport.failedChecks -ne 0) { throw 'Card capture checks failed.' }
Write-Output ('PASS; captured card records=' + $cardReport.representatives.Count)
