param([string]$OutputRoot='')
$ErrorActionPreference='Stop'
$taskWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputRoot){$OutputRoot=Join-Path $taskWorkspace 'outputs/boarding-field-balance-20261024'}
$taskFinal=Join-Path $OutputRoot 'experiments/combined-final'
$taskProduction=Join-Path $taskWorkspace 'UnityClient/Assets/_Project/Core/Runtime'
$taskAnalysis=Join-Path $OutputRoot 'final-analysis'
function SampleKey($r){return @($r.boss,$r.tier,$r.rank,$r.school,$r.policy,$r.field,$r.owner,$r.duration,$r.seed)-join '|'}
$taskIdentity=@(foreach($taskFile in (Get-ChildItem -LiteralPath $taskFinal -Filter '*.cs' | Sort-Object Name)){
    [pscustomobject]@{File=$taskFile.Name;FinalSHA256=(Get-FileHash -LiteralPath $taskFile.FullName).Hash;CSHA256=(Get-FileHash -LiteralPath (Join-Path $OutputRoot ('experiments/combined-c/'+$taskFile.Name))).Hash;ProductionSHA256=(Get-FileHash -LiteralPath (Join-Path $taskProduction $taskFile.Name)).Hash}
})
$taskDifferences=@($taskIdentity|Where-Object {$_.FinalSHA256 -ne $_.CSHA256})
if($taskIdentity.Count -ne 147 -or @(Get-ChildItem -LiteralPath $taskProduction -Filter '*.cs').Count -ne 147 -or @($taskIdentity|Where-Object {$_.FinalSHA256 -ne $_.ProductionSHA256}).Count -ne 0 -or $taskDifferences.Count -ne 1 -or $taskDifferences[0].File -ne 'CoreBattleStatusPresentation.cs'){throw 'Unexpected production or matrix source mismatch'}
$taskIdentity|Export-Csv (Join-Path $taskAnalysis 'production-source-identity.csv') -NoTypeInformation -Encoding utf8
[pscustomobject]@{Files=$taskIdentity.Count;ProductionExactMatch=$true;DifferencesFromFullMatrixSource=@($taskDifferences.File);FullMatrix='combined-c';FinalSnapshot='combined-final';Comment='Only UI status presentation changed after final matrix. Matching-key production smoke is recorded separately.'}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $taskAnalysis 'production-source-identity.json') -Encoding utf8
$taskMap=@{}
foreach($taskRow in (Import-Csv (Join-Path $OutputRoot 'final-all/runs.csv'))){$taskKey=SampleKey $taskRow;if($taskMap.ContainsKey($taskKey)){throw 'Duplicate matrix key'};$taskMap[$taskKey]=$taskRow}
$taskSmoke=@(Import-Csv (Join-Path $OutputRoot 'final-production-smoke/runs.csv'));$taskSeen=@{}
if($taskSmoke.Count -ne 12){throw 'Expected 12 final smoke samples'}
foreach($taskRow in $taskSmoke){$taskKey=SampleKey $taskRow;if(!$taskMap.ContainsKey($taskKey) -or $taskSeen.ContainsKey($taskKey)){throw 'Missing/duplicate smoke key'};$taskSeen[$taskKey]=$true;foreach($taskProperty in $taskRow.PSObject.Properties.Name){if($taskRow.$taskProperty -ne $taskMap[$taskKey].$taskProperty){throw ('Smoke mismatch: '+$taskKey+' '+$taskProperty)}}}
$taskChecks=@(Import-Csv (Join-Path $OutputRoot 'final-production-smoke/source-hashes.csv'))
foreach($taskHash in $taskChecks){if((Get-FileHash -LiteralPath $taskHash.Snapshot).Hash -ne $taskHash.SHA256 -or (Get-FileHash -LiteralPath (Join-Path $taskProduction ([IO.Path]::GetFileName($taskHash.Source)))).Hash -ne $taskHash.SHA256){throw 'Smoke production source mismatch'}}
if($taskChecks.Count -ne 147){throw 'Incomplete final smoke source'}
[pscustomobject]@{ComparedSamples=$taskSmoke.Count;ComparedProperties=$taskSmoke[0].PSObject.Properties.Name.Count;Differences=0;SourceFiles=$taskChecks.Count;ProductionExactMatch=$true;Source='combined-final';ComparedAgainst='final-all (combined-c)';Result='All result fields identical; only CoreBattleStatusPresentation.cs differs from C matrix source'}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $taskAnalysis 'production-smoke-pairing.json') -Encoding utf8
Write-Output 'PASS: 147 final Core sources match production; all 35 result fields in 12 same-key smoke samples match final C matrix.'
