param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$DataPath,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$reportInputPath = [IO.Path]::GetFullPath($DataPath)
$reportOutputPath = [IO.Path]::GetFullPath($OutputPath)
$reportData = Get-Content -LiteralPath $reportInputPath -Raw -Encoding UTF8 | ConvertFrom-Json
$reportCards = @($reportData.Cards)
$reportGlossary = @($reportData.Glossary)
if ($reportCards.Count -eq 0 -or $reportGlossary.Count -eq 0) {
    throw 'The report requires Cards and Glossary entries.'
}

function ConvertTo-ReportHtml([object]$Value) {
    return [Net.WebUtility]::HtmlEncode([string]$Value)
}

$reportHtml = [Text.StringBuilder]::new(256000)
function Add-ReportHtml([string]$Value) {
    [void]$reportHtml.AppendLine($Value)
}

$reportSchoolOrder = @(
    [pscustomobject]@{ Id = ''; Name = '普通' },
    [pscustomobject]@{ Id = 'fire'; Name = '火焰' },
    [pscustomobject]@{ Id = 'arcane'; Name = '秘术' },
    [pscustomobject]@{ Id = 'curse'; Name = '诅咒' },
    [pscustomobject]@{ Id = 'tide'; Name = '潮汐' },
    [pscustomobject]@{ Id = 'thunder'; Name = '雷霆' },
    [pscustomobject]@{ Id = 'blood'; Name = '血契' }
)
$reportKnownSchools = @($reportSchoolOrder | ForEach-Object { $_.Id })
$reportUnknownSchools = @($reportCards | Where-Object { [string]$_.SchoolId -notin $reportKnownSchools })
if ($reportUnknownSchools.Count -gt 0) {
    throw ('Unknown school in report: ' + ($reportUnknownSchools.Id -join ', '))
}
$reportGlossaryIds = @{}
foreach ($reportTerm in $reportGlossary) {
    if ([string]::IsNullOrWhiteSpace([string]$reportTerm.Id) -or $reportGlossaryIds.ContainsKey([string]$reportTerm.Id)) {
        throw ('Missing or duplicate glossary ID: ' + $reportTerm.Id)
    }
    $reportGlossaryIds[[string]$reportTerm.Id] = $true
}

