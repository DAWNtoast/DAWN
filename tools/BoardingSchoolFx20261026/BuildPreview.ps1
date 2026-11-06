param([string]$CaptureRun = 'capture1', [ValidateSet('validation','v2-validation')][string]$EvidenceSet = 'v2-validation')
$ErrorActionPreference = 'Stop'
$root = 'D:\Game\outputs\boarding-school-fx-20261026'
if ($CaptureRun -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid capture run.' }
$capture = Join-Path $root ($EvidenceSet + '\' + $CaptureRun)
$report = Get-Content -LiteralPath (Join-Path $capture 'report.json') -Raw | ConvertFrom-Json
$frames = @($report.frames | ForEach-Object {
    if (!(Test-Path -LiteralPath (Join-Path $capture $_.file))) { throw ('Missing frame: ' + $_.file) }
    [pscustomobject]@{ school=$_.school; role=$_.role; progress=$_.progress; duration=$_.duration; file=($EvidenceSet + '/' + $CaptureRun + '/' + $_.file) }
}) | ConvertTo-Json -Compress
$html = @'
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>接舷战 · 六系出牌动画</title>
<style>
:root{color-scheme:dark;font:16px/1.6 "Microsoft YaHei",sans-serif;background:#0d141c;color:#e8e3d7}*{box-sizing:border-box}body{margin:0 auto;max-width:1680px;padding:32px}h1{font-size:30px;font-weight:600;margin:0 0 6px}p{color:#aebac5;margin:0 0 18px}.controls{display:flex;align-items:center;gap:12px;flex-wrap:wrap;margin:24px 0}button,select{font:inherit;background:#1e2d3b;border:1px solid #435364;color:#f5efe4;border-radius:7px;padding:7px 15px;cursor:pointer}button.active{border-color:#d7b46b;color:#f2d7a3}input{accent-color:#d7b46b}#scrub{width:240px}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:22px}article{background:#17212c;border:1px solid #34414d;border-top:3px solid var(--tint);border-radius:10px;overflow:hidden}article h2{font-size:20px;margin:14px 18px 2px}article p{font-size:14px;margin:0 18px 15px}article img{width:100%;display:block;cursor:zoom-in}footer{font-size:13px;color:#8f9eac;margin:26px 0}dialog{border:1px solid #52616d;background:#0d141c;max-width:95vw;width:1440px;padding:12px;border-radius:10px;color:#e8e3d7}dialog::backdrop{background:#000c}dialog img{width:100%;display:block}dialog button{float:right;margin-bottom:8px}@media(max-width:1000px){.grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:650px){body{padding:18px}.grid{grid-template-columns:1fr}}
</style><h1>接舷战 · 六系出牌动画</h1><p>查看火焰、秘术、诅咒、潮汐、雷霆与血契的施法轨迹和效果。点击画面可放大。</p>
<div class="controls"><button id="attack" class="active">攻击与命中</button><button id="support">增益与施咒</button><button id="play">暂停</button><label>播放速度 <select id="speed"><option value="1" selected>正常</option><option value="0.4">慢放 0.4×</option></select></label><input id="scrub" type="range" min="0" max="9" value="0" aria-label="动画关键帧"><span id="frame"></span></div><main class="grid" id="grid"></main>
<footer>画面取自工程中的接舷战 UI 与正式特效代码，按同一时间轴离线采样；真实按钮出牌、伤害一致性及动画中断清理由 Unity PlayMode 测试验证。预览循环之间留有停顿，游戏中只播放一次。</footer>
<dialog id="zoom"><button id="close">关闭</button><img id="large" alt="放大的接舷战特效"></dialog>
<script>
const frames=__FRAMES__;
const count=frames.filter(f=>f.school==="Fire"&&f.role==="Attack").length,duration=frames[0].duration||.68;
const schools=[['Fire','火焰','#ff6e1f','甲板预热 → 脚下火柱喷发','Affliction','燃烧附着'],['Arcane','秘术','#a387ff','多点聚能 → 秘术束汇聚','Charge','延迟攻击蓄势'],['Curse','诅咒','#99e042','地面暗影 → 根须向上缠绕','Affliction','目标咒印收束'],['Tide','潮汐','#3bdeff','贴地推进 → 浪头撞击与飞沫','Guard','潮汐护持'],['Thunder','雷霆','#ffe147','头顶蓄电 → 闪电向下劈落','Empower','电荷凝聚'],['Blood','血契','#ff3361','目标切击 → 血流返回施法者','Heal','血契复苏']];
let support=false,playing=!matchMedia('(prefers-reduced-motion: reduce)').matches,index=0,clock=0,last=performance.now(),zoomSchool=null;
const grid=document.getElementById('grid'),slider=document.getElementById('scrub'),play=document.getElementById('play');
slider.max=count-1;
for(const school of schools){const card=document.createElement('article');card.style.setProperty('--tint',school[2]);card.innerHTML=`<h2>${school[1]}</h2><p></p><img alt="${school[1]}接舷战特效" loading="eager">`;card.querySelector('img').onclick=()=>{zoomSchool=school[0];render();document.getElementById('zoom').showModal()};grid.append(card)}
function render(){schools.forEach((s,i)=>{let role=support?s[4]:'Attack',list=frames.filter(f=>f.school===s[0]&&f.role===role),f=list[index];if(!f)return;const card=grid.children[i];card.querySelector('p').textContent=support?s[5]:s[3];card.querySelector('img').src=f.file;if(zoomSchool===s[0])document.getElementById('large').src=f.file});slider.value=index;document.getElementById('frame').textContent=`${index+1} / ${count}`;play.textContent=playing?'暂停':'播放'}
document.getElementById('attack').onclick=()=>setMode(false);document.getElementById('support').onclick=()=>setMode(true);
function setMode(value){support=value;clock=0;index=0;document.getElementById('attack').classList.toggle('active',!value);document.getElementById('support').classList.toggle('active',value);render()}
play.onclick=()=>{playing=!playing;render()};slider.oninput=()=>{playing=false;index=+slider.value;clock=index/(count-1)*duration;render()};document.getElementById('close').onclick=()=>document.getElementById('zoom').close();
function tick(now){const delta=Math.min(.1,(now-last)/1000);last=now;if(playing){clock=(clock+delta*+document.getElementById('speed').value)%(duration+.45);const next=Math.min(count-1,Math.floor(clock/duration*(count-1)));if(next!==index){index=next;render()}}requestAnimationFrame(tick)}render();requestAnimationFrame(tick);
</script></html>
'@
[IO.File]::WriteAllText((Join-Path $root '动画预览.html'), $html.Replace('__FRAMES__',$frames), [Text.UTF8Encoding]::new($false))
Write-Output ('Built ' + (Join-Path $root '动画预览.html') + ' from ' + $report.frames.Count + ' captured frames.')
