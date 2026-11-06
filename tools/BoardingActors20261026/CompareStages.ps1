param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$CaptureRun='capture1',[ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EvidenceSet='card-actions-final')
$ErrorActionPreference='Stop'
$capture=Join-Path 'D:\Game\outputs\boarding-actors-20261026' ($EvidenceSet+'\'+$CaptureRun)
$report=Get-Content -LiteralPath (Join-Path $capture 'report.json') -Raw|ConvertFrom-Json
$inputs=[Collections.Generic.List[string]]::new();$stages=[Collections.Generic.List[object]]::new()
foreach($row in @(@{Sequence='attack_hit';Times=@(0,.11,.27,1.3);Label='Original / windup / cut / original'},@{Sequence='cast_hit';Times=@(0,.22,.38,1.3);Label='Original / raise hands / release / original'})){
    $frames=@($report.frames|Where-Object {$_.sequence -eq $row.Sequence -and $_.view -eq 'player'})
    foreach($seconds in $row.Times){
        $selected=$frames|Sort-Object {[Math]::Abs([double]$_.seconds-$seconds)}|Select-Object -First 1
        if(!$selected){throw 'Required action frames missing.'}
        $inputs.Add((Join-Path $capture $selected.file));$stages.Add([pscustomobject]@{Sequence=$row.Sequence;File=$selected.file;Seconds=$selected.seconds;RowLabels=$row.Label})
    }
}
$ffmpeg='D:\Game\tmp\boarding-video-tools\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe';$arguments=@('-nostdin','-y','-loglevel','error')
foreach($inputFile in $inputs){$arguments+=@('-i',$inputFile)}
$target=Join-Path $capture '动作阶段对比.png'
$arguments+=@('-filter_complex','[0:v][1:v][2:v][3:v]hstack=inputs=4[a];[4:v][5:v][6:v][7:v]hstack=inputs=4[b];[a][b]vstack=inputs=2','-frames:v','1',$target)
$process=Start-Process -FilePath $ffmpeg -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $target)){throw 'Action stage comparison failed.'}
$stages.ToArray()|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $capture 'stage-comparison.json') -Encoding utf8
Write-Output $target