Add-ReportHtml @'
<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>全卡牌清单 · 按系别</title>
<style>
:root { color-scheme: light; --ink:#172c3a; --muted:#566a77; --line:#d7e1e5; --paper:#fff; --accent:#086f78; }
* { box-sizing:border-box; }
html { scroll-behavior:smooth; }
body { margin:0; background:#eef3f5; color:var(--ink); font-family:"Microsoft YaHei","PingFang SC",system-ui,sans-serif; line-height:1.65; }
main { max-width:1560px; margin:auto; padding:42px 32px 72px; }
header { margin-bottom:30px; }
.eyebrow { margin:0 0 8px; color:var(--accent); font-size:13px; font-weight:700; letter-spacing:.12em; }
h1 { margin:0 0 12px; font-size:clamp(28px,4vw,42px); line-height:1.25; }
h2 { margin:0; font-size:24px; line-height:1.4; }
.lead { margin:0; max-width:1000px; font-size:16px; }
.notes { margin:12px 0 0; max-width:1160px; color:var(--muted); font-size:14px; }
a { color:var(--accent); text-underline-offset:3px; }
a:hover { color:#04464b; }
a:focus-visible,button:focus-visible,input:focus-visible,select:focus-visible { outline:3px solid #d69a2d; outline-offset:3px; }
.panel { background:var(--paper); border:1px solid var(--line); border-radius:14px; overflow:visible; }
.section-heading { display:flex; align-items:baseline; justify-content:space-between; flex-wrap:wrap; gap:10px; padding:20px 24px; }
.section-heading p { margin:0; color:var(--muted); font-size:14px; }
table { width:100%; border-collapse:collapse; table-layout:fixed; }
th,td { padding:15px 18px; text-align:left; vertical-align:top; border-top:1px solid var(--line); overflow-wrap:anywhere; }
thead th { background:#f2f7f8; color:#415a67; font-size:13px; font-weight:700; }
.glossary-table th { width:27%; font-size:15px; }
.glossary-table td { font-size:15px; }
.glossary-table tr:target { background:#fff1cd; outline:2px solid #d69a2d; outline-offset:-2px; }
.glossary-table tbody tr { scroll-margin-top:20px; }
.toolbar { margin:32px 0 18px; padding:20px 24px; }
.controls { display:grid; grid-template-columns:minmax(240px,1fr) 180px auto auto; gap:14px; align-items:end; }
label { display:block; font-size:13px; font-weight:700; }
input,select,button { font:inherit; }
input,select { display:block; width:100%; min-height:44px; margin-top:5px; padding:8px 12px; border:1px solid #9fb5bf; border-radius:7px; background:#fff; color:var(--ink); }
button { min-height:44px; padding:8px 15px; border:1px solid #9fb5bf; border-radius:7px; background:#f5f9fa; color:var(--ink); cursor:pointer; }
button:hover { background:#e6f0f2; }
.filter-summary { margin:13px 0 0; color:var(--muted); font-size:14px; }
.jump-links { display:flex; flex-wrap:wrap; gap:8px 20px; margin:18px 0 24px; font-size:14px; }
.school-section { margin-top:24px; scroll-margin-top:18px; }
.school-section h2 { padding-left:12px; border-left:5px solid var(--school-color,#687c86); }
.card-table col:nth-child(1) { width:20%; }
.card-table col:nth-child(2) { width:9%; }
.card-table col:nth-child(3) { width:19%; }
.card-table col:nth-child(4) { width:38%; }
.card-table col:nth-child(5) { width:14%; }
.card-block { font-size:14px; }
.card-block:nth-of-type(even) { background:#fafcfc; }
.card-block th { font-weight:400; }
.card-name { display:block; font-size:16px; font-weight:700; line-height:1.5; }
.card-number { display:block; margin-bottom:4px; color:var(--muted); font-size:12px; font-variant-numeric:tabular-nums; }
.card-source { display:block; margin-top:7px; color:var(--muted); font-size:12px; }
.stage-name { color:#425c6a; }
.variant-name { display:block; margin-bottom:6px; font-weight:700; }
.energy { display:block; font-weight:700; margin-bottom:6px; }
.other-cost,.effect { white-space:pre-line; }
.keywords a { display:inline; }
.empty { color:#6d7d86; }
.no-results { padding:28px; text-align:center; background:#fff; border:1px solid var(--line); border-radius:12px; }
footer { margin-top:30px; color:var(--muted); font-size:13px; }
[hidden] { display:none !important; }
@media (max-width:850px) {
    main { padding:24px 12px 48px; }
    .controls { grid-template-columns:1fr 1fr; }
    .controls .search-control { grid-column:1/-1; }
    .section-heading,.toolbar { padding:16px; }
    th,td { padding:12px 10px; }
    .card-table col:nth-child(1) { width:20%; }
    .card-table col:nth-child(2) { width:10%; }
    .card-table col:nth-child(3) { width:19%; }
    .card-table col:nth-child(4) { width:35%; }
    .card-table col:nth-child(5) { width:16%; }
    .card-name { font-size:14px; }
    .card-block { font-size:13px; }
}
@media print {
    @page { size:A4 landscape; margin:12mm; }
    html { scroll-behavior:auto; }
    body { background:#fff; font-size:10pt; }
    main { max-width:none; padding:0; }
    h1 { font-size:24pt; }
    h2 { font-size:16pt; }
    header { margin-bottom:14pt; }
    .lead,.notes { font-size:10pt; }
    .toolbar,.jump-links,.back-top,.no-results { display:none !important; }
    .panel { border:0; border-radius:0; }
    .section-heading { padding:12pt 0 8pt; break-after:avoid; }
    .section-heading p { font-size:9pt; }
    .school-section { margin-top:18pt; }
    thead { display:table-header-group; }
    .card-block,.glossary-table tr { break-inside:avoid; page-break-inside:avoid; }
    th,td { padding:7pt 6pt; }
    .card-block,.glossary-table th,.glossary-table td { font-size:9pt; }
    .card-name { font-size:10pt; }
    .card-number,.card-source { font-size:8pt; }
    a { color:inherit; text-decoration:none; }
    footer { font-size:8pt; }
}
</style>
</head>
<body>
<main id="top">
<header>
'@
Add-ReportHtml ('<p class="eyebrow">CARD CATALOG · ' + (ConvertTo-ReportHtml $reportData.Date) + '</p>')
Add-ReportHtml '<h1>全卡牌清单 · 按系别</h1>'
Add-ReportHtml ('<p class="lead">共 <strong>' + $reportCards.Count + '</strong> 张现役卡牌，按普通、火焰、秘术、诅咒、潮汐、雷霆、血契分组；包含基础牌、船员牌和部件牌。下列数值均为 <strong>+0 基础等级</strong>，不含装备加成；不同阶段的效果或消耗分别列出。</p>')
Add-ReportHtml '<p class="notes">先列出全部字段及其说明，再列卡牌信息。字段释义仅解释含义，不包含计算流程。字段列仅列出效果中的悬浮释义关键词；“—”表示没有这类关键词或其他资源消耗。消耗中的资源 X 表示全部对应资源；焚烧 X 表示选择 1～3 张其他手牌。增效消耗用于额外效果，缺少耗材时按卡面使用基础效果；治疗实际支付还受缺失生命影响。实战临时加价、减免和战斗状态变化不计入本表。</p>'
Add-ReportHtml '</header>'
Add-ReportHtml '<section id="glossary" class="panel" aria-labelledby="glossary-title">'
Add-ReportHtml ('<div class="section-heading"><h2 id="glossary-title">字段名称与说明</h2><p>' + $reportGlossary.Count + ' 组字段 · 同义名称合并列出</p></div>')
Add-ReportHtml '<table class="glossary-table"><thead><tr><th scope="col">字段名称</th><th scope="col">说明</th></tr></thead><tbody>'
foreach ($reportTerm in $reportGlossary) {
    Add-ReportHtml ('<tr id="keyword-' + (ConvertTo-ReportHtml $reportTerm.Id) + '"><th scope="row">' + (ConvertTo-ReportHtml (@($reportTerm.Names) -join '／')) + '</th><td>' + (ConvertTo-ReportHtml $reportTerm.Meaning) + '</td></tr>')
}
Add-ReportHtml '</tbody></table></section>'
Add-ReportHtml @'
<section class="toolbar panel" aria-label="筛选卡牌">
<div class="controls">
<label class="search-control" for="card-search">搜索名称、效果、字段或来源<input id="card-search" type="search" placeholder="例如：额外装药、火场、木匠" autocomplete="off"></label>
<label for="school-filter">系别<select id="school-filter"><option value="all">全部系别</option>
'@
foreach ($reportSchool in $reportSchoolOrder) {
    $reportSchoolKey = if ($reportSchool.Id.Length -eq 0) { 'ordinary' } else { $reportSchool.Id }
    Add-ReportHtml ('<option value="' + $reportSchoolKey + '">' + $reportSchool.Name + '</option>')
}
Add-ReportHtml '</select></label><button id="clear-filters" type="button">清除筛选</button><button id="print-report" type="button">打印当前清单</button></div>'
Add-ReportHtml ('<p id="filter-summary" class="filter-summary" role="status" aria-live="polite">显示 ' + $reportCards.Count + ' / ' + $reportCards.Count + ' 张卡牌</p></section>')
Add-ReportHtml '<nav class="jump-links" aria-label="系别目录">'
foreach ($reportSchool in $reportSchoolOrder) {
    $reportSchoolKey = if ($reportSchool.Id.Length -eq 0) { 'ordinary' } else { $reportSchool.Id }
    $reportGroupCount = @($reportCards | Where-Object { [string]$_.SchoolId -eq $reportSchool.Id }).Count
    Add-ReportHtml ('<a href="#school-' + $reportSchoolKey + '">' + $reportSchool.Name + ' · ' + $reportGroupCount + '</a>')
}
Add-ReportHtml '</nav><p id="no-results" class="no-results" hidden>没有符合筛选条件的卡牌。可以减少搜索词或选择其他系别。</p>'
$reportSchoolColors = @{ ordinary = '#687c86'; fire = '#c55936'; arcane = '#7960ae'; curse = '#865e82'; tide = '#287e9a'; thunder = '#987719'; blood = '#a04455' }
foreach ($reportSchool in $reportSchoolOrder) {
    $reportSchoolKey = if ($reportSchool.Id.Length -eq 0) { 'ordinary' } else { $reportSchool.Id }
    $reportSchoolCards = @($reportCards | Where-Object { [string]$_.SchoolId -eq $reportSchool.Id } | Sort-Object { [int]$_.Number }, Id)
    Add-ReportHtml ('<section id="school-' + $reportSchoolKey + '" class="school-section panel" data-school="' + $reportSchoolKey + '" style="--school-color:' + $reportSchoolColors[$reportSchoolKey] + '" aria-labelledby="heading-' + $reportSchoolKey + '">')
    Add-ReportHtml ('<div class="section-heading"><h2 id="heading-' + $reportSchoolKey + '">' + $reportSchool.Name + '</h2><p><span class="school-count">' + $reportSchoolCards.Count + '</span> 张卡牌 · <a class="back-top" href="#glossary">返回字段说明</a></p></div>')
    Add-ReportHtml '<table class="card-table"><colgroup><col><col><col><col><col></colgroup><thead><tr><th scope="col">名称</th><th scope="col">阶段</th><th scope="col">消耗</th><th scope="col">效果</th><th scope="col">字段</th></tr></thead>'
    foreach ($reportCard in $reportSchoolCards) {
        $reportVariants = @($reportCard.StageVariants)
        if ($reportVariants.Count -eq 0) { throw ('Missing stage variants: ' + $reportCard.Id) }
        $reportSearchParts = [Collections.Generic.List[string]]::new()
        foreach ($reportValue in @($reportCard.Name, $reportCard.Id, $reportCard.Number, $reportCard.School, $reportCard.Source)) {
            $reportSearchParts.Add([string]$reportValue)
        }
        foreach ($reportVariant in $reportVariants) {
            foreach ($reportValue in @($reportVariant.Name, $reportVariant.Effect, $reportVariant.OtherCost, (@($reportVariant.Stages) -join ' '))) {
                $reportSearchParts.Add([string]$reportValue)
            }
            foreach ($reportKeyword in @($reportVariant.Keywords)) {
                if (-not $reportGlossaryIds.ContainsKey([string]$reportKeyword.Id)) {
                    throw ('Unknown keyword ' + $reportKeyword.Id + ' in card ' + $reportCard.Id)
                }
                $reportSearchParts.Add([string]$reportKeyword.Name)
                $reportSearchParts.Add([string]$reportKeyword.Meaning)
            }
        }
        Add-ReportHtml ('<tbody class="card-block" data-card-id="' + (ConvertTo-ReportHtml $reportCard.Id) + '" data-school="' + $reportSchoolKey + '" data-search="' + (ConvertTo-ReportHtml ($reportSearchParts -join ' ')) + '">')
        for ($reportVariantIndex = 0; $reportVariantIndex -lt $reportVariants.Count; $reportVariantIndex++) {
            $reportVariant = $reportVariants[$reportVariantIndex]
            Add-ReportHtml '<tr>'
            if ($reportVariantIndex -eq 0) {
                Add-ReportHtml ('<th scope="rowgroup" rowspan="' + $reportVariants.Count + '"><span class="card-number">#' + (ConvertTo-ReportHtml $reportCard.Number) + '</span><span class="card-name">' + (ConvertTo-ReportHtml $reportCard.Name) + '</span><span class="card-source">' + (ConvertTo-ReportHtml $reportCard.Source) + '</span></th>')
            }
            Add-ReportHtml ('<td class="stage-name">' + (ConvertTo-ReportHtml (@($reportVariant.Stages) -join '／')) + '</td>')
            Add-ReportHtml ('<td><span class="energy">能量：' + (ConvertTo-ReportHtml $reportVariant.Energy) + '</span><div class="other-cost">' + (ConvertTo-ReportHtml $reportVariant.OtherCost) + '</div></td>')
            Add-ReportHtml '<td>'
            if ([string]$reportVariant.Name -ne [string]$reportCard.Name) {
                Add-ReportHtml ('<span class="variant-name">' + (ConvertTo-ReportHtml $reportVariant.Name) + '</span>')
            }
            Add-ReportHtml ('<div class="effect">' + (ConvertTo-ReportHtml $reportVariant.Effect) + '</div></td><td class="keywords">')
            $reportKeywordLinks = @($reportVariant.Keywords | ForEach-Object {
                '<a href="#keyword-' + (ConvertTo-ReportHtml $_.Id) + '" title="' + (ConvertTo-ReportHtml $_.Meaning) + '">' + (ConvertTo-ReportHtml $_.Name) + '</a>'
            })
            if ($reportKeywordLinks.Count -eq 0) { Add-ReportHtml '<span class="empty">—</span>' }
            else { Add-ReportHtml ($reportKeywordLinks -join '、') }
            Add-ReportHtml '</td></tr>'
        }
        Add-ReportHtml '</tbody>'
    }
    Add-ReportHtml '</table></section>'
}
Add-ReportHtml ('<footer>数据日期：' + (ConvertTo-ReportHtml $reportData.Date) + ' · ' + $reportCards.Count + ' 个现役卡牌身份 · ' + (ConvertTo-ReportHtml $reportData.StageCount) + ' 个可用阶段定义。相同阶段效果与消耗已合并显示。退役卡牌及合并别名不重复列出。</footer>')
Add-ReportHtml @'
</main>
<script>
(() => {
    'use strict';
    const search = document.getElementById('card-search');
    const school = document.getElementById('school-filter');
    const cards = Array.from(document.querySelectorAll('.card-block'));
    const sections = Array.from(document.querySelectorAll('.school-section'));
    const summary = document.getElementById('filter-summary');
    const normalize = value => value.normalize('NFKC').toLocaleLowerCase().trim();
    const searchTexts = new Map(cards.map(card => [card, normalize(card.dataset.search)]));
    const update = () => {
        const terms = normalize(search.value).split(/\s+/).filter(Boolean);
        let matchingCards = 0;
        cards.forEach(card => {
            const visible = (school.value === 'all' || school.value === card.dataset.school)
                && terms.every(term => searchTexts.get(card).includes(term));
            card.hidden = !visible;
            if (visible) matchingCards += 1;
        });
        sections.forEach(section => {
            const count = Array.from(section.querySelectorAll('.card-block')).filter(card => !card.hidden).length;
            section.hidden = count === 0;
            section.querySelector('.school-count').textContent = count;
        });
        summary.textContent = `显示 ${matchingCards} / ${cards.length} 张卡牌`;
        document.getElementById('no-results').hidden = matchingCards !== 0;
    };
    search.addEventListener('input', update);
    school.addEventListener('change', update);
    document.getElementById('clear-filters').addEventListener('click', () => {
        search.value = '';
        school.value = 'all';
        update();
        search.focus();
    });
    document.getElementById('print-report').addEventListener('click', () => window.print());
    update();
})();
</script>
</body>
</html>
'@
$reportOutputDirectory = [IO.Path]::GetDirectoryName($reportOutputPath)
if (-not [IO.Directory]::Exists($reportOutputDirectory)) {
    [void][IO.Directory]::CreateDirectory($reportOutputDirectory)
}
[IO.File]::WriteAllText($reportOutputPath, $reportHtml.ToString(), [Text.UTF8Encoding]::new($false))
Write-Output ('Rendered ' + $reportCards.Count + ' cards and ' + $reportGlossary.Count + ' glossary entries: ' + $reportOutputPath)
