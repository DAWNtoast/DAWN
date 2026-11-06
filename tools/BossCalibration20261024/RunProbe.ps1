param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$OutputTag,
    [string]$SourceSnapshot='', [string]$LoadoutsFile='', [string]$DecksFile='',
    [int]$Seeds=8, [int]$MaxTurns=14,
    [ValidateSet('dummy','guard-pressure','boss')][string]$Mode='dummy',
    [string]$BossFilter='*', [string]$Schools='*', [string]$Cohorts='base,candidate',
    [switch]$Sources, [switch]$CompileOnly, [switch]$UseSnapshot
)
$ErrorActionPreference='Stop'
$taskWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=Join-Path $taskWorkspace ('outputs/boss-calibration-20261024/'+$OutputTag)
$taskSnapshot=Join-Path $taskOutput 'CoreSnapshot'
$taskUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
if(!$SourceSnapshot){$SourceSnapshot=Join-Path $taskWorkspace 'outputs/boss-calibration-20261024/Inputs/CoreSnapshot'}
if(!$LoadoutsFile){$LoadoutsFile=Join-Path $taskWorkspace 'outputs/naval-balance-20261024/calibration-loadouts.csv'}
if(!$DecksFile){$DecksFile=Join-Path $PSScriptRoot 'candidate-decks.csv'}
if($Seeds -lt 1 -or $MaxTurns -lt 1){throw 'Seeds and MaxTurns must be positive'}
New-Item -ItemType Directory -Force -Path $taskOutput,$taskSnapshot | Out-Null
if(!$UseSnapshot){
    if(Test-Path -LiteralPath (Join-Path $taskOutput 'source-hashes.csv')){throw 'Output snapshot already exists. Choose a fresh tag or use its frozen inputs with -UseSnapshot.'}
    $taskHashes=foreach($taskFile in (Get-ChildItem -LiteralPath $SourceSnapshot -Filter '*.cs' | Sort-Object Name)){
        $taskHash=(Get-FileHash -LiteralPath $taskFile.FullName).Hash
        $taskCopy=Join-Path $taskSnapshot $taskFile.Name
        Copy-Item -LiteralPath $taskFile.FullName -Destination $taskCopy
        if((Get-FileHash -LiteralPath $taskCopy).Hash -ne $taskHash -or (Get-FileHash -LiteralPath $taskFile.FullName).Hash -ne $taskHash){throw 'Source changed during capture'}
        [pscustomobject]@{Source=$taskFile.FullName;Snapshot=$taskCopy;SHA256=$taskHash}
    }
    if(@($taskHashes).Count -lt 140){throw 'Core snapshot incomplete'}
    $taskHashes | Export-Csv -LiteralPath (Join-Path $taskOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
    Copy-Item -LiteralPath $LoadoutsFile -Destination (Join-Path $taskOutput 'calibration-loadouts.csv')
    if(Test-Path -LiteralPath $DecksFile){Copy-Item -LiteralPath $DecksFile -Destination (Join-Path $taskOutput 'decks.csv')}
    foreach($taskHelper in @('SchoolPowerProbe.cs','NavalBalanceProbe.cs','BossBalanceProbe.cs','BossProfileAudit.cs')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskHelper) -Destination (Join-Path $taskOutput $taskHelper)}
    Get-FileHash -LiteralPath @((Get-ChildItem -LiteralPath $taskOutput -File | Where-Object {$_.Extension -in '.cs','.csv'} | Where-Object Name -ne 'source-hashes.csv').FullName) | Export-Csv -LiteralPath (Join-Path $taskOutput 'input-hashes.csv') -NoTypeInformation -Encoding utf8
}
$taskHashes=@(Import-Csv -LiteralPath (Join-Path $taskOutput 'source-hashes.csv'))
foreach($taskRow in $taskHashes){if((Get-FileHash -LiteralPath $taskRow.Snapshot).Hash -ne $taskRow.SHA256){throw 'Frozen Core changed'}}
foreach($taskRow in (Import-Csv -LiteralPath (Join-Path $taskOutput 'input-hashes.csv'))){if((Get-FileHash -LiteralPath $taskRow.Path).Hash -ne $taskRow.Hash){throw 'Frozen tool/input changed'}}
$taskExe=Join-Path $taskOutput 'SchoolPowerProbe.exe'
$taskArgs=@("$taskUnity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-main:SchoolPowerProbe','-langversion:9','-noconfig','-nostdlib',"-out:$taskExe")
foreach($taskAssembly in @('mscorlib.dll','System.dll','System.Core.dll')){$taskArgs+="-r:$taskUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$taskAssembly"}
$taskArgs+=@($taskHashes | ForEach-Object Snapshot)
$taskArgs+=@('SchoolPowerProbe.cs','NavalBalanceProbe.cs','BossBalanceProbe.cs','BossProfileAudit.cs' | ForEach-Object {Join-Path $taskOutput $_})
& "$taskUnity/NetCoreRuntime/dotnet.exe" @taskArgs 2>&1 | Tee-Object -FilePath (Join-Path $taskOutput 'compile.log')
if($LASTEXITCODE -ne 0){throw 'School power tool compilation failed'}
if($CompileOnly){Write-Output 'Compiled frozen Core and probe; no samples run';return}
$taskCohort=if($Sources){'sources'}else{'isolated'}
[pscustomobject]@{Mode=$Mode;Seeds=$Seeds;MaxTurns=$MaxTurns;Sources=[bool]$Sources;BossFilter=$BossFilter;Schools=$Schools;Cohorts=$Cohorts} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'run-parameters.json') -Encoding utf8
& "$taskUnity/MonoBleedingEdge/bin/mono.exe" $taskExe $taskOutput $Seeds $MaxTurns $Mode $taskCohort $BossFilter $Schools $Cohorts 2>&1 | Tee-Object -FilePath (Join-Path $taskOutput 'run.log')
if($LASTEXITCODE -ne 0){throw 'School power samples failed'}
foreach($taskRow in $taskHashes){if((Get-FileHash -LiteralPath $taskRow.Snapshot).Hash -ne $taskRow.SHA256){throw 'Frozen Core changed during run'}}
Write-Output 'PASS: finite paired samples complete; no claim of global win rate or scalar overall power.'
