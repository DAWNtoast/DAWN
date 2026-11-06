param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$CaptureRun='capture1',[ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EvidenceSet='card-actions-final')
$ErrorActionPreference='Stop'
$root='D:\Game\outputs\boarding-actors-20261026'
$media=Join-Path $root $EvidenceSet
$capture=[IO.Path]::GetFullPath((Join-Path $root ($EvidenceSet+'\'+$CaptureRun)))
function Assert-TaskFile([string]$path){
    $resolved=[IO.Path]::GetFullPath($path)
    if(!$resolved.StartsWith($capture+'\',[StringComparison]::OrdinalIgnoreCase)){throw ('Escaped capture folder: '+$resolved)}
    for($at=$resolved;$at -and $at.Length -ge $root.Length;$at=Split-Path $at -Parent){if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw ('Linked path: '+$at)}}
}
$active=@(Get-CimInstance Win32_Process | Where-Object {($_.Name -eq 'Unity.exe' -or $_.Name -like 'ffmpeg*') -and $_.CommandLine -and ($_.CommandLine.Contains($root) -or $_.CommandLine.Contains('D:\Game\tmp\shipyard-assembly-20261017'))})
if($active.Count){throw 'Capture or preview process is still active.'}
$record=Join-Path $capture 'raw-frame-cleanup.json'
if(Test-Path -LiteralPath $record){throw 'Frame cleanup already recorded; preserve evidence.'}
$integrity=@(Get-Content -LiteralPath (Join-Path $capture 'frame-integrity.json') -Raw|ConvertFrom-Json)
$report=Get-Content -LiteralPath (Join-Path $capture 'report.json') -Raw|ConvertFrom-Json
$videos=@(Get-Content -LiteralPath (Join-Path $capture 'video-verification.json') -Raw|ConvertFrom-Json)
if($videos.Count -ne @($report.frames|Group-Object sequence,view).Count -or $integrity.Count -ne $report.frames.Count){throw 'Incomplete preview evidence.'}
foreach($video in $videos){
    $path=Join-Path $media ('videos\'+$video.File)
    if($video.DecodeExitCode -ne 0 -or (Get-FileHash -LiteralPath $path).Hash -ne $video.SHA256){throw ('Preview changed or not decoded: '+$path)}
}
foreach($name in @('角色待机-双方.gif','角色待机-我方.gif','角色待机-敌方.gif','角色出刀.gif','角色施法.gif','角色格挡.gif','角色治疗.gif','角色准备.gif')){if((Get-Item -LiteralPath (Join-Path $media ('gifs\'+$name))).Length -eq 0){throw ('Empty final preview: '+$name)}}
if(!(Test-Path -LiteralPath (Join-Path $root '角色动态预览.html'))){throw 'HTML preview missing.'}
foreach($sequence in @($report.frames.sequence|Select-Object -Unique)){foreach($view in @('full','both','player','enemy')){if(!(Test-Path -LiteralPath (Join-Path $capture ($sequence+'_'+$view+'_Keyframe.png')))){throw 'Required PNG missing.'}}}
$freed=0L
foreach($frame in $integrity){
    if($frame.File -notmatch '^(idle|attack_hit|cast_hit|guard|heal|prepare)_(full|both|player|enemy)_[0-9]{2,3}\.jpg$'){throw ('Unexpected frame: '+$frame.File)}
    $path=Join-Path $capture $frame.File;Assert-TaskFile $path
    if((Get-FileHash -LiteralPath $path).Hash -ne $frame.SHA256){throw ('Raw frame changed: '+$frame.File)}
}
foreach($frame in $integrity){$path=Join-Path $capture $frame.File;$freed+=(Get-Item -LiteralPath $path).Length;Remove-Item -LiteralPath $path -Force;if(Test-Path -LiteralPath $path){throw 'Raw frame removal failed.'}}
[pscustomobject]@{FramesRemoved=$integrity.Count;ReclaimedBytes=$freed;Retained='Decoded MP4 clips, static idle GIFs, five action GIFs, all sequence/view PNG keyframes, real PlayMode screenshots, original frame metadata/hashes, source snapshots, logs and tests';MainProjectModified=$false;CompletedAtUtc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json|Set-Content -LiteralPath $record -Encoding utf8
Write-Output ('Removed '+$integrity.Count+' archived raw JPEG frames; reclaimed '+$freed+' bytes.')