param([ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Name='WorkspaceRepairSync')
$ErrorActionPreference='Stop'
$repairEvidence=Join-Path 'D:/Game/Artifacts/MemoryRelease20261015' $Name
if(Test-Path -LiteralPath $repairEvidence){throw 'Preserve previous repair-sync evidence.'}
$repairs=@(Get-Content 'D:/Game/Artifacts/MemoryRelease20261015/FrozenUiValidation/changes-sha256.json' -Raw|ConvertFrom-Json)
$repairs+=@(Get-Content 'D:/Game/Artifacts/MemoryRelease20261015/Validation/CardContractsStatic/ChangedFilesSHA256.json' -Raw|ConvertFrom-Json|ForEach-Object {[pscustomobject]@{Path=('Assets/_Project/Tests/EditMode/'+$_.File);BeforeSHA256=$_.OriginalSHA256;AfterSHA256=$_.NewSHA256}})
foreach($repairGroup in @('FrozenPlayBugRepairs','FrozenPlayUiRepairs','FrozenPlayTradeRepairs','FrozenSmokeRepairs')){
 $repairManifest=Join-Path ('D:/Game/Artifacts/MemoryRelease20261015/'+$repairGroup) 'changes-sha256.json'
 if(Test-Path -LiteralPath $repairManifest){$repairs+=@(Get-Content -LiteralPath $repairManifest -Raw|ConvertFrom-Json)}
}
foreach($repair in $repairs){
 $candidate=Join-Path 'D:/Game/tmp/runtime-memory-release-20261014' $repair.Path
 if((Get-FileHash -LiteralPath $candidate).Hash -ne $repair.AfterSHA256){throw ('Reviewed repair hash changed: '+$repair.Path)}
}
[IO.Directory]::CreateDirectory($repairEvidence)|Out-Null
$repairResults=@(foreach($repair in $repairs){
 $target=Join-Path 'D:/Game/UnityClient' $repair.Path
 $actual=(Get-FileHash -LiteralPath $target).Hash
 if($actual -eq $repair.AfterSHA256){[pscustomobject]@{Path=$repair.Path;Result='Already applied'};continue}
 if($actual -ne $repair.BeforeSHA256){[pscustomobject]@{Path=$repair.Path;Result='Skipped concurrent workspace change'};continue}
 $backup=Join-Path (Join-Path $repairEvidence 'Before') $repair.Path
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($backup))|Out-Null
 Copy-Item -LiteralPath $target -Destination $backup
 Copy-Item -LiteralPath (Join-Path 'D:/Game/tmp/runtime-memory-release-20261014' $repair.Path) -Destination $target
 if((Get-FileHash -LiteralPath $target).Hash -ne $repair.AfterSHA256){throw ('Workspace changed during promotion: '+$repair.Path)}
 [pscustomobject]@{Path=$repair.Path;Result='Applied after original-hash check'}
})
$repairResults|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $repairEvidence 'result.json') -Encoding utf8
$repairResults|Group-Object Result|Select-Object Name,Count
