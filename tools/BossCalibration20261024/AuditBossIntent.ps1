param(
    [string]$CoreSource='',
    [string]$Output=''
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$CoreSource){$CoreSource=Join-Path $taskRoot 'outputs/boss-calibration-20261024/Inputs/CoreSnapshot'}
if(!$Output){$Output=Join-Path $taskRoot 'outputs/boss-calibration-20261024/intent-before'}
$taskUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$taskFiles=@(Get-ChildItem -LiteralPath $CoreSource -Filter '*.cs' | Sort-Object Name)
if($taskFiles.Count -lt 140){throw 'Incomplete Core source'}
$taskHashes=@($taskFiles | ForEach-Object {[pscustomobject]@{Source=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$taskHashes | Export-Csv -LiteralPath (Join-Path $Output 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$taskExe=Join-Path $Output 'AuditBossIntent.exe'
$taskArgs=@("$taskUnity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-main:AuditBossIntent','-langversion:9','-noconfig','-nostdlib',"-out:$taskExe")
foreach($taskAssembly in @('mscorlib.dll','System.dll','System.Core.dll')){$taskArgs+="-r:$taskUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$taskAssembly"}
$taskArgs+=@($taskFiles.FullName)
$taskArgs+=(Join-Path $PSScriptRoot 'AuditBossIntent.cs')
& "$taskUnity/NetCoreRuntime/dotnet.exe" @taskArgs 2>&1 | Tee-Object -FilePath (Join-Path $Output 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Audit compilation failed'}
& "$taskUnity/MonoBleedingEdge/bin/mono.exe" $taskExe $Output 2>&1 | Tee-Object -FilePath (Join-Path $Output 'run.log')
$taskExit=$LASTEXITCODE
foreach($taskHash in $taskHashes){if((Get-FileHash -LiteralPath $taskHash.Source).Hash -ne $taskHash.SHA256){throw 'Core source changed during audit'}}
if($taskExit -ne 0){throw 'Intent consistency audit has failures; inspect CSV'}
