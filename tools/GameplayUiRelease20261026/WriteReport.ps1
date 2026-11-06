param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDirectory
)
$ErrorActionPreference = 'Stop'
$reportWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\', '/')
$reportBuilds = Join-Path $reportWorkspace 'Builds'
$reportArtifacts = Join-Path $reportWorkspace 'Artifacts/GameplayUiRelease20261026'

function Assert-ReportPath([string]$Path, [string]$Root, [switch]$AllowRoot, [switch]$MayNotExist) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'A report input path is missing.' }
    $full = [IO.Path]::GetFullPath($Path)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $isRoot = $full.TrimEnd('\', '/') -eq $rootFull
    if ((-not $AllowRoot -and $isRoot) -or
        (-not $isRoot -and -not $full.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) {
        throw ('Report path escaped its allowed directory: ' + $full)
    }
    if (-not $MayNotExist -and -not (Test-Path -LiteralPath $full)) { throw ('Report input is missing: ' + $full) }
    # Check every existing ancestor, including Builds/Artifacts and the workspace.
    $cursor = $full
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Refusing linked report path: ' + $cursor) }
        }
        $parent = [IO.Path]::GetDirectoryName($cursor.TrimEnd('\', '/'))
        if ($parent -eq $cursor) { break }
        $cursor = $parent
    }
    return $full
}

function Read-ReportJson([string]$Path) {
    return Get-Content -LiteralPath $Path -Raw -Encoding utf8 | ConvertFrom-Json
}

function Assert-ReportHash([string]$Path, [string]$Expected) {
    if ($Expected -notmatch '^[A-Fa-f0-9]{64}$') { throw ('Invalid recorded SHA256 for ' + $Path) }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($actual -ine $Expected) { throw ('Report evidence hash mismatch: ' + $Path) }
    return $actual
}

function Html([object]$Value) { return [Net.WebUtility]::HtmlEncode([string]$Value) }
function Number([object]$Value) { return ([long]$Value).ToString('N0', [Globalization.CultureInfo]::InvariantCulture) }
function Assert-NonnegativeCount([object]$Value, [string]$Name) {
    if ($null -eq $Value -or [string]$Value -notmatch '^\d+(?:\.0+)?$' -or [decimal]$Value -gt [long]::MaxValue) {
        throw ('Invalid validation count: ' + $Name)
    }
}

$reportRelease = Assert-ReportPath $ReleaseDirectory $reportBuilds
if (-not (Get-Item -LiteralPath $reportRelease).PSIsContainer) { throw 'ReleaseDirectory must be an existing sealed release directory.' }
$reportManifestPath = Assert-ReportPath (Join-Path $reportRelease 'release-manifest.json') $reportRelease
$reportManifest = Read-ReportJson $reportManifestPath
if ($reportManifest.Result -cne 'PASS' -or $reportManifest.Package.Result -cne 'PASS' -or $reportManifest.SmokePassed -ne $true) {
    throw 'The release must be sealed with successful package and player-smoke results before writing a report.'
}
if ([string]$reportManifest.Version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'The sealed release version is invalid.' }

$reportEvidence = Assert-ReportPath $reportManifest.Evidence $reportArtifacts
if ((Split-Path -Leaf $reportEvidence) -notmatch '^Snapshot(?<revision>\d{2})$') { throw 'The release evidence is not a task snapshot.' }
$reportRevision = $Matches.revision
$reportValidationPath = Assert-ReportPath $reportManifest.Validation $reportArtifacts
$reportExpectedValidation = [IO.Path]::GetFullPath((Join-Path $reportArtifacts ('Verification' + $reportRevision + '/verification-summary.json')))
if ($reportValidationPath -ine $reportExpectedValidation) { throw 'The validation summary belongs to a different release revision.' }
$reportValidation = Read-ReportJson $reportValidationPath
$reportSnapshot = Read-ReportJson (Assert-ReportPath (Join-Path $reportEvidence 'snapshot.json') $reportEvidence)
if ($reportValidation.Result -cne 'PASS' -or $reportSnapshot.Result -cne 'PASS' -or $reportSnapshot.Version -cne $reportManifest.Version) {
    throw 'Snapshot or validation identity does not match the sealed release.'
}

$reportSourceManifest = Assert-ReportPath (Join-Path $reportEvidence 'source-manifest.csv') $reportEvidence
$reportProjectManifest = Assert-ReportPath (Join-Path $reportEvidence 'project-input-manifest.csv') $reportEvidence
$reportSourceHash = Assert-ReportHash $reportSourceManifest $reportManifest.SourceManifestSHA256
$reportProjectHash = Assert-ReportHash $reportProjectManifest $reportManifest.ProjectInputManifestSHA256
foreach ($identity in @($reportValidation, $reportSnapshot)) {
    if ($identity.SourceManifestSHA256 -ine $reportSourceHash -or $identity.ProjectInputManifestSHA256 -ine $reportProjectHash) {
        throw 'Validation, snapshot and sealed release do not identify the same official inputs.'
    }
}
$reportSourceFiles = @(Import-Csv -LiteralPath $reportSourceManifest).Count
$reportProjectFiles = @(Import-Csv -LiteralPath $reportProjectManifest).Count
if ($reportSourceFiles -eq 0 -or $reportProjectFiles -eq 0 -or
    $reportSourceFiles -ne [long]$reportSnapshot.Files -or $reportProjectFiles -ne [long]$reportSnapshot.ProjectFiles) {
    throw 'Official input manifests are empty or disagree with the sealed snapshot counts.'
}

foreach ($name in @('Passed', 'Failed', 'Skipped')) { Assert-NonnegativeCount $reportValidation.$name $name }
if ([long]$reportValidation.Failed -ne 0) { throw 'Cannot report a release with failed regression tests.' }
$reportSuites = @($reportValidation.Suites)
if ($reportSuites.Count -ne 2 -or @($reportSuites | Where-Object Suite -eq 'EditMode').Count -ne 1 -or
    @($reportSuites | Where-Object Suite -eq 'PlayMode').Count -ne 1) { throw 'Both complete EditMode and PlayMode evidence are required.' }
foreach ($suite in $reportSuites) {
    foreach ($name in @('Total', 'Passed', 'Failed', 'Skipped')) { Assert-NonnegativeCount $suite.$name ($suite.Suite + '.' + $name) }
    if ([long]$suite.Failed -ne 0 -or [long]$suite.Total -ne ([long]$suite.Passed + [long]$suite.Failed + [long]$suite.Skipped)) {
        throw ('Invalid successful suite totals: ' + $suite.Suite)
    }
    if ($suite.SourceManifestSHA256 -ine $reportSourceHash -or $suite.ProjectInputManifestSHA256 -ine $reportProjectHash) {
        throw ('Test suite belongs to different official inputs: ' + $suite.Suite)
    }
    $suitePath = Assert-ReportPath $suite.Report $reportArtifacts
    [void](Assert-ReportHash $suitePath $suite.ResultsSHA256)
}
foreach ($name in @('Passed', 'Failed', 'Skipped')) {
    if ([long]$reportValidation.$name -ne [long](($reportSuites | Measure-Object -Property $name -Sum).Sum)) {
        throw ('Overall validation total differs from suite evidence: ' + $name)
    }
}

$reportZip = Assert-ReportPath $reportManifest.Package.Zip $reportRelease
if ((Split-Path -Parent $reportZip) -ine $reportRelease -or [IO.Path]::GetExtension($reportZip) -ine '.zip') {
    throw 'The sealed ZIP must be a file directly inside this release directory.'
}
$reportZipInfo = Get-Item -LiteralPath $reportZip
Assert-NonnegativeCount $reportManifest.Package.ZipBytes 'Package.ZipBytes'
Assert-NonnegativeCount $reportManifest.Package.FilesVerified 'Package.FilesVerified'
if ([long]$reportManifest.Package.FilesVerified -le 0) { throw 'The package has no verified files.' }
if ($reportZipInfo.PSIsContainer -or $reportZipInfo.Length -ne [long]$reportManifest.Package.ZipBytes) { throw 'The sealed ZIP size does not match its manifest.' }
$reportZipHash = Assert-ReportHash $reportZip $reportManifest.Package.ZipSHA256
$reportExecutable = Assert-ReportPath $reportManifest.Executable (Join-Path $reportRelease 'Windows')
[void](Assert-ReportHash $reportExecutable $reportManifest.Package.ExeSHA256)
$reportPackageEvidence = Read-ReportJson (Assert-ReportPath (Join-Path $reportEvidence 'package.json') $reportEvidence)
if ($reportPackageEvidence.Result -cne 'PASS' -or $reportPackageEvidence.ZipSHA256 -ine $reportZipHash -or
    $reportPackageEvidence.ExeSHA256 -ine $reportManifest.Package.ExeSHA256 -or
    [long]$reportPackageEvidence.FilesVerified -ne [long]$reportManifest.Package.FilesVerified -or
    [long]$reportPackageEvidence.ZipBytes -ne $reportZipInfo.Length) { throw 'Package evidence and sealed manifest disagree.' }

$reportSmokeResult = Read-ReportJson (Assert-ReportPath (Join-Path $reportEvidence 'Smoke/result.json') $reportEvidence)
$reportSmokePath = Assert-ReportPath (Join-Path $reportEvidence 'Smoke/Report/player-smoke-report.txt') $reportEvidence
$reportSmokeText = [IO.File]::ReadAllText($reportSmokePath)
if ($reportSmokeResult.ExitCode -ne 0 -or $reportSmokeText -notmatch '^PASS\r?\n' -or
    $reportSmokeText -notmatch ('(?m)^Version: ' + [regex]::Escape($reportManifest.Version) + '\r?$') -or
    $reportSmokeText -notmatch '(?m)^Company: ShatteredSail\r?$' -or $reportSmokeText -notmatch '(?m)^Product: Shattered Sail\r?$') {
    throw 'Actual player smoke result or production identity failed.'
}
$reportImages = Read-ReportJson (Assert-ReportPath (Join-Path $reportEvidence 'Windows/packaged-images.json') $reportEvidence)
if ($reportImages.Result -cne 'PASS' -or @($reportImages.Missing).Count -gt 0 -or
    [long]$reportImages.Images -ne [long]$reportManifest.ImagesReferencedInBuildLog) { throw 'Packaged-image evidence is incomplete.' }

$reportChangesPath = Assert-ReportPath (Join-Path $reportWorkspace 'outputs/release-20261026/变更检查.txt') $reportWorkspace
$reportBalancePath = Assert-ReportPath (Join-Path $reportWorkspace 'outputs/dual-fields-20261026/数值检查.txt') $reportWorkspace
$reportChanges = [IO.File]::ReadAllText($reportChangesPath)
$reportBalance = [IO.File]::ReadAllText($reportBalancePath)
if ([string]::IsNullOrWhiteSpace($reportChanges) -or [string]::IsNullOrWhiteSpace($reportBalance)) { throw 'Change and balance reports must not be empty.' }

$reportUpdates = @(
    'Boss 行为：新增第3、5、8、10……海战回合的极速行进和全部场地正负效果免疫；已有燃烧与咒印保留。',
    '双面场地：火场主动攻击+25%，折射阵承受主动攻击+25%，禁域承受主动攻击-25%；血池主动支付1点生命返2格挡，回合末流失剩余持续时间R点船体/生命。',
    '潮汐与结算修正：史诗延时2/4/6、传说5/8/10/13；伤害报价、卡面、移动后意图、血池风险和自爆蓄潮结算同步。',
    'Header 与功能导航：图标统一80×80、功能页图标与名称、左右切换箭头、共用深色渐变底板及设置入口。',
    '船坞与工坊：全窗口自适应装备和卡组页面，装备引线、稀有度展示、部件/附带卡牌比较、提示框与返回上下文调整。',
    '库存与出航补给：统一物品网格，仓库/船舱位置浏览切换，移除排序和全部存入入口，保留单件交易与出航补给编辑。',
    '航行显示与操作：日期、时钟、昼夜时间带和下一阶段倒计时更新；滑块使用旋转船舵手柄，原操作回调保留。',
    '卡牌与功能图标美术：51张卡图修复（新增50张专属图、替换1张），11张功能页Header图标；不同名共图问题修正。'
)
$reportRisk = '已知设计风险，未作为已修复事项：高阶延时仍可能使火场累计燃烧和血池/禁域代价过大。血池R=5/10/13完整停留累计流失15/55/91点；登船继承后无法靠海战走位离开。80生命、R=10血池与禁域共同继承，在无其他伤害/治疗的示例中第三次自身回合末可触发咒印斩杀。自动化通过说明实现与规则一致，不代表全体系数值已平衡；静态火场模型也不是Boss实战必然伤害。'
$reportBaselineNote = '对比基线为上一次成功发布0.3.20261025.1。六港独立设计稿没有接入，港口调色尝试已撤回；宝藏漫画、基础战斗图标及此前部件调参已在旧包中，不重复列为本次新增。Forecast行距旧测试断言同步仅属验收维护。'
$reportZipName = Split-Path -Leaf $reportZip
$reportZipHref = Html ([Uri]::EscapeDataString($reportZipName))
$reportZipGiB = ($reportZipInfo.Length / 1GB).ToString('F2', [Globalization.CultureInfo]::InvariantCulture)
$reportSuiteLines = @($reportSuites | ForEach-Object {
    $_.Suite + '：通过 ' + (Number $_.Passed) + '，失败 ' + (Number $_.Failed) + '，跳过 ' + (Number $_.Skipped) + '。'
})
$reportPlain = @(
    ('第七港口 ' + $reportManifest.Version + ' — 场地与界面更新'),
    '', '对比基线：0.3.20261025.1', ('平台：' + $reportManifest.Platform),
    '发布状态：PASS（已封存，报告生成时重新核验ZIP与正式输入身份）',
    ('自动化总计：通过 ' + (Number $reportValidation.Passed) + '，失败 ' + (Number $reportValidation.Failed) + '，跳过 ' + (Number $reportValidation.Skipped) + '。')
) + $reportSuiteLines + @(
    ('实际成品smoke：PASS；进程退出码 ' + $reportSmokeResult.ExitCode + '；版本、公司和产品身份匹配。'),
    ('包完整性：PASS；逐文件校验 ' + (Number $reportManifest.Package.FilesVerified) + ' 项；构建资源图片 ' + (Number $reportImages.Images) + ' 张。'),
    ('ZIP：' + $reportZipName + '；' + (Number $reportZipInfo.Length) + ' 字节（' + $reportZipGiB + ' GiB）。'),
    ('ZIP SHA256：' + $reportZipHash),
    '', '主要更新：'
) + @($reportUpdates | ForEach-Object { '- ' + $_ }) + @(
    '', $reportRisk, '', $reportBaselineNote,
    '', '完整变更检查：', $reportChanges,
    '', '相关证据：', '发布清单：release-manifest.json', ('回归验证：' + $reportValidationPath),
    ('正式源清单 SHA256：' + $reportSourceHash), ('隔离构建输入清单 SHA256：' + $reportProjectHash),
    '完整数值检查：数值检查报告.txt',
    '', '启动：解压ZIP后保留整个Windows目录，双击第七港口.exe；不要只复制EXE。',
    '本报告只在包根附加文档，不改Windows目录、ZIP或发布清单。跳过用例是原测试结果的一部分，不计为通过。'
)
$reportRows = ($reportSuites | ForEach-Object {
    '<tr><td>' + (Html $_.Suite) + '</td><td>' + (Number $_.Passed) + '</td><td>' + (Number $_.Failed) + '</td><td>' + (Number $_.Skipped) + '</td></tr>'
}) -join "`n"
$reportList = ($reportUpdates | ForEach-Object { '<li>' + (Html $_) + '</li>' }) -join "`n"
$reportHtml = @"
<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>第七港口 $(Html $reportManifest.Version) · 验收报告</title>
<style>body{margin:0;background:#edf2f3;color:#182e36;font:16px/1.75 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1060px;margin:32px auto;padding:32px;background:white;border-radius:14px}h1{font-size:28px;line-height:1.4}h2{font-size:20px;margin-top:30px}.status{background:#e6f5ec;padding:16px;border-left:5px solid #287448}.risk{background:#fff3dd;padding:18px;border-left:5px solid #b67820}a{color:#075f91}table{border-collapse:collapse;width:100%}td,th{border-bottom:1px solid #ccd7dc;text-align:left;padding:9px}li{margin:9px 0}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f4f6f7;padding:16px;border-radius:8px;font:14px/1.75 system-ui,"Microsoft YaHei",sans-serif}code{overflow-wrap:anywhere}small{color:#52666f}@media(max-width:700px){main{margin:0;padding:20px;border-radius:0}h1{font-size:23px}}</style></head>
<body><main><h1>第七港口 $(Html $reportManifest.Version)<br>场地与界面更新 · 验收报告</h1>
<p>对比基线：<strong>0.3.20261025.1</strong>　平台：$(Html $reportManifest.Platform)</p>
<div class="status"><strong>发布封存与证据核验：PASS</strong><br>通过 $(Number $reportValidation.Passed) 项，失败 $(Number $reportValidation.Failed) 项，跳过 $(Number $reportValidation.Skipped) 项。正式源清单与构建输入身份匹配。</div>
<p><a href="$reportZipHref">下载本包：$(Html $reportZipName)</a> · $(Number $reportZipInfo.Length) 字节（$reportZipGiB GiB）</p>
<p><small>解压后保留整个 Windows 目录，双击第七港口.exe。不要只复制 EXE。</small></p>
<h2>自动化与成品检查</h2><table><thead><tr><th>测试套件</th><th>通过</th><th>失败</th><th>跳过</th></tr></thead><tbody>$reportRows</tbody></table>
<p>实际成品 smoke：<strong>PASS</strong>，进程退出码 $(Html $reportSmokeResult.ExitCode)，版本、公司和产品身份匹配。ZIP 逐文件完整性验证 <strong>PASS</strong>，共 $(Number $reportManifest.Package.FilesVerified) 项；构建日志确认 $(Number $reportImages.Images) 张资源图片。</p>
<p><small>跳过用例按原始测试报告保留，不计为通过；封包验证不能替代完整的玩家体验评估。</small></p>
<h2>主要更新</h2><ol>$reportList</ol>
<h2>保留的数值风险</h2><div class="risk">$(Html $reportRisk)</div><p><a href="%E6%95%B0%E5%80%BC%E6%A3%80%E6%9F%A5%E6%8A%A5%E5%91%8A.txt">完整数值检查报告</a></p>
<p>$(Html $reportBaselineNote)</p>
<details><summary>展开完整版本差异检查</summary><pre>$(Html $reportChanges)</pre></details>
<details><summary>展开实际成品 smoke 记录</summary><pre>$(Html $reportSmokeText)</pre></details>
<details><summary>展开数值检查全文与模型边界</summary><pre>$(Html $reportBalance)</pre></details>
<h2>文件与身份核验</h2><p>ZIP SHA256：<code>$(Html $reportZipHash)</code></p>
<p>正式源清单 SHA256：<code>$(Html $reportSourceHash)</code><br>隔离构建输入清单 SHA256：<code>$(Html $reportProjectHash)</code></p>
<p>发布清单：<a href="release-manifest.json">release-manifest.json</a><br>回归验证：<code>$(Html $reportValidationPath)</code></p>
<p><small>这些报告在 Seal 成功后添加到包目录根，不修改 Windows 内容或 ZIP。</small></p>
</main></body></html>
"@

$reportHtmlPath = Assert-ReportPath (Join-Path $reportRelease '中文验收报告.html') $reportRelease -MayNotExist
$reportTextPath = Assert-ReportPath (Join-Path $reportRelease '更新说明.txt') $reportRelease -MayNotExist
$reportBalanceCopy = Assert-ReportPath (Join-Path $reportRelease '数值检查报告.txt') $reportRelease -MayNotExist
foreach ($target in @($reportHtmlPath, $reportTextPath, $reportBalanceCopy)) {
    if ((Test-Path -LiteralPath $target) -and (Get-Item -LiteralPath $target).PSIsContainer) { throw ('A report destination is a directory: ' + $target) }
}
[IO.File]::WriteAllText($reportHtmlPath, $reportHtml, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($reportTextPath, ($reportPlain -join [Environment]::NewLine), [Text.UTF8Encoding]::new($true))
[IO.File]::WriteAllBytes($reportBalanceCopy, [IO.File]::ReadAllBytes($reportBalancePath))
[void](Assert-ReportHash $reportBalanceCopy (Get-FileHash -LiteralPath $reportBalancePath -Algorithm SHA256).Hash)
Write-Output ('PASS sealed release reports: ' + $reportHtmlPath)
Write-Output ('Verified ' + (Number $reportValidation.Passed) + ' passed / ' + (Number $reportValidation.Skipped) + ' skipped; ZIP ' + $reportZipName + ' ' + (Number $reportZipInfo.Length) + ' bytes.')
Write-Output 'Only three root-level report files were written. No Windows/package/cache files were changed or created.'
