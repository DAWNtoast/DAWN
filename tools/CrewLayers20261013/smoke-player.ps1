$ErrorActionPreference = 'Stop'
$crewPlayer = 'D:\Game\Builds\CrewLayers20261013\Windows\第七港口.exe'
$crewSmoke = 'D:\Game\Artifacts\CrewLayers20261013\verification\player-smoke'
$crewLog = 'D:\Game\Artifacts\CrewLayers20261013\verification\player-smoke.log'
if (-not (Test-Path -LiteralPath $crewPlayer)) { throw 'Build the comparison player first.' }
if (Test-Path -LiteralPath $crewSmoke) { throw 'Smoke output already exists; preserve this run.' }
$crewArgs = @('-screen-fullscreen','0','-screen-width','1920','-screen-height','1080',
    '-shatteredSailSmokeTest',$crewSmoke,'-logFile',$crewLog)
$crewQuoted = $crewArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
$crewProcess = Start-Process -FilePath $crewPlayer -ArgumentList $crewQuoted -WindowStyle Hidden -PassThru
$crewProcess.WaitForExit()
$crewProcess.Refresh()
if ($crewProcess.ExitCode -ne 0) { throw ('Player smoke failed: ' + $crewProcess.ExitCode) }
$crewReport = Get-Content -Raw -LiteralPath (Join-Path $crewSmoke 'player-smoke-report.txt')
Write-Output $crewReport
if (-not $crewReport.StartsWith('PASS')) { throw 'Player smoke did not pass.' }
