param(
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Run='capture1',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EvidenceSet='validation',
    [string]$Ffmpeg='D:/Game/tmp/boarding-video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskRoot=Join-Path $workspace 'outputs/naval-vfx-remaster-20261105'
$capture=[IO.Path]::GetFullPath((Join-Path $taskRoot ($EvidenceSet+'/'+$Run)))
if(!$capture.StartsWith([IO.Path]::GetFullPath($taskRoot)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected capture path.'}
for($at=$capture;$at -and $at.Length -ge $workspace.Length;$at=Split-Path $at -Parent){if((Test-Path -LiteralPath $at)-and((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw ('Linked output: '+$at)}}
if(!(Test-Path -LiteralPath $Ffmpeg)){throw 'Existing ffmpeg tool required; preserve frames until encoder is available.'}
$report=Get-Content -LiteralPath (Join-Path $capture 'report.json') -Raw | ConvertFrom-Json
$log=Join-Path $capture 'video-encoding.log'
if(Test-Path -LiteralPath (Join-Path $capture 'encoded-report.json')){throw 'Video evidence already exists; choose a new run.'}
$manifest=[Collections.Generic.List[object]]::new()
$videos=@()
foreach($sequence in $report.sequences){
    if($sequence.id -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Invalid sequence id.'}
    $frames=@($sequence.frames)
    if($frames.Count -lt 24){throw 'Expected complete capture sequence.'}
    foreach($frame in $frames){
        if([IO.Path]::GetFileName($frame.file)-ne $frame.file -or $frame.file -notmatch ('^'+[Regex]::Escape($sequence.id)+'_\d{3}\.jpg$')){throw 'Unexpected intermediate filename.'}
        $path=Join-Path $capture $frame.file
        if(!(Test-Path -LiteralPath $path)){throw ('Missing frame: '+$path)}
        $item=Get-Item -LiteralPath $path -Force
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked frame.'}
        $manifest.Add([pscustomobject]@{File=$frame.file;SHA256=(Get-FileHash -LiteralPath $path).Hash;Length=$item.Length})
    }
    foreach($keyframe in $sequence.keyframes){if(!(Test-Path -LiteralPath (Join-Path $capture $keyframe))){throw 'Missing retained keyframe.'}}
    $video=$sequence.id+'.mp4';$videoPath=Join-Path $capture $video
    $fps=(($frames.Count-1)/[double]$sequence.duration).ToString('0.########',[Globalization.CultureInfo]::InvariantCulture)
    & $Ffmpeg -hide_banner -nostdin -v error -n -framerate $fps -i (Join-Path $capture ($sequence.id+'_%03d.jpg')) -vf 'fps=60,tpad=stop_mode=clone:stop_duration=0.45' -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -movflags +faststart $videoPath 2>&1 | Tee-Object -FilePath $log -Append
    if($LASTEXITCODE -ne 0){throw ('Encoding failed: '+$sequence.id)}
    & $Ffmpeg -hide_banner -nostdin -v error -i $videoPath -f null - 2>&1 | Tee-Object -FilePath $log -Append
    if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $videoPath)){throw ('Decode verification failed: '+$sequence.id)}
    $sequence.video=$video;$videos+=$video
    # The first nine sequences are neutral hit/guard/miss and all six schools.
    # Encode actual Unity-rendered frames for convenient inline review as well as the full MP4.
    if($videos.Count -le 9) {
        $gif=$sequence.id+'.gif';$gifPath=Join-Path $capture $gif
        & $Ffmpeg -hide_banner -nostdin -v error -n -i $videoPath -filter_complex 'fps=24,scale=800:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a' -loop 0 $gifPath 2>&1 | Tee-Object -FilePath $log -Append
        if($LASTEXITCODE -ne 0){throw ('GIF encoding failed: '+$sequence.id)}
        & $Ffmpeg -hide_banner -nostdin -v error -i $gifPath -f null - 2>&1 | Tee-Object -FilePath $log -Append
        if($LASTEXITCODE -ne 0){throw ('GIF decode verification failed: '+$sequence.id)}
        $sequence | Add-Member -NotePropertyName gif -NotePropertyValue $gif
    }
    ('PASS encode and full decode '+$video) | Add-Content -LiteralPath $log -Encoding utf8
}
$concat=Join-Path $capture 'overview-inputs.txt'
[IO.File]::WriteAllLines($concat,@($videos | ForEach-Object {"file '"+$_+"'"}),[Text.UTF8Encoding]::new($false))
$overview=Join-Path $capture '海战特效总览.mp4'
& $Ffmpeg -hide_banner -nostdin -v error -n -f concat -safe 1 -i $concat -c copy -movflags +faststart $overview 2>&1 | Tee-Object -FilePath $log -Append
if($LASTEXITCODE -ne 0){throw 'Overview encoding failed; frames preserved.'}
& $Ffmpeg -hide_banner -nostdin -v error -i $overview -f null - 2>&1 | Tee-Object -FilePath $log -Append
if($LASTEXITCODE -ne 0){throw 'Overview decode failed; frames preserved.'}
('PASS full overview decode') | Add-Content -LiteralPath $log -Encoding utf8
$manifest | Export-Csv -LiteralPath (Join-Path $capture 'source-frame-hashes.csv') -NoTypeInformation -Encoding utf8
$report | Add-Member -NotePropertyName overview -NotePropertyValue '海战特效总览.mp4'
$report | Add-Member -NotePropertyName frameLifecycle -NotePropertyValue 'Intermediate JPG frames were encoded to validated MP4 videos, then removed. Three PNG keyframes per sequence and frame hashes are retained.'
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $capture 'encoded-report.json') -Encoding utf8
$report.mappings | Export-Csv -LiteralPath (Join-Path $capture '卡牌特效对应表.csv') -NoTypeInformation -Encoding utf8
$json=($report | ConvertTo-Json -Depth 10 -Compress).Replace('</','<\/')
$html=@'
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>海战 · 卡牌特效</title>
<style>:root{color-scheme:dark;font:16px/1.6 "Microsoft YaHei",sans-serif;background:#08131e;color:#e7eff3}*{box-sizing:border-box}body{max-width:1600px;margin:auto;padding:28px}h1{font-size:30px;margin:0 0 8px}p{color:#a8bdca;margin:0 0 18px}.layout{display:grid;grid-template-columns:1fr 300px;gap:18px}video{width:100%;display:block;border:1px solid #354956;background:#050a10;border-radius:9px}.choices{display:grid;gap:7px;align-content:start;max-height:760px;overflow:auto}button,input{font:inherit;border:1px solid #354956;border-radius:6px;color:#e7eff3;background:#132839;padding:9px 12px;text-align:left}button{cursor:pointer}button.active{border-color:#7acdd9;background:#1b3b49}button small{display:block;color:#9fb7c6}.toolbar{display:flex;gap:12px;margin:18px 0;align-items:center;flex-wrap:wrap}.toolbar button{padding:6px 12px}h2{font-size:22px;margin:0 0 7px}.keys{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin-top:15px}.keys img{width:100%;border-radius:5px}a{color:#8ed8e2}.table-wrap{max-height:650px;overflow:auto;border:1px solid #354956;border-radius:8px}table{border-collapse:collapse;width:100%;font-size:14px}th,td{padding:10px 12px;border-bottom:1px solid #263a49;text-align:left;vertical-align:top}th{position:sticky;top:0;background:#132839}td:last-child{max-width:550px;min-width:240px}input{min-width:280px}footer{margin:24px 0;color:#8aa3b3;font-size:13px}@media(max-width:1000px){.layout{grid-template-columns:1fr}.choices{grid-template-columns:repeat(2,1fr);max-height:none}body{padding:16px}}</style>
<h1>海战 · 卡牌特效</h1><p>六系攻击与减益、防护、修理、装填、场地、范围、驱散、推移。选择卡牌观看完整动画，或播放全部效果。</p>
<div class="toolbar"><button id="overview">播放总览</button><label>速度 <select id="speed"><option value="1">正常</option><option value="0.5">0.5×</option><option value="0.25">0.25×</option></select></label><a href="卡牌特效对应表.csv">下载完整对应表</a></div>
<div class="layout"><div><h2 id="title"></h2><p id="detail"></p><video id="player" controls loop playsinline preload="metadata"></video><div class="keys" id="keys"></div></div><div class="choices" id="choices"></div></div>
<div class="toolbar"><h2>全部海战卡牌对应</h2><input id="search" placeholder="搜索卡名、流派、效果或 ID"><span id="count"></span></div><div class="table-wrap"><table><thead><tr><th>卡牌</th><th>特效</th><th>作用位置</th><th>最终卡牌效果</th></tr></thead><tbody id="rows"></tbody></table></div>
<footer>画面为正式 Unity 海战界面与当前特效代码的连续采样；原始规则与费用未改动。播放视频经过完整解码验证，保留起始、命中与余波关键帧。实际出牌结算和中断清理由独立 PlayMode 回归验证。</footer>
<script>const report=__REPORT__;const role={Cannon:'炮击',Strike:'法术攻击',Affliction:'减益附着',Guard:'格挡防护',Heal:'修复',Empower:'强化与装填',Field:'场地生成',Area:'范围效果',Dispel:'驱散',Push:'推移'},school={Fire:'火焰',Arcane:'秘术',Curse:'诅咒',Tide:'潮汐',Thunder:'雷霆',Blood:'血契',Neutral:'通用'},target={'Selected cells':'选定格子','All existing tide cells':'所有已有水流格','Existing owned school cells':'己方对应流派场地','Enemy ship':'敌舰','Player ship':'我舰'};const player=document.querySelector('#player');function select(s){document.querySelector('#title').textContent=s.cardName;document.querySelector('#detail').textContent=school[s.school]+' · '+role[s.role]+(s.salvoCount>1?' · '+s.salvoCount+' 段':'');player.src=s.video;player.poster=s.poster;player.playbackRate=+document.querySelector('#speed').value;player.play().catch(()=>{});document.querySelectorAll('.choices button').forEach(b=>b.classList.toggle('active',b.dataset.id===s.id));document.querySelector('#keys').replaceChildren(...s.keyframes.map(f=>{const a=document.createElement('a');a.href=f;a.target='_blank';const img=document.createElement('img');img.src=f;img.loading='lazy';a.append(img);return a}));}report.sequences.forEach(s=>{const b=document.createElement('button');b.dataset.id=s.id;b.append(document.createTextNode(s.cardName));const small=document.createElement('small');small.textContent=school[s.school]+' · '+role[s.role];b.append(small);b.onclick=()=>select(s);document.querySelector('#choices').append(b)});document.querySelector('#overview').onclick=()=>{player.src=report.overview;document.querySelector('#title').textContent='海战特效总览';document.querySelector('#detail').textContent=report.sequences.length+' 组正式游戏画面';player.play().catch(()=>{})};document.querySelector('#speed').onchange=e=>player.playbackRate=+e.target.value;function table(){const q=document.querySelector('#search').value.trim().toLowerCase();const items=report.mappings.filter(m=>[m.id,m.name,m.description,school[m.school],role[m.role]].join(' ').toLowerCase().includes(q));document.querySelector('#count').textContent=items.length+' 条（包含合法强化等级与连锁条件）';document.querySelector('#rows').replaceChildren(...items.map(m=>{const tr=document.createElement('tr');[m.name+' +'+m.rank+'\n'+m.id,school[m.school]+' / '+role[m.role]+(m.chainBefore?' / 连锁'+m.chainBefore:'')+(m.salvoCount>1?' / '+m.salvoCount+'段':''),target[m.target]||m.target,m.description.replace(/<[^>]+>/g,'')].forEach(t=>{const td=document.createElement('td');td.textContent=t;tr.append(td)});return tr}));}document.querySelector('#search').oninput=table;table();select(report.sequences[0]);</script></html>
'@
[IO.File]::WriteAllText((Join-Path $capture 'index.html'),$html.Replace('__REPORT__',$json),[Text.UTF8Encoding]::new($false))
# Every deletion names a validated frame in this run; no recursive or shared-cache cleanup.
$freed=0L
foreach($frame in $manifest){
    $path=[IO.Path]::GetFullPath((Join-Path $capture $frame.File))
    if([IO.Path]::GetDirectoryName($path)-ne $capture){throw 'Frame cleanup escaped capture.'}
    if((Get-FileHash -LiteralPath $path).Hash-ne $frame.SHA256){throw 'Frame changed after encoding; preserve it.'}
    $freed+=$frame.Length;Remove-Item -LiteralPath $path -Force
    if(Test-Path -LiteralPath $path){throw 'Frame cleanup failed.'}
}
Remove-Item -LiteralPath $concat -Force
[pscustomobject]@{ReleasedBytes=$freed;RemovedFrames=$manifest.Count;RetainedVideos=$videos.Count+1;RetainedPngKeyframes=($report.sequences | ForEach-Object {$_.keyframes.Count} | Measure-Object -Sum).Sum;DecodeVerified=$true;MainProjectTouched=$false;SharedEncoderRetained=$Ffmpeg} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $capture 'frame-cleanup.json') -Encoding utf8
Write-Output ('PASS naval preview: '+(Join-Path $capture 'index.html')+'; intermediate bytes reclaimed '+$freed)
