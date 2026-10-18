param(
 [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Name,
 [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedManifestSHA256,
 [ValidateSet('FinalCandidate','FinalCandidate02')][string]$CandidateName='FinalCandidate'
)
$ErrorActionPreference='Stop'
if(@(Get-CimInstance Win32_Process|Where-Object {$_.Name -eq 'Unity.exe' -or $_.Name -eq 'MemoryAudit.exe'}).Count){throw 'Verify only after the serialized Unity/diagnostic Player process has exited.'}
$candidateProject=[IO.Path]::GetFullPath('D:/Game/tmp/runtime-memory-release-20261014').TrimEnd('\','/')
$candidateRoot='D:/Game/Artifacts/MemoryRelease20261015'
$candidateManifest=Join-Path (Join-Path $candidateRoot $CandidateName) 'source-manifest.json'
$candidateSealPath=Join-Path (Join-Path $candidateRoot $CandidateName) 'seal-result.json'
$candidateEvidence=Join-Path (Join-Path $candidateRoot 'FinalCandidateChecks') $Name
if(Test-Path -LiteralPath $candidateEvidence){throw 'Preserve prior source verification evidence.'}
if(-not(Test-Path -LiteralPath $candidateManifest -PathType Leaf)){throw 'FinalCandidate must be sealed first.'}
$candidateManifestHash=(Get-FileHash -LiteralPath $candidateManifest).Hash
if($candidateManifestHash -ne $ExpectedManifestSHA256){throw 'Final candidate manifest changed.'}
$candidateSeal=Get-Content -LiteralPath $candidateSealPath -Raw|ConvertFrom-Json
if($candidateSeal.Result -ne 'PASS' -or $candidateSeal.Version -ne '0.3.20261015.1' -or $candidateSeal.SourceManifestSHA256 -ne $candidateManifestHash){throw 'Final candidate seal does not match.'}
$candidateEntries=@(Get-Content -LiteralPath $candidateManifest -Raw|ConvertFrom-Json)
if($candidateEntries.Count -ne [int]$candidateSeal.Files){throw 'Sealed manifest count mismatch.'}
$candidateExpected=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($candidateEntry in $candidateEntries){
 $candidateRelative=([string]$candidateEntry.Path).Replace('/','\')
 $candidateResolved=[IO.Path]::GetFullPath((Join-Path $candidateProject $candidateRelative))
 if([IO.Path]::IsPathRooted($candidateRelative) -or $candidateRelative -notmatch '^(Assets|Packages|ProjectSettings)\\' -or -not $candidateResolved.StartsWith($candidateProject+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe or unexpected manifest path.'}
 if(-not $candidateExpected.Add($candidateRelative)){throw 'Duplicate sealed path.'}
}
[IO.Directory]::CreateDirectory($candidateEvidence)|Out-Null
$candidateRows=@(foreach($candidateEntry in $candidateEntries){
 $candidatePath=Join-Path $candidateProject $candidateEntry.Path
 $candidateExists=Test-Path -LiteralPath $candidatePath -PathType Leaf
 $candidateHash=if($candidateExists){(Get-FileHash -LiteralPath $candidatePath).Hash}else{$null}
 $candidateLength=if($candidateExists){(Get-Item -LiteralPath $candidatePath).Length}else{$null}
 [pscustomobject]@{Path=$candidateEntry.Path;ExpectedSHA256=$candidateEntry.SHA256;ActualSHA256=$candidateHash;ExpectedLength=$candidateEntry.Length;ActualLength=$candidateLength;Match=$candidateExists -and $candidateHash -eq $candidateEntry.SHA256 -and $candidateLength -eq $candidateEntry.Length}
})
$candidateExtras=@(foreach($candidatePart in @('Assets','Packages','ProjectSettings')){
 foreach($candidateFile in Get-ChildItem -LiteralPath (Join-Path $candidateProject $candidatePart) -File -Recurse -Force){
  $candidateRelative=$candidateFile.FullName.Substring($candidateProject.Length+1)
  # Match the explicitly documented Seal.ps1 exception for Unity's empty-folder
  # metadata. No retired preview content is allowed, copied, or regenerated.
  if($candidateRelative -eq 'Assets\_Project\Scenes\UIPreviewsForMe.meta'){continue}
  if(-not $candidateExpected.Contains($candidateRelative)){[pscustomobject]@{Path=$candidateRelative;Length=$candidateFile.Length;SHA256=(Get-FileHash -LiteralPath $candidateFile.FullName).Hash}}
 }
})
$candidateChanges=@($candidateRows|Where-Object {-not $_.Match})
$candidateRows|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $candidateEvidence 'file-parity.json') -Encoding utf8
ConvertTo-Json -InputObject $candidateChanges -Depth 4|Set-Content -LiteralPath (Join-Path $candidateEvidence 'changed-or-missing.json') -Encoding utf8
ConvertTo-Json -InputObject $candidateExtras -Depth 4|Set-Content -LiteralPath (Join-Path $candidateEvidence 'unexpected-files.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $candidateProject 'ProjectSettings/ProjectSettings.asset') -Destination (Join-Path $candidateEvidence 'ProjectSettings.observed.asset')
$candidateFinalManifestHash=(Get-FileHash -LiteralPath $candidateManifest).Hash
$candidatePassed=$candidateChanges.Count -eq 0 -and $candidateExtras.Count -eq 0 -and $candidateFinalManifestHash -eq $ExpectedManifestSHA256
[pscustomobject]@{Result=if($candidatePassed){'PASS'}else{'FAIL'};Project=$candidateProject;ManifestSHA256=$candidateManifestHash;Files=$candidateRows.Count;ChangedOrMissing=$candidateChanges.Count;UnexpectedFiles=$candidateExtras.Count;RetiredEmptyFolderMetaPresent=(Test-Path -LiteralPath (Join-Path $candidateProject 'Assets/_Project/Scenes/UIPreviewsForMe.meta'));CheckedUtc=[DateTime]::UtcNow.ToString('o');Policy='Read-only verification. No sync, overwrite, rollback or reseal. Inspect and explain any normalization differences before continuing.'}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $candidateEvidence 'result.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $candidateEvidence 'result.json')
if(-not $candidatePassed){throw 'Frozen release project differs from FinalCandidate; preserve evidence and stop for diagnosis.'}
