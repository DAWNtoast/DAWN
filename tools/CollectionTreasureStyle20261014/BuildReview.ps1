$ErrorActionPreference = 'Stop'
$styleRoot = 'D:\Game\Artifacts\CollectionTreasureStyle20261014'
$styleScreens = Join-Path $styleRoot 'validation\final\Screens'
$styleReport = Get-Content -Raw -LiteralPath (Join-Path $styleScreens 'report.json') | ConvertFrom-Json
if (-not $styleReport.passed) { throw 'Final screenshots have not passed.' }
$styleLabels = [ordered]@{
    collection = '收藏室 · 藏品陈列'
    collectionsingle = '收藏室 · 单件藏品'
    catalog = '收藏室 · 藏品图鉴'
    storage = '收藏室 · 基础货物'
    rarestorage = '收藏室 · 珍稀物品'
    itemactions = '收藏室 · 物品操作'
    adjustment = '收藏室 · 存入与取出'
    sale = '收藏室 · 出售'
    foliooverview = '藏宝图阁 · 藏图册总览'
    foliounused = '藏宝图阁 · 未使用的藏宝图'
    foliounusedsingle = '藏宝图阁 · 单张藏宝图'
    foliounusedlongseed = '藏宝图阁 · 64 字符种子'
    foliohistory = '藏宝图阁 · 历史记录'
    foliohistoryempty = '藏宝图阁 · 空历史记录'
    foliolegacyhistory = '藏宝图阁 · 旧存档记录'
    treasuredesk = '藏宝图阁 · 拼图工作台'
}
$styleCards = foreach ($styleKey in $styleLabels.Keys) {
    $styleFile = $styleKey + '_1920x1080.png'
    if (-not (Test-Path -LiteralPath (Join-Path $styleScreens $styleFile))) { throw "Missing $styleFile" }
    $styleTitle = [System.Net.WebUtility]::HtmlEncode($styleLabels[$styleKey])
    "<figure><a href='validation/final/Screens/$styleFile' target='_blank'><img loading='lazy' src='validation/final/Screens/$styleFile' alt='$styleTitle'></a><figcaption>$styleTitle</figcaption></figure>"
}
$styleHtml = @"
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>收藏室与藏宝图阁 · 市场样式统一</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#171512;color:#f2e7d1;font:16px/1.7 system-ui,'Microsoft YaHei',sans-serif}main{max-width:1500px;margin:auto;padding:42px 28px}h1{font-size:30px;margin:0 0 12px}p{color:#cfbfa7;max-width:1040px;margin:10px 0}.checks{display:flex;gap:12px;flex-wrap:wrap;margin:24px 0}.checks span{border:1px solid #78613f;background:#29241c;padding:8px 18px;border-radius:5px}.gallery{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:22px}figure{margin:0;background:#25211b;border:1px solid #4b4030}img{display:block;width:100%;height:auto}figcaption{padding:10px 16px}a{color:#e7c58a}details{margin:20px 0}summary{cursor:pointer}@media(max-width:850px){.gallery{grid-template-columns:1fr}main{padding:24px 16px}}
</style><main>
<h1>收藏室与藏宝图阁</h1>
<p>以现有市场二级窗口为标准：统一象牙色纸面、细木框、黄铜包角、浅色控件与深木主按钮。收藏室覆盖账册、图鉴和物品弹窗；藏图册增加分类总览，地图展示面积约增加 29%，单图居中。</p>
<div class="checks"><span>40 项布局测试通过</span><span>13 项交互回归通过</span><span>$($styleReport.pages.Count) 张实际 Unity 渲染</span><span>720p / 1080p / 1440p / 1280×1024</span></div>
<p>截图为隔离工程中的生产界面与样本数据；交互回归覆盖翻页、关闭、藏品陈列、库存存取、出售、排序与旧回调防护。点击图片查看原尺寸。</p>
<details><summary>验证记录</summary><p><a href="validation/edit02/results.xml">布局测试 40/40</a> · <a href="validation/flow01/results.xml">首轮交互 12/13</a> · <a href="validation/flow02/results.xml">配色预期更新后，剩余 1/1 通过</a> · <a href="validation/final/Screens/report.json">48 帧控件边界报告</a></p><p>首轮交互唯一失败项仍要求旧版深底浅字；已按新纸面深字更新断言并通过复测。未更改库存、经济、陈列与藏宝图规则。</p></details>
<div class="gallery">$($styleCards -join "`n")</div>
</main></html>
"@
[IO.File]::WriteAllText((Join-Path $styleRoot 'review.html'), $styleHtml, [Text.UTF8Encoding]::new($false))
Write-Output (Join-Path $styleRoot 'review.html')
