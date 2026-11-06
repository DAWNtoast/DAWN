param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$CaptureRun='capture1',[ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EvidenceSet='card-actions-final')
$ErrorActionPreference='Stop'
$root='D:\Game\outputs\boarding-actors-20261026'
$capture=Join-Path $root ($EvidenceSet+'\'+$CaptureRun)
$media=Join-Path $root $EvidenceSet
$report=Get-Content -LiteralPath (Join-Path $capture 'report.json') -Raw|ConvertFrom-Json
$frames=@($report.frames|ForEach-Object {if(!(Test-Path -LiteralPath (Join-Path $capture $_.file))){throw ('Missing frame '+$_.file)};[pscustomobject]@{file=($EvidenceSet+'/'+$CaptureRun+'/'+$_.file);view=$_.view;sequence=$_.sequence;seconds=$_.seconds;duration=$_.duration}})|ConvertTo-Json -Compress
$ffmpeg='D:\Game\tmp\boarding-video-tools\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe'
if(!(Test-Path -LiteralPath $ffmpeg)){throw 'Existing shared ffmpeg helper is unavailable.'}
$oldMedia=Join-Path $root 'previous-continuous-idle-media'
if(!(Test-Path -LiteralPath $oldMedia)){
    New-Item -ItemType Directory -Path $oldMedia|Out-Null
    foreach($name in @('角色待机-双方.gif','角色待机-我方.gif','角色待机-敌方.gif','角色出牌与受击.gif')){if(Test-Path -LiteralPath (Join-Path $root $name)){Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $oldMedia $name)}}
    '旧版持续呼吸/摆动预览，仅保留历史验收证据。当前游戏待机完全静止，请打开上一级角色动态预览.html。'|Set-Content -LiteralPath (Join-Path $oldMedia '旧版说明.txt') -Encoding utf8
}
$gifDir=Join-Path $media 'gifs';$videoDir=Join-Path $media 'videos'
New-Item -ItemType Directory -Path $gifDir,$videoDir -Force|Out-Null
$movies=@(@{Sequence='idle';View='both';File='角色待机-双方.gif'},@{Sequence='idle';View='player';File='角色待机-我方.gif'},@{Sequence='idle';View='enemy';File='角色待机-敌方.gif'},@{Sequence='attack_hit';View='both';File='角色出刀.gif'},@{Sequence='cast_hit';View='both';File='角色施法.gif'},@{Sequence='guard';View='player';File='角色格挡.gif'},@{Sequence='heal';View='player';File='角色治疗.gif'},@{Sequence='prepare';View='player';File='角色准备.gif'})
foreach($movie in $movies){
    $items=@($report.frames|Where-Object {$_.sequence -eq $movie.Sequence -and $_.view -eq $movie.View})
    if(($movie.Sequence -ne 'idle' -and $items.Count -lt 24) -or $items.Count -lt 2){throw 'Animation sequence is incomplete.'}
    $rate=$items.Count/[double]$items[0].duration
    $sequenceInput=Join-Path $capture ($movie.Sequence+'_'+$movie.View+'_%02d.jpg');$output=Join-Path $gifDir $movie.File
    $arguments=@('-nostdin','-y','-loglevel','warning','-framerate',$rate.ToString([Globalization.CultureInfo]::InvariantCulture),'-i',$sequenceInput,'-filter_complex','[0:v]split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a')
    if($movie.Sequence -eq 'idle'){$arguments+=@('-frames:v','1')}
    $arguments+=@('-loop','0',$output)
    $log=Join-Path $capture ($movie.Sequence+'_'+$movie.View+'-gif.log')
    $process=Start-Process -FilePath $ffmpeg -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -Wait -PassThru -RedirectStandardError $log
    if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $output) -or (Get-Item -LiteralPath $output).Length -eq 0){throw ('GIF failed: '+$movie.File)}
}
$sequences=@($report.frames.sequence|Select-Object -Unique)
foreach($sequence in $sequences){foreach($view in @('both','player','enemy','full')){
    $items=@($report.frames|Where-Object {$_.sequence -eq $sequence -and $_.view -eq $view});$rate=$items.Count/[double]$items[0].duration
    $sequenceInput=Join-Path $capture ($sequence+'_'+$view+'_%02d.jpg');$output=Join-Path $videoDir ($sequence+'_'+$view+'.mp4')
    $arguments=@('-nostdin','-y','-loglevel','warning','-framerate',$rate.ToString([Globalization.CultureInfo]::InvariantCulture),'-i',$sequenceInput,'-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',$output)
    $log=Join-Path $capture ($sequence+'_'+$view+'-mp4.log')
    $process=Start-Process -FilePath $ffmpeg -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -Wait -PassThru -RedirectStandardError $log
    if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $output) -or (Get-Item -LiteralPath $output).Length -eq 0){throw ('MP4 failed: '+$sequence+'_'+$view)}
}}
foreach($name in @('角色待机-双方.gif','角色待机-我方.gif','角色待机-敌方.gif')){Copy-Item -LiteralPath (Join-Path $gifDir $name) -Destination (Join-Path $root $name) -Force}
Copy-Item -LiteralPath (Join-Path $gifDir '角色施法.gif') -Destination (Join-Path $root '角色出牌与受击.gif') -Force
$template=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'preview-template.html') -Raw
[IO.File]::WriteAllText((Join-Path $root '角色动态预览.html'),$template.Replace('__FRAMES__',$frames).Replace('__MEDIA_BASE__',$EvidenceSet),[Text.UTF8Encoding]::new($false))
Write-Output (Join-Path $root '角色动态预览.html')