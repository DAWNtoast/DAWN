$ErrorActionPreference='Stop'
# Retired by user request; exit before filesystem or Unity actions.
throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.'
$taskRoot=[IO.Path]::GetFullPath('D:\Game\UnityClient')
$taskExportRoot='D:\Game\Artifacts\ComponentShopImplementation20261012\preview-export'
$taskReport=Get-Content -LiteralPath (Join-Path $taskExportRoot 'export-report.json') -Raw | ConvertFrom-Json
$taskBackup='D:\Game\Artifacts\ComponentShopImplementation20261012\baseline-previews'
foreach($taskScene in $taskReport.originalScenes){
 $taskCurrent=Join-Path $taskRoot $taskScene.path
 if((Get-FileHash -LiteralPath $taskCurrent -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskScene.sha256){throw ('Scene changed since capture: '+$taskScene.path)}
}
$taskFiles=@()
foreach($taskRelative in $taskReport.files){
 $taskTarget=[IO.Path]::GetFullPath((Join-Path $taskRoot $taskRelative))
 if(-not $taskTarget.StartsWith($taskRoot+'\Assets\_Project\')){throw ('Unexpected target: '+$taskTarget)}
 $taskSource=Join-Path $taskExportRoot $taskRelative
 if(-not(Test-Path -LiteralPath $taskSource -PathType Leaf)){throw ('Missing export: '+$taskSource)}
 if(Test-Path -LiteralPath $taskTarget){
  $taskBefore=Join-Path $taskBackup $taskRelative
  New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskBefore)) -Force | Out-Null
  Copy-Item -LiteralPath $taskTarget -Destination $taskBefore
 }
 New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskTarget)) -Force | Out-Null
 Copy-Item -LiteralPath $taskSource -Destination $taskTarget
 $taskFiles += [pscustomobject]@{path=$taskRelative;sha256=(Get-FileHash -LiteralPath $taskTarget -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$taskFiles | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $taskExportRoot 'installed-files.json') -Encoding utf8
Write-Output ('Installed '+$taskReport.scenes.Count+' shop preview scenes and '+$taskReport.assets.Count+' derived asset files. Original scene GUIDs preserved.')
