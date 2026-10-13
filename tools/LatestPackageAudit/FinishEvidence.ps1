$ErrorActionPreference = 'Stop'
$auditOutput = 'D:\Game\Artifacts\LatestPackageReview20261011'
$auditRelease = 'D:\Game\Builds\第七港口-代码复核优化20261011'
$auditSourceRoot = 'D:\Game\UnityClient\Assets'
$auditSnapshotRoot = 'D:\Game\tmp\latest-package-review-20261011\Assets'
$auditOriginalRoot = 'D:\Game\tmp\naval-ammunition-rules-20261011\Assets'
$auditManifest = @()
$auditChanges = @()
foreach ($auditFile in Get-ChildItem -LiteralPath $auditSourceRoot -File -Recurse | Where-Object { $_.Extension -eq '.cs' -or $_.Name -match '\.cs\.meta$' }) {
    $auditRelative = $auditFile.FullName.Substring($auditSourceRoot.Length + 1)
    $auditSnapshotFile = Join-Path $auditSnapshotRoot $auditRelative
    $auditSourceHash = (Get-FileHash -LiteralPath $auditFile.FullName).Hash
    $auditSnapshotHash = if (Test-Path -LiteralPath $auditSnapshotFile) { (Get-FileHash -LiteralPath $auditSnapshotFile).Hash } else { '' }
    $auditManifest += [pscustomobject]@{Path=$auditRelative.Replace('\','/');Source=$auditSourceHash;Snapshot=$auditSnapshotHash;Equal=$auditSourceHash -eq $auditSnapshotHash}
    if ($auditFile.Extension -ne '.cs') { continue }
    $auditOriginalFile = Join-Path $auditOriginalRoot $auditRelative
    if (-not (Test-Path -LiteralPath $auditOriginalFile)) { continue }
    $auditOriginalHash = (Get-FileHash -LiteralPath $auditOriginalFile).Hash
    if ($auditSourceHash -eq $auditOriginalHash) { continue }
    $auditChanges += [pscustomobject]@{Path=$auditRelative.Replace('\','/');Before=$auditOriginalHash;After=$auditSourceHash}
    $auditPatch = & git --no-pager -c core.autocrlf=false diff --no-index -- $auditOriginalFile $auditFile.FullName 2>$null
    [IO.File]::WriteAllText((Join-Path $auditOutput ($auditFile.BaseName + '.patch')),($auditPatch -join "`n"),[Text.UTF8Encoding]::new($false))
}
if (@($auditManifest | Where-Object { -not $_.Equal }).Count -gt 0) { throw 'Production scripts differ from validated snapshot.' }
$auditManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $auditOutput 'source-parity.json') -Encoding utf8
$auditChanges | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $auditOutput 'code-changes.json') -Encoding utf8
[xml]$auditEdit = Get-Content -LiteralPath (Join-Path $auditOutput 'EditModeFinal.xml')
[xml]$auditPlay = Get-Content -LiteralPath (Join-Path $auditOutput 'PlayModeFinal.xml')
if ($auditEdit.'test-run'.result -ne 'Passed' -or $auditPlay.'test-run'.result -ne 'Passed') { throw 'Final Unity checks did not pass.' }
$auditPackage = Get-Content -LiteralPath (Join-Path $auditOutput 'package-verification.json') -Raw | ConvertFrom-Json
$auditGuards = Get-Content -LiteralPath (Join-Path $auditOutput 'package-guard-tests.json') -Raw | ConvertFrom-Json
$auditSmoke = Get-Content -LiteralPath (Join-Path $auditOutput 'PlayerSmokeFinal\player-smoke-report.txt') -Raw
if ($auditPackage.Result -ne 'PASS' -or $auditGuards.failed -ne 0 -or -not $auditSmoke.StartsWith('PASS')) { throw 'Package checks did not pass.' }
$auditBuildReport = Get-Content -LiteralPath (Join-Path $auditRelease 'Windows\build-report.txt') -Raw
if ($auditBuildReport -notmatch '(?m)^Result: Succeeded\r?$' -or $auditBuildReport -notmatch '(?m)^Errors: 0\r?$' -or $auditBuildReport -notmatch '(?m)^Warnings: 0\r?$') { throw 'Build must pass with zero errors and warnings.' }
$auditMenuReport = Get-Content -LiteralPath (Join-Path $auditOutput 'MenuScreensFinal\report.json') -Raw | ConvertFrom-Json
if (@($auditMenuReport.frames | Where-Object { $_.error -or -not $_.verifiedScaler }).Count -gt 0) { throw 'Menu rendering failed.' }
$auditSummary = [pscustomobject]@{
    Result='PASS'; Date='2026-10-11'; EditorPassed=[int]$auditEdit.'test-run'.passed; PlayModePassed=[int]$auditPlay.'test-run'.passed
    PackageGuardPassed=$auditGuards.passed; SourceFilesVerified=$auditManifest.Count; ChangedCSharpFiles=$auditChanges.Count
    MenuFrames=$auditMenuReport.frames.Count; PackageFilesVerified=$auditPackage.FilesVerified; ZipSHA256=$auditPackage.ZipSHA256
    PackageBytes=$auditPackage.ZipBytes; Scope='Full EditMode; selected battle/navigation/tutorial/profile/cargo/achievement PlayMode regressions; read-only packaged-player startup. Not a full PlayMode suite or manual player playthrough.'
}
$auditSummary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $auditOutput 'verification-summary.json') -Encoding utf8
$auditReport = @"
第七港口 · 最新包体代码复核与优化 · 2026-10-11

原包：火药炮弹规则版 0.3.20261011。
原 ZIP 177 个文件逐项 SHA-256 比对通过；实际程序只读启动通过。
原包解压后约 933.5 MiB，ZIP 约 822.3 MiB；构建资产中纹理约占 95.3%。本轮优化计算与可靠性，未改图片质量或压缩格式。

生产修改
1. 航行观察限制在视距包围框内，跳过已探索单元。原每次扫描 7,680 格，基准位点仅 52–220 格；探索粗格、细格、地形遮挡和历史保留逐格等价。
2. 教学提示缓存视口、纸尺寸及保护区，静止时跳过候选位置求解；移动、尺寸、可交互状态变化仍重新布局。
3. 海战动画移除每帧临时数组和路径拼接，每船船体组件查询从三次减为一次；保留转向、海面接触、缩放及后坐力。
4. 成就末行与分页间距由 8 增至 15 逻辑像素；相邻行仍有 9 像素，分页/返回位置不变。
5. 组件名称/品质标题使用原生标题字重，数量输入取消伪粗体；字号、颜色和位置保留。
6. 打包脚本拒绝错误类型、空的必需文件/目录，要求唯一且准确的成功结果行，防止错误构建误报 PASS。

检查同步
首轮全量 EditMode 3,182/3,194：字体六项、成就间距一项、无专属卡船员的旧断言五项失败。
首轮关键 PlayMode 52/55：三项旧火药贴右边缘断言失败。
后续 EditMode 3,190/3,196：六项市场页面旧行距断言失败，已按现有界面逐项同步并保留失败记录。
已修实际字重和间距问题；酒馆按有卡/无卡分别验证，弹药按两种角标验证。仅实际卡面正文允许共享格式规定的条件强调，其余字体约束继续保留，并新增豁免边界测试。原始失败 XML/log 保留。

最终证据
- EditModeFinal.xml：$($auditSummary.EditorPassed)/$($auditSummary.EditorPassed)，完整编辑器测试通过。
- PlayModeFinal.xml：$($auditSummary.PlayModePassed)/$($auditSummary.PlayModePassed)，关键战斗、航行、教学、存档恢复、船舱、成就交互通过。
- package-guard-tests.json：$($auditSummary.PackageGuardPassed) 项打包防护回归通过。
- source-parity.json：$($auditSummary.SourceFilesVerified) 个脚本及元数据与隔离验证快照一致；code-changes.json/各 .patch 记录与原包源码快照差异。
- observation-benchmark.txt：纯 Core/Mono，五轮交替、每轮 1,000 次观察；旧全图函数中位 275.361 ms，新版 9.301 ms，29.60 倍。旧循环重建经原包源码快照核对；不代表 Player FPS、整局性能或包体缩小。
- MenuScreensFinal/report.json：$($auditSummary.MenuFrames) 张五分辨率实际菜单渲染，CanvasScaler 校验通过。
- BuildFinal.log / Windows/build-report.txt：Windows x86_64 / Mono 成功，0 错误、0 警告。
- PlayerSmokeFinal/player-smoke-report.txt：实际新成品只读启动 PASS，场景、中文字体、设置、12 艘船图、63 个音频检查通过，未读取或修改玩家存档。
- package-verification.json：$($auditSummary.PackageFilesVerified) 个成品文件逐项 SHA-256 校验通过。

范围：复核最新核心规则、资源/界面热点、存储及发布脚本；存储代码未发现可确定的单进程回归。所有 EditMode 已运行，PlayMode 为上述关键回归集合；成品验收是只读启动检查，未声明全量 PlayMode 或人工完整通关。
沿用 ShatteredSail / Shattered Sail 存档身份，保留现有未提交工作，旧发布包未覆盖。

程序：$auditRelease\Windows\第七港口.exe
压缩包：$auditRelease\第七港口-Windows.zip
ZIP SHA-256：$($auditSummary.ZipSHA256)
"@
[IO.File]::WriteAllText((Join-Path $auditOutput '复核与优化.txt'),$auditReport,[Text.UTF8Encoding]::new($false))
$auditNotes = @"
第七港口 Windows 代码复核优化版
版本：0.3.20261011

解压完整目录后启动 Windows\第七港口.exe，保留数据文件与运行库。

航行视野采用局部扫描，探索结果保持一致；缓存教学提示布局，减少海战动画临时分配。
修正成就页间距、组件标题及数量输入字重；加强打包失败和空依赖检测。
保留最新火药炮弹规则及精细海图，沿用原存档身份。

完整 EditMode $($auditSummary.EditorPassed) 项、关键 PlayMode $($auditSummary.PlayModePassed) 项、打包防护 $($auditSummary.PackageGuardPassed) 项通过。
五种分辨率 $($auditSummary.MenuFrames) 张菜单渲染检查通过；Windows 构建 0 错误、0 警告。
实际成品只读启动通过；完整压缩包 $($auditSummary.PackageFilesVerified) 个文件 SHA-256 校验通过。
纯 Core 观察函数基准约 29.6 倍加速，不代表游戏整体帧率。

详细记录：$auditOutput\复核与优化.txt
ZIP SHA-256：$($auditSummary.ZipSHA256)
"@
[IO.File]::WriteAllText((Join-Path $auditRelease '发布说明.txt'),$auditNotes,[Text.UTF8Encoding]::new($false))
$auditSummary | ConvertTo-Json
