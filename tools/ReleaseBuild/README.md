# Windows 测试构建与成品启动验收

在独立 Unity 验证工程运行，避免锁定玩家正在使用的主工程。先通过 EditMode / PlayMode，再构建；不要把 UIPreviews 或 SampleScene 加入成品。

## 现役发布流程

顺序为 `DesktopRelease.ps1 Snapshot` → 完整 EditMode / PlayMode → `Verify.ps1` → 提交并推送 Git → `Windows` → `Smoke` → `Package` → `Seal`。以下是流程说明，不代表本轮测试或发布已经通过。

1. 在 `D:/Game/tmp` 下准备独立 Unity 工程，使用全新的快照、测试、验收和发布目录。调用 `DesktopRelease.ps1 -Stage Snapshot`，显式指定 `-ProjectPath`、`-EvidenceDirectory`、`-ReleaseDirectory`、`-Version`、`-ZipFileName` 与 `-ValidationSummary`；后续各阶段沿用相同参数。`-ValidationSummary` 指向稍后由 `Verify.ps1` 生成的 `verification-summary.json`。不要预先创建 Windows 成品目录，不覆盖历史证据，不恢复 `UIPreviewsForMe`。
2. 对同一快照分别运行完整 EditMode 和 PlayMode，两次都必须传 `-SourceManifest`，不可带 `-TestFilter`。完整交互回归可能运行约40分钟，单轮超时默认为60分钟，可用 `-TimeoutMinutes` 显式设置（1–240分钟）；不会改变用例筛选或通过标准。测试需要图形设备，使用 `RunTests.ps1` 的默认图形参数，不加 `-nographics`。例如下列路径必须先由本轮 Snapshot 准备；每次重跑换新的输出目录：

```powershell
$project = 'D:/Game/tmp/release-20261018-01'
$snapshot = 'D:/Game/Artifacts/Release20261018/Snapshot01'
$editRun = 'D:/Game/Artifacts/Release20261018/Edit01'
$playRun = 'D:/Game/Artifacts/Release20261018/Play01'
$verification = 'D:/Game/Artifacts/Release20261018/Verification01'
& 'D:/Game/tools/ReleaseBuild/RunTests.ps1' -Platform EditMode -ProjectPath $project -Output $editRun -SourceManifest "$snapshot/source-manifest.csv"
& 'D:/Game/tools/ReleaseBuild/RunTests.ps1' -Platform PlayMode -ProjectPath $project -Output $playRun -SourceManifest "$snapshot/source-manifest.csv"
& 'D:/Game/tools/ReleaseBuild/Verify.ps1' -EditRun $editRun -PlayRun $playRun -SnapshotDirectory $snapshot -OutputDirectory $verification
```

3. 只有两套完整测试及 `Verify.ps1` 均成功后，才提交、推送已审核的源码和工具到 Git。校验器核对测试前后的输入清单哈希、结果 XML 哈希和逐项数量；`skipped-tests.json` 单独记录退役及专项条件跳过项，不能把跳过项算作通过。源码有修复时应重新建立快照并验证，不能沿用旧快照的通过结果。
4. 使用同一快照依次运行 `DesktopRelease.ps1 -Stage Windows`、`-Stage Smoke`。添加准确的发布说明后运行 `-Stage Package`、`-Stage Seal`；每阶段成功再继续。实际成品冒烟与 ZIP 逐文件哈希检查属于出包验收，Git 推送成功不代替这些检查。

Snapshot 创建前及 RunTests 启动 Unity 前会只读检查全部 Assets `.meta`：每份须有唯一的 32 位十六进制 GUID，资产之间不能重复，失败会列出路径并停止。可单独调用 `AssertMetaGuids.ps1 -AssetsPath <Assets目录>`；`TestMetaGuids.ps1` 使用 `tmp` 内隔离合成数据验证规则和两个入口，不启动 Unity。

若已决定发布审核完成的快照、把主工程的后续改动留到下一版，可将 `source-manifest.csv` 对应的生产源码按原路径与哈希完整保存为冻结镜像，后续 `DesktopRelease.ps1` 调用统一传 `-SourceProjectPath 'D:/Game/Artifacts/CodeReviewRelease20261023/FrozenSource11/UnityClient'`。默认仍为工作区 `UnityClient`；指定目录必须在工作区内，且与 `-ProjectPath` 独立、互不包含。Windows/Seal 仍完整校验镜像与隔离工程的文件清单、快照和测试哈希及版本，镜像不能直接使用含验证身份覆盖项的隔离工程。

