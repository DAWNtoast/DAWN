param(
    [Parameter(Mandatory = $true)][string]$RuntimeDirectory,
    [Parameter(Mandatory = $true)][string]$ReferenceDirectory,
    [Parameter(Mandatory = $true)][string[]]$EditResults,
    [Parameter(Mandatory = $true)][string[]]$PlayResults,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$runtimeRoot = [IO.Path]::GetFullPath($RuntimeDirectory)
$referenceRoot = [IO.Path]::GetFullPath($ReferenceDirectory)
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$outputUri = [Uri]::new($outputRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar)
$diagnostics = [Collections.Generic.List[string]]::new()

function HtmlEncode([object]$Value) { [Net.WebUtility]::HtmlEncode([string]$Value) }
function LocalUrl([string]$Path) {
    $target = [Uri]::new([IO.Path]::GetFullPath($Path))
    $relative = $outputUri.MakeRelativeUri($target)
    if ($relative.IsAbsoluteUri) { throw "无法生成跨盘符的相对链接：$Path" }
    $relative.ToString()
}
function LocalLink([string]$Path, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return "<span class='muted'>$(HtmlEncode $Label)（未提供）</span>"
    }
    "<a href='$(HtmlEncode (LocalUrl $Path))'>$(HtmlEncode $Label)</a>"
}
function ReadJson([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        $diagnostics.Add("缺少文件：$Path")
        return $null
    }
    try { Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { $diagnostics.Add("JSON 无法读取：$Path；$($_.Exception.Message)"); return $null }
}
function State([object]$Value) {
    if ([string]::IsNullOrWhiteSpace([string]$Value)) { return $null }
    try { $Value | ConvertFrom-Json } catch { return $null }
}
function Number([object]$Value, [string]$Key) {
    if ($null -eq $Value -or $null -eq $Value.PSObject.Properties[$Key]) { return 0 }
    [double]$Value.$Key
}
function Figure([string]$Path, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return "<figure><div class='missing'>本轮未提供此状态截图</div><figcaption>$(HtmlEncode $Label)</figcaption></figure>"
    }
    $url = HtmlEncode (LocalUrl $Path)
    $ui = [IO.Path]::ChangeExtension($Path, '.ui.json')
    $extra = if (Test-Path -LiteralPath $ui -PathType Leaf) { ' · ' + (LocalLink $ui 'UI 数据') } else { '' }
    "<figure><a href='$url'><img loading='lazy' src='$url' alt='$(HtmlEncode $Label)'></a><figcaption>$(HtmlEncode $Label) · $(LocalLink $Path '原图')$extra</figcaption></figure>"
}
function TestCard([string]$Path, [string]$Label) {
    $status = 'pending'; $heading = '未验证'; $detail = '未提供可读 XML，不能据此宣称通过。'; $failures = ''
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        try {
            $xml = [Xml.XmlDocument]::new(); $xml.XmlResolver = $null; $xml.Load([IO.Path]::GetFullPath($Path))
            $run = $xml.SelectSingleNode('/test-run')
            if ($null -eq $run -or -not $run.HasAttribute('total') -or -not $run.HasAttribute('passed') -or -not $run.HasAttribute('failed')) { throw '缺少 NUnit test-run 统计。' }
            $total = [int]$run.GetAttribute('total'); $passed = [int]$run.GetAttribute('passed'); $failed = [int]$run.GetAttribute('failed')
            $skipped = [int]$run.GetAttribute('skipped'); $result = $run.GetAttribute('result')
            $status = if ($result -eq 'Passed' -and $total -gt 0 -and $failed -eq 0) { 'pass' } elseif ($failed -gt 0 -or $result -eq 'Failed') { 'fail' } else { 'pending' }
            $heading = if ($status -eq 'pass') { '通过' } elseif ($status -eq 'fail') { '失败' } else { '未完成' }
            $detail = "通过 $passed / $total；失败 $failed；跳过 $skipped；XML 状态 $result；结束时间 $($run.GetAttribute('end-time'))"
            $failedNodes = @($xml.SelectNodes("//test-case[@result='Failed']"))
            if ($failedNodes.Count -gt 0) {
                $items = @($failedNodes | ForEach-Object { "<li>$(HtmlEncode ($_.GetAttribute('fullname')))</li>" }) -join ''
                $failures = "<details><summary>失败测试</summary><ul>$items</ul></details>"
            }
            $adventure = $xml.SelectSingleNode("//test-case[contains(@fullname,'NewGame_FirstCraftedTreasureAdventure_CompletesThroughRealActions')]")
            if ($null -ne $adventure) {
                $detail += '；完整藏宝流程用例：' + $adventure.GetAttribute('result')
                if ($adventure.GetAttribute('result') -eq 'Passed') { $detail += '（包含场景重载后完成种子、已消耗地图与教程状态断言）' }
            }
        } catch { $heading = 'XML 不可读'; $status = 'fail'; $detail = $_.Exception.Message }
    }
    "<article class='test $status'><h3>$(HtmlEncode $Label) · $(HtmlEncode $heading)</h3><p>$(HtmlEncode $detail)</p>$(LocalLink $Path ([IO.Path]::GetFileName($Path)))$failures</article>"
}

