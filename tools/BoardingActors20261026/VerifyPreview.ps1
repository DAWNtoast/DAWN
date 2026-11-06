param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$CaptureRun='capture1',[ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EvidenceSet='card-actions-final')
$ErrorActionPreference='Stop'
$root='D:\Game\outputs\boarding-actors-20261026'
$capture=Join-Path $root ($EvidenceSet+'\'+$CaptureRun);$media=Join-Path $root $EvidenceSet
$report=Get-Content -LiteralPath (Join-Path $capture 'report.json') -Raw|ConvertFrom-Json
$manifest=@($report.frames|ForEach-Object {$path=Join-Path $capture $_.file;[pscustomobject]@{File=$_.file;Sequence=$_.sequence;View=$_.view;Seconds=$_.seconds;Width=$_.width;Height=$_.height;SHA256=(Get-FileHash -LiteralPath $path).Hash;Bytes=(Get-Item -LiteralPath $path).Length}})
$manifest|ConvertTo-Json -Depth 3|Set-Content -LiteralPath (Join-Path $capture 'frame-integrity.json') -Encoding utf8
$motion=@()
foreach($group in $manifest|Group-Object Sequence,View){
    $items=@($group.Group);$unique=@($items.SHA256|Select-Object -Unique).Count;$returnExact=$items[0].SHA256 -eq $items[-1].SHA256
    if($items[0].Sequence -eq 'idle' -or ($items[0].View -eq 'enemy' -and $items[0].Sequence -in @('guard','heal','prepare'))){if($unique -ne 1){throw ('Inactive actor view changed: '+$group.Name)}}
    elseif($unique -lt 4 -or !$returnExact){throw ('Action must visibly move and return to exactly the first frame: '+$group.Name)}
    $motion+=[pscustomobject]@{Sequence=$items[0].Sequence;View=$items[0].View;Frames=$items.Count;UniqueFrames=$unique;ReturnsToOriginalPixels=$returnExact}
}
$motion|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $capture 'motion-checks.json') -Encoding utf8
$ffmpeg='D:\Game\tmp\boarding-video-tools\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe';$results=@()
foreach($video in Get-ChildItem -LiteralPath (Join-Path $media 'videos') -Filter '*.mp4' -File){
    $outlog=Join-Path $capture ($video.BaseName+'-decode.log');$errlog=Join-Path $capture ($video.BaseName+'-decode-error.log')
    $arguments=@('-nostdin','-v','error','-i',$video.FullName,'-progress','pipe:1','-nostats','-f','null','NUL')
    $process=Start-Process -FilePath $ffmpeg -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $outlog -RedirectStandardError $errlog
    if($process.ExitCode -ne 0){throw ('Video decode failed: '+$video.Name)}
    $last=@(Get-Content -LiteralPath $outlog|Where-Object {$_ -match '^frame='})[-1];$count=[int]$last.Split('=')[1]
    $expected=@($report.frames|Where-Object {($_.sequence+'_'+$_.view+'.mp4') -eq $video.Name}).Count
    if($count -ne $expected){throw ('Decoded frame count mismatch: '+$video.Name)}
    $results+=[pscustomobject]@{File=$video.Name;Frames=$count;Bytes=$video.Length;SHA256=(Get-FileHash -LiteralPath $video.FullName).Hash;DecodeExitCode=$process.ExitCode}
}
if($results.Count -ne $motion.Count){throw 'Missing video views.'}
$results|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $capture 'video-verification.json') -Encoding utf8
Write-Output ('Verified '+$manifest.Count+' frames, '+$results.Count+' videos: idle exactly unchanged and every action returns to its first-frame pixels.')