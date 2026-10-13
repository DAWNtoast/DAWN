$ErrorActionPreference = 'Stop'
$startupRoot = 'D:\Game\Artifacts\StartupMemory20261011'
$startupRelease = 'D:\Game\Builds\第七港口-启动内存优化20261011'
& (Join-Path $PSScriptRoot 'Compare.ps1') -OutputRoot $startupRoot -TrialDirectory VisibleTrials
$startupComparison = Get-Content (Join-Path $startupRoot 'comparison.json') -Raw | ConvertFrom-Json
[xml]$startupEdit = Get-Content (Join-Path $startupRoot 'EditModeMemoryFinal.xml')
[xml]$startupPlay = Get-Content (Join-Path $startupRoot 'PlayModeMemoryFinal.xml')
if ([int]$startupEdit.'test-run'.failed -ne 0 -or [int]$startupPlay.'test-run'.failed -ne 0 -or [int]$startupPlay.'test-run'.passed -lt 41) { throw 'Unity validation failed.' }
$startupPackage = Get-Content (Join-Path $startupRoot 'package-verification.json') -Raw | ConvertFrom-Json
$startupSmoke = Get-Content (Join-Path $startupRoot 'PlayerSmokeMemoryFinal\player-smoke-report.txt') -Raw
$startupBuild = Get-Content (Join-Path $startupRelease 'Windows\build-report.txt') -Raw
if ($startupPackage.Result -ne 'PASS' -or -not $startupSmoke.StartsWith('PASS') -or $startupBuild -notmatch '(?m)^Errors: 0\r?$' -or $startupBuild -notmatch '(?m)^Warnings: 0\r?$') { throw 'Release validation failed.' }
$startupSemantic = Get-Content (Join-Path $startupRoot 'presentation-semantic-parity.json') -Raw | ConvertFrom-Json
$startupNegativeControls = Get-Content (Join-Path $startupRoot 'AssemblyComparisonNegativeControlsRetry\validation.json') -Raw | ConvertFrom-Json
if ($startupNegativeControls.Result -ne 'PASS') { throw 'Assembly comparison negative controls failed.' }
$startupParity = foreach ($startupName in @('ShatteredSail.Core.dll','ShatteredSail.Content.dll','ShatteredSail.Persistence.dll','ShatteredSail.Presentation.dll')) {
    $startupDiagnostic = Join-Path ($startupRoot + '\Optimized\Windows\StartupAudit_Data\Managed') $startupName
    $startupDelivered = Join-Path ($startupRelease + '\Windows\第七港口_Data\Managed') $startupName
    $startupDiagnosticHash = (Get-FileHash -LiteralPath $startupDiagnostic).Hash
    $startupDeliveredHash = (Get-FileHash -LiteralPath $startupDelivered).Hash
    $startupByteEqual = $startupDiagnosticHash -eq $startupDeliveredHash
    $startupSemanticAccepted = $startupName -eq 'ShatteredSail.Presentation.dll' -and $startupSemantic.Result -eq 'PASS' -and $startupSemantic.BeforeSHA256 -eq $startupDiagnosticHash -and $startupSemantic.AfterSHA256 -eq $startupDeliveredHash -and @($startupSemantic.SemanticDifferences).Count -eq 0 -and $startupSemantic.CheckedRecords -eq 9266
    $startupEquivalent = $startupByteEqual -or $startupSemanticAccepted
    [pscustomobject]@{Name=$startupName;Diagnostic=$startupDiagnosticHash;Delivered=$startupDeliveredHash;Equal=$startupByteEqual;RuntimeEquivalent=$startupEquivalent;Evidence=if ($startupByteEqual) {'SHA256'} else {'presentation-semantic-parity.json'}}
}
if (@($startupParity | Where-Object { -not $_.RuntimeEquivalent }).Count) { throw 'Production diagnostic/release assemblies differ in runtime content.' }
$startupParity | ConvertTo-Json | Set-Content (Join-Path $startupRoot 'optimized-assembly-parity.json') -Encoding utf8
if (Test-Path -LiteralPath ($startupRelease + '\Windows\第七港口_Data\Managed\ShatteredSail.StartupMemoryAudit.dll')) { throw 'Diagnostic assembly must not ship.' }
$startupManifest = foreach ($startupFile in Get-ChildItem 'D:\Game\UnityClient\Assets' -File -Recurse | Where-Object { $_.Extension -eq '.cs' -or $_.Name -match '\.cs\.meta$' }) {
    $startupRelative = $startupFile.FullName.Substring('D:\Game\UnityClient\Assets\'.Length)
    $startupSnapshotFile = Join-Path 'D:\Game\tmp\latest-package-review-20261011\Assets' $startupRelative
    $startupFileHash = (Get-FileHash -LiteralPath $startupFile.FullName).Hash
    $startupSnapshotHash = (Get-FileHash -LiteralPath $startupSnapshotFile).Hash
    [pscustomobject]@{Path=$startupRelative;Source=$startupFileHash;Snapshot=$startupSnapshotHash;Equal=$startupFileHash -eq $startupSnapshotHash}
}
if (@($startupManifest | Where-Object { -not $_.Equal }).Count) { throw 'Validated source differs.' }
$startupManifest | ConvertTo-Json | Set-Content (Join-Path $startupRoot 'source-parity.json') -Encoding utf8
$startupMenus = Get-Content (Join-Path $startupRoot 'MenuScreensMemoryFinal\report.json') -Raw | ConvertFrom-Json
if (@($startupMenus.frames | Where-Object { $_.error -or -not $_.verifiedScaler }).Count -ne 0) { throw 'Menu capture failed.' }
$startupVisual = Get-Content (Join-Path $startupRoot 'NormalScreenshots\visual-comparison.json') -Raw | ConvertFrom-Json
if ($startupVisual.Result -ne 'PASS') { throw 'Normal window visual comparison failed.' }
$startupRenderEvidence = foreach ($startupTrial in Get-ChildItem -LiteralPath (Join-Path $startupRoot 'VisibleTrials') -Directory) {
    $startupInventory = Get-Content (Join-Path $startupTrial.FullName 'memory.json') -Raw | ConvertFrom-Json
    $startupCameraBuffers = @($startupInventory.renderTextures | Where-Object { $_.name -match '^_Camera(TargetAttachment|DepthAttachment)' -and $_.width -eq 1920 -and $_.height -eq 1080 }).Count
    $startupExpectedBuffers = if ($startupTrial.Name.StartsWith('Baseline-')) { 3 } else { 0 }
    if ($startupCameraBuffers -ne $startupExpectedBuffers) { throw ('Camera buffer inventory changed: ' + $startupTrial.Name) }
    [pscustomobject]@{Trial=$startupTrial.Name;CameraBuffers=$startupCameraBuffers;RenderedFrames=$startupInventory.renderedCameraFrames;Gpu=$startupInventory.gpu;GraphicsAPI=$startupInventory.graphicsApi}
}
$startupRenderEvidence | ConvertTo-Json | Set-Content (Join-Path $startupRoot 'render-buffer-verification.json') -Encoding utf8
function Startup-Row([string]$Fixture,[string]$Metric) { $startupComparison | Where-Object { $_.Fixture -eq $Fixture -and $_.Metric -eq $Metric } }
$startupEmptyWorking = Startup-Row 'Empty' 'StableWorkingSetMiB'
$startupVoyageWorking = Startup-Row 'Voyage' 'StableWorkingSetMiB'
$startupEmptyPrivate = Startup-Row 'Empty' 'StablePrivateMiB'
$startupVoyagePrivate = Startup-Row 'Voyage' 'StablePrivateMiB'
$startupTable = ($startupComparison | Where-Object Metric -in @('StableWorkingSetMiB','StablePrivateMiB','PeakWorkingSetMiB','PeakPrivateMiB','ManagedUsedMiB','ManagedReservedMiB') | Format-Table Fixture,Metric,Before,After,Reduction,ReductionPercent -AutoSize | Out-String).Trim()
$startupSummary = [pscustomobject]@{
    Result='PASS';EditorPassed=[int]$startupEdit.'test-run'.passed;PlayModePassed=[int]$startupPlay.'test-run'.passed;PlayModeSkipped=[int]$startupPlay.'test-run'.skipped
    Trials=12;TrialsPerFixturePerVariant=3;SourceFiles=$startupManifest.Count;MenuFrames=$startupMenus.frames.Count;PackageFiles=$startupPackage.FilesVerified
    ZipSHA256=$startupPackage.ZipSHA256;EmptyWorkingSet=$startupEmptyWorking;VoyageWorkingSet=$startupVoyageWorking;EmptyPrivateCommit=$startupEmptyPrivate;VoyagePrivateCommit=$startupVoyagePrivate
    VisualComparison=$startupVisual;GPU=$startupRenderEvidence[0].Gpu;GraphicsAPI=$startupRenderEvidence[0].GraphicsAPI
    PresentationSemanticRecords=$startupSemantic.CheckedRecords;AssemblyComparisonNegativeControls=$startupNegativeControls.Result
    MeasurementScope='Non-Development isolated identity, 1920x1080 visible Windows Player and normal production camera/canvas rendering. No extra camera/output/screenshot allocation during measurement; no dedicated VRAM measurement.'
}
$startupSummary | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $startupRoot 'verification-summary.json') -Encoding utf8
$startupReport = @"
第七港口 · 启动内存检查与优化 · 2026-10-11

