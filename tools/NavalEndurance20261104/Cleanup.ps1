$ErrorActionPreference='Stop'
$taskBuild=[IO.Path]::GetFullPath('D:/Game/outputs/naval-endurance-20261104/Build')
$taskAllowed=[IO.Path]::GetFullPath('D:/Game/outputs/naval-endurance-20261104/Build')
if($taskBuild -cne $taskAllowed){throw 'Unexpected build path'}
$taskBytes=0L
if(Test-Path -LiteralPath $taskBuild){
 foreach($taskPath in @('D:/Game','D:/Game/outputs','D:/Game/outputs/naval-endurance-20261104',$taskBuild)){if((Get-Item -LiteralPath $taskPath).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked cleanup path'}}
 $taskBusy=@(Get-CimInstance Win32_Process | Where-Object {$_.Name -in @('mono.exe','dotnet.exe','Unity.exe') -and $_.CommandLine -and ($_.CommandLine.Contains($taskBuild) -or $_.CommandLine.Contains($taskBuild.Replace('\','/')))})
 if($taskBusy.Count){throw 'Build still in use'}
 foreach($taskFile in Get-ChildItem -LiteralPath $taskBuild -Force){if($taskFile.PSIsContainer -or $taskFile.Attributes -band [IO.FileAttributes]::ReparsePoint -or $taskFile.Extension -notin @('.exe','.dll','.pdb','.mdb')){throw 'Unexpected build content'};$taskBytes+=$taskFile.Length;Remove-Item -LiteralPath $taskFile.FullName -Force}
 Remove-Item -LiteralPath $taskBuild
}
if(Test-Path -LiteralPath $taskBuild){throw 'Cleanup incomplete'}
[pscustomobject]@{ReleasedBytes=$taskBytes;BuildRemoved=$true;MainProjectChangedByCleanup=$false;Retained='Program, runner, source hashes, per-action logs, result JSON, deck and summary'}|ConvertTo-Json|Set-Content -LiteralPath 'D:/Game/outputs/naval-endurance-20261104/cleanup.json' -Encoding utf8
