param(
    [Parameter(Mandatory = $true)][string]$AssemblyPath,
    [Parameter(Mandatory = $true)][string]$Output
)

$ErrorActionPreference = 'Stop'
$reviewAssemblyPath = [IO.Path]::GetFullPath($AssemblyPath)
if (!(Test-Path -LiteralPath $reviewAssemblyPath -PathType Leaf)) {
    throw "Compiled Core assembly not found: $reviewAssemblyPath"
}
$reviewNunit = Join-Path ([IO.Path]::GetDirectoryName($reviewAssemblyPath)) 'nunit.framework.dll'
if (Test-Path -LiteralPath $reviewNunit) { [void][Reflection.Assembly]::LoadFrom($reviewNunit) }
$reviewAssembly = [Reflection.Assembly]::LoadFrom($reviewAssemblyPath)
$reviewFlags = [Reflection.BindingFlags]'Public,Static,Instance'
$reviewCatalog = $reviewAssembly.GetType('ShatteredSail.Core.Gameplay.CoreVoyageEventCatalog', $true)
$reviewSessionType = $reviewAssembly.GetType('ShatteredSail.Core.Gameplay.CoreVoyageSession', $true)
$reviewSession = [Activator]::CreateInstance($reviewSessionType)
$reviewRoutes = $reviewSessionType.GetProperty('Routes').GetValue($null)

function Invoke-ReviewFactory($Type, [string]$Name, $Instance, [object[]]$Arguments) {
    $methods = @($Type.GetMethods($reviewFlags) | Where-Object {
        $_.Name -eq $Name -and $_.GetParameters().Count -eq $Arguments.Count
    })
    if ($methods.Count -ne 1) {
        throw "Expected one $Name factory with $($Arguments.Count) arguments; found $($methods.Count)."
    }
    # Pipeline values can carry a PSObject wrapper; reflection needs the CLR
    # encounter/choice instances, including after factory enumeration.
    $invokeArguments = [object[]]::new($Arguments.Count)
    for ($argumentIndex = 0; $argumentIndex -lt $Arguments.Count; $argumentIndex++) {
        $invokeArguments[$argumentIndex] = if ($null -eq $Arguments[$argumentIndex]) {
            $null
        } else { $Arguments[$argumentIndex].PSObject.BaseObject }
    }
    $methods[0].Invoke($Instance, $invokeArguments)
}

function ConvertTo-ReviewHtml([object]$Value) {
    [Net.WebUtility]::HtmlEncode([string]$Value)
}

$reviewIds = [ordered]@{
    route_shattered_sail_reef = @(
        'event_reef_v2_tidewright_register',
        'event_reef_v2_three_bell_array',
        'event_reef_v2_kelp_loom'
    )
    route_golden_tide_trade = @(
        'event_trade_v2_notary_barge',
        'event_trade_v2_weighing_raft',
        'event_trade_v2_floating_school'
    )
    route_moon_tide_ruins = @(
        'event_moon_v2_submerged_scriptorium',
        'event_moon_v2_exorcist_kiln',
        'event_moon_v2_survey_barge'
    )
}
$reviewRows = [Collections.Generic.List[object]]::new()
foreach ($routeId in $reviewIds.Keys) {
    $route = @($reviewRoutes | Where-Object Id -eq $routeId)
    if ($route.Count -ne 1) { throw "Expected one route: $routeId" }
    $fixedEvents = @(Invoke-ReviewFactory $reviewCatalog 'CreateExpandedFixedEncounters' $null @($routeId))
    foreach ($eventId in $reviewIds[$routeId]) {
        $raw = @($fixedEvents | Where-Object Id -eq $eventId)
        if ($raw.Count -ne 1) { throw "Expected one fixed event in ${routeId}: $eventId" }
        $event = Invoke-ReviewFactory $reviewCatalog 'EnrichExploration' $null @($raw[0], $routeId)
        if ([string]::IsNullOrWhiteSpace($event.Description)) { throw "Empty description: $eventId" }
        $requirements = @($event.Choices | ForEach-Object {
            Invoke-ReviewFactory $reviewSessionType 'EncounterChoiceConditionSummary' $reviewSession @($_)
        })
        $reviewRows.Add([pscustomobject][ordered]@{
            RouteId = $routeId
            RouteName = $route[0].Name
            Id = $event.Id
            Name = $event.Name
            Category = $event.Category
            Description = $event.Description
            Hidden = $event.Hidden
            Mandatory = $event.Mandatory
            RepeatPolicy = $event.RepeatPolicy.ToString()
            Choices = @($event.Choices)
            ChoiceRequirements = $requirements
        })
    }
}
if ($reviewRows.Count -ne 9 -or @($reviewRows.Id | Sort-Object -Unique).Count -ne 9) {
    throw 'The review must contain exactly nine distinct events.'
}
$reviewChoiceCount = ($reviewRows | ForEach-Object { $_.Choices.Count } | Measure-Object -Sum).Sum
if ($reviewChoiceCount -ne 34) { throw "Expected 34 existing choices, found $reviewChoiceCount." }