基线：Windows 代码复核优化版 0.3.20261011。
范围：启动至主菜单稳定、空角色档和合法已保存航程两种状态，以及主菜单返回的资源生命周期。

改动与原因
1. 主菜单设置/新游戏确认按首次点击构建，关闭后复用，避免启动就保留隐藏控件和它们的图片。编辑器保留完整预览。
2. 羊皮纸对话卡直接使用最终阅读纸，避免先加载被替换的通用纸。
3. 仅主菜单相机关闭 HDR，并同步场景生成器；主菜单仍为普通 ScreenSpaceOverlay UI，其他游戏场景、全局画质和图片尺寸不变。基线保留两个 1920×1080 Color Attachment 和一个 Depth Attachment，优化版不再保留这三项。相关 inventory 在各轮 memory.json。
4. 有航程档的菜单构建由约七次完整读取/恢复验证合并为一次。只传摘要字串和状态，不缓存地图/存档record；继续与新游戏点击仍重新读取，损坏后阻止继续、修复后可正常恢复。
5. 返回主菜单清除物品/成就图集静态引用，并解除投资/收藏纸自建 Sprite 的卸载固定标记；活 UI 仍保护所用资源，关闭后由 Unity 回收，重开可重新加载。

测量方法与限制
独立身份 ShatteredSailStartupAudit / StartupMemory20261011，非 Development Windows x64 Mono，额外探针只在诊断包。基线四个生产程序集与此前正式包逐项SHA-256相同；优化版Core/Content/Persistence与本次正式包SHA-256相同，Presentation由逐项代码与数据比较确认运行内容一致，具体检查及构建元数据差异见presentation-semantic-parity.json。
Presentation共9,266项比较通过，包括363种类型、2,908个方法签名与方法体、2,617个字段、417个属性及42个RVA静态初始化数据。两构建PDB路径长度相差7字节，引起调试标识和后续静态数据地址8字节对齐位移；方法代码、签名、常量、静态初始字节和引用等运行内容一致，未替换或修改正式包DLL。
比较工具另成功拒绝两个故意修改方法操作数或静态初始字节的独立副本，见AssemblyComparisonNegativeControlsRetry/validation.json。首次负向夹具创建缺Cecil依赖解析路径失败，记录保留；补全解析目录后两项拒绝检查通过。
每轮先用单独准备进程建立新隔离数据目录，调用 Core Capture/Restore 与生产 TrySave 校验航程。测量进程不生成 fixture，玩家正式存档不参与。
两版顺序交替，每种状态每版三轮，共十二个新进程。1920×1080；外部每250毫秒采样，10–22秒为稳定区间，各轮中位数再取三轮中位数；峰值排除最后 inventory/JSON。文件与系统缓存为热缓存，并非重启电脑后的首次冷启动。
测试设备：$($startupSummary.GPU)，$($startupSummary.GraphicsAPI)，Unity 6000.3.23f1。
预检确认隐藏窗口不渲染（renderedCameraFrames=0），其约377.7MiB工作集不作为正式前后比较。先完成的十二轮相同离屏渲染结果仅作辅助证据，保留在Trials和offscreen-comparison.json；该组包含共同输出RT开销，不能作为正常窗口的绝对占用。
获用户明确允许后，最终十二轮改为正常显示游戏窗口，使用原相机与ScreenSpaceOverlay UI，不添加额外相机/离屏RT/截图读回/强制GC，要求至少100帧实际相机渲染。各窗口自动退出，VisibleTrials每轮报告包含帧数、RT清单和OS样本。下面数值只来自这十二轮正常窗口测量。没有独立显存测量，不把Native texture估计与OS进程内存混用。
沿用生产 targetFrameRate=60 与原 VSync设置；目标60不等于实际60FPS。本机VSync优先，首轮约5300次渲染/22秒，约240Hz。原始probe.method的“60FPS”为描述误差，帧数与OS样本均保留，不修改原始统计，也不以此比较帧率。

