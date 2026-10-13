# 启动内存测量

用独立存档身份的非 Development Windows Player 比较启动至主菜单稳定期间的内存。

`StartupMemoryProbe.cs`、`StartupMemoryAuditBuilder.cs` 和两个 asmdef 只复制到 `D:\Game\tmp\startup-memory-20261011\Assets\StartupMemoryAudit`，不加入正式 `UnityClient/Assets`。构建方法为 `ShatteredSail.Editor.StartupMemoryAuditBuilder.BuildFromCommandLine`，参数 `-startupMemoryBuildOutput <新的绝对exe路径>`。基线/优化分别核对正式版生产程序集与源码；优先用 SHA-256，只有确证差异属于构建元数据或地址布局、且逐项函数/签名/静态数据等运行内容一致时才接受语义比较。两版使用相同探针 DLL，具体验收记录于 assembly-parity 与 presentation-semantic-parity.json。

运行示例：

```powershell
& .\tools\StartupMemoryAudit\Measure.ps1 `
  -Executable 'D:\Game\Artifacts\StartupMemory20261011\Baseline\Windows\StartupAudit.exe' `
  -OutputDirectory 'D:\Game\Artifacts\StartupMemory20261011\Trials\Baseline-Empty-1' `
  -Fixture Empty -Offscreen
```

`Fixture` 支持 `Empty` / `Voyage`。准备进程独立创建角色与航程，使用生产 Capture/Restore/TrySave 校验，数据目录在本轮输出目录内；测量进程不计入准备分配。所有轮次串行执行，共用隔离身份的 PlayerPrefs，不能并行。

默认隐藏窗口会暂停正常渲染，因此有效测量使用 `-Offscreen`，或在用户明确允许临时显示窗口后使用 `-Visible`。前者加入相同的 SDR 输出 RenderTexture，并把生产菜单接到原相机，属于受控渲染比较，不能称为正常显示窗口的绝对 RAM。探针要求至少 100 次实际相机渲染；没有截图读回、强制 GC 或探针触发的资源卸载。

外部每 250 ms 读取 OS 工作集/私有提交；稳定区间为进程启动后 10–22 秒，峰值排除最后对象清单及 JSON。保存 CSV、日志、完整清单和每轮汇总。文件缓存为热缓存；工作集是驻留物理内存，私有提交包含运行时、驱动与分配器，两者不可相加，也不代表独立显存。

基线/优化顺序交替，每个 fixture 每版 3 轮。`Compare.ps1` 汇总十二轮，`FinishEvidence.ps1` 结合 Unity 回归、正式构建、成品只读启动和 ZIP 校验生成报告。

本轮获用户允许后，正式结果采用 `RunVisibleTrials.ps1` 的十二轮正常窗口样本，位于 `VisibleTrials`；`Trials` 中的离屏样本作为辅助证据。VSync 会优先于目标帧率，本机实际约 240Hz；已保存的原探针描述误写了“60 FPS”，原始计数与统计未修改，勘误见 `measurement-method-correction.json`。

`-Visible -Capture` 可在统计结束后保存原相机与 ScreenSpaceOverlay UI 的实际帧，截图位于独立 `NormalScreenshots` 目录，不加入十二轮比较。已测量的探针源码和 DLL 保留于 `MeasuredProbe`，截图探针另记 SHA-256。

编辑器的资源字节接口与 OS 指标不同，且部分原生对象字节接口在非 Development Player 中不可用；本工具只把已加载纹理/RT清单作为资源证据，内存总量以外部 OS 采样为准。Unity 分配器统计用于辅助分析，见 [Unity 6.3 Profiler API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Profiling.Profiler.GetTotalAllocatedMemoryLong.html)。
