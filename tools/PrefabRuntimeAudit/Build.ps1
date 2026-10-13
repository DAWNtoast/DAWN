param([Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Name)
$ErrorActionPreference='Stop'
$project='D:\Game\tmp\prefab-runtime-20261014'
$root='D:\Game\Artifacts\PrefabRuntimeOptimization20261014'
if(-not(Test-Path -LiteralPath (Join-Path $project '.prefab-runtime-owned.json'))){throw 'Owned diagnostic clone required.'}
if(@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'").Count -gt 0){throw 'Coordinate Unity ownership before a build.'}
$output=Join-Path $root $Name
if(Test-Path -LiteralPath $output){throw 'Fresh diagnostic build directory required.'}
New-Item -ItemType Directory -Path $output|Out-Null
$exe=Join-Path $output 'Windows\PrefabAudit.exe'
$arguments=@('-batchmode','-quit','-projectPath',$project,'-executeMethod','ShatteredSail.Editor.PrefabRuntimeAuditBuilder.BuildFromCommandLine','-prefabRuntimeBuildOutput',$exe,'-logFile',(Join-Path $output 'build.log'))
$quoted=foreach($value in $arguments){$escaped=[regex]::Replace($value,'(\\*)"','$1$1\"');'"'+[regex]::Replace($escaped,'(\\+)$','$1$1')+'"'}
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -ArgumentList ($quoted-join' ') -WindowStyle Hidden -PassThru
[pscustomobject]@{PID=$process.Id;StartedUtc=$started.ToString('o');Arguments=$arguments}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $output 'build-launch.json') -Encoding utf8
Write-Output ('UNITY_DIAGNOSTIC_BUILD_PID='+$process.Id)
$process.WaitForExit()
[pscustomobject]@{ExitCode=$process.ExitCode;CompletedUtc=[DateTime]::UtcNow.ToString('o');Seconds=([DateTime]::UtcNow-$started).TotalSeconds;Executable=$exe}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'build-result.json') -Encoding utf8
if($process.ExitCode-ne0 -or -not(Test-Path -LiteralPath $exe)){throw 'Diagnostic build failed; evidence preserved.'}
Write-Output ('DIAGNOSTIC_PLAYER='+$exe)