$actionPath = Join-Path $runtimeRoot 'actions.json'
$actionDocument = ReadJson $actionPath
$referencePath = Join-Path $referenceRoot 'report.json'
$referenceReport = ReadJson $referencePath
$actions = if ($null -ne $actionDocument) { @($actionDocument.actions) } else { @() }
$events = @($actions | ForEach-Object { [pscustomobject]@{ Name = [string]$_.action; Before = (State $_.before); After = (State $_.after); Sequence = $_.sequence } })
$gm = @($events | Where-Object { $_.Name -match '^F10 ' })
$gmGoldAdded = 0
$gmOtherChanges = [Collections.Generic.List[string]]::new()
foreach ($item in $gm) {
    $gmGoldAdded += (Number $item.After 'gold') - (Number $item.Before 'gold')
    if ($null -eq $item.Before -or $null -eq $item.After) { $gmOtherChanges.Add('动作 #' + $item.Sequence + ' 缺少完整前后状态'); continue }
    foreach ($property in $item.Before.PSObject.Properties) {
        if ($property.Name -ne 'gold' -and [string]$property.Value -ne [string]$item.After.($property.Name)) {
            $gmOtherChanges.Add('动作 #' + $item.Sequence + '：' + $property.Name + ' ' + $property.Value + ' → ' + $item.After.($property.Name))
        }
    }
}
$recruits = @($events | Where-Object { $_.Name -match '^Click: Recruit Tavern Crew [1-5]$' -and (Number $_.Before 'gold') -gt (Number $_.After 'gold') })
$upgrades = @($events | Where-Object { $_.Name -eq 'Click: Confirm Battle Card Upgrade' -and (Number $_.Before 'gold') -gt (Number $_.After 'gold') })
$investments = @($events | Where-Object { $_.Name -eq 'Click: Confirm Port Investment' -and (Number $_.Before 'gold') -gt (Number $_.After 'gold') })
$investmentGold = 0; foreach ($item in $investments) { $investmentGold += (Number $item.Before 'gold') - (Number $item.After 'gold') }
$ordinaryDepartures = @($events | Where-Object { $_.Name -eq 'Click: Start Voyage' -and $_.After.phase -eq 'Sailing' -and (Number $_.After 'maps') -eq (Number $_.Before 'maps') -and (Number $_.After 'requiredClues') -eq 0 })
$fragmentEvents = @($events | Where-Object { $_.Name -match '^Click: Encounter Choice \d+$' -and (Number $_.After 'pendingFragments') -gt (Number $_.Before 'pendingFragments') })
$bankEvents = @($events | Where-Object { $_.Name -eq 'Click: Return To Port' -and (Number $_.After 'fragments') -gt (Number $_.Before 'fragments') })
$crafts = @($events | Where-Object { $_.Name -eq 'Click: Treasure Craft Map' -and (Number $_.After 'maps') -gt (Number $_.Before 'maps') })
$departures = @($events | Where-Object { $_.Name -eq 'Click: Start Voyage' -and (Number $_.After 'maps') -lt (Number $_.Before 'maps') -and $_.After.phase -eq 'Sailing' })
$coreOpened = @($events | Where-Object { $_.Name -match '^Click: Encounter Choice [0-2]$' -and $_.Before.encounter -eq 'treasure_core' -and $_.After.phase -eq 'Settlement' })
$clueEvents = @($events | Where-Object { $_.Name -match '^Click: Encounter Choice \d+$' -and (Number $_.After 'clues') -gt (Number $_.Before 'clues') })
$complete = @($events | Where-Object { $_.Name -eq 'Acceptance completed' }).Count -gt 0
$earned = 0; foreach ($item in $fragmentEvents) { $earned += (Number $item.After 'pendingFragments') - (Number $item.Before 'pendingFragments') }
$banked = 0; foreach ($item in $bankEvents) { $banked += (Number $item.After 'fragments') - (Number $item.Before 'fragments') }
$spent = 0; foreach ($item in $crafts) { $spent += (Number $item.Before 'fragments') - (Number $item.After 'fragments') }
$consumed = 0; foreach ($item in $departures) { $consumed += (Number $item.Before 'maps') - (Number $item.After 'maps') }
$pngs = if (Test-Path -LiteralPath $runtimeRoot -PathType Container) { @(Get-ChildItem -LiteralPath $runtimeRoot -Filter '*.png' -File | Sort-Object @{ Expression = { if ($_.Name -match '^(\d+)_') { [int]$Matches[1] } else { -1 } } }, Name) } else { @() }
function Shot([string]$Pattern, [switch]$Last) {
    $matches = @($pngs | Where-Object { $_.Name -like $Pattern })
    if ($matches.Count -eq 0) { return '' }
    if ($Last) { return $matches[-1].FullName }
    $matches[0].FullName
}
function CoreShot([bool]$Ready) {
    foreach ($file in @($pngs | Where-Object { $_.Name -like '*_Encounter_*_treasure_core.png' })) {
        $uiPath = [IO.Path]::ChangeExtension($file.FullName, '.ui.json')
        if (-not (Test-Path -LiteralPath $uiPath -PathType Leaf)) { continue }
        try {
            $ui = Get-Content -LiteralPath $uiPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $hours = @($ui.objects | Where-Object { $_.path -match '/Encounter Choice 0/Hours$' } | Select-Object -First 1)
            if ($hours.Count -gt 0 -and (([string]$hours[0].text -notmatch '不可选|线索不足') -eq $Ready)) { return $file.FullName }
        } catch { $diagnostics.Add("核心状态 UI 数据不可读：$uiPath") }
    }
    return ''
}
$clueShot = ''
if ($clueEvents.Count -gt 0) { $clueShot = Shot ('*_Encounter_*_' + $clueEvents[0].Before.encounter + '.png') }
$marketShot = Shot '*_Purchased_Supplies.png' -Last
$investedMarketShot = Shot '*_Supplies_After_Port_Investment.png' -Last
if ($investedMarketShot) { $marketShot = $investedMarketShot }
$beforeFragments = Shot '*_Workbench_BeforeFragments.png'
$cards = @(
    @{ Title = '藏宝工作台 · 碎片不足'; Ref = 'UI_TreasureMap'; Frame = $beforeFragments; Note = '主入口布局。拼合按钮受海盗等级与碎片数量约束。' },
    @{ Title = '藏宝工作台 · 未拼合状态'; Ref = 'UITreasureMap_MapUnopened'; Frame = $beforeFragments; Note = '此参考与上一项共用同一次实际工作台截图，不额外声称发生了第二个动作。' },
    @{ Title = '船员契约 · 招募前'; Ref = 'UITavern_RecruitmentOffer'; Frame = (Shot '*_Recruitment_Offer_1.png'); Note = '五次实际付费招募取一例展示；费用扣除从 actions.json 统计。' },
    @{ Title = '船员契约 · 签约后'; Ref = 'UITavern_ContractSigned'; Frame = (Shot '*_Contract_Signed_1.png'); Note = '核对签约状态、招募按钮和船员档案区域。' },
    @{ Title = '卡牌强化 · 升级前'; Ref = 'UICardWorkshop_UpgradePreview'; Frame = (Shot '*_Upgrade_Preview_0.png'); Note = '实际选择卡牌后打开强化预览，检查成本和效果对照。' },
    @{ Title = '卡牌强化 · 升级后'; Ref = 'UICardWorkshop_UpgradeComplete'; Frame = (Shot '*_Upgrade_Complete_0.png'); Note = '实际三张卡各强化三级；完成次数按成功扣费的强化点击统计。' },
    @{ Title = '卡牌强化 · 独立预览入口'; Ref = 'UICardUpgrade_UpgradePreview'; Frame = (Shot '*_Upgrade_Preview_0.png'); Note = '同一运行界面的第二份已保存场景入口，复用升级前截图。' },
    @{ Title = '市场 · 总览'; Ref = 'UI_Market'; Frame = $marketShot; Note = '核对商品列、数量调节、买卖按钮、价签和底部说明。' },
    @{ Title = '市场 · 购买参考'; Ref = 'UIMarket_Buy'; Frame = $marketShot; Note = '使用实际补给购买完成后的市场截图；未将其描述为点击发生瞬间。' },
    @{ Title = '市场 · 出售参考'; Ref = 'UIMarket_Sell'; Frame = $marketShot; Note = '该样本用于比对市场通用排版，不代表运行截图正处于出售动作。' },
    @{ Title = '市场 · 交易后'; Ref = 'UIMarket_TransactionComplete'; Frame = $marketShot; Note = '运行存档库存、当日价格与预览样本可能不同，布局应保持一致。' },
    @{ Title = '港口投资 · 扩大市场额度'; Ref = 'UI_Maintenance'; Frame = (Shot '*_Port_Investment_Expanded_Quota.png'); Note = '补给售罄后，通过市场的投资入口购买所需材料并支付建设成本，扩增额度后继续正常采购；此截图不是 GM 补货。' },
    @{ Title = '收集四片 · 可以拼合'; Ref = 'UITreasureMap_FragmentsReady'; Frame = (Shot '*_Fragments_Ready.png'); Note = '碎片来自普通航海事件并经过返港结算，不通过 GM 注入。实际获得数量以操作日志为准。' },
    @{ Title = '第一张藏宝图 · 拼合完成'; Ref = 'UITreasureMap_MapDecoded'; Frame = (Shot '*_First_Crafted_Map.png'); Note = '消耗四片生成一张具有唯一种子与等级的藏宝图。名称、等级与种子随本轮生成结果变化。' },
    @{ Title = '藏宝出航 · 装备地图'; Ref = 'UIVoyageDeparture_TreasureReady'; Frame = (Shot '*_Treasure_Departure.png'); Note = '选图与装备不消耗地图，成功通过出航检查后才消耗。' },
    @{ Title = '隐藏海域 · 开始探索'; Ref = 'UINavigation_TreasureExploration'; Frame = (Shot '*_Treasure_Exploration_Start.png'); Note = '独立隐藏海域保留核心叉号，线索与其他节点通过实际探索获得。地图地形和位置随种子变化。' },
    @{ Title = '藏宝线索 · 事件选择'; Ref = 'UIEncounter_TreasureClue'; Frame = $clueShot; Note = '选取操作日志中确实增加线索的首次事件截图；不同事件正文与选项内容允许不同。' },
    @{ Title = '藏宝核心 · 线索不足'; Ref = 'UIEncounter_TreasureCoreLocked'; Frame = (CoreShot $false); Note = '按截图 UI 数据识别首个开启选项的不可用状态；可标记位置继续探索。若本次未提前到达，则留空，不借用可开启状态。' },
    @{ Title = '藏宝核心 · 可以开启'; Ref = 'UIEncounter_TreasureCoreReady'; Frame = (CoreShot $true); Note = '按首个开启选项的 UI 文案识别可用状态；避免把线索不足截图错配成核心已解锁。' },
    @{ Title = '藏宝结算 · 实际保护后奖励'; Ref = 'UISettlement_TreasureRecovered'; Frame = (Shot '*_Treasure_Completed_Settlement.png'); Note = '正常完成核心采用 95% 保护率。稀有整数物品损失向下取整，结算展示实际保留数量。' },
    @{ Title = '返港 · 完成记录'; Ref = 'UITreasureMap_RewardClaim'; Frame = (Shot '*_Completed_Treasure_Receipt.png'); Note = '完成种子进入记录；消耗的地图不恢复，奖励不重复领取。保存重载验证是否通过，以 PlayMode XML 为准。' }
)