三轮中位数，单位MiB
$startupTable

空档稳定工作集：$($startupEmptyWorking.Before) → $($startupEmptyWorking.After)，减少 $($startupEmptyWorking.Reduction) MiB（$($startupEmptyWorking.ReductionPercent)%）。
航程档稳定工作集：$($startupVoyageWorking.Before) → $($startupVoyageWorking.After)，减少 $($startupVoyageWorking.Reduction) MiB（$($startupVoyageWorking.ReductionPercent)%）。
工作集为驻留物理内存，Private为私有提交（包含运行时/驱动/分配器）；两者不可相加，设备及驱动变化会改变结果。

验证
完整 EditMode $($startupSummary.EditorPassed) 项通过；关键 PlayMode $($startupSummary.PlayModePassed) 项通过，1项显式旧性能基准按设置跳过。初次39通过/2失败/1跳过，是测试 fixture缺角色scope及强引用保活；修复夹具后复测通过，失败XML/log保留。
五种分辨率 $($startupSummary.MenuFrames) 张菜单渲染/CanvasScaler检查通过；正式 Windows 构建0错误、0警告；真实成品只读启动检查通过；ZIP完整 $($startupSummary.PackageFiles) 文件SHA-256逐项通过。
额外正常窗口截图前后对照通过，见NormalScreenshots/visual-comparison.json及两张Player-MainMenu.png。截图位于独立复核进程的统计结束之后，不加入十二轮内存比较；MeasuredProbe保留正式采样的原探针源码/DLL，capture-probe-parity.json单独记录截图探针。
$($startupSummary.SourceFiles) 个脚本和元数据与验证项目一致；baseline-assembly-parity.json / optimized-assembly-parity.json区分字节完全相同与运行内容一致，不掩盖Presentation文件哈希差异。all-project-source-comparison.json另记录诊断/验证/主项目全部生产源码完全一致，诊断项目两处旧测试夹具不参与Player。正式包没有内存探针，沿用 ShatteredSail / Shattered Sail 存档身份。
原发布包和现有未提交修改均保留。本轮不宣称全量PlayMode、完整人工通关或无限时长内存稳定性。

