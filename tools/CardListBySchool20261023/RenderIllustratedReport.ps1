param([Parameter(Mandatory=$true)][string]$DataPath,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$inputRoot=[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($DataPath))
$outputFile=[IO.Path]::GetFullPath($OutputPath)
$outputRoot=[IO.Path]::GetDirectoryName($outputFile)
$catalog=Get-Content -LiteralPath $DataPath -Raw -Encoding UTF8|ConvertFrom-Json
$upgrades=Get-Content -LiteralPath (Join-Path $inputRoot 'card-upgrade-data.json') -Raw -Encoding UTF8|ConvertFrom-Json
$art=Get-Content -LiteralPath (Join-Path $inputRoot 'card-art-data.json') -Raw -Encoding UTF8|ConvertFrom-Json
$gameViews=Get-Content -LiteralPath (Join-Path $inputRoot 'card-game-view-data.json') -Raw -Encoding UTF8|ConvertFrom-Json
$cards=@($catalog.Cards)
$upgradeById=@{};$artByStage=@{};$gameByRank=@{};$glossaryById=@{}
foreach($row in @($upgrades.Cards)){
 if($upgradeById.ContainsKey([string]$row.Id)){throw ('Duplicate upgrade card: '+$row.Id)}
 $upgradeById[[string]$row.Id]=$row
}
foreach($row in @($art.Cards)){
 $key=[string]$row.Id+'|'+[string]$row.Stage
 if($artByStage.ContainsKey($key)){throw ('Duplicate art stage: '+$key)}
 if([string]::IsNullOrWhiteSpace([string]$row.OriginalImage)){throw ('Missing original image: '+$key)}
 if(-not(Test-Path -LiteralPath (Join-Path $inputRoot ([string]$row.OriginalImage)) -PathType Leaf)){throw ('Original image not found: '+$row.OriginalImage)}
 if([int]$row.SourceWidth -le 0 -or [int]$row.SourceHeight -le 0){throw ('Missing original dimensions: '+$key)}
 $artByStage[$key]=$row
}
foreach($row in @($gameViews.Cards)){
 $key=[string]$row.Id+'|'+[string]$row.Stage+'|'+[int]$row.Level
 if($gameByRank.ContainsKey($key)){throw ('Duplicate game view: '+$key)}
 if([string]::IsNullOrWhiteSpace([string]$row.Image) -or -not(Test-Path -LiteralPath (Join-Path $inputRoot ([string]$row.Image)) -PathType Leaf)){throw ('Missing game view image: '+$key)}
 $designWidth=if($row.PSObject.Properties['DesignWidth']){[double]$row.DesignWidth}else{[double]$row.DisplayWidth}
 $designHeight=if($row.PSObject.Properties['DesignHeight']){[double]$row.DesignHeight}else{[double]$row.DisplayHeight}
 if([int]$row.Width -le 0 -or [int]$row.Height -le 0 -or $designWidth -le 0 -or $designHeight -le 0){throw ('Missing game view dimensions: '+$key)}
 $gameByRank[$key]=[pscustomobject]@{Data=$row;DesignWidth=$designWidth;DesignHeight=$designHeight}
}
foreach($term in @($catalog.Glossary)){$glossaryById[[string]$term.Id]=$term}
if($cards.Count -eq 0 -or $cards.Count -ne $upgradeById.Count -or @($cards.Id|Sort-Object -Unique).Count -ne $cards.Count){throw 'Card identities are empty, duplicate or unmatched.'}
$schools=@(
 @{Id='';Key='ordinary';Name='普通';Color='#64748b'},
 @{Id='fire';Key='fire';Name='火焰';Color='#bd5635'},
 @{Id='arcane';Key='arcane';Name='秘术';Color='#775ba3'},
 @{Id='curse';Key='curse';Name='诅咒';Color='#795877'},
 @{Id='tide';Key='tide';Name='潮汐';Color='#287f95'},
 @{Id='thunder';Key='thunder';Name='雷霆';Color='#98771b'},
 @{Id='blood';Key='blood';Name='血契';Color='#a24055'}
)
$knownSchools=@($schools|ForEach-Object{$_.Id})
if(@($cards|Where-Object{[string]$_.SchoolId -notin $knownSchools}).Count -gt 0){throw 'Unknown school.'}
function EncodeHtml([object]$Value){[Net.WebUtility]::HtmlEncode([string]$Value)}
function EffectHtml([object]$Level){
 if($null -eq $Level.PSObject.Properties['EffectRichText']){throw 'Export current runtime keyword emphasis before rendering.'}
 $richText=[string]$Level.EffectRichText
 if($richText.Replace('<b>','').Replace('</b>','') -cne [string]$Level.Effect){throw 'Keyword emphasis changed effect text.'}
 # Only runtime-generated bold tags become markup; all other content stays escaped.
 (EncodeHtml $richText).Replace('&lt;b&gt;','<strong class="effect-keyword">').Replace('&lt;/b&gt;','</strong>')
}
function ImageHref([string]$RelativePath){
 $target=[IO.Path]::GetFullPath((Join-Path $inputRoot $RelativePath))
 $baseUri=[Uri]::new($outputRoot.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar)
 $baseUri.MakeRelativeUri([Uri]::new($target)).ToString()
}
$html=[Text.StringBuilder]::new(1400000)
function Emit([string]$Line){[void]$html.AppendLine($Line)}
Emit @'
<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>全部卡牌图鉴 · 卡图、效果、升级与关键词</title>
<style>
:root{color-scheme:light;--ink:#1c3039;--muted:#60747d;--line:#dce5e7;--accent:#16707b}
*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:#eff3f3;color:var(--ink);font-family:"Microsoft YaHei","PingFang SC",system-ui,sans-serif;line-height:1.65}a{color:var(--accent);text-underline-offset:3px}button,input,select{font:inherit}a:focus-visible,button:focus-visible,input:focus-visible,select:focus-visible,summary:focus-visible{outline:3px solid #bc892f;outline-offset:3px}main{max-width:1520px;margin:auto;padding:42px 32px 72px}header{margin-bottom:24px}.eyebrow{font-size:12px;font-weight:800;letter-spacing:.14em;color:var(--accent);margin:0 0 8px}h1{font-size:clamp(28px,4vw,42px);line-height:1.3;margin:0 0 10px}h2,h3,h4,p{margin-top:0}.lead{font-size:16px;margin-bottom:10px;max-width:1120px}.muted{color:var(--muted)}.notes{font-size:13px;color:var(--muted);margin:0}.intro-links{display:flex;flex-wrap:wrap;gap:8px 22px;margin:12px 0 0;font-size:13px}
.toolbar{background:#fff;border:1px solid var(--line);border-radius:12px;padding:18px 20px;margin:22px 0 16px}.controls{display:grid;grid-template-columns:minmax(220px,1fr) repeat(3,minmax(115px,145px)) auto;gap:12px;align-items:end}label{font-size:12px;font-weight:700}input,select{width:100%;margin-top:4px;padding:9px 11px;min-height:42px;border:1px solid #a9bdc5;border-radius:6px;background:#fff;color:var(--ink)}button{cursor:pointer;min-height:42px;padding:8px 13px;border:1px solid #a9bdc5;border-radius:6px;color:var(--ink);background:#f4f8f8}button:hover{background:#e2eff0}.toolbar-bottom{display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:10px;margin-top:12px}.filter-summary{font-size:13px;color:var(--muted);margin:0}.display-actions{display:flex;gap:8px}.display-actions button{font-size:12px;min-height:32px;padding:4px 10px}.jump-links{display:flex;flex-wrap:wrap;gap:8px;margin:0 0 24px}.jump-links a{display:inline-flex;gap:9px;padding:6px 13px;border:1px solid var(--line);border-radius:6px;background:#fff;text-decoration:none;font-size:13px}.jump-links strong{color:var(--muted);font-variant-numeric:tabular-nums}
.school-section{margin-top:30px;scroll-margin-top:18px}.section-heading{display:flex;align-items:baseline;justify-content:space-between;gap:16px;margin-bottom:14px}.section-heading h2{margin:0;font-size:24px;border-left:5px solid var(--school);padding-left:12px}.section-heading p{font-size:13px;color:var(--muted);margin:0}.card-block{background:#fff;border:1px solid var(--line);border-radius:12px;margin:0 0 18px;overflow:hidden;scroll-margin-top:20px}.card-heading{padding:15px 20px;border-bottom:1px solid var(--line);display:flex;align-items:center;justify-content:space-between;gap:16px;background:linear-gradient(90deg,#f9fbfb,#fff)}.card-heading h3{font-size:21px;line-height:1.4;margin:0}.card-number{color:var(--muted);font-size:12px;font-weight:500;margin-right:10px}.card-source{font-size:12px;color:var(--muted);margin:4px 0 0}.badges{display:flex;flex-wrap:wrap;gap:6px}.badge{display:inline-block;padding:2px 8px;background:#edf3f4;color:#45616b;border-radius:4px;font-size:11px;white-space:nowrap}.school-badge{color:var(--school);border:1px solid currentColor;background:#fff}
.card-body{display:grid;grid-template-columns:220px minmax(0,1fr);gap:24px;padding:20px}.card-art figure{margin:0 0 12px}.card-art a{display:block}.card-art img{display:block;width:100%;height:auto;max-height:320px;object-fit:contain;background:#edf1f1;border-radius:8px}.card-art figcaption{margin-top:6px;font-size:11px;color:var(--muted);text-align:center}.art-note{font-size:11px;color:var(--muted);text-align:center;margin:8px 0}.card-content{min-width:0}.stage-block+.stage-block{margin-top:22px;border-top:1px solid var(--line);padding-top:16px}.stage-heading{display:flex;align-items:baseline;justify-content:space-between;flex-wrap:wrap;gap:6px;margin-bottom:10px}.stage-heading h4{font-size:14px;margin:0}.stage-limit{font-size:11px;color:var(--muted);margin:0}
.rank-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:10px}.rank{min-width:0;padding:13px 14px;background:#f8fafb;border:1px solid #e0e8eb;border-radius:7px}.rank.base{background:#f0f7f7;border-color:#c7dedf}.rank-label{margin-bottom:8px;font-size:13px;font-weight:800;color:#526974}.rank.base .rank-label{color:#146d76}.rank-name{font-size:12px;font-weight:700;margin:0 0 7px}.play-cost{font-size:12px;color:#45616b;margin:0 0 9px;white-space:pre-line}.effect{font-size:14px;white-space:pre-line;margin:0;line-height:1.75;overflow-wrap:anywhere}.upgrade-cost{font-size:11px;color:var(--muted);margin:10px 0 0;padding-top:8px;border-top:1px solid #dfe8eb}.rank-keywords{font-size:11px;margin:9px 0 0;line-height:1.7}.rank-keywords a{white-space:nowrap}.empty{color:var(--muted)}
.keyword-panel{margin-top:16px;padding-top:13px;border-top:1px solid var(--line)}.keyword-panel h4{font-size:13px;margin:0 0 8px}.keyword-definitions{margin:0;display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:9px 18px}.keyword-definition{font-size:12px;scroll-margin-top:20px}.keyword-definition dt{font-weight:700;color:#365866;display:inline}.keyword-definition dd{margin:0;display:inline;color:#4f6570}.keyword-definition dd:before{content:' · '}.keyword-definition:target{background:#fff1c9;outline:4px solid #fff1c9}.no-keywords{font-size:12px;color:var(--muted);margin:0}
.reference-panel{margin-top:18px;background:#fff;border:1px solid var(--line);border-radius:10px;padding:16px 20px;scroll-margin-top:20px}.reference-panel>summary{font-weight:700;cursor:pointer}.reference-content{margin-top:14px}.glossary-table{width:100%;border-collapse:collapse;font-size:13px}.glossary-table th,.glossary-table td{text-align:left;vertical-align:top;padding:10px 12px;border-top:1px solid var(--line)}.glossary-table th{width:23%}.glossary-table tr:target{background:#fff1c9}.rules-list{margin:8px 0;padding-left:22px;font-size:13px}.rules-list li+li{margin-top:8px}.no-results{padding:28px;text-align:center;background:#fff;border:1px solid var(--line);border-radius:10px}footer{font-size:12px;color:var(--muted);margin-top:28px}[hidden]{display:none!important}.back-top{font-size:12px}.compact-upgrades .rank.upgraded{display:none}.compact-upgrades .rank-grid{grid-template-columns:1fr}.compact-keywords .keyword-panel{display:none}
@media(max-width:1150px){.controls{grid-template-columns:minmax(200px,1fr) repeat(3,120px)}.controls .clear-button{grid-column:4}.card-body{grid-template-columns:180px minmax(0,1fr);gap:18px}.rank-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.keyword-definitions{grid-template-columns:1fr}}
@media(max-width:760px){main{padding:24px 14px 48px}.controls{grid-template-columns:1fr 1fr}.search-control{grid-column:1/-1}.controls .clear-button{grid-column:auto}.card-body{grid-template-columns:1fr;padding:16px}.card-art{display:flex;justify-content:center;flex-wrap:wrap;gap:12px}.card-art figure{width:190px;max-width:100%;margin:0}.card-art img{max-height:240px}.card-art .art-note{width:100%;margin:0}.card-heading{align-items:flex-start;padding:14px 16px}.card-heading h3{font-size:19px}.card-heading>.badges{justify-content:flex-end}.section-heading{align-items:center}.section-heading p{text-align:right}.toolbar{padding:15px}.rank{padding:12px}.effect{font-size:13px}.display-actions{flex-wrap:wrap}.glossary-table th,.glossary-table td{padding:9px 6px}.keyword-definitions{grid-template-columns:1fr}}
@media(max-width:440px){.rank-grid{grid-template-columns:1fr}.card-heading{display:block}.card-heading>.badges{justify-content:flex-start;margin-top:9px}.stage-heading{display:block}.stage-limit{margin-top:3px}.card-number{display:block;margin:0 0 2px}.section-heading h2{font-size:22px}}
@media print{@page{size:A4 landscape;margin:12mm}html{scroll-behavior:auto}body{background:#fff}main{max-width:none;padding:0}.toolbar,.jump-links,.intro-links,.back-top,.no-results{display:none!important}header{margin-bottom:14pt}h1{font-size:24pt}.lead{font-size:10pt}.notes{font-size:9pt}.card-block{break-inside:avoid;page-break-inside:avoid;margin-bottom:12pt}.card-heading{padding:8pt 10pt}.card-heading h3{font-size:14pt}.card-body{grid-template-columns:120pt minmax(0,1fr);gap:12pt;padding:10pt}.card-art img{max-height:175pt}.rank-grid{grid-template-columns:repeat(auto-fit,minmax(130pt,1fr))}.rank{padding:7pt}.effect{font-size:9pt}.rank-label,.play-cost{font-size:8pt}.upgrade-cost,.rank-keywords,.keyword-definition,.stage-limit,.card-source{font-size:7pt}.section-heading h2{font-size:16pt}.section-heading{break-after:avoid}.keyword-panel{margin-top:8pt;padding-top:6pt}.keyword-definitions{grid-template-columns:repeat(2,minmax(0,1fr))}.reference-panel{break-before:page;border:0}.compact-upgrades .rank.upgraded{display:block}.compact-upgrades .rank-grid{grid-template-columns:repeat(auto-fit,minmax(130pt,1fr))}.compact-keywords .keyword-panel{display:block}a{color:inherit;text-decoration:none}footer{font-size:8pt}}
.review-toolbar{background:#fff;border:1px solid var(--line);border-radius:12px;padding:16px 20px;margin:0 0 18px}.review-toolbar-top{display:flex;flex-wrap:wrap;gap:12px 20px;align-items:center;justify-content:space-between}.review-toolbar h2{font-size:16px;margin:0 0 3px}.review-progress{font-size:13px;color:var(--accent);margin:0}.review-tools{display:flex;align-items:end;flex-wrap:wrap;gap:9px}.review-tools label{min-width:150px}.review-help{margin:10px 0 0;font-size:12px;color:var(--muted)}.review-storage-status{margin:4px 0 0;font-size:12px;color:#26724b}.review-storage-status.error{color:#b13d28}.card-review{display:grid;grid-template-columns:220px minmax(0,1fr);gap:24px;border-top:1px solid var(--line);background:#f8faf9;padding:18px 20px}.review-check-label{display:flex;align-items:center;gap:10px;align-self:start;cursor:pointer;font-size:15px;min-height:44px}.review-checkbox{width:24px;height:24px;min-height:24px;padding:0;margin:0;accent-color:#16776a;flex:none}.review-note-field{min-width:0}.review-note-field>label{display:block;margin-bottom:6px;font-size:13px}.review-notes{font:inherit;width:100%;min-height:92px;resize:vertical;border:1px solid #a9bdc5;border-radius:6px;padding:10px 12px;background:#fff;color:var(--ink);line-height:1.6;font-size:14px}.review-notes:focus-visible{outline:3px solid #bc892f;outline-offset:3px}.reviewed .card-review{background:#edf7f2}.reviewed .review-check-label{color:#26724b}.has-review-notes .review-notes{border-color:#b78c38}.review-card-print{display:none;white-space:pre-wrap;overflow-wrap:anywhere}
@media(max-width:1150px){.card-review{grid-template-columns:180px minmax(0,1fr);gap:18px}}
@media(max-width:760px){.card-review{grid-template-columns:1fr;gap:8px;padding:14px 16px}.review-toolbar{padding:15px}.review-toolbar-top{display:block}.review-tools{margin-top:12px}.review-tools label{flex:1 1 100%}.review-tools button{flex:1 1 auto}.review-notes{font-size:16px;min-height:110px}}
@media print{.review-toolbar{display:none!important}.card-review{display:block;padding:7pt 10pt;break-inside:avoid}.review-check-label{font-size:9pt;min-height:0}.review-checkbox{width:12pt;height:12pt;min-height:0}.review-note-field{margin-top:6pt}.review-notes{display:none}.review-card-print{display:block;font-size:9pt}.review-note-field>label{font-size:9pt}}
.card-body{display:block;padding:22px 24px}.card-visuals{padding:20px 24px 24px;border-bottom:1px solid var(--line);background:#fbfcfc}.visuals-toolbar{display:flex;align-items:end;justify-content:space-between;gap:16px;flex-wrap:wrap;margin-bottom:16px}.visuals-toolbar h4{font-size:16px;margin:0 0 3px}.visuals-toolbar p{margin:0;font-size:12px;color:var(--muted)}.preview-controls{display:flex;gap:10px;flex-wrap:wrap}.preview-controls label{min-width:126px}.preview-controls select{margin-top:3px;min-height:38px;padding:6px 9px}.visual-columns{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:20px}.visual-panel,.visual-panel.card-art{display:flex;flex-direction:column;min-width:0;margin:0;background:#fff;border:1px solid var(--line);border-radius:9px;padding:16px}.visual-panel-header{display:flex;align-items:baseline;justify-content:space-between;gap:8px;flex-wrap:wrap;margin-bottom:12px}.visual-panel-header strong{font-size:14px}.visual-panel-header span{font-size:11px;color:var(--muted)}.visual-image-area{display:flex;align-items:center;justify-content:center;flex:1;min-height:510px;min-width:0;background:#f4f7f7;border-radius:6px;padding:12px}.visual-panel a.preview-image-link{display:block;max-width:100%;cursor:zoom-in}.visual-panel img{display:block;object-fit:contain;max-width:100%;height:auto;border-radius:0}.visual-panel .original-art-image{width:420px;max-height:none;background:transparent}.visual-panel .game-card-image{width:var(--game-design-width);max-height:none;background:transparent}.visual-panel figcaption{margin-top:10px;color:var(--muted);font-size:12px;line-height:1.6;text-align:center}.visual-panel .visual-file-link{display:inline-block;margin-top:5px;font-size:12px}.view-size-note{font-size:12px;color:var(--muted);margin:14px 0 0}.card-image-dialog{width:96vw;max-width:1500px;height:94dvh;max-height:1100px;border:1px solid #a9bdc5;border-radius:12px;padding:0;background:#f5f7f7;color:var(--ink);overflow:hidden}.card-image-dialog::backdrop{background:rgba(12,25,32,.76)}.image-view-header{display:flex;align-items:center;justify-content:space-between;gap:16px;padding:14px 18px;background:#fff;border-bottom:1px solid var(--line)}.image-view-header h2{font-size:17px;margin:0}.image-view-header button{flex:none;min-height:36px;padding:4px 12px}.image-view-tools{display:flex;align-items:center;gap:8px;flex-wrap:wrap;padding:10px 18px;background:#fff;border-bottom:1px solid var(--line)}.image-view-tools button{min-height:34px;padding:3px 11px;font-size:13px}.image-view-tools button[aria-pressed="true"]{background:#16707b;border-color:#16707b;color:#fff}.image-view-tools a{margin-left:auto;font-size:13px}.image-view-size{font-size:12px;color:var(--muted)}.image-view-canvas{height:calc(100% - 121px);overflow:auto;padding:18px;text-align:center;overscroll-behavior:contain}.image-view-canvas img{display:block;margin:auto;max-width:100%;max-height:100%;width:auto;height:auto;object-fit:contain}.image-view-canvas.is-pixels img{max-width:none;max-height:none;margin:0 auto}.image-view-open{overflow:hidden}
@media(max-width:900px){.card-visuals{padding:16px}.visual-columns{gap:12px}.visual-panel,.visual-panel.card-art{padding:12px}.visual-image-area{padding:8px;min-height:460px}.card-body{padding:18px}}
@media(max-width:650px){.visual-columns{grid-template-columns:1fr}.visuals-toolbar{align-items:stretch}.preview-controls{width:100%}.preview-controls label{flex:1;min-width:0}.visual-panel,.visual-panel.card-art{width:auto;max-width:none}.visual-image-area{min-height:0;padding:12px}.visual-panel .original-art-image{width:100%;max-height:430px}.visual-panel .game-card-image{width:var(--game-design-width)}.card-image-dialog{width:100vw;height:100dvh;max-height:none;border-radius:0}.image-view-header{padding:10px 12px}.image-view-header h2{font-size:15px}.image-view-tools{padding:8px 12px}.image-view-tools a{margin-left:0}.image-view-canvas{padding:8px;height:calc(100% - 145px)}.image-view-size{display:none}}
@media print{.card-visuals{padding:8pt 10pt}.visuals-toolbar,.view-size-note,.visual-file-link{display:none!important}.visual-columns{grid-template-columns:1fr 1fr;gap:10pt}.visual-panel,.visual-panel.card-art{padding:6pt;break-inside:avoid}.visual-image-area{min-height:0;padding:4pt}.visual-panel .original-art-image{width:auto;max-height:160pt}.visual-panel .game-card-image{width:auto;max-height:200pt}.visual-panel figcaption{font-size:7pt;margin-top:4pt}.visual-panel-header{margin-bottom:4pt}.visual-panel-header strong{font-size:9pt}.visual-panel-header span{font-size:7pt}.card-body{display:block;padding:10pt}.card-image-dialog{display:none!important}}
.card-image-dialog[open]{display:grid;grid-template-rows:auto auto minmax(0,1fr)}.image-view-canvas{height:auto;min-height:0}
</style></head>
'@
Emit ('<body data-catalog-date="'+(EncodeHtml $catalog.Date)+'"><main id="top"><header>')
Emit ('<p class="eyebrow">CARD ATLAS · '+(EncodeHtml $catalog.Date)+'</p><h1>全部卡牌图鉴</h1><p class="lead">共 <strong>'+$cards.Count+'</strong> 张卡牌。对照原始卡图与游戏实际卡面，并查看基础效果、各级升级效果，以及关键词和完整介绍。</p>')
Emit '<p class="notes">按普通、火焰、秘术、诅咒、潮汐、雷霆、血契分组，包含常规牌、船员牌和部件牌。数值来自当前卡牌定义，不含装备或实战临时加成；各阶段相同的效果合并显示。</p>'
Emit '<p class="notes">原始卡图保留完整像素，可点击放大至 1:1 查看。游戏卡面展示卡组／工坊中的卡框、文字与费用图标，可切换阶段和强化等级；默认按 1920×1080 基准界面的比例显示，实际游戏窗口和浏览器缩放会改变屏幕大小。</p>'
Emit '<nav class="intro-links" aria-label="参考资料"><a href="#glossary" data-open-reference>关键词总表</a><a href="#catalog-rules" data-open-reference>升级、解锁与场地规则</a><a href="全部卡牌强化清单.html">独立强化清单</a></nav></header>'
Emit @'
<section class="toolbar" aria-label="筛选卡牌"><div class="controls">
<label class="search-control" for="card-search">搜索卡牌、效果、关键词或来源<input id="card-search" type="search" placeholder="例如：整顿号令、火场、木匠" autocomplete="off"></label>
<label for="school-filter">系别<select id="school-filter"><option value="all">全部系别</option>
'@
foreach($school in $schools){Emit ('<option value="'+$school.Key+'">'+$school.Name+'</option>')}
Emit @'
</select></label><label for="stage-filter">阶段<select id="stage-filter"><option value="all">全部阶段</option><option value="准备">准备</option><option value="海战">海战</option><option value="接舷">接舷</option></select></label>
<label for="source-filter">来源<select id="source-filter"><option value="all">全部来源</option><option value="regular">常规卡牌</option><option value="crew">船员牌</option><option value="component">部件牌</option></select></label>
<button id="clear-filters" class="clear-button" type="button">清除筛选</button></div>
<div class="toolbar-bottom"><p id="filter-summary" class="filter-summary" role="status" aria-live="polite"></p><div class="display-actions"><button id="toggle-upgrades" type="button" aria-pressed="false">收起升级效果</button><button id="toggle-keywords" type="button" aria-pressed="false">收起关键词介绍</button><button id="print-report" type="button">打印当前清单</button></div></div></section>
<section class="review-toolbar" aria-label="审核进度与记录">
<div class="review-toolbar-top"><div><h2>逐张审核</h2><p id="review-progress" class="review-progress" role="status" aria-live="polite">正在读取审核进度…</p></div>
<div class="review-tools"><label for="review-filter">审核状态<select id="review-filter"><option value="all">全部卡牌</option><option value="pending">未检查</option><option value="reviewed">已检查</option><option value="notes">有修改建议</option></select></label><button id="export-reviews" type="button">导出审核记录</button><button id="import-reviews" type="button">导入审核记录</button><input id="import-reviews-file" type="file" accept=".json,application/json" hidden aria-label="导入审核记录文件"></div></div>
<p class="review-help">在每张卡牌下方勾选“已检查”、填写修改建议。记录自动保存在当前浏览器；刷新或更新同一网页后可继续。完成后导出审核记录交给我，换浏览器时可导入继续。</p>
<p id="review-storage-status" class="review-storage-status" role="status" aria-live="polite"></p></section>
<nav class="jump-links" aria-label="系别目录">
'@
foreach($school in $schools){Emit ('<a href="#school-'+$school.Key+'">'+$school.Name+'<strong>'+@($cards|Where-Object{[string]$_.SchoolId -eq $school.Id}).Count+'</strong></a>')}
Emit '</nav><p id="no-results" class="no-results" hidden>没有符合筛选条件的卡牌，请减少搜索词或清除筛选。</p>'
$seenStageCount=0;$seenRankCount=0
foreach($school in $schools){
 $schoolCards=@($cards|Where-Object{[string]$_.SchoolId -eq $school.Id}|Sort-Object{[int]$_.Number},Id)
 Emit ('<section id="school-'+$school.Key+'" class="school-section" data-school="'+$school.Key+'" style="--school:'+$school.Color+'"><div class="section-heading"><h2>'+$school.Name+'</h2><p><span class="school-count">'+$schoolCards.Count+'</span> 张卡牌 · <a class="back-top" href="#top">回到筛选</a></p></div>')
 foreach($card in $schoolCards){
  $upgrade=$upgradeById[[string]$card.Id]
  if($null -eq $upgrade -or $upgrade.Name -ne $card.Name -or $upgrade.Number -ne $card.Number){throw ('Missing/mismatched identity: '+$card.Id)}
  $stageNames=@($card.StageVariants|ForEach-Object{$_.Stages})
  $actualStages=@($upgrade.Stages|ForEach-Object{[string]$_.Stage})
  if((($stageNames|Sort-Object)-join '|') -cne (($actualStages|Sort-Object)-join '|')){throw ('Stage mismatch: '+$card.Id)}
  $sourceKey=if([string]$card.Source -match '船员'){'crew'}elseif([string]$card.Source -match '部件'){'component'}else{'regular'}
  $searchParts=[Collections.Generic.List[string]]::new()
  foreach($value in @($card.Id,$card.Number,$card.Name,$card.School,$card.Source,$card.UnlockRequirement)){$searchParts.Add([string]$value)}
  $groups=[Collections.Generic.List[object]]::new();$cardViews=[Collections.Generic.List[object]]::new();$terms=[ordered]@{}
  foreach($stage in @($upgrade.Stages)){
   $seenStageCount++
   $stageArt=$artByStage[([string]$card.Id+'|'+[string]$stage.Stage)]
   if($null -eq $stageArt){throw ('Missing image stage: '+$card.Id+'/'+$stage.Stage)}
   $stageImage=[string]$stageArt.OriginalImage
   $levels=@($stage.Levels)
   if($levels.Count -ne ([int]$stage.MaximumLevel+1)){throw ('Incomplete ranks: '+$card.Id+'/'+$stage.Stage)}
   $baseVariant=@($card.StageVariants|Where-Object{@($_.Stages) -contains [string]$stage.Stage})[0]
   if($levels[0].Effect -cne $baseVariant.Effect -or [string]$levels[0].Energy -ne [string]$baseVariant.Energy -or $levels[0].OtherCost -cne $baseVariant.OtherCost){throw ('Base effect differs: '+$card.Id+'/'+$stage.Stage)}
   for($rankIndex=0;$rankIndex -lt $levels.Count;$rankIndex++){
    $level=$levels[$rankIndex];$seenRankCount++
    if([int]$level.Level -ne $rankIndex -or $null -eq $level.PSObject.Properties['Keywords']){throw ('Missing rank or runtime keyword data: '+$card.Id+'/'+$stage.Stage+'/'+$rankIndex)}
    $gameView=$gameByRank[([string]$card.Id+'|'+[string]$stage.Stage+'|'+$rankIndex)]
    if($null -eq $gameView){throw ('Missing game view rank: '+$card.Id+'/'+$stage.Stage+'/'+$rankIndex)}
    $cardViews.Add([ordered]@{stage=[string]$stage.Stage;level=$rankIndex;image=(ImageHref ([string]$gameView.Data.Image));width=[int]$gameView.Data.Width;height=[int]$gameView.Data.Height;designWidth=$gameView.DesignWidth;designHeight=$gameView.DesignHeight;originalImage=(ImageHref $stageImage);originalWidth=[int]$stageArt.SourceWidth;originalHeight=[int]$stageArt.SourceHeight})
    foreach($value in @($stage.Stage,$level.Name,$level.Effect,$level.OtherCost,$level.UpgradeCost)){$searchParts.Add([string]$value)}
    foreach($term in @($level.Keywords)){
     if([string]::IsNullOrWhiteSpace([string]$term.Name) -or [string]::IsNullOrWhiteSpace([string]$term.Meaning)){throw ('Incomplete keyword: '+$card.Id)}
     if(-not $glossaryById.ContainsKey([string]$term.Id)){throw ('Unknown keyword: '+$term.Id)}
     $termKey=[string]$term.Id+'|'+[string]$term.Meaning
     if(-not $terms.Contains($termKey)){$terms[$termKey]=[pscustomobject]@{Id=$term.Id;Name=$term.Name;Meaning=$term.Meaning;Anchor=('term-'+$card.Id+'-'+$terms.Count)}}
     $searchParts.Add([string]$term.Name);$searchParts.Add([string]$term.Meaning)
    }
   }
   $signature=([ordered]@{MaximumLevel=$stage.MaximumLevel;LimitExplanation=$stage.LimitExplanation;Levels=$stage.Levels;Image=$stageImage}|ConvertTo-Json -Depth 20 -Compress)
   $group=@($groups|Where-Object{$_.Signature -ceq $signature}|Select-Object -First 1)
   if($group.Count -gt 0){$group[0].Stages.Add([string]$stage.Stage)}
   else{$groupStages=[Collections.Generic.List[string]]::new();$groupStages.Add([string]$stage.Stage);$groups.Add([pscustomobject]@{Signature=$signature;Stages=$groupStages;Data=$stage})}
  }
  Emit ('<article id="card-'+(EncodeHtml $card.Id)+'" class="card-block" data-card-id="'+(EncodeHtml $card.Id)+'" data-card-number="'+(EncodeHtml $card.Number)+'" data-card-name="'+(EncodeHtml $card.Name)+'" data-school="'+$school.Key+'" data-stages="'+(EncodeHtml ($stageNames -join '|'))+'" data-source="'+$sourceKey+'" data-search="'+(EncodeHtml ($searchParts -join ' '))+'">')
  Emit ('<div class="card-heading"><div><h3><span class="card-number">#'+(EncodeHtml $card.Number)+'</span>'+(EncodeHtml $card.Name)+'</h3><p class="card-source">'+(EncodeHtml $card.Source)+' · '+(EncodeHtml $card.UnlockRequirement)+'</p></div><div class="badges"><span class="badge school-badge">'+$school.Name+'</span>')
  foreach($stageName in $stageNames){Emit ('<span class="badge">'+(EncodeHtml $stageName)+'</span>')}
  $firstView=$cardViews[0]
  $viewsJson=ConvertTo-Json -InputObject @($cardViews.ToArray()) -Depth 6 -Compress
  $stageSelectId='preview-stage-'+[string]$card.Id
  $levelSelectId='preview-level-'+[string]$card.Id
  Emit ('</div></div><section class="card-visuals" aria-label="'+(EncodeHtml $card.Name)+'卡图与游戏卡面对照" data-views="'+(EncodeHtml $viewsJson)+'"><div class="visuals-toolbar"><div><h4>原图与游戏显示对照</h4><p>点击任一图片放大查看，可选择适应窗口或 1:1 像素。</p></div><div class="preview-controls"><label for="'+(EncodeHtml $stageSelectId)+'">显示阶段<select class="preview-stage" id="'+(EncodeHtml $stageSelectId)+'">')
  foreach($stage in @($upgrade.Stages)){Emit ('<option value="'+(EncodeHtml $stage.Stage)+'">'+(EncodeHtml $stage.Stage)+'</option>')}
  Emit ('</select></label><label for="'+(EncodeHtml $levelSelectId)+'">强化等级<select class="preview-level" id="'+(EncodeHtml $levelSelectId)+'">')
  foreach($view in @($cardViews|Where-Object{$_.stage -eq $firstView.stage})){$levelLabel=if($view.level -eq 0){'基础 +0'}else{'强化 +'+$view.level};Emit ('<option value="'+$view.level+'">'+$levelLabel+'</option>')}
  Emit ('</select></label></div></div><div class="visual-columns"><figure class="visual-panel card-art"><div class="visual-panel-header"><strong>原始卡图</strong><span class="original-art-size">'+$firstView.originalWidth+' × '+$firstView.originalHeight+' 像素</span></div><div class="visual-image-area"><a class="preview-image-link original-art-link" data-image-title="'+(EncodeHtml ($card.Name+' · 原始卡图'))+'" href="'+(EncodeHtml $firstView.originalImage)+'" target="_blank" rel="noopener"><img class="original-art-image" src="'+(EncodeHtml $firstView.originalImage)+'" width="'+$firstView.originalWidth+'" height="'+$firstView.originalHeight+'" alt="'+(EncodeHtml $card.Name)+'原始卡图" loading="lazy" decoding="async"></a></div><figcaption class="original-art-caption">'+(EncodeHtml $firstView.stage)+' · 原始文件，点击查看 1:1 像素</figcaption></figure>')
  Emit ('<figure class="visual-panel game-card-panel"><div class="visual-panel-header"><strong>游戏卡面（卡组／工坊）</strong><span class="game-view-state">'+(EncodeHtml $firstView.stage)+' · 基础 +0</span></div><div class="visual-image-area"><a class="preview-image-link game-card-link" data-image-title="'+(EncodeHtml ($card.Name+' · '+$firstView.stage+' · 基础 +0 · 游戏卡面'))+'" href="'+(EncodeHtml $firstView.image)+'" target="_blank" rel="noopener"><img class="game-card-image" src="'+(EncodeHtml $firstView.image)+'" width="'+$firstView.width+'" height="'+$firstView.height+'" style="--game-design-width:'+([string]$firstView.designWidth)+'px" alt="'+(EncodeHtml ($card.Name+' '+$firstView.stage+' 基础 +0 游戏卡面'))+'" loading="lazy" decoding="async"></a></div><figcaption class="game-view-caption">包含游戏中的卡框、卡图、名称、效果和费用图标</figcaption></figure></div><p class="view-size-note">右侧按游戏基准界面比例显示；窄屏会适应可用宽度。放大查看不会改变游戏内卡牌尺寸。</p></section><div class="card-body"><div class="card-content">')
  foreach($group in $groups){
   $stage=$group.Data
   Emit ('<section class="stage-block" data-stages="'+(EncodeHtml ($group.Stages -join '|'))+'"><div class="stage-heading"><h4>'+(EncodeHtml ($group.Stages -join '／'))+'</h4><p class="stage-limit">'+(EncodeHtml $stage.LimitExplanation)+'</p></div><div class="rank-grid">')
   foreach($level in @($stage.Levels)){
    $rankClass=if([int]$level.Level -eq 0){'base'}else{'upgraded'}
    $rankLabel=if([int]$level.Level -eq 0){'基础效果 +0'}else{'升级效果 +'+[int]$level.Level}
    Emit ('<div class="rank '+$rankClass+'" data-level="'+[int]$level.Level+'"><div class="rank-label">'+$rankLabel+'</div>')
    if([string]$level.Name -cne [string]$card.Name){Emit ('<p class="rank-name">'+(EncodeHtml $level.Name)+'</p>')}
    $playCost='能量 '+$level.Energy
    if([string]$level.OtherCost -ne '—'){$playCost+=' · '+$level.OtherCost}
    Emit ('<p class="play-cost">'+(EncodeHtml $playCost)+'</p><p class="effect">'+(EffectHtml $level)+'</p>')
    $keywordLinks=@($level.Keywords|ForEach-Object{$entry=$terms[([string]$_.Id+'|'+[string]$_.Meaning)];'<a href="#'+(EncodeHtml $entry.Anchor)+'" data-open-keywords title="'+(EncodeHtml $_.Meaning)+'"><strong>'+(EncodeHtml $_.Name)+'</strong></a>'})
    $keywordsHtml=if($keywordLinks.Count -eq 0){'<span class="empty">无</span>'}else{$keywordLinks -join '、'}
    Emit ('<p class="rank-keywords"><span class="muted">关键词：</span>'+$keywordsHtml+'</p>')
    if([int]$level.Level -gt 0){Emit ('<p class="upgrade-cost">'+(EncodeHtml $level.UpgradeCost)+'</p>')}
    Emit '</div>'
   }
   Emit '</div></section>'
  }
  Emit '<section class="keyword-panel"><h4>关键词介绍</h4>'
  if($terms.Count -eq 0){Emit '<p class="no-keywords">此卡各阶段及升级等级均无关键词。</p>'}
  else{
   Emit '<dl class="keyword-definitions">'
   foreach($term in $terms.Values){Emit ('<div id="'+(EncodeHtml $term.Anchor)+'" class="keyword-definition"><dt>'+(EncodeHtml $term.Name)+'</dt><dd>'+(EncodeHtml $term.Meaning)+'</dd></div>')}
   Emit '</dl>'
  }
  Emit '</section></div></div>'
  $reviewCheckboxId='review-check-'+[string]$card.Id
  $reviewNotesId='review-notes-'+[string]$card.Id
  Emit ('<section class="card-review" aria-label="'+(EncodeHtml $card.Name)+'审核"><label class="review-check-label" for="'+(EncodeHtml $reviewCheckboxId)+'"><input id="'+(EncodeHtml $reviewCheckboxId)+'" class="review-checkbox" type="checkbox"><span>已检查</span></label><div class="review-note-field"><label for="'+(EncodeHtml $reviewNotesId)+'">修改建议</label><textarea id="'+(EncodeHtml $reviewNotesId)+'" class="review-notes" rows="3" placeholder="填写这张卡的效果、升级、卡图或关键词修改建议；无建议可留空。"></textarea><div class="review-card-print"></div></div></section></article>')
 }
 Emit '</section>'
}
if($seenStageCount -ne [int]$upgrades.StageCount -or $seenRankCount -ne [int]$upgrades.LevelCount -or $seenStageCount -ne $artByStage.Count -or $seenRankCount -ne $gameByRank.Count){throw 'Rendered stage/rank/art/game view count mismatch.'}
Emit ('<details id="glossary" class="reference-panel"><summary>关键词总表 · '+@($catalog.Glossary).Count+' 组</summary><div class="reference-content"><table class="glossary-table"><thead><tr><th scope="col">关键词</th><th scope="col">介绍</th></tr></thead><tbody>')
foreach($term in @($catalog.Glossary)){Emit ('<tr id="keyword-'+(EncodeHtml $term.Id)+'"><th scope="row">'+(EncodeHtml (@($term.Names) -join '／'))+'</th><td>'+(EncodeHtml $term.Meaning)+'</td></tr>')}
Emit '</tbody></table></div></details><details id="catalog-rules" class="reference-panel"><summary>升级、解锁与场地规则</summary><div class="reference-content"><h4>升级规则</h4><ul class="rules-list">'
foreach($rule in @($upgrades.Rules)){Emit ('<li>'+(EncodeHtml $rule)+'</li>')}
Emit '</ul><h4 id="school-unlocks">解锁规则</h4><ul class="rules-list">'
$unlockText=(@($catalog.SchoolUnlocks)|ForEach-Object{$_.School+' '+$_.RequiredLevel+' 级'}) -join '、'
Emit ('<li>基础流派牌：'+(EncodeHtml $unlockText)+'。普通常规牌无流派等级门槛。</li><li>'+(EncodeHtml $catalog.SourceCardUnlockRule)+'</li>')
Emit '</ul><h4 id="boarding-field-rules">接舷场地继承规则</h4><ul class="rules-list">'
foreach($rule in @($catalog.Rules)){Emit ('<li>'+(EncodeHtml $rule)+'</li>')}
Emit '</ul></div></details>'
Emit ('<footer>数据日期：'+(EncodeHtml $catalog.Date)+' · '+$cards.Count+' 个卡牌条目 · '+$seenStageCount+' 个可用阶段 · '+$seenRankCount+' 个合法等级与阶段组合及游戏卡面。已删除的通用船员牌、通用部件牌不再列出；合并别名不重复列出。原图直接使用游戏当前素材，游戏卡面由当前游戏界面渲染导出。</footer>')
Emit @'
</main><dialog id="card-image-dialog" class="card-image-dialog" aria-labelledby="card-image-dialog-title"><div class="image-view-header"><h2 id="card-image-dialog-title">卡图预览</h2><button id="image-view-close" type="button" autofocus>关闭</button></div><div class="image-view-tools"><button id="image-view-fit" type="button" aria-pressed="true">适应窗口</button><button id="image-view-pixels" type="button" aria-pressed="false">1:1 像素</button><span id="image-view-size" class="image-view-size"></span><a id="image-view-original-link" href="#" target="_blank" rel="noopener">新窗口打开原文件</a></div><div id="image-view-canvas" class="image-view-canvas"><img id="card-image-dialog-image" alt=""></div></dialog><script>
(()=>{
'use strict';
const $=id=>document.getElementById(id),search=$('card-search'),school=$('school-filter'),stage=$('stage-filter'),source=$('source-filter');
const cards=[...document.querySelectorAll('.card-block')],sections=[...document.querySelectorAll('.school-section')];
const normalize=value=>value.normalize('NFKC').toLocaleLowerCase().trim();
const searchText=new Map(cards.map(card=>[card,normalize(card.dataset.search)]));
function update(){const terms=normalize(search.value).split(/\s+/).filter(Boolean);let count=0;
cards.forEach(card=>{const visible=(school.value==='all'||school.value===card.dataset.school)&&(stage.value==='all'||card.dataset.stages.split('|').includes(stage.value))&&(source.value==='all'||source.value===card.dataset.source)&&terms.every(term=>searchText.get(card).includes(term))&&(!window.CardReview||window.CardReview.matches(card));card.hidden=!visible;if(visible)count++;card.querySelectorAll('.stage-block').forEach(block=>{block.hidden=stage.value!=='all'&&!block.dataset.stages.split('|').includes(stage.value);});});
sections.forEach(section=>{const count=[...section.querySelectorAll('.card-block')].filter(card=>!card.hidden).length;section.hidden=count===0;section.querySelector('.school-count').textContent=count;});
$('filter-summary').textContent='显示 '+count+' / '+cards.length+' 张卡牌';$('no-results').hidden=count!==0;
}
search.addEventListener('input',update);[school,stage,source,$('review-filter')].forEach(control=>control.addEventListener('change',update));window.addEventListener('card-review-change',update);
$('clear-filters').addEventListener('click',()=>{search.value='';school.value=stage.value=source.value=$('review-filter').value='all';update();search.focus();});
function toggle(button,cls,collapsed,expanded){button.addEventListener('click',()=>{const value=document.body.classList.toggle(cls);button.setAttribute('aria-pressed',String(value));button.textContent=value?expanded:collapsed;});}
toggle($('toggle-upgrades'),'compact-upgrades','收起升级效果','展开升级效果');toggle($('toggle-keywords'),'compact-keywords','收起关键词介绍','展开关键词介绍');
$('print-report').addEventListener('click',()=>window.print());
document.querySelectorAll('[data-open-reference]').forEach(link=>link.addEventListener('click',()=>{const target=document.querySelector(link.getAttribute('href'));if(target)target.open=true;}));
document.querySelectorAll('[data-open-keywords]').forEach(link=>link.addEventListener('click',()=>{document.body.classList.remove('compact-keywords');$('toggle-keywords').setAttribute('aria-pressed','false');$('toggle-keywords').textContent='收起关键词介绍';}));
function revealHash(){if(!location.hash)return;const target=document.getElementById(decodeURIComponent(location.hash.slice(1)));if(!target)return;const details=target.closest('details');if(details)details.open=true;}
window.addEventListener('hashchange',revealHash);revealHash();update();
let printStates=[];window.addEventListener('beforeprint',()=>{printStates=[...document.querySelectorAll('.reference-panel')].map(el=>[el,el.open]);printStates.forEach(([el])=>el.open=true);});window.addEventListener('afterprint',()=>printStates.forEach(([el,open])=>el.open=open));
})();
</script>
'@
Emit '<script>'
Emit ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CardReview.js')))
Emit '</script><script>'
Emit ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CardVisualCompare.js')))
Emit '</script></body></html>'
if(-not [IO.Directory]::Exists($outputRoot)){[void][IO.Directory]::CreateDirectory($outputRoot)}
[IO.File]::WriteAllText($outputFile,$html.ToString(),[Text.UTF8Encoding]::new($false))
Write-Output ('Rendered illustrated catalogue: {0} cards, {1} stages, {2} rank/stage rows, {3} glossary entries: {4}' -f $cards.Count,$seenStageCount,$seenRankCount,@($catalog.Glossary).Count,$outputFile)