每次发布必须明确版本：通用脚本传 `-Version`（例如 `0.3.20261018.1`，四段数字），直接调用 Unity builder 时传 `-releaseVersion`。示例版本仅供说明，发布时使用本轮指定的新版本；不要依赖 builder 的历史默认版本。下面保留的旧示例用于说明单项操作，不代替上述完整流程。

```powershell
& 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' `
  -batchmode -quit `
  -projectPath D:/Game/tmp/regional-ports-check-20260906 `
  -executeMethod ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine `
  -releaseOutput D:/Game/Builds/ShatteredSail-Windows-20260916/ShatteredSail.exe `
  -releaseVersion '0.3.20260916.1' `
  -logFile D:/Game/UnityClient/Artifacts/ReleaseAudit20260916/Build.log
```

构建入口拒绝覆盖已有 exe，也拒绝非空输出目录，防止混入旧版文件。重建请使用新的输出目录。设置以 `try/finally` 恢复；构建时沿用已发布的存档产品身份，并采用窗口模式和 Mono Win64。输出旁的 `build-report.txt` 是 Unity BuildReport 摘要。通过 `Start-Process` 后台调用时使用 `-WindowStyle Hidden`。

成品验收使用实际 exe，不能拿 Editor 截图冒充成品：

```powershell
& 'D:/Game/Builds/ShatteredSail-Windows-20260916/ShatteredSail.exe' `
  -screen-fullscreen 0 -screen-width 1280 -screen-height 720 `
  -shatteredSailSmokeTest D:/Game/UnityClient/Artifacts/ReleaseAudit20260916/PlayerSmoke `
  -logFile D:/Game/UnityClient/Artifacts/ReleaseAudit20260916/Player.log
```

`-shatteredSailSmokeTest` 是显式只读验收开关：检查启动场景、资源、中文字体引用、输入系统、设置打开/关闭、绘制帧和打包场景；还用成品 Shader 离屏绘制全部船型的完整图块、镜像及不同转向，检查边缘透明和船体可见。写出截图与报告后以 0（通过）或 1（失败）退出。不会开始新游戏、应用设置或执行 GM，不写 PlayerPrefs。普通双击不创建此检查器。

隐藏窗口没有可读屏幕缓冲区时，成品中的真实主菜单 Canvas 通过当前渲染管线输出到 1920×1080 RenderTexture；这是打包后的 UI 渲染，不是 Windows 桌面截图，也不使用 Editor 预览素材。

完整游戏与 GM 交互由 PlayMode 回归覆盖；上述成品验收只证明该机器的启动/渲染/主菜单交互，不是全流程成品人工通关。

`Package.ps1` 的 ZIP 路径必须在构建目录之外，脚本会拒绝自包含路径及覆盖旧 ZIP。打包前会检查必需文件非空、数据和 Mono 运行时确为包含文件的目录，并要求构建报告中只有一条明确的 `Result: Succeeded` 结果。压缩结束后逐文件读取 ZIP 内数据并比对 SHA-256，确认完整运行依赖没有遗漏或损坏。

可通过 `& 'D:/Game/tools/ReleaseBuild/TestPackage.ps1'` 运行隔离的打包防护回归。它在 `tmp` 下创建新的测试目录，验证错误类型、缺失或空依赖、失败或歧义报告、自包含路径、旧 ZIP 保护以及中英文 exe 文件名；成功测试包也会逐文件校验。测试保留 `summary.json` 和全部输入，不使用 Unity，也不证明测试文件具有可运行的游戏内容。

默认可执行文件名仍为 `ShatteredSail.exe`；如果发布包需要使用其他名称，可传入
`-ExecutableName`。例如第七港口 v1 的目录、程序和 ZIP 可以这样命名：

```powershell
& 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath D:/Game/UnityClient `
  -executeMethod ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine `
  -releaseOutput 'D:/Game/Builds/第七港口v1/第七港口v1.exe' `
  -releaseVersion '0.3.20260916.1' `
  -logFile 'D:/Game/Builds/第七港口v1-build.log'

& 'D:/Game/tools/ReleaseBuild/Package.ps1' `
  -BuildDirectory 'D:/Game/Builds/第七港口v1' `
  -ZipPath 'D:/Game/Builds/第七港口v1.zip' `
  -ExecutableName '第七港口v1.exe'
```