证据：VisibleTrials各轮memory.json/process-memory.csv/summary.json；trial-summary.csv、comparison.json；EditModeMemoryFinal.xml、PlayModeMemoryFinal.xml；PlayerSmokeMemoryFinal；package-verification.json。
程序：$startupRelease\Windows\第七港口.exe
压缩包：$startupRelease\第七港口-Windows.zip
ZIP SHA-256：$($startupSummary.ZipSHA256)

统计接口说明：https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Profiling.Profiler.GetTotalAllocatedMemoryLong.html
"@
[IO.File]::WriteAllText((Join-Path $startupRoot '启动内存检查与优化.txt'),$startupReport,[Text.UTF8Encoding]::new($false))
$startupNotes = @"
第七港口 · Windows 启动内存优化版 0.3.20261011
解压完整目录后运行 Windows\第七港口.exe，保留所有数据及运行库。

主菜单按需创建设置/确认弹窗，避免加载替代纸纹理，关闭主菜单不必要的HDR缓冲；航程存档构建期一次验证，点击继续仍重新读取；返回菜单释放闲置图集及设施纸。
沿用原存档身份，保留现有火药炮弹规则、精细海图与上一轮代码优化。

正常显示窗口1920×1080测量：
空档稳定工作集 $($startupEmptyWorking.Before) → $($startupEmptyWorking.After) MiB；航程档 $($startupVoyageWorking.Before) → $($startupVoyageWorking.After) MiB。各为三轮中位数，不代表独立显存，设备/驱动/分辨率变化会改变结果。
完整Editor $($startupSummary.EditorPassed)项、关键交互 $($startupSummary.PlayModePassed)项通过；构建0错误、0警告，成品只读启动及ZIP完整校验通过。
详细记录：$startupRoot\启动内存检查与优化.txt
ZIP SHA-256：$($startupSummary.ZipSHA256)
"@
[IO.File]::WriteAllText((Join-Path $startupRelease '发布说明.txt'),$startupNotes,[Text.UTF8Encoding]::new($false))
$startupSummary | ConvertTo-Json -Depth 4
