param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Name='OptimizedControlled01')
$ErrorActionPreference='Stop'
if(@(Get-CimInstance Win32_Process|Where-Object {$_.Name -eq 'Unity.exe' -or $_.Name -eq 'MemoryAudit.exe'}).Count){throw 'Exclusive Unity/Player slot required.'}
$memoryRoot='D:\Game\Artifacts\MemoryRelease20261015'
$memoryProject='D:\Game\tmp\runtime-memory-probe-20261014'
$memoryFrozen='D:\Game\tmp\runtime-memory-release-20261014'
$memoryEvidence=Join-Path $memoryRoot ($Name+'-Patch')
if(Test-Path -LiteralPath $memoryEvidence){throw 'Preserve existing evidence.'}
$memoryBaseline=@(Get-Content -LiteralPath (Join-Path $memoryRoot 'Baseline-Snapshot/source-manifest.json') -Raw|ConvertFrom-Json)
$memoryFreeze=@(Get-Content -LiteralPath (Join-Path $memoryRoot 'Validation/RegressionReady02-Snapshot/source-manifest.json') -Raw|ConvertFrom-Json)
$memoryNames=@('CoreGameplayResourceMemory.cs','CoreGameplayAudioController.cs','CoreGameplayRamComicPresentation.cs','CoreGameplayBattleAnimationController.cs')
$memoryTargets=@($memoryNames|ForEach-Object {'Assets\_Project\Presentation\Runtime\'+$_})
New-Item -ItemType Directory -Path $memoryEvidence|Out-Null
$memoryBefore=@(foreach($memoryFile in $memoryBaseline){$memoryPath=Join-Path $memoryProject $memoryFile.Path;if(-not(Test-Path -LiteralPath $memoryPath -PathType Leaf)){throw ('Baseline file missing: '+$memoryFile.Path)};$memoryHash=(Get-FileHash -LiteralPath $memoryPath).Hash;[pscustomobject]@{Path=$memoryFile.Path;BaselineSHA256=$memoryFile.SHA256;ActualSHA256=$memoryHash;Changed=$memoryHash -ne $memoryFile.SHA256}})
$memoryBefore|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $memoryEvidence 'baseline-prepatch-parity.json') -Encoding utf8
$memoryImportedBaseline=@{
 'Assets\_Project\Resources\PortFacilities\DepartureCharts4K\chart-golden-trade.png.meta'='AF6EDC9847B71DCDD0BD8565F4E9850A5D499151F417592E931258719E531825';
 'Assets\_Project\Resources\PortFacilities\DepartureCharts4K\chart-moon-ruins.png.meta'='C604EB0D6958FC5C0EC2DB344C7EE241B9A8C368B66C74A81EC02A37A7BFB652';
 'Assets\_Project\Resources\PortFacilities\DepartureCharts4K\chart-shattered-reef.png.meta'='D21368CB74B1A8D811823FCBEB06203066AD6D364B4A86488C3411CF306E9A06';
 'Assets\_Project\Resources\UISurfaces\journal_parchment_v1.png.meta'='1365705A1A15C4F35898C36404041C5EAB49611EEB84D1856BAF7FC0D56B6A93';
 'Assets\_Project\Resources\UISurfaces\nautical_canvas_v1.png.meta'='DD4750A1DDC0868E08FB07FB09CA4D02F676EA2BAAF7141DFF25275F658971D1'
}
# Existing DesktopBackgroundCompressionPolicy normalized these five metas during
# the measured baseline build. Preserve that exact imported baseline state.
$memoryUnexpected=@($memoryBefore|Where-Object {$_.Changed -and $_.Path -ne 'ProjectSettings\ProjectSettings.asset' -and $memoryImportedBaseline[$_.Path] -ne $_.ActualSHA256})
if($memoryUnexpected.Count){$memoryUnexpected|ConvertTo-Json;throw 'Unexpected changes in locked baseline project.'}
$memorySettings=Join-Path $memoryProject 'ProjectSettings/ProjectSettings.asset'
Copy-Item -LiteralPath $memorySettings -Destination (Join-Path $memoryEvidence 'diagnostic-project-settings-before.asset')
$memoryPatchRows=@(foreach($memoryRelative in $memoryTargets){
 $memoryEntry=@($memoryFreeze|Where-Object Path -eq $memoryRelative)
 if($memoryEntry.Count -ne 1){throw 'Frozen manifest entry missing or ambiguous.'}
 $memoryPath=Join-Path $memoryFrozen $memoryRelative
 if((Get-FileHash -LiteralPath $memoryPath).Hash -ne $memoryEntry[0].SHA256){throw ('Frozen file changed: '+$memoryRelative)}
 $memoryDestination=Join-Path $memoryProject $memoryRelative
 Copy-Item -LiteralPath $memoryDestination -Destination (Join-Path $memoryEvidence ('Baseline.'+[IO.Path]::GetFileName($memoryRelative)))
 Copy-Item -LiteralPath $memoryPath -Destination $memoryDestination -Force
 [pscustomobject]@{Path=$memoryRelative;Source=$memoryPath;FrozenSHA256=$memoryEntry[0].SHA256;AfterSHA256=(Get-FileHash -LiteralPath $memoryDestination).Hash}
})
$memoryPatchRows|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $memoryEvidence 'four-file-patch.json') -Encoding utf8
$memoryAfter=@(foreach($memoryFile in $memoryBefore){$memoryHash=(Get-FileHash -LiteralPath (Join-Path $memoryProject $memoryFile.Path)).Hash;[pscustomobject]@{Path=$memoryFile.Path;Before=$memoryFile.ActualSHA256;After=$memoryHash;Changed=$memoryFile.ActualSHA256 -ne $memoryHash}})
$memoryAfter|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $memoryEvidence 'postpatch-parity.json') -Encoding utf8
$memoryChanged=@($memoryAfter|Where-Object Changed)
if($memoryChanged.Count -ne 4 -or @($memoryChanged|Where-Object {$_.Path -notin $memoryTargets}).Count){throw 'Only the four memory runtime files may differ.'}
[pscustomobject]@{Result='PASS';Scope='Controlled comparison: measured baseline import state with only four frozen memory runtime fixes. Five metas normalized by the existing desktop compression policy during baseline build and diagnostic identity settings remain unchanged by this patch. Not the full final release snapshot.';BaselineFiles=$memoryBaseline.Count;PreExistingBaselineBuildChanges=@($memoryBefore|Where-Object Changed);FrozenManifest='Validation/RegressionReady02-Snapshot/source-manifest.json';Changed=$memoryChanged;CreatedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $memoryEvidence 'patch-result.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $memoryEvidence 'patch-result.json')
