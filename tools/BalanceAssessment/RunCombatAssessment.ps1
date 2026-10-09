param([int]$Seeds=16,[string]$Output='D:/Game/UnityClient/Artifacts/BalanceAssessment20261003/Combat')
$ErrorActionPreference='Stop'
$auditWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditData='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$auditSnapshot=Join-Path $Output 'CoreSnapshot'
$auditBuild=Join-Path $Output 'Build'
if(Test-Path -LiteralPath (Join-Path $Output 'source-hashes.csv')){throw 'Use a fresh output directory.'}
New-Item -ItemType Directory -Force -Path $auditSnapshot,$auditBuild | Out-Null
$auditHashes=foreach($f in Get-ChildItem -LiteralPath (Join-Path $auditWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs') {
 $target=Join-Path $auditSnapshot $f.Name
 $before=(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
 Copy-Item -LiteralPath $f.FullName -Destination $target
 $copy=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
 $after=(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
 if($before -ne $copy -or $after -ne $copy){throw 'Source changed while copying'}
 [pscustomobject]@{Name=$f.Name;SourceSHA256=$before;CompiledSHA256=$copy;Identical=$true}
}
$auditHashes | Export-Csv -LiteralPath (Join-Path $Output 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$auditCompiler=Join-Path $auditData 'DotNetSdkRoslyn/csc.dll'
$auditRuntime=Join-Path $auditData 'NetCoreRuntime/dotnet.exe'
$auditMono=Join-Path $auditData 'MonoBleedingEdge/bin/mono.exe'
$auditFramework=Join-Path $auditData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$auditRefs=@('mscorlib.dll','System.dll','System.Core.dll')|ForEach-Object {'-r:'+(Join-Path $auditFramework $_)}
$auditDll=Join-Path $auditBuild 'Production.Core.dll'
$auditArgs=@($auditCompiler,'-nologo','-target:library','-langversion:9','-noconfig','-nostdlib',"-out:$auditDll")+$auditRefs+@(Get-ChildItem -LiteralPath $auditSnapshot -Filter '*.cs'|ForEach-Object FullName)
& $auditRuntime @auditArgs
if($LASTEXITCODE -ne 0){throw 'Core compile failed'}
$auditSimulator=[IO.File]::ReadAllText((Join-Path $auditWorkspace 'Tools/BattleBalance/ShipProgressionSimulator.cs'))
$auditSimulator=$auditSimulator.Replace('public static int SpecialistCount;','public static int SpecialistCount; public static int MedicineSupply = 10;')
if(-not $auditSimulator.Contains('session.Cargo.Medicine = 10;')){throw 'Simulator medicine fixture signature changed'}
$auditSimulator=$auditSimulator.Replace('session.Cargo.Medicine = 10;','session.Cargo.Medicine = MedicineSupply;')
[IO.File]::WriteAllText((Join-Path $auditBuild 'ShipProgressionSimulator.cs'),$auditSimulator)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CombatAssessmentProgram.cs') -Destination (Join-Path $auditBuild 'CombatAssessmentProgram.cs')
$auditExe=Join-Path $auditBuild 'CombatAssessment.exe'
$auditArgs=@($auditCompiler,'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$auditExe","-r:$auditDll")+$auditRefs+@((Join-Path $auditBuild 'ShipProgressionSimulator.cs'),(Join-Path $auditBuild 'CombatAssessmentProgram.cs'))
& $auditRuntime @auditArgs
if($LASTEXITCODE -ne 0){throw 'Simulator compile failed'}
[IO.File]::WriteAllText((Join-Path $Output 'provenance.txt'),"Unmodified production Core snapshot, separately compiled DLL. Every source SHA256 verified. Simulator policy is existing ShipProgressionSimulator; only pre-battle fixture medicine supply made configurable (0 or 10). Fixed public-hand/intent greedy policy reads GetBattleCardEffects including actual medicine boost. No Core transformations, equipment bonuses, future cards or within-battle resources. 3 specialists except tutorial; source XP fresh or matched II=360/III=840. Supplies and independent full health are fixtures, not earned voyages. First 3 encounter types/route bosses early tiers 0/I; late 3 route+ruin+5 legendary at tiers II/III, fresh/mature. Same seeds pair medicine cases. Each group $Seeds seeds; tutorial geometry fixed. Captured $([DateTime]::UtcNow.ToString('o')).")
& $auditMono $auditExe $Seeds $Output | Tee-Object -FilePath (Join-Path $Output 'run.log')
if($LASTEXITCODE -ne 0){throw 'Assessment failed'}
