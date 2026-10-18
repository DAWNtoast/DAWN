param([string]$Name='WindowsSmoke01')
$ErrorActionPreference='Stop'
if($Name -notmatch '^[A-Za-z0-9_-]+$'){throw 'Simple evidence name required.'}
if(@(Get-CimInstance Win32_Process|Where-Object {$_.Name -eq 'Unity.exe' -or $_.Name -eq 'MemoryAudit.exe' -or $_.Name -eq '第七港口.exe'}).Count){throw 'Coordinate an exclusive Unity/Player slot.'}
$memoryExe='D:/Game/Builds/第七港口-内存优化与修复20261015/Windows/第七港口.exe'
$memoryOutput=Join-Path 'D:/Game/Artifacts/MemoryRelease20261015' $Name
if(Test-Path -LiteralPath $memoryOutput){throw 'Preserve earlier smoke evidence.'}
if(-not(Test-Path -LiteralPath $memoryExe -PathType Leaf)){throw 'Build final Windows executable first.'}
New-Item -ItemType Directory -Path $memoryOutput|Out-Null
$memoryArguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-shatteredSailSmokeTest',(Join-Path $memoryOutput 'Report'),'-logFile',(Join-Path $memoryOutput 'player.log'))
$memoryQuoted=foreach($memoryValue in $memoryArguments){$memoryEscaped=[regex]::Replace($memoryValue,'(\\*)"','$1$1\"');'"'+[regex]::Replace($memoryEscaped,'(\\+)$','$1$1')+'"'}
$memoryStarted=[DateTime]::UtcNow
$memoryPlayer=Start-Process -FilePath $memoryExe -ArgumentList ($memoryQuoted -join ' ') -WindowStyle Hidden -PassThru
[pscustomobject]@{ProcessId=$memoryPlayer.Id;Arguments=$memoryArguments;StartedUtc=$memoryStarted.ToString('o');Scope='Read-only packaged Player smoke; does not start new game or mutate PlayerPrefs.'}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $memoryOutput 'launch.json') -Encoding utf8
Write-Output ('RELEASE_SMOKE_PID='+$memoryPlayer.Id)
if(-not $memoryPlayer.WaitForExit(180000)){$memoryPlayer.Kill();throw 'Owned smoke Player timed out.'}
[pscustomobject]@{ExitCode=$memoryPlayer.ExitCode;Seconds=([DateTime]::UtcNow-$memoryStarted).TotalSeconds}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $memoryOutput 'result.json') -Encoding utf8
if($memoryPlayer.ExitCode -ne 0){throw 'Packaged Player smoke failed; retain evidence.'}
$memoryReport=Get-Content -LiteralPath (Join-Path $memoryOutput 'Report/player-smoke-report.txt') -Raw
if($memoryReport -notmatch '^PASS\r?\n' -or $memoryReport -notmatch '(?m)^Version: 0\.3\.20261015\.1\r?$' -or $memoryReport -notmatch '(?m)^Company: ShatteredSail\r?$' -or $memoryReport -notmatch '(?m)^Product: Shattered Sail\r?$'){throw 'Smoke report result/version/production save identity mismatch.'}
Write-Output $memoryReport
