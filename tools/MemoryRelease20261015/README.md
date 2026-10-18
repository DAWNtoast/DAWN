# 2026-10-15 内存与发布诊断

仅用于 `D:/Game/tmp/runtime-memory-probe-20261014` 隔离工程；不得复制诊断代码进生产 Assets，也不得恢复 `UIPreviewsForMe`。

本轮从 `tools/RuntimeMemoryAudit` 复用同一 57 点测试流程，并改用独立产品身份 `ShatteredSailAudit / MemoryRelease20261015`、独立世界经济 scope 和 `Artifacts/MemoryRelease20261015` 输出。每次 Player 启动先验证实际身份及 LocalLow 路径，再清理自己的 PlayerPrefs。正式用户数据不参与测量。

1. 所有 Unity / 诊断 Player 停止后运行 `Sync.ps1 -Name Baseline`。完整 SHA256 清单和隔离副本一致性证据存入 `Baseline-Snapshot`；生产快照期间避免并发编辑。
2. 将本目录 `RuntimeMemoryPlayerProbe.cs`、`RuntimeMemoryAuditBuilder.cs` 分别复制至隔离工程 `Assets/RuntimeMemoryAudit` 与其 `Editor` 子目录。现有 asmdef 沿用。首次新建诊断目录可用 `PrepareProbe.ps1 -Project <上述工程>`。
3. `Build.ps1 -Name Baseline`：隐藏 Unity 构建非 Development 的 Windows x64 / Mono 诊断 Player。
4. `Measure.ps1 -Executable D:/Game/Artifacts/MemoryRelease20261015/Baseline/Windows/MemoryAudit.exe -OutputDirectory D:/Game/Artifacts/MemoryRelease20261015/Baseline-Measure01`。
5. 优化落盘后按新名称建立快照、构建并实测，不覆盖已有证据。要进行三次 A/B，先确保两个构建的诊断程序集字节相同，再使用 `RunTrials.ps1`。

测量通过仅证明流程、渲染和数据采样有效。受控渲染包含额外 1920×1080 RenderTexture、相机、探针及驱动开销，不能称正常可见窗口的绝对 RAM。Private Bytes 与 Working Set 不相加；没有测独立显存。静态对象数和同态检查点更适合判断资源是否被持续保留，单次 OS 分配器波动不足以证明收益或泄漏。

本轮实际基线与受控优化
--------------------

- `Baseline-Snapshot` 锁定 4,166 个源文件。首个图形 Editor 构建发生原生 OOM，保留在 `Baseline`；随后 `-nographics` Editor 构建成功，实际已测基线为 `BaselineRetry01` 与 `Baseline-Measure01`。Player 测量仍使用图形渲染，不使用 `-nographics`。
- 其他聊天随后修改战斗代码，因此优化诊断不再全量同步生产。`ApplyControlledMemoryPatch.ps1` 从 `Validation/RegressionReady02-Snapshot` 锁定清单对应的发布验证工程，只复制 ResourceMemory、AudioController、RamComicPresentation、BattleAnimationController 四个 Runtime 文件，逐项验 SHA256。
- 基线 Unity 构建已按现有 `DesktopBackgroundCompressionPolicy` 自动规范化五个贴图 meta；这与冻结发布工程导入状态一致。受控比较保留这些实际已测状态和诊断产品设置，补丁前后变化精确为四个 Runtime 文件，证据在 `OptimizedControlled02-Patch`。第一次更严格检查安全停止的证据在 `OptimizedControlled01-Patch`。
- `CanonicalProbe` 保存已经用于基线测量的原始 Unity 诊断 DLL。优化构建后 `UnifyProbe.ps1 -BuildName OptimizedControlled02` 核对源代码、初始化入口以及完整构建文件前后哈希，只允许该诊断 DLL 发生变化，不修改生产程序集或资源。
- 最终交付另行使用已冻结的完整发布快照，包含本轮错误修复；受控四文件内存诊断不是最终发布包的替代验收。

正式 Windows/macOS 构建应使用独立发布工程，生产入口 `ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine` / `ShatteredSail.Editor.MacReleaseBuilder.BuildFromCommandLine`，参数均为 `-releaseOutput <新的绝对 exe/app 路径>`。正式产物不可含 `ShatteredSail.RuntimeMemoryAudit` 程序集。Windows 完成实际 Player 烟测；macOS 在 Windows 构建机只可核验 app 结构、Intel/Apple silicon 架构与 ZIP 可执行权限，不能声称已通过 macOS 实机运行。

压缩使用 `tools/ReleaseBuild/Package.ps1` 和 `PackageMac.py`；它们校验压缩包逐文件哈希且拒绝覆盖现有包。启动任何后台 Unity / Player 一律 `Start-Process -WindowStyle Hidden`，全局测量串行。
