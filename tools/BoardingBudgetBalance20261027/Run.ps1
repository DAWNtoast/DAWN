param([string]$RunTag='Smoke01',[int]$Seeds=2,[string]$Cohorts='review-pending',[string]$Modes='cannon,direct,soften60,soften55,soften50',[int]$MaxTurns=80,[int]$SeedStart=0,[string]$BossFilter="*",[string]$BoardingDeck="neutral",[switch]$CompileOnly,[string]$CrewScales="1",[string]$AttackScales="1")
$ErrorActionPreference='Stop'
$taskStopwatch=[Diagnostics.Stopwatch]::StartNew()
$root='D:/Game';$source=Join-Path $root 'UnityClient/Assets/_Project';$out=Join-Path $root ('outputs/boarding-budget-balance-20261027/'+$RunTag)
if($RunTag -notmatch '^[A-Za-z0-9_-]+$' -or (Test-Path -LiteralPath $out)){throw 'Fresh safe RunTag required'}
$snapshot=Join-Path $out 'SourceSnapshot';$build=Join-Path $out 'Build';$inputs=Join-Path $out 'Inputs'
New-Item -ItemType Directory -Path $snapshot,$build,$inputs -Force | Out-Null
$files=@((Get-ChildItem -LiteralPath (Join-Path $source 'Core/Runtime') -Filter '*.cs' -File).FullName)
$files+=@((Get-ChildItem -LiteralPath (Join-Path $source 'Content/Runtime') -Filter '*.cs' -File).FullName)
foreach($n in @('ComponentArtCatalog.cs','PortMarketCatalog.cs','PortInvestmentCatalog.cs')){$files+=Join-Path $source ('Presentation/Runtime/'+$n)}
$manifest=@(foreach($f in $files){$rel=$f.Substring($source.Length).TrimStart('\','/');$dest=Join-Path $snapshot $rel;New-Item -ItemType Directory -Path (Split-Path $dest) -Force|Out-Null;$hash=(Get-FileHash -LiteralPath $f).Hash;Copy-Item -LiteralPath $f -Destination $dest;if((Get-FileHash -LiteralPath $f).Hash -ne $hash -or (Get-FileHash -LiteralPath $dest).Hash -ne $hash){throw 'Source changed during capture'};[pscustomobject]@{Source=$f;Snapshot=$dest;SHA256=$hash}})
$manifest|Export-Csv -LiteralPath (Join-Path $out 'source-hashes.csv') -NoTypeInformation -Encoding utf8
foreach($n in @('Program.cs','Run.ps1','UnityDataAdapter.cs','NavalBalanceProbe.cs','AuditAccess.cs','DiagnosticPressure.cs')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $n) -Destination $inputs}
$data=Join-Path $out 'Resources/Data';New-Item -ItemType Directory -Path $data -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $source 'Resources/Data/component_catalog_v2.csv') -Destination $data
$hashInputs=@((Get-ChildItem -LiteralPath $inputs -File).FullName);$hashInputs+=Join-Path $data 'component_catalog_v2.csv'
Get-FileHash -LiteralPath $hashInputs|Export-Csv -LiteralPath (Join-Path $out 'input-hashes.csv') -NoTypeInformation -Encoding utf8
$unity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data';$exe=Join-Path $build 'BattleModes.exe';$args=@("$unity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-main:BattleModes','-langversion:9','-noconfig','-nostdlib',"-out:$exe")
foreach($n in @('mscorlib.dll','System.dll','System.Core.dll','System.Web.Extensions.dll')){$args+="-r:$unity/MonoBleedingEdge/lib/mono/4.7.2-api/$n"}
$args+=@($manifest|ForEach-Object Snapshot);$args+=@((Get-ChildItem -LiteralPath $inputs -Filter '*.cs').FullName)
[pscustomobject]@{RunTag=$RunTag;Seeds=$Seeds;SeedStart=$SeedStart;Partition=$(if($SeedStart -ge 1000){'held-out-validation'}else{'exploration'});Cohorts=$Cohorts;Modes=$Modes;BossFilter=$BossFilter;BoardingDeck=$BoardingDeck;CrewScales=$CrewScales;AttackScales=$AttackScales;MaxTurns=$MaxTurns;CompileOnly=[bool]$CompileOnly;Fixture='Common existing stage assets, incremental purchases only; not a campaign playthrough'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'run-parameters.json') -Encoding utf8
$exitCode=1;$released=0
try {
 & "$unity/NetCoreRuntime/dotnet.exe" @args 2>&1|Tee-Object -FilePath (Join-Path $out 'compile.log')
 if($LASTEXITCODE -ne 0){throw 'Compile failed'}
 if($CompileOnly){$exitCode=0}else{
 & "$unity/MonoBleedingEdge/bin/mono.exe" $exe $out $Seeds $MaxTurns (Split-Path $data) $Cohorts $Modes $SeedStart $BossFilter $BoardingDeck $CrewScales $AttackScales 2>&1|Tee-Object -FilePath (Join-Path $out 'run.log')
 $exitCode=$LASTEXITCODE;if($exitCode -ne 0){throw 'Samples failed'}
 }
 foreach($row in $manifest){if((Get-FileHash -LiteralPath $row.Source).Hash -ne $row.SHA256 -or (Get-FileHash -LiteralPath $row.Snapshot).Hash -ne $row.SHA256){throw 'Source drift'}}
} finally {
 $resolved=[IO.Path]::GetFullPath($build);if($resolved -ne [IO.Path]::GetFullPath((Join-Path $out 'Build')) -or !$resolved.StartsWith([IO.Path]::GetFullPath((Join-Path $root 'outputs/boarding-budget-balance-20261027')),[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe cleanup'}
 if((Get-Item -LiteralPath $build).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked build directory'}
 foreach($f in Get-ChildItem -LiteralPath $build -File){if($f.Extension -notin @('.exe','.dll','.pdb','.mdb')){throw 'Unexpected artifact'};$released+=$f.Length;Remove-Item -LiteralPath $f.FullName -Force}
 if(@(Get-ChildItem -LiteralPath $build -Force).Count -eq 0){Remove-Item -LiteralPath $build}
 [pscustomobject]@{ElapsedSeconds=$taskStopwatch.Elapsed.TotalSeconds;ExitCode=$exitCode;ReleasedBytes=$released;BuildRemaining=(Test-Path -LiteralPath $build);MainProjectModified=$false;UserSavesAccessed=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'cleanup.json') -Encoding utf8
}
exit $exitCode
