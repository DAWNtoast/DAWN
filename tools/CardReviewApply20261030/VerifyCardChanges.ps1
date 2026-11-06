param(
 [ValidateSet('baseline','verify')][string]$Mode='verify',
 [string]$Output='D:/Game/outputs/card-review-apply-20261030/validation',
 [string]$UnityData='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference='Stop'
$auditWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditOutput=[IO.Path]::GetFullPath($Output)
$auditBuild=Join-Path $auditOutput ('AuditBuild-'+[Guid]::NewGuid().ToString('N'))
$auditCreated=$false
try {
 New-Item -ItemType Directory -Force -Path $auditOutput | Out-Null
 New-Item -ItemType Directory -Path $auditBuild | Out-Null
 $auditCreated=$true
 $auditCore=Join-Path $auditWorkspace 'UnityClient/Assets/_Project/Core/Runtime'
 if($Mode -eq 'baseline'){$auditCore=Join-Path $auditOutput 'BeforeCore'}
 $auditSources=@(Get-ChildItem -LiteralPath $auditCore -Filter '*.cs' -Recurse | Sort-Object FullName)
 $auditSources+=Get-Item -LiteralPath (Join-Path $PSScriptRoot 'VerifyCardChanges.cs')
 $auditSources+=Get-Item -LiteralPath (Join-Path $PSScriptRoot '../SpecialCards20261017/CatalogResourceCosts.cs')
 foreach($auditName in @('CardSpecialCostPresentation.cs','CardSupplyCostPresentation.cs','CardOrnamentKind.cs')){
  $auditSource=Join-Path $auditWorkspace ('UnityClient/Assets/_Project/Presentation/Runtime/'+$auditName)
  $auditBackup=Join-Path $auditWorkspace ('outputs/card-review-apply-20261030/input-backup/UnityClient/Assets/_Project/Presentation/Runtime/'+$auditName)
  if($Mode -eq 'baseline' -and (Test-Path -LiteralPath $auditBackup)){$auditSource=$auditBackup}
  $auditSources+=Get-Item -LiteralPath $auditSource
 }
 $auditHashes=@($auditSources | ForEach-Object{[pscustomobject]@{Source=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
 $auditHashes | Export-Csv -LiteralPath (Join-Path $auditOutput ($Mode+'-source-hashes.csv')) -Encoding utf8 -NoTypeInformation
 $auditExecutable=Join-Path $auditBuild 'VerifyCardChanges.exe'
 $auditFramework=Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
 $auditArguments=@((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'),'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$auditExecutable")
 foreach($auditAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll','System.Web.Extensions.dll')){$auditArguments+='-r:'+(Join-Path $auditFramework $auditAssembly)}
 $auditArguments+=$auditSources.FullName
 & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @auditArguments 2>&1 | Tee-Object -FilePath (Join-Path $auditOutput ($Mode+'-compile.log'))
 if($LASTEXITCODE -ne 0){throw 'Independent card audit compilation failed.'}
 & (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $auditExecutable $Mode $auditOutput 2>&1 | Tee-Object -FilePath (Join-Path $auditOutput ($Mode+'-runtime.log'))
 $auditExit=$LASTEXITCODE
 foreach($auditHash in $auditHashes){if((Get-FileHash -LiteralPath $auditHash.Source -Algorithm SHA256).Hash -ne $auditHash.SHA256){throw ('Source changed during audit: '+$auditHash.Source)}}
 if($auditExit -ne 0){throw 'Independent card audit found failures; inspect runtime-validation.json.'}
} finally {
 if($auditCreated -and (Test-Path -LiteralPath $auditBuild)){
  try {
   $auditCleanup=[IO.Path]::GetFullPath($auditBuild).TrimEnd('\','/')
   if([IO.Path]::GetDirectoryName($auditCleanup) -ne $auditOutput.TrimEnd('\','/') -or [IO.Path]::GetFileName($auditCleanup) -notmatch '^AuditBuild-[a-f0-9]{32}$'){throw 'Unsafe build cleanup target.'}
   $auditAncestor=Get-Item -LiteralPath $auditCleanup -Force
   while($null -ne $auditAncestor){if(($auditAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Cleanup refuses directory links.'};$auditAncestor=$auditAncestor.Parent}
   $auditItems=@(Get-ChildItem -LiteralPath $auditCleanup -Force)
   foreach($auditItem in $auditItems){if($auditItem.PSIsContainer -or ($auditItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $auditItem.Name -ne 'VerifyCardChanges.exe'){throw ('Unexpected build content: '+$auditItem.FullName)}}
   $auditProcesses=@(Get-CimInstance Win32_Process | Where-Object{$_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.IndexOf($auditCleanup,[StringComparison]::OrdinalIgnoreCase) -ge 0})
   if($auditProcesses.Count -gt 0){throw 'Build output still referenced by another process.'}
   $auditReleased=0L
   foreach($auditItem in $auditItems){$auditStream=[IO.File]::Open($auditItem.FullName,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$auditStream.Dispose();$auditReleased+=$auditItem.Length;Remove-Item -LiteralPath $auditItem.FullName -Force}
   [IO.Directory]::Delete($auditCleanup,$false)
   if(Test-Path -LiteralPath $auditCleanup){throw 'Build output still exists after cleanup.'}
   [pscustomobject]@{Path=$auditCleanup;Removed=$true;ReleasedBytes=$auditReleased;Retained='Core source baseline, compiler/runtime logs, hashes, and JSON evidence'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $auditOutput ($Mode+'-cleanup.json')) -Encoding utf8
  } catch {Write-Warning ('Build retained for inspection: '+$auditBuild+'. '+$_.Exception.Message)}
 }
}