$testHtml = @()
foreach ($path in $EditResults) { $testHtml += TestCard $path 'EditMode' }
foreach ($path in $PlayResults) { $testHtml += TestCard $path 'PlayMode' }
$referenceCount = 0
if ($null -ne $referenceReport) { $referenceCount = @($referenceReport.captures | Where-Object { $_.captured -eq $true -and (Test-Path -LiteralPath (Join-Path $referenceRoot $_.file) -PathType Leaf) }).Count }
$referenceRows = @(); $gallery = @(); $index = 0
foreach ($card in $cards) {
    $index++
    $record = @($referenceReport.captures | Where-Object { [IO.Path]::GetFileNameWithoutExtension([string]$_.file) -eq $card.Ref } | Select-Object -First 1)
    $refFile = Join-Path $referenceRoot ($card.Ref + '.png')
    $refState = '未找到采集记录'
    if ($record.Count -gt 0) {
        $refFile = Join-Path $referenceRoot $record[0].file
        $refState = 'captured=' + $record[0].captured + '；sourceSceneUnchanged=' + $record[0].sourceSceneUnchanged
        if ($record[0].error) { $refState += '；' + $record[0].error }
        if (@($record[0].unityErrors).Count -gt 0) { $refState += '；Unity 错误 ' + @($record[0].unityErrors).Count }
    }
    $referenceRows += "<tr><td>$index</td><td>$(LocalLink $refFile ($card.Ref + '.png'))</td><td>$(HtmlEncode $refState)</td></tr>"
    $gallery += "<article class='comparison'><h3>$index. $(HtmlEncode $card.Title)</h3><p>$(HtmlEncode $card.Note)</p><div class='pair'>$(Figure $card.Frame '实际 Unity PlayMode 截图')$(Figure $refFile '保存的 UIPreviewsForMe 场景')</div></article>"
}
$summaryRows = @(
    @('操作日志', "$($actions.Count) 条", '只统计指定 RuntimeDirectory，不合并旧轮次。'),
    @('F10 辅助', "$($gm.Count) 次", '只计 F10 汇总动作；不再重复计算其内部按钮点击。'),
    @('F10 金币净增加', "$gmGoldAdded", '前后总金币差值之和；非金币字段的差异在下方逐项列出。'),
    @('实际付费招募', "$($recruits.Count) / 5 次", '招募按钮点击且金币减少；目标为五次。'),
    @('实际卡牌强化', "$($upgrades.Count) / 9 次", '强化确认且金币减少；目标为九次。'),
    @('普通航海出航', "$($ordinaryDepartures.Count) 次", '教学结束后日志中成功进入 Sailing 的普通航行。'),
    @('正常港口投资', "$($investments.Count) 次；金币 $investmentGold", '正常确认建设并扣费，用于扩大市场额度；不计作 F10 补货。'),
    @('事件获得碎片', "$earned 片 / $($fragmentEvents.Count) 次选择", '只计事件按钮后待结算碎片增加，避免汇总记录重复。'),
    @('返港入账碎片', "$banked 片", '只计结算返港后的档案碎片增加。'),
    @('拼合地图', "$($crafts.Count) 次；耗 $spent 片", '拼合按钮后持有地图数增加。'),
    @('成功出航耗图', "$consumed 张", '开始航行且持有地图数减少。'),
    @('线索选择', "$($clueEvents.Count) 次", '事件选择后线索增加。'),
    @('核心完成结算', "$($coreOpened.Count) 次", '核心开启选项使阶段变为 Settlement。'),
    @('流程完成日志', $(if ($complete) { '已记录' } else { '未记录' }), 'Acceptance completed 表示已查看返港完成记录；不替代随后的重载测试结果。'),
    @('已采集保存预览', "$referenceCount / $($cards.Count) 张", 'report.json 标记 captured 且图片文件存在；不等于已人工逐像素验收。')
) | ForEach-Object { "<tr><th>$(HtmlEncode $_[0])</th><td>$(HtmlEncode $_[1])</td><td>$(HtmlEncode $_[2])</td></tr>" }
$gmDetails = if ($gm.Count -gt 0) { '<ul>' + (@($gm | ForEach-Object { "<li>$(HtmlEncode $_.Name)：金币 $(Number $_.Before 'gold') → $(Number $_.After 'gold')</li>" }) -join '') + '</ul>' } else { '<p>本轮没有记录到 F10 汇总动作；缺失日志时不等同于证明未使用。</p>' }
if ($gmOtherChanges.Count -gt 0) {
    $gmDetails += '<p>F10 前后快照还记录了以下非金币变化：</p><ul>' + (@($gmOtherChanges | ForEach-Object { "<li>$(HtmlEncode $_)</li>" }) -join '') + '</ul>'
} elseif ($gm.Count -gt 0) {
    $gmDetails += '<p>这些 F10 操作的前后快照中，经验、碎片、地图、线索、补给等已记录字段均未变化。日志未记录的字段不在此比较结论内。</p>'
}
$fragmentDetails = if ($fragmentEvents.Count -gt 0) { '<ul>' + (@($fragmentEvents | ForEach-Object { "<li>动作 #$(HtmlEncode $_.Sequence)：$(HtmlEncode $_.Before.encounter)，待结算碎片 $(Number $_.Before 'pendingFragments') → $(Number $_.After 'pendingFragments')</li>" }) -join '') + '</ul>' } else { '<p>尚无可验证的真实事件碎片收入。</p>' }
$seedDetails = if ($departures.Count -gt 0) { '<ul>' + (@($departures | ForEach-Object { "<li>出航种子：<code>$(HtmlEncode $_.After.seed)</code>；持有地图 $(Number $_.Before 'maps') → $(Number $_.After 'maps')</li>" }) -join '') + '</ul>' } else { '<p>尚无成功出航耗图记录。</p>' }
$lastState = if ($events.Count -gt 0) { $events[-1].After } else { $null }
$stateText = if ($null -ne $lastState) { "最终记录阶段 $($lastState.phase)；等级 $($lastState.level)；经验 $($lastState.experience)；碎片 $($lastState.fragments)；持有地图 $($lastState.maps)。" } else { '最终状态不可读。' }
$diagnosticHtml = if ($diagnostics.Count -gt 0) { '<ul>' + (@($diagnostics | ForEach-Object { "<li>$(HtmlEncode $_)</li>" }) -join '') + '</ul>' } else { '<p>报告输入未发现读取错误。缺失的状态截图仍在对应卡片中明确标记。</p>' }
$generatedAt = [DateTimeOffset]::Now.ToString('yyyy-MM-dd HH:mm:ss zzz')
$html = @"
<!doctype html><html lang='zh-CN'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>第一张藏宝图冒险 · 实玩与预览审查</title>
<style>
:root{color-scheme:light}*{box-sizing:border-box}body{margin:0;background:#f3efe5;color:#263137;font:16px/1.65 system-ui,'Microsoft YaHei',sans-serif}main{max-width:1600px;margin:auto;padding:36px}h1{font-size:32px;line-height:1.25}h2{margin-top:40px}h3{margin:0 0 8px;font-size:20px}a{color:#12606c;overflow-wrap:anywhere}code{overflow-wrap:anywhere}p{max-width:1100px}.muted,figcaption{color:#60696c;font-size:14px}.scope{border-left:5px solid #377c86;padding:14px 20px;background:#e4eeeb}.tests{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:16px}.test,.comparison{background:#fffdf7;border:1px solid #d7d7c9;border-radius:10px;padding:20px}.test.pass{border-top:5px solid #2c8162}.test.fail{border-top:5px solid #b4483f}.test.pending{border-top:5px solid #b58c36}.comparison{margin:20px 0}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0;min-width:0}img{display:block;width:100%;aspect-ratio:16/9;object-fit:contain;background:#e9e2d2;border:1px solid #dad6cb}.missing{aspect-ratio:16/9;display:grid;place-items:center;background:#efebe0;border:1px dashed #ada99a;color:#756f5d}figcaption{padding-top:6px}table{border-collapse:collapse;width:100%;background:#fffdf7}th,td{padding:10px 14px;border:1px solid #d7d7c9;text-align:left;vertical-align:top}th{font-weight:600;width:200px}li{margin:7px 0}details{margin-top:12px}.sources{display:flex;flex-wrap:wrap;gap:18px}@media(max-width:850px){main{padding:18px}.pair{grid-template-columns:1fr}h1{font-size:26px}table{font-size:14px}th{width:125px}}@media print{main{padding:0}.comparison,.test{break-inside:avoid}details{display:block}a{color:inherit}}
</style></head><body><main>
<h1>第一张藏宝图冒险 · 实玩与预览审查</h1><p class='muted'>生成时间：$(HtmlEncode $generatedAt) · 结果仅对应本次传入的日志、截图与 XML。</p>
<div class='scope'><strong>验证范围</strong><p>Unity PlayMode 界面驱动从新游戏与三段教学航行继续，通过真实按钮、地图点击和游戏规则进行招募、强化、普通航海、碎片结算、拼图、藏宝出航、探索线索与核心结算。这是自动化实玩回归，不是人工桌面逐步操作，也不是 Windows 发布包测试。是否走完整条链以以下日志和测试结果为准。</p><p>按授权，可在金币不足或部件商店全部售罄时使用 F10；报告逐次列出日志中实际使用的辅助。碎片和线索应来自事件选择，地图由四片拼合。回归夹具对所用存档与市场键做隔离与恢复。</p></div>
<h2>实际结果</h2><table><tbody>$($summaryRows -join '')</tbody></table><p>$(HtmlEncode $stateText)</p>
<details><summary>F10 使用明细</summary>$gmDetails</details><details><summary>真实事件碎片来源</summary>$fragmentDetails</details><details><summary>消耗地图与种子</summary>$seedDetails</details>
<h2>测试 XML</h2><p>每张卡片独立读取传入的 NUnit XML；历史失败不会被另一个成功文件覆盖。日志中的完成标记不能代替测试通过。</p><div class='tests'>$($testHtml -join '')</div>
<h2>本轮修正</h2><ul>
<li><strong>补齐拼图与完整藏宝链：</strong>普通事件获得碎片、返港入账、等级门槛、四片拼图、地图持有上限、唯一种子、成功出航耗图、独立隐藏海域、线索保底、核心结算与完成记录。</li>
<li><strong>修复核心反复吸附：</strong>提前到达核心后可暂离并继续探索；避免附近地图点击再次自动打开同一核心，保留后续明确重访。</li>
<li><strong>修复窄窗口海图裁切：</strong>画布使用 Expand 缩放完整容纳设计区域，避免海图边缘事件落在屏幕外而无法点击；回归检查海图四角位于实际窗口内。</li>
<li><strong>修正航海经验单位：</strong>按当前航线海里比例换算地图距离，避免一次长航程因海里直接乘经验倍率而跃升满级；等级阈值和其他奖励保持原规则。</li>
<li><strong>修正市场排版与价签：</strong>整理商品、买卖操作及价格信息的位置，处理重叠和裁切，并同步保存的市场状态预览。</li>
<li><strong>补充市场投资入口：</strong>达到购买额度后可从市场前往港口投资，通过购买建设材料并正常付费扩大额度，继续采购补给；提示明确区分额度与库存。</li>
<li><strong>保留教学重载检查点：</strong>教学出航继续使用出航前的钱包和货舱快照，避免后续保存覆盖检查点后让港口钱包少显示已转移的随船资金；藏宝出航仍立即保存地图消耗。</li>
<li><strong>清理保存预览遗留层：</strong>移除运行时已不再创建的设施说明，解决六个预览中的残留文字与重影；21 个场景的画布缩放同步为 Expand。</li>
<li><strong>稀有奖励采用离散损失规则：</strong>稀有整数物品的损失向下取整，等价于保留数量向上取整，避免 95% 保护率抹去唯一一件稀有奖励；普通金币与材料继续采用原规则。</li>
</ul><p>以上为修改内容说明，验证状态仍以当前 XML 和截图为准。</p>
<h2>流程截图与 $($cards.Count) 个保存预览</h2><p>左侧是当前实际存档，右侧是 UIPreviewsForMe 的已保存场景。港口、船员、地图种子、等级、金币、库存、奖励和事件正文是预览样本数据，可与实际不同；面板结构、文字区域、按钮布局及对应状态应一致。市场优先展示投资扩容后再次完成采购的截图，多份保存预览复用同一实际市场界面进行结构比较，并逐项说明限制。点击图片可打开本地原图。</p>
$($gallery -join "`n")
<h2>预览采集清单</h2><p>sourceSceneUnchanged 表示采集过程未改写源场景，不表示场景从未经过本轮修正。</p><table><thead><tr><th>序号</th><th>保存预览</th><th>实际采集记录</th></tr></thead><tbody>$($referenceRows -join '')</tbody></table>
<h2>原始证据与读取情况</h2><div class='sources'>$(LocalLink $actionPath 'actions.json')$(LocalLink (Join-Path $runtimeRoot 'report.json') '运行截图 report.json')$(LocalLink $referencePath '预览采集 report.json')</div>$diagnosticHtml
</main></body></html>
"@
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$reviewPath = Join-Path $outputRoot 'Review.html'
[IO.File]::WriteAllText($reviewPath, $html, [Text.UTF8Encoding]::new($false))
Write-Output $reviewPath
