param(
    [string]$ArtifactDirectory = ''
)

$ErrorActionPreference = 'Stop'
$reviewWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([string]::IsNullOrWhiteSpace($ArtifactDirectory)) {
    $ArtifactDirectory = Join-Path $reviewWorkspace 'UnityClient/Artifacts/NewGamePreviewAudit20260930'
}
$reviewRoot = [IO.Path]::GetFullPath($ArtifactDirectory)
if (-not (Test-Path -LiteralPath $reviewRoot -PathType Container)) {
    throw "Artifact directory does not exist: $reviewRoot"
}

function ConvertTo-ReviewHtml([object]$Value) {
    return [System.Net.WebUtility]::HtmlEncode([string]$Value)
}

function ConvertTo-ReviewUrl([string]$RelativePath) {
    return (($RelativePath.Replace('\', '/') -split '/' | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/')
}

function Read-ReviewTestResult([string]$File, [string]$Title) {
    $result = [ordered]@{ File = $File; Title = $Title; Status = 'pending'; Label = '待生成'; Detail = '尚未读取到此 XML；不据此宣称通过。'; Failures = @() }
    $path = Join-Path $reviewRoot $File
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return [pscustomobject]$result }
    try {
        $document = [System.Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($path)
        $run = $document.SelectSingleNode('/test-run')
        if ($null -eq $run -or -not $run.HasAttribute('total') -or -not $run.HasAttribute('failed')) {
            throw 'XML 缺少 NUnit test-run 统计。'
        }
        $total = [int]$run.GetAttribute('total')
        $passed = [int]$run.GetAttribute('passed')
        $failed = [int]$run.GetAttribute('failed')
        $skipped = [int]$run.GetAttribute('skipped')
        $state = $run.GetAttribute('result')
        $result.Status = if ($state -eq 'Passed' -and $total -gt 0 -and $failed -eq 0) { 'pass' } elseif ($failed -gt 0 -or $state -eq 'Failed') { 'fail' } else { 'pending' }
        $result.Label = if ($result.Status -eq 'pass') { '通过' } elseif ($result.Status -eq 'fail') { '失败' } else { $state }
        $result.Detail = "通过 $passed / $total · 失败 $failed · 跳过 $skipped · XML 状态 $state"
        $result.Detail += ' · 结束 ' + $run.GetAttribute('end-time')
        $result.Failures = @($document.SelectNodes("//test-case[@result='Failed']") | ForEach-Object { $_.GetAttribute('fullname') })
    } catch {
        $result.Status = 'fail'
        $result.Label = '报告不可读'
        $result.Detail = $_.Exception.Message
    }
    return [pscustomobject]$result
}

$reviewPlayModeXml = if (Test-Path -LiteralPath (Join-Path $reviewRoot 'PlayMode-full-final.xml') -PathType Leaf) { 'PlayMode-full-final.xml' } else { 'PlayMode-full.xml' }
$reviewEditModeXml = if (Test-Path -LiteralPath (Join-Path $reviewRoot 'EditMode-full-final.xml') -PathType Leaf) { 'EditMode-full-final.xml' } else { 'EditMode-full.xml' }
$reviewTests = @(
    Read-ReviewTestResult $reviewPlayModeXml '完整 PlayMode 回归'
    Read-ReviewTestResult $reviewEditModeXml '完整 EditMode 回归'
    Read-ReviewTestResult 'FinalFlow.xml' '最终新游戏流程与截图'
)
$reviewFinalFolder = if (Test-Path -LiteralPath (Join-Path $reviewRoot 'FinalFlowVerified/report.json') -PathType Leaf) { 'FinalFlowVerified' } else { 'FinalFlow' }
$reviewFinalFrames = Test-Path -LiteralPath (Join-Path $reviewRoot ($reviewFinalFolder + '/report.json')) -PathType Leaf
$reviewAfterFolder = if ($reviewFinalFrames) { $reviewFinalFolder } else { 'After' }
$reviewAfterLabel = if ($reviewFinalFrames) { '最终运行截图' } else { '中间运行截图 · 最终帧待生成' }
$reviewReferenceFolder = if (Test-Path -LiteralPath (Join-Path $reviewRoot 'ReferencesVerified/report.json') -PathType Leaf) { 'ReferencesVerified' } else { 'ReferencesFinal' }
$reviewReferenceLabel = if ($reviewReferenceFolder -eq 'ReferencesVerified') { '修正后的保存预览' } else { '中间保存预览 · 最终验证待生成' }
$reviewReferenceReport = $null
$reviewReferenceError = ''
try {
    $referencePath = Join-Path $reviewRoot ($reviewReferenceFolder + '/report.json')
    if (Test-Path -LiteralPath $referencePath -PathType Leaf) {
        $reviewReferenceReport = Get-Content -LiteralPath $referencePath -Raw | ConvertFrom-Json
    }
} catch { $reviewReferenceError = $_.Exception.Message }

$reviewCards = @(
    @{ Id = 'mainmenu'; Title = '主菜单'; Frame = '00_MainMenu.png'; Reference = ''; Note = '修复新游戏介绍文案裁切；从真实 MainMenu 的「新游戏」进入。本轮保存预览采集没有单列主菜单。'; Changed = $true },
    @{ Id = 'departure'; Title = '出航码头'; Frame = '01_FirstDeparture_Port_Voyage_Setup.png'; Reference = 'UI_VoyageDeparture.png'; Note = '核对目的港、海图与出航入口；引导文字留在区域标题栏内。'; Changed = $true },
    @{ Id = 'navigation'; Title = '航海与昼夜时间轴'; Frame = '02_FirstSail_Sailing_Navigation_Chart_Heading.png'; Reference = 'UI_Navigation.png'; Note = '核对四个面板的边界和浅色纸面文字对比度；时间轴补齐 CanvasRenderer，并接受视窗遮罩。'; Changed = $true },
    @{ Id = 'encounter'; Title = '强制事件'; Frame = '05_FirstMandatory_Encounter_Encounter_Card.png'; Reference = '03_FirstMandatory.png'; Note = '事件仍使用右侧瞭望区内的纸面选项；航海面板和引导文字采用一致布局。'; Changed = $true },
    @{ Id = 'port'; Title = '返港与功能入口'; Frame = '16_Tavern_Port_Port_Hotspot_Voyage.png'; Reference = 'UI_PortHub.png'; Note = '确认首次结算后进入正常港口，并由实际酒馆入口继续。'; Changed = $false },
    @{ Id = 'tavern'; Title = '酒馆'; Frame = '17_RecruitNavigator_Port_Tavern_Crew_Stage.png'; Reference = 'UI_Tavern.png'; Note = '实际港口与参考预览港口不同，船员姓名、设施标题及区域美术允许随港口变化。'; Changed = $false },
    @{ Id = 'crew'; Title = '航海士档案与付费招募'; Frame = '18_RecruitNavigator_Port_Tavern_Crew_Detail.png'; Reference = 'UI_TavernCrewDetail.png'; Note = '通过真实契约按钮招募，随后进行第二次航程的职业事件。'; Changed = $false },
    @{ Id = 'navigator'; Title = '第二航程：潮汐暗标'; Frame = '26_SecondCrewEvent_Encounter_Encounter_Card.png'; Reference = '14_SecondCrewEvent.png'; Note = '正常职业选项完成后进入抵达结算；补给不足的备用选项现在返港整备，不能冒充解读成功。'; Changed = $true },
    @{ Id = 'battle'; Title = '第三航程：首领备战'; Frame = '35_TutorialBoss_Battle_Battle_Card_Preparation.png'; Reference = 'UI_Battle.png'; Note = '修复教学标题和说明超出画面或被标题栏盖住的问题；战斗仍使用默认卡组及合法出牌。'; Changed = $true },
    @{ Id = 'naval'; Title = '首领炮战'; Frame = '36_TutorialBoss_Battle_Battle_Card_Naval.png'; Reference = '25_TutorialBoss.png'; Note = '检查炮战阶段的标题栏、距离、姿态、手牌与回合操作；敌舰与牌面状态取决于实际战斗。'; Changed = $true },
    @{ Id = 'settlement'; Title = '首胜结算与首次绘图'; Frame = '38_DrawChart_Settlement_Settlement_Card.png'; Reference = '28_DrawChart.png'; Note = 'DrawChart 对应真实结算页，已从错误的藏宝图桌副本改为结算布局及「航海士 · 绘制海图」按钮。'; Changed = $true },
    @{ Id = 'warehouse'; Title = '收藏室陈列罗盘'; Frame = '42_DisplayTreasure_Port_Warehouse_Panel.png'; Reference = '31_DisplayTreasure.png'; Note = '修复 7 个保存预览中的序列化引用与父级关系。首胜奖励确认后入库；玩家选择并亲自陈列罗盘，预览库存与运行存档内容可以不同。'; Changed = $true },
    @{ Id = 'shipyard'; Title = '皇家船坞'; Frame = '45_Shipyard_Port_Shipyard_Panel.png'; Reference = 'UI_Shipyard.png'; Note = '通过当前旗舰进入船只档案，再打开配件页；没有自动装配奖励。'; Changed = $false },
    @{ Id = 'equipment'; Title = '亲自装配首胜部件'; Frame = '48_EquipRewardComponent_Port_Shipyard_Equipment_Page.png'; Reference = '37_EquipRewardComponent.png'; Note = '实际装配成功才解除教学锁定；替换下来的旧件保留在库存。'; Changed = $false },
    @{ Id = 'treasure'; Title = '首次进入藏宝图桌'; Frame = '52_First_TreasureMap.png'; Reference = 'UI_TreasureMap.png'; Note = '阅读栏保留所选航线说明和切换提示；历史绘图仅显示在它所属的航线，不再覆盖任意选中的地图。'; Changed = $true }
)

function New-ReviewFigure([string]$RelativePath, [string]$Label) {
    $caption = ConvertTo-ReviewHtml $Label
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or -not (Test-Path -LiteralPath (Join-Path $reviewRoot $RelativePath) -PathType Leaf)) {
        return "<figure><div class='missing'>此截图尚未提供</div><figcaption>$caption</figcaption></figure>"
    }
    $url = ConvertTo-ReviewUrl $RelativePath
    return "<figure><a href='$url' target='_blank' rel='noopener'><img loading='lazy' src='$url' alt='$caption'></a><figcaption>$caption · <a href='$url' target='_blank' rel='noopener'>原图</a></figcaption></figure>"
}

$reviewHtml = [Text.StringBuilder]::new()
[void]$reviewHtml.Append(@'
<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>新游戏至首次藏宝图桌 · 场景与流程复核</title>
<style>
:root{color-scheme:dark;--bg:#11191e;--panel:#1b282f;--line:#40525b;--text:#eee9dc;--muted:#b6c4c9;--gold:#efc77d}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:16px/1.75 "Microsoft YaHei",system-ui,sans-serif}main{max-width:1560px;margin:auto;padding:38px 28px 80px}h1{font-size:32px;line-height:1.35;margin:8px 0 18px}h2{font-size:24px;margin:32px 0 12px}h3{font-size:21px;margin:0 0 6px}p{margin:9px 0;color:var(--muted)}a{color:var(--gold);text-decoration:none}a:hover{text-decoration:underline}.eyebrow{color:var(--gold);letter-spacing:.12em;font-size:13px}.scope{max-width:1150px}.status-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:16px;margin:20px 0}.status,.callout,article{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:20px}.status strong{display:block;font-size:19px}.pill{display:inline-block;padding:1px 10px;border-radius:100px;font-size:13px;font-weight:700;background:#665431;color:#ffe7af}.pill.pass{background:#234d3e;color:#b8efce}.pill.fail{background:#653434;color:#ffd1c6}.detail{font-size:14px;overflow-wrap:anywhere}.small{font-size:13px;color:var(--muted)}.route{padding:17px 22px;background:#26343c;border-left:3px solid var(--gold);border-radius:8px;line-height:2}ul{padding-left:23px}li{margin:8px 0}.fixes{display:grid;grid-template-columns:1fr 1fr;gap:20px}.fixes>div{padding:18px 22px;border:1px solid var(--line);border-radius:10px}.gallery-tools{position:sticky;top:0;z-index:3;padding:12px 16px;border:1px solid var(--line);border-radius:9px;background:#152129ef;backdrop-filter:blur(8px);display:flex;gap:22px;align-items:center;flex-wrap:wrap}.gallery-tools select{padding:7px 12px;background:#26343c;color:var(--text);border:1px solid #63727a;border-radius:5px}article{margin:20px 0;scroll-margin-top:84px}.images{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px;margin-top:16px}figure{margin:0;min-width:0}img,.missing{width:100%;aspect-ratio:16/9;display:block;border:1px solid #43525a;border-radius:5px;background:#090e11;object-fit:contain}.missing{display:grid;place-items:center;color:#869ba7;font-size:14px}figcaption{font-size:13px;color:var(--muted);margin-top:6px}.route-images{grid-template-columns:repeat(3,minmax(0,1fr))}.links{display:flex;gap:10px 20px;flex-wrap:wrap}.failures{color:#ffd1c6}.hidden{display:none}@media(max-width:1000px){.images{grid-template-columns:1fr}.status-grid,.fixes{grid-template-columns:1fr}.route-images{grid-template-columns:1fr}main{padding:24px 16px}h1{font-size:27px}}
</style></head><body><main>
<div class="eyebrow">UNITY · UI 与真实流程复核</div><h1>从新游戏到首次进入藏宝图桌</h1>
<p class="scope">本轮在 Unity PlayMode 中从真实主菜单新游戏开始，通过正常 UI 控件、指针命中检查和合法卡牌操作推进。它是自动化运行中的真实游戏流程，不是人工桌面逐步试玩，也没有以预览场景或注入胜利替代正常航程。</p>
<div class="route">主菜单 → 初航基础事件与返航 → 付费招募航海士 → 第二航程解读潮汐暗标与抵达结算 → 第三航程首领战 → 主动返航、绘图与确认结算 → 收藏室陈列罗盘 → 船坞亲自装配首胜部件 → 首次藏宝图桌 → 切换航线 1 / 2 / 0</div>
<p class="small">界面截帧为 1920 × 1080。回归夹具对所用存档与市场键做隔离与恢复；报告区分正常成功路径与另行构造的低补给边界回归。未声称覆盖所有窗口比例或所有人工输入组合。</p>
<h2>验证状态</h2><p>下列数字直接读取指定 XML，不用日志片段或截图数量代替测试通过结果。</p><div class="status-grid">
'@)
foreach ($test in $reviewTests) {
    $title = ConvertTo-ReviewHtml $test.Title
    $label = ConvertTo-ReviewHtml $test.Label
    $detail = ConvertTo-ReviewHtml $test.Detail
    $xmlLink = if (Test-Path -LiteralPath (Join-Path $reviewRoot $test.File) -PathType Leaf) { "<a href='$(ConvertTo-ReviewUrl $test.File)'>读取原始 XML</a>" } else { "<span class='small'>等待 $(ConvertTo-ReviewHtml $test.File)</span>" }
    [void]$reviewHtml.Append("<div class='status'><strong>$title</strong><span class='pill $($test.Status)'>$label</span><p class='detail'>$detail</p>$xmlLink")
    if ($test.Failures.Count -gt 0) {
        [void]$reviewHtml.Append("<details class='failures'><summary>失败项目</summary><ul>")
        foreach ($failure in $test.Failures) { [void]$reviewHtml.Append('<li>' + (ConvertTo-ReviewHtml $failure) + '</li>') }
        [void]$reviewHtml.Append('</ul></details>')
    }
    [void]$reviewHtml.Append('</div>')
}
[void]$reviewHtml.Append('</div><div class="callout"><strong>保存预览的采集记录</strong>')
if ($null -ne $reviewReferenceReport) {
    $referenceErrors = @($reviewReferenceReport.errors)
    $referencePass = $reviewReferenceFolder -eq 'ReferencesVerified' -and $referenceErrors.Count -eq 0 -and $reviewReferenceReport.successfulCaptureCount -eq $reviewReferenceReport.expectedCaptureCount
    $referenceState = if ($referencePass) { 'pass' } elseif ($referenceErrors.Count -gt 0) { 'fail' } else { 'pending' }
    $referenceStatus = if ($referencePass) { '已验证' } elseif ($referenceErrors.Count -gt 0) { '含错误' } else { '最终验证待生成' }
    [void]$reviewHtml.Append(" <span class='pill $referenceState'>$referenceStatus</span><p>$(ConvertTo-ReviewHtml $reviewReferenceFolder)：成功 $(ConvertTo-ReviewHtml $reviewReferenceReport.successfulCaptureCount) / $(ConvertTo-ReviewHtml $reviewReferenceReport.expectedCaptureCount)；采集时间 $(ConvertTo-ReviewHtml $reviewReferenceReport.capturedAtUtc)。<a href='$(ConvertTo-ReviewUrl ($reviewReferenceFolder + '/report.json'))'>原始 report.json</a></p>")
    if ($referenceErrors.Count -gt 0) {
        [void]$reviewHtml.Append('<details class="failures"><summary>采集错误明细</summary><ul>')
        foreach ($errorText in $referenceErrors) { [void]$reviewHtml.Append('<li>' + (ConvertTo-ReviewHtml $errorText) + '</li>') }
        [void]$reviewHtml.Append('</ul></details>')
    }
} else {
    [void]$reviewHtml.Append('<p>未读取到有效的 ReferencesVerified/report.json；预览采集尚未确认。' + (ConvertTo-ReviewHtml $reviewReferenceError) + '</p>')
}
[void]$reviewHtml.Append(@'
</div><h2>本轮修正</h2><div class="fixes"><div><h3>场景与显示</h3><ul>
<li>把引导标题与操作说明放回标题栏可用区域，避免战斗、港口与结算中的遮挡或越界。</li>
<li>统一航海、事件页面的面板边界、纸面文字对比度和保存预览；昼夜时间轴补齐渲染与遮罩支持。</li>
<li>纠正 <code>28_DrawChart</code> 错误使用藏宝图桌的问题：它现在展示首胜结算页及实际绘图操作布局。</li>
<li>调整藏宝图阅读栏，容纳当前航线说明、绘图记录与切换提示；同步对应的保存预览。</li>
</ul></div><div><h3>真实流程</h3><ul>
<li>航海士事件备用返港原本也会被算作解读成功并直接抵达新港。现在不记录事件完成，不解锁首领航程，按正常返航或拖带规则结算，并保留检查点 2 供补给后重试。</li>
<li>藏宝图桌不再把第一张地图的历史绘图记录显示到其他航线；选择 1 / 2 / 0 后分别核对所属航线资料与记录。</li>
<li>修正第二航程错误提及首领的说明，以及游戏内与文档中残留的“两次航行”说法。当前流程是三次航行。</li>
</ul></div></div>
<h2>逐场景对照</h2><p>左为修复前真实运行，中为最终运行（尚未产出时明确标为中间截图），右为保存的可编辑预览。点击图片可查看原图。预览使用的港口、探索进度、库存、敌舰与文案样本可能不同；这些数据差异不视为布局缺陷。</p>
<div class="gallery-tools"><label><input type="checkbox" id="changed-only"> 仅显示本轮修正重点</label><label>跳至 <select id="scene-picker"><option value="">选择场景</option>
'@)
foreach ($card in $reviewCards) { [void]$reviewHtml.Append("<option value='$($card.Id)'>$(ConvertTo-ReviewHtml $card.Title)</option>") }
[void]$reviewHtml.Append('</select></label></div><section id="gallery">')
foreach ($card in $reviewCards) {
    $flag = if ($card.Changed) { 'yes' } else { 'no' }
    [void]$reviewHtml.Append("<article id='$($card.Id)' data-changed='$flag'><h3>$(ConvertTo-ReviewHtml $card.Title)</h3><p>$(ConvertTo-ReviewHtml $card.Note)</p><div class='images'>")
    [void]$reviewHtml.Append((New-ReviewFigure ('Before/' + $card.Frame) '修复前 · 真实运行'))
    [void]$reviewHtml.Append((New-ReviewFigure ($reviewAfterFolder + '/' + $card.Frame) $reviewAfterLabel))
    $reference = if ($card.Reference) { $reviewReferenceFolder + '/' + $card.Reference } else { '' }
    [void]$reviewHtml.Append((New-ReviewFigure $reference $reviewReferenceLabel))
    [void]$reviewHtml.Append('</div></article>')
}
[void]$reviewHtml.Append('</section><h2>藏宝图切换验证</h2><p>首次进入之后再选择航线 1、2，最后返回 0；运行断言要求保留航线主题、推荐战力、首领与切换提示，并仅在航线 0 显示本次教学绘图。</p><div class="images route-images">')
foreach ($pair in @(@('53_TreasureMap_SelectedRoute_1.png', '航线 1 · 不显示航线 0 的历史绘图'), @('54_TreasureMap_SelectedRoute_2.png', '航线 2 · 不显示航线 0 的历史绘图'), @('55_TreasureMap_SelectedRoute_0.png', '返回航线 0 · 显示所属教学绘图'))) {
    [void]$reviewHtml.Append((New-ReviewFigure ($reviewAfterFolder + '/' + $pair[0]) ($pair[1] + ' · ' + $reviewAfterLabel)))
}
[void]$reviewHtml.Append('</div>')
$reviewSmokePath = Join-Path $reviewRoot 'PlayerSmoke/player-smoke-report.txt'
if (Test-Path -LiteralPath $reviewSmokePath -PathType Leaf) {
    $reviewSmoke = Get-Content -LiteralPath $reviewSmokePath -Raw
    $reviewSmokePassed = $reviewSmoke.StartsWith("PASS`n") -or $reviewSmoke.StartsWith("PASS`r`n")
    $reviewSmokeState = if ($reviewSmokePassed) { 'pass' } else { 'fail' }
    $reviewSmokeLabel = if ($reviewSmokePassed) { '通过' } else { '未通过' }
    [void]$reviewHtml.Append("<h2>Windows 游戏包验收</h2><div class='callout'><span class='pill $reviewSmokeState'>$reviewSmokeLabel</span><p>启动、场景打包、背景与字体、设置开关、界面实际渲染及舰船材质检查。完整教学流程验证见上方 Unity PlayMode 结果。</p><pre class='detail'>$(ConvertTo-ReviewHtml $reviewSmoke)</pre></div>")
}
[void]$reviewHtml.Append('<h2>原始产物</h2><div class="links">')
foreach ($entry in @(@('Before/index.html', '修复前完整截图'), @(($reviewAfterFolder + '/index.html'), '最终流程完整截图'), @('After/index.html', '中间运行完整截图'), @('ReferencesVerified/index.html', '最终保存预览图库'), @('ReferencesVerified/report.json', '最终保存预览报告'), @('preview-layout-sync.json', '布局同步明细'), @('PlayerSmoke/player-smoke-report.txt', 'Windows 游戏包验收记录'))) {
    if (Test-Path -LiteralPath (Join-Path $reviewRoot $entry[0]) -PathType Leaf) {
        [void]$reviewHtml.Append("<a href='$(ConvertTo-ReviewUrl $entry[0])'>$(ConvertTo-ReviewHtml $entry[1])</a>")
    }
}
[void]$reviewHtml.Append('</div><p class="small">报告生成时间：' + (ConvertTo-ReviewHtml ([DateTimeOffset]::Now.ToString('yyyy-MM-dd HH:mm:ss zzz'))) + '。重新运行 tools/UiFlowAudit/BuildReview.ps1 可读取新生成的 XML 与最终截图；本脚本不运行游戏、不修改场景或玩家存档。</p>')
[void]$reviewHtml.Append(@'
</main><script>
document.getElementById('changed-only').addEventListener('change', function () {
  document.querySelectorAll('#gallery article').forEach(function (card) { card.classList.toggle('hidden', document.getElementById('changed-only').checked && card.dataset.changed !== 'yes'); });
});
document.getElementById('scene-picker').addEventListener('change', function () {
  var card = document.getElementById(this.value); if (!card) return;
  if (card.classList.contains('hidden')) { document.getElementById('changed-only').checked = false; document.querySelectorAll('#gallery article').forEach(function (item) { item.classList.remove('hidden'); }); }
  card.scrollIntoView({behavior:'smooth',block:'start'});
});
</script></body></html>
'@)
$reviewOutput = Join-Path $reviewRoot 'Review.html'
[IO.File]::WriteAllText($reviewOutput, $reviewHtml.ToString(), [Text.UTF8Encoding]::new($false))
Write-Output "Wrote $reviewOutput"
foreach ($test in $reviewTests) { Write-Output ($test.Title + ': ' + $test.Label + ' — ' + $test.Detail) }
