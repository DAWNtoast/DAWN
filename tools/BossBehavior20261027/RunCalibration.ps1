param(
 [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag,
 [Parameter(Mandatory=$true)][string]$SourceSnapshot,
 [int]$Seeds=1,[int]$MaxTurns=30,[string]$BossFilter='*',[string]$Schools='*',
 [string]$LoadoutsFile='',[string]$DecksFile='',[switch]$Sources
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=Join-Path $taskRoot ('outputs/boss-behavior-20261027/calibration/'+$RunTag)
$taskCore=Join-Path $taskOutput 'CoreSnapshot'
$taskInputs=Join-Path $taskOutput 'Inputs'
$taskBuild=Join-Path $taskOutput 'Build'
$taskUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
if(Test-Path -LiteralPath $taskOutput){throw 'Choose a fresh run tag; previous evidence is immutable.'}
if($Seeds -lt 1 -or $MaxTurns -lt 1){throw 'Positive seeds and turns required.'}
if(!$LoadoutsFile){$LoadoutsFile=Join-Path $taskRoot 'outputs/naval-balance-20261024/calibration-loadouts.csv'}
if(!$DecksFile){$DecksFile=Join-Path $taskRoot 'tools/BossCalibration20261024/candidate-decks.csv'}
New-Item -ItemType Directory -Force -Path $taskOutput,$taskCore,$taskInputs,$taskBuild | Out-Null
$taskSources=@(Get-ChildItem -LiteralPath $SourceSnapshot -File -Filter '*.cs'|Sort-Object Name)
if($taskSources.Count -lt 140){throw 'Core snapshot incomplete.'}
$taskHashes=@(foreach($f in $taskSources){
 $sha=(Get-FileHash -LiteralPath $f.FullName).Hash;$to=Join-Path $taskCore $f.Name
 Copy-Item -LiteralPath $f.FullName -Destination $to
 if((Get-FileHash -LiteralPath $to).Hash -ne $sha){throw 'Snapshot copy mismatch.'}
 [pscustomobject]@{Source=$f.FullName;Snapshot=$to;SHA256=$sha}
})
$taskHashes|Export-Csv -LiteralPath (Join-Path $taskOutput 'source-hashes.csv') -Encoding utf8 -NoTypeInformation
Copy-Item -LiteralPath $LoadoutsFile -Destination (Join-Path $taskOutput 'calibration-loadouts.csv')
Copy-Item -LiteralPath $DecksFile -Destination (Join-Path $taskOutput 'decks.csv')
foreach($name in @('SchoolPowerProbe.cs','NavalBalanceProbe.cs','BossBalanceProbe.cs','BossProfileAudit.cs')){
 Copy-Item -LiteralPath (Join-Path $taskRoot ('tools/BossCalibration20261024/'+$name)) -Destination (Join-Path $taskInputs $name)
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BossBehaviorTelemetry.cs') -Destination (Join-Path $taskInputs 'BossBehaviorTelemetry.cs')
# Instrument only this frozen copy; shared old probes and production code remain untouched.
$probe=Join-Path $taskInputs 'BossBalanceProbe.cs';$code=[IO.File]::ReadAllText($probe)
$needle='if(!s.EndBattleTurn())throw new InvalidOperationException("Legal end turn rejected.");'
if(!$code.Contains($needle)){throw 'Probe changed; review instrumentation anchor.'}
$before='BossBehaviorTelemetry.Observe(s,scenario.Id,school,hp,seed,r.NavalTurns,"before_enemy",survival?"survival":"damage",b);'
$after='BossBehaviorTelemetry.Observe(s,scenario.Id,school,hp,seed,r.NavalTurns,"after_enemy",survival?"survival":"damage",b);'
[IO.File]::WriteAllText($probe,$code.Replace($needle,$before+[Environment]::NewLine+$needle+[Environment]::NewLine+$after),[Text.UTF8Encoding]::new($false))
$code=[IO.File]::ReadAllText($probe)
$start='if(preparing)r.PreparationTurns++;else r.NavalTurns++;'
$end='r.FinalHull=s.Hull;r.EnemyFinal=b.EnemyHull;'
if(!$code.Contains($start) -or !$code.Contains($end)){throw 'Turn observation anchors changed.'}
$startHook='BossBehaviorTelemetry.Observe(s,scenario.Id,school,hp,seed,r.NavalTurns,"start_turn",survival?"survival":"damage",b);'
$endHook='BossBehaviorTelemetry.Observe(s,scenario.Id,school,hp,seed,r.NavalTurns,"sample_end",survival?"survival":"damage",b);'
$code=$code.Replace($start,$start+[Environment]::NewLine+$startHook).Replace($end,$endHook+[Environment]::NewLine+$end)
[IO.File]::WriteAllText($probe,$code,[Text.UTF8Encoding]::new($false))
$taskHashInputs=@((Get-ChildItem -LiteralPath $taskInputs -File).FullName)
$taskHashInputs+=(Join-Path $taskOutput 'calibration-loadouts.csv'),(Join-Path $taskOutput 'decks.csv')
Get-FileHash -LiteralPath $taskHashInputs | Export-Csv -LiteralPath (Join-Path $taskOutput 'input-hashes.csv') -NoTypeInformation -Encoding utf8
$exe=Join-Path $taskBuild 'Calibration.exe'
$args=@("$taskUnity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-main:BossCalibrationMain','-langversion:9','-noconfig','-nostdlib',"-out:$exe")
foreach($asm in @('mscorlib.dll','System.dll','System.Core.dll')){$args+="-r:$taskUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$asm"}
$args+=@($taskHashes|ForEach-Object Snapshot);$args+=@((Get-ChildItem -LiteralPath $taskInputs -Filter '*.cs').FullName)
$watch=[Diagnostics.Stopwatch]::StartNew();$priorEnv=$env:BOSS_BEHAVIOR_OUTPUT;$released=0
try {
 & "$taskUnity/NetCoreRuntime/dotnet.exe" @args 2>&1|Tee-Object -FilePath (Join-Path $taskOutput 'compile.log')
 if($LASTEXITCODE -ne 0){throw 'Calibration compile failed.'}
 $env:BOSS_BEHAVIOR_OUTPUT=$taskOutput
 $sourceMode=if($Sources){'sources'}else{'isolated'}
 [pscustomobject]@{Seeds=$Seeds;MaxTurns=$MaxTurns;Cohorts='candidate';Mode='boss';Sources=[bool]$Sources;BossFilter=$BossFilter;Schools=$Schools}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOutput 'run-parameters.json') -Encoding utf8
 & "$taskUnity/MonoBleedingEdge/bin/mono.exe" $exe $taskOutput $Seeds $MaxTurns 'boss' $sourceMode $BossFilter $Schools 'candidate' 2>&1|Tee-Object -FilePath (Join-Path $taskOutput 'run.log')
 if($LASTEXITCODE -ne 0){throw 'Calibration samples failed.'}
 foreach($row in $taskHashes){if((Get-FileHash -LiteralPath $row.Snapshot).Hash -ne $row.SHA256){throw 'Frozen source changed.'}}
} finally {
 $env:BOSS_BEHAVIOR_OUTPUT=$priorEnv;$watch.Stop()
 $expected=[IO.Path]::GetFullPath((Join-Path $taskOutput 'Build/Calibration.exe'))
 if([IO.Path]::GetFullPath($exe) -ne $expected){throw 'Unexpected cleanup target.'}
 if((Get-Item -LiteralPath $taskBuild).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refuse linked build directory.'}
 if(Test-Path -LiteralPath $exe){$released=(Get-Item -LiteralPath $exe).Length;Remove-Item -LiteralPath $exe -Force}
 if(@(Get-ChildItem -LiteralPath $taskBuild -Force).Count -eq 0){Remove-Item -LiteralPath $taskBuild}
 [pscustomobject]@{ElapsedSeconds=$watch.Elapsed.TotalSeconds;RemovedExecutableBytes=$released;BuildDirectoryRemaining=(Test-Path -LiteralPath $taskBuild);Retained='Frozen Core, inputs, logs, results and source hashes';MainProjectChanged=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $taskOutput 'cleanup.json') -Encoding utf8
}
