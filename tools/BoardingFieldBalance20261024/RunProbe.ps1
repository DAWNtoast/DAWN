param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag,
    [string]$CoreSource='', [string]$LoadoutsFile='', [string]$DecksFile='',
    [ValidateRange(1,256)][int]$Seeds=2, [ValidateRange(1,100)][int]$MaxTurns=30,
    [string]$Fields='none,fire,arcane,curse,thunder,blood,fire+arcane,fire+curse',
    [string]$Durations='1,3,5,10', [string]$Owners='player', [string]$Schools='*',
    [string]$Bosses='*', [string]$Ranks='stage', [string]$Policies='damage,survival',
    [ValidateRange(0,256)][int]$TraceSeeds=1, [switch]$FieldOnly, [switch]$CompileOnly
)
$ErrorActionPreference='Stop'
$taskWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=Join-Path $taskWorkspace ('outputs/boarding-field-balance-20261024/'+$RunTag)
$taskSnapshot=Join-Path $taskOutput 'CoreSnapshot'
$taskUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
if(!$CoreSource){$CoreSource=Join-Path $taskWorkspace 'outputs/boarding-field-balance-20261024/Inputs/CoreSnapshot'}
if(!$LoadoutsFile){$LoadoutsFile=Join-Path $taskWorkspace 'outputs/naval-balance-20261024/calibration-loadouts.csv'}
if(!$DecksFile){$DecksFile=Join-Path $PSScriptRoot 'decks.csv'}
if(Test-Path -LiteralPath (Join-Path $taskOutput 'source-hashes.csv')){throw 'Choose a fresh run tag; existing snapshots are immutable.'}
New-Item -ItemType Directory -Force -Path $taskOutput,$taskSnapshot | Out-Null
$taskHashes=foreach($taskFile in (Get-ChildItem -LiteralPath $CoreSource -Filter '*.cs' | Sort-Object Name)){
    $taskHash=(Get-FileHash -LiteralPath $taskFile.FullName).Hash
    $taskCopy=Join-Path $taskSnapshot $taskFile.Name
    Copy-Item -LiteralPath $taskFile.FullName -Destination $taskCopy
    if((Get-FileHash -LiteralPath $taskCopy).Hash -ne $taskHash -or (Get-FileHash -LiteralPath $taskFile.FullName).Hash -ne $taskHash){throw 'Source changed while freezing'}
    [pscustomobject]@{Source=$taskFile.FullName;Snapshot=$taskCopy;SHA256=$taskHash}
}
if(@($taskHashes).Count -lt 140){throw 'Incomplete Core snapshot'}
$taskHashes | Export-Csv -LiteralPath (Join-Path $taskOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
Copy-Item -LiteralPath $LoadoutsFile -Destination (Join-Path $taskOutput 'calibration-loadouts.csv')
Copy-Item -LiteralPath $DecksFile -Destination (Join-Path $taskOutput 'decks.csv')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BoardingProbe.cs') -Destination (Join-Path $taskOutput 'BoardingProbe.cs')
Copy-Item -LiteralPath (Join-Path $taskWorkspace 'tools/BossCalibration20261024/NavalBalanceProbe.cs') -Destination (Join-Path $taskOutput 'NavalBalanceProbe.cs')
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $taskOutput 'RunProbe.ps1')
Get-FileHash -LiteralPath @((Get-ChildItem -LiteralPath $taskOutput -File | Where-Object {$_.Extension -in '.cs','.csv','.ps1'} | Where-Object Name -ne 'source-hashes.csv').FullName) | Export-Csv -LiteralPath (Join-Path $taskOutput 'input-hashes.csv') -NoTypeInformation -Encoding utf8
$taskExe=Join-Path $taskOutput 'BoardingProbe.exe'
$taskArgs=@("$taskUnity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-main:BoardingProbe','-langversion:9','-noconfig','-nostdlib',"-out:$taskExe")
foreach($taskAssembly in @('mscorlib.dll','System.dll','System.Core.dll')){$taskArgs+="-r:$taskUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$taskAssembly"}
$taskArgs+=@($taskHashes | ForEach-Object Snapshot)
$taskArgs+=@('BoardingProbe.cs','NavalBalanceProbe.cs' | ForEach-Object {Join-Path $taskOutput $_})
& "$taskUnity/NetCoreRuntime/dotnet.exe" @taskArgs 2>&1 | Tee-Object -FilePath (Join-Path $taskOutput 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Probe compile failed'}
if($CompileOnly){return}
[pscustomobject]@{Seeds=$Seeds;MaxTurns=$MaxTurns;Fields=$Fields;Durations=$Durations;Owners=$Owners;Schools=$Schools;Bosses=$Bosses;Ranks=$Ranks;Policies=$Policies;FieldOnly=[bool]$FieldOnly;TraceSeeds=$TraceSeeds} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'run-parameters.json') -Encoding utf8
& "$taskUnity/MonoBleedingEdge/bin/mono.exe" $taskExe $taskOutput $Seeds $MaxTurns $Fields $Durations $Owners $Schools $Bosses $Ranks $Policies ([int][bool]$FieldOnly) $TraceSeeds 2>&1 | Tee-Object -FilePath (Join-Path $taskOutput 'run.log')
if($LASTEXITCODE -ne 0){throw 'Probe execution failed'}
foreach($taskRow in $taskHashes){if((Get-FileHash -LiteralPath $taskRow.Snapshot).Hash -ne $taskRow.SHA256){throw 'Snapshot mutated'}}
Write-Output 'PASS: finite paired boarding samples complete.'