$reviewHtml = [Text.StringBuilder]::new()
[void]$reviewHtml.AppendLine(@'
<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>余帆：人们记得的老船长</title>
<style>
:root{color-scheme:light;font-family:"Microsoft YaHei","PingFang SC",sans-serif;color:#2a302f;background:#f0f1ed}
*{box-sizing:border-box}body{margin:0}main{max-width:1000px;margin:0 auto;padding:44px 24px 64px}
h1{font-size:30px;line-height:1.4;margin:0 0 18px}h2{font-size:24px;margin:38px 0 18px;border-bottom:2px solid #bbcac3;padding-bottom:12px}
h3{font-size:21px;margin:0 0 8px}p{line-height:1.9}.intro{color:#52615a;margin:0 0 12px}.route-nav{display:flex;flex-wrap:wrap;gap:12px;margin:24px 0}
a{color:#235e4b;text-underline-offset:3px}.event{background:#fff;border:1px solid #d8dfd9;border-radius:10px;padding:26px;margin:18px 0;break-inside:avoid}
.category{font-size:14px;color:#627568;margin:0 0 10px}.description{font-size:18px;white-space:pre-wrap;margin:18px 0 22px}
.choices{margin:0;padding-left:26px}.choices li{padding:12px 0 14px 4px;border-top:1px solid #e2e7e2}.label{font-weight:600;line-height:1.7}
.detail,.condition{font-size:14px;line-height:1.8;margin:6px 0 0}.condition{color:#62705f}.source{color:#788078;font-size:12px;overflow-wrap:anywhere;margin:18px 0 0}
.hours{font-size:13px;font-weight:400;color:#66746a;margin-left:12px;white-space:nowrap}footer{margin-top:34px;font-size:13px;color:#6b746c;line-height:1.8;overflow-wrap:anywhere}
@media(max-width:600px){main{padding:26px 14px}.event{padding:20px 17px}.description{font-size:17px}h1{font-size:26px}}
@media print{body{background:white}main{max-width:none;padding:0}.route-nav{display:none}.event{box-shadow:none}a{color:inherit}}
</style></head><body><main>
<h1>余帆：人们记得的老船长</h1>
<p class="intro">三类海图，九处地点。怀念、崇敬、质疑、遗忘、失落与反对，留在不同人的生活和选择里。</p>
<p class="intro">这些既有固定地点随海图生成出现，可以乱序遇见；并非必须依次完成的剧情任务。以下描述、选项、资源效果和条件均读取本次编译后的游戏定义。</p>
<p class="intro">选项耗时还会按游戏规则消耗航行补给。标注的资源效果为基础定义，实际执行继续遵循船员加成、容量上限与地点重访规则。</p>
<nav class="route-nav" aria-label="航域目录">
'@)
foreach ($routeId in $reviewIds.Keys) {
    $routeName = ($reviewRows | Where-Object RouteId -eq $routeId | Select-Object -First 1).RouteName
    [void]$reviewHtml.AppendLine(('<a href="#{0}">{1}</a>' -f (ConvertTo-ReviewHtml $routeId), (ConvertTo-ReviewHtml $routeName)))
}
[void]$reviewHtml.AppendLine('</nav>')
foreach ($routeId in $reviewIds.Keys) {
    $routeRows = @($reviewRows | Where-Object RouteId -eq $routeId)
    [void]$reviewHtml.AppendLine(('<section id="{0}"><h2>{1}</h2>' -f (ConvertTo-ReviewHtml $routeId), (ConvertTo-ReviewHtml $routeRows[0].RouteName)))
    foreach ($row in $routeRows) {
        [void]$reviewHtml.AppendLine(('<article class="event" id="{0}"><h3>{1}</h3>' -f (ConvertTo-ReviewHtml $row.Id), (ConvertTo-ReviewHtml $row.Name)))
        $visibility = if ($row.Hidden) { '隐藏地点，需要获取情报' } else { '可通过瞭望发现' }
        [void]$reviewHtml.AppendLine(('<p class="category">{0} · {1}</p><p class="description">{2}</p><ol class="choices">' -f (ConvertTo-ReviewHtml $row.Category), $visibility, (ConvertTo-ReviewHtml $row.Description)))
        for ($index = 0; $index -lt $row.Choices.Count; $index++) {
            $choice = $row.Choices[$index]
            $hours = $choice.Hours.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture)
            $conditionHtml = if ([string]::IsNullOrWhiteSpace($row.ChoiceRequirements[$index])) {
                ''
            } else { '<p class="condition">条件：{0}</p>' -f (ConvertTo-ReviewHtml $row.ChoiceRequirements[$index]) }
            [void]$reviewHtml.AppendLine(('<li><div class="label">{0}<span class="hours">耗时 {1} 小时</span></div><p class="detail">{2}</p>{3}</li>' -f (ConvertTo-ReviewHtml $choice.Label), $hours, (ConvertTo-ReviewHtml $choice.Detail), $conditionHtml))
        }
        [void]$reviewHtml.AppendLine(('</ol><p class="source">事件 ID：<code>{0}</code></p></article>' -f (ConvertTo-ReviewHtml $row.Id)))
    }
    [void]$reviewHtml.AppendLine('</section>')
}
$reviewHash = (Get-FileHash -LiteralPath $reviewAssemblyPath -Algorithm SHA256).Hash
[void]$reviewHtml.AppendLine(('<footer>运行时核对：9 个事件，{0} 个选项。<br>编译程序集 SHA-256：{1}</footer></main></body></html>' -f $reviewChoiceCount, $reviewHash))

# No Unity project or build cache is created. Only the two requested review
# artifacts are written, after all runtime factories and counts are verified.
$reviewOutput = [IO.Path]::GetFullPath($Output)
[void][IO.Directory]::CreateDirectory($reviewOutput)
$reviewJsonPath = Join-Path $reviewOutput 'events.json'
$reviewHtmlPath = Join-Path $reviewOutput '余帆-老船长事件审阅.html'
$reviewJson = ConvertTo-Json -InputObject @($reviewRows.ToArray()) -Depth 20
[IO.File]::WriteAllText($reviewJsonPath, $reviewJson, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($reviewHtmlPath, $reviewHtml.ToString(), [Text.UTF8Encoding]::new($false))
Write-Output "Exported 9 events / $reviewChoiceCount choices."
Write-Output $reviewJsonPath
Write-Output $reviewHtmlPath
