$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root='D:\Game\outputs\small-text-screenshots-20261104'
$byIndex=@{}
foreach($run in Get-ChildItem -LiteralPath $root -Directory | Where-Object Name -match '^run\d+$' | Sort-Object Name){
 $report=Join-Path $run.FullName 'report.json'; if(!(Test-Path -LiteralPath $report)){continue}
 foreach($frame in (Get-Content -LiteralPath $report -Raw | ConvertFrom-Json).frames){
  if($frame.error -or !(Test-Path -LiteralPath (Join-Path $run.FullName $frame.file))){continue}
  $frame.file=$run.Name+'/'+$frame.file
  $byIndex[[int]$frame.index]=$frame
 }
}
if($byIndex.Count -ne 12){throw ('Expected 12 successful screenshots, found '+$byIndex.Count)}
$frames=@($byIndex.Values | Sort-Object index)
foreach($f in $frames){ if($f.variant -eq 'ComponentResaleEmpty'){ $f.regions=@($f.regions | Where-Object name -eq 'Component Resale Empty Detail') } }
$marked=Join-Path $root 'marked';New-Item -ItemType Directory -Path $marked -Force | Out-Null
foreach($f in $frames){
 $original=Join-Path $root $f.file
 $bitmap=[Drawing.Bitmap]::new($original);$g=[Drawing.Graphics]::FromImage($bitmap);$pen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,255,56,55),3)
 try{
  foreach($r in $f.regions){$b=$r.bounds;$g.DrawRectangle($pen,[single]($b.x-3),[single]($b.y-2),[single]($b.width+6),[single]($b.height+4))}
  $name=('{0:D2}_{1}.png' -f [int]$f.index,$f.variant)
  $bitmap.Save((Join-Path $marked $name),[Drawing.Imaging.ImageFormat]::Png)
  $f | Add-Member -NotePropertyName annotated -NotePropertyValue ('marked/'+$name) -Force
 }finally{$pen.Dispose();$g.Dispose();$bitmap.Dispose()}
}
$frames | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $root 'frames.json') -Encoding utf8
function Encode-Html([string]$s){[Net.WebUtility]::HtmlEncode($s)}
$html=[Text.StringBuilder]::new()
[void]$html.Append(@"
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>说明小字 · 逐项截图</title><style>*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:#141a1d;color:#eee;font:16px/1.65 system-ui,'Microsoft YaHei',sans-serif}header,main{max-width:1720px;margin:auto;padding:24px}h1{font-size:28px}h2{font-size:22px;margin-bottom:6px}p{margin:8px 0 14px}.muted{color:#b9c3c7}nav{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:4px 16px}a{color:#f0ce91}section{margin:34px 0 56px;scroll-margin-top:16px}.shot img{display:block;width:100%}.shot{background:#050607;border:1px solid #465055}blockquote{margin:12px 0;padding:8px 14px;border-left:3px solid #e8bb70;color:#ead9ba;background:#222b30}button{background:#2b3940;border:1px solid #77858a;color:white;padding:8px 12px;border-radius:5px;cursor:pointer}.controls{position:fixed;right:18px;bottom:18px;background:#26343bea;padding:8px;z-index:10;border-radius:6px}small{color:#aeb9bf}.links{display:flex;justify-content:space-between;margin-top:8px}.off .marked{display:none}.original{display:none}.off .original{display:block}</style><header id="top"><h1>说明小字 · 逐项截图</h1><p>按前一轮清单顺序，共 12 张。当前生产源码由 Unity 原生渲染，1920×1080，使用独立内存样本。红框标出对应文字；原始截图保留。</p><p class="muted">检查了目标文字可见性和实际 CanvasScaler。条件页面的状态注明在每张图上方，未执行交易或更改玩家进度。</p><nav>
"@)
foreach($f in $frames){[void]$html.Append('<a href="#f'+$f.index+'">'+('{0:D2}' -f [int]$f.index)+' · '+(Encode-Html $f.title)+'</a>')}
[void]$html.Append('</nav></header><main>')
foreach($f in $frames){
 [void]$html.Append('<section id="f'+$f.index+'"><h2>'+('{0:D2}' -f [int]$f.index)+' · '+(Encode-Html $f.title)+'</h2><p class="muted">'+(Encode-Html $f.condition)+'</p><div class="shot"><img class="marked" loading="lazy" src="'+$f.annotated+'" alt="'+(Encode-Html $f.title)+'"><img class="original" loading="lazy" src="'+$f.file+'" alt="未标注原图"></div><blockquote>')
 [void]$html.Append((@($f.regions | ForEach-Object {Encode-Html $_.text}) -join '<br>'))
 [void]$html.Append('</blockquote><div class="links"><a href="'+$f.file+'" target="_blank">查看 1920×1080 原图</a><a href="'+$f.annotated+'" target="_blank">查看红框标注图</a>')
 if($f.index -lt 12){[void]$html.Append('<a href="#f'+([int]$f.index+1)+'">下一张 →</a>')}
 [void]$html.Append('</div></section>')
}
[void]$html.Append('</main><div class="controls"><button onclick="document.body.classList.toggle(''off'')">显示／隐藏红框</button> <a href="#top">目录 ↑</a></div></html>')
[IO.File]::WriteAllText((Join-Path $root 'index.html'),$html.ToString(),[Text.UTF8Encoding]::new($false))
for($batch=0;$batch -lt 2;$batch++){
 $contact=[Drawing.Bitmap]::new(1280,1236);$g=[Drawing.Graphics]::FromImage($contact);$font=[Drawing.Font]::new('Microsoft YaHei',19);$brush=[Drawing.Brushes]::White
 try{$g.Clear([Drawing.Color]::FromArgb(20,26,29));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  for($slot=0;$slot -lt 6;$slot++){$f=$frames[$batch*6+$slot];$x=($slot%2)*640;$y=[math]::Floor($slot/2)*412;$img=[Drawing.Image]::FromFile((Join-Path $root $f.annotated));try{$g.DrawString(('{0:D2}  {1}' -f [int]$f.index,$f.title),$font,$brush,[single]($x+12),[single]($y+10));$g.DrawImage($img,[int]$x,[int]($y+48),640,360)}finally{$img.Dispose()}}
  $contact.Save((Join-Path $root ('overview-'+($batch+1)+'.jpg')),[Drawing.Imaging.ImageFormat]::Jpeg)
 }finally{$g.Dispose();$font.Dispose();$contact.Dispose()}
}
Write-Output ('Gallery ready: 12 screenshots; '+(Join-Path $root 'index.html'))
