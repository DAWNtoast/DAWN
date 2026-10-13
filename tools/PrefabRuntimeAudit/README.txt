Prefab 固定界面与资源生命周期对照 · 2026-10-14

本工具为独立诊断用途，不得放入正式 UnityClient/Assets 或发布包。
不可生成、保存、同步 UIPreviewsForMe。不调用任何外部图像 API。
所有历史 RuntimeMemoryAudit 文件和证据保持原样。

基线：8f5563e80d11984b2c616cd4093c7cc1f39b8350。
准确源码快照工程：D:/Game/tmp/prefab-runtime-20261014。
归属标记：.prefab-runtime-owned.json（由协调方在核验源码逐文件 SHA 后建立）。
独立公司/产品：ShatteredSailAudit / PrefabRuntimeOptimization20261014。
输出根：D:/Game/Artifacts/PrefabRuntimeOptimization20261014。
正式游戏 PlayerPrefs、角色存档、存档 journal 不读写；探针启动前严格核对身份与 LocalLow 路径。
Unity、测试、Player 由协调方串行运行；不要与其他 Unity/游戏进程并发测量。

步骤（以下命令由 D:/Game 执行）：
1. 精确基线源码核验并准备隔离工程后：
   & tools/PrefabRuntimeAudit/PrepareProbe.ps1 -Project D:/Game/tmp/prefab-runtime-20261014
   & tools/PrefabRuntimeAudit/Build.ps1 -Name Baseline
2. 用实际新基线 Player 的 Managed 依赖，编译唯一 canonical probe：
   & tools/PrefabRuntimeAudit/CompileProbe.ps1 -BaselineExecutable D:/Game/Artifacts/PrefabRuntimeOptimization20261014/Baseline/Windows/PrefabAudit.exe -Revision v2
   & tools/PrefabRuntimeAudit/ApplyMeasuredProbe.ps1 -BuildName Baseline -Revision v2
   编译 rsp、依赖/source/DLL SHA、源文件快照和原始 DLL 均保存。不能借用旧轮 canonical DLL。
   v2使用ProbeTools/MeasuredProbe-v2；已存在的MeasuredProbe/v1与CalibrationBaseline01为保留证据。
   本轮v1校准后新建Baseline02：由协调方逐文件核验复制Baseline/Windows生产文件，仅替换v2诊断DLL；
   此时命令中的基线路径/BuildName使用Baseline02，不能覆盖已应用v1后的Baseline。
   已有隔离工程更新唯一probe源文件（Unity/Player停止时）：
   & tools/PrefabRuntimeAudit/SyncProbe.ps1 -Project D:/Game/tmp/prefab-runtime-20261014 -Revision v2
   它只更新Assets/PrefabRuntimeAudit/PrefabRuntimePlayerProbe.cs，并备份旧源与记录SHA。
3. 校准新进程（目录必须全新）：
   & tools/PrefabRuntimeAudit/Measure.ps1 -Executable <Baseline.exe> -OutputDirectory D:/Game/Artifacts/PrefabRuntimeOptimization20261014/CalibrationBaseline01
   只有完整 68 点、所有循环计数、实际帧数、独立身份、外部采样完整才 PASS。
4. 将优化生产源码逐文件精确同步到同一隔离工程，保留同一探针，再 Build -Name Optimized01。
   & tools/PrefabRuntimeAudit/ApplyMeasuredProbe.ps1 -BuildName Optimized01 -Revision v2
   比较两包生产源码/Managed，确认只有本轮预期差异；Apply 校验只有探针 DLL 被后处理。
5. & tools/PrefabRuntimeAudit/RunTrials.ps1 -BaselineExecutable <Baseline.exe> -OptimizedExecutable <Optimized.exe>
   顺序 B1/O1/B2/O2/B3/O3，六个新进程。强制 probe DLL SHA 相同。
   Compare.ps1 / CompareTimings.ps1 输出每点与每类操作的三轮中位数、原始值、正负变化。
6. 独立视觉运行，绝不能混入正式性能六轮：
   & tools/PrefabRuntimeAudit/Measure.ps1 -Executable <Baseline.exe> -OutputDirectory <新的BaselineVisual目录> -VisualCapture
   & tools/PrefabRuntimeAudit/Measure.ps1 -Executable <Optimized.exe> -OutputDirectory <新的OptimizedVisual目录> -VisualCapture
   & tools/PrefabRuntimeAudit/CompareVisuals.ps1 -BaselineDirectory <BaselineVisual目录> -OptimizedDirectory <OptimizedVisual目录> -OutputDirectory <新的VisualComparison目录>
   每轮 68 点 + 最后菜单共 69 张 PNG 和活动 UI 结构 JSON。像素差值图+几何/文字/字体/资源差值人工审核。
7. 正式发布前移除隔离工程 Assets/PrefabRuntimeAudit 及其 meta，并确认 Managed/初始化表没有探针。
   正式构建与已测优化生产源逐文件/程序集语义核验，由协调方独立执行。

测量口径
--------
继续原 57 点覆盖：菜单/成就/港口，9设施+仓库 Storage，20设施循环，六港36人物详情与配件店，
9航行天气时段，4冲撞海战/漫画/接舷，3场景往返。新加11个固定 UI 点，共68：
菜单设置首开/第6次；成就第6次（每次真实 next/previous 分页）；新游戏确认首开/第6次；
保存提示首开/第6次；港口设置首开/第6次；返回主菜单确认首开/第6次。
新游戏确认与保存提示通过明确标记的私有方法 fixture 打开，取消/关闭使用生产 Button 回调，
不模拟损坏存档、不执行新游戏清档。其他操作使用真实回调或已有明示 fixture。

CallbackMs：Stopwatch 从调用生产 Button.onClick / Render / LoadScene / 指定 modal 方法开始，到返回结束。
对象查找和 MethodInfo 查找在计时外（Reflection Invoke 包装开销相同）。
BindAndLayoutMs：共同诊断 Canvas 绑定和强制布局时间，单独列出。
FirstRenderedMs：操作开始到下一次真实受控相机 endCameraRendering/onPostRender 回调。
ReadyRenderedMs：到生产 fade 完成且 runtimeArtCollection 完成的实际相机帧回调。
固定 0.8 秒 settle 和 1.5 秒内存窗口不进入加载时间；时间戳由相机回调关闭，而非等待协程返回。
同一帧多次操作导致的 superseded 记录保留；缺少 first/ready 的值不冒充零或有效加载时间。
三轮比对要求动作 Id/Stage/顺序完全相同。暖操作先在每轮取中位数，再跨三轮取中位数。
“首次”指新进程首次访问，不代表清空 OS 文件缓存后的冷盘加载。没有清 OS 缓存。
相机回调表示 CPU 侧已提交该帧渲染，非 GPU fence、显示器 present 或普通可见窗口响应时间。

校准后的修正（当前canonical probe=v2，AnalysisVersion=3，历史v1原始报告不改）：
成就已在 Startup 段实际打开一次，因此 Click:Achievements 与返回操作按 occurrence=1 归首次、其余均暖访问。
MenuModalCycle:1 的再次成就打开属于暖访问；首次翻页仍按原阶段分类。
Startup:BeforeFirstScene 的 CallbackMs 是 bootstrap到sceneLoaded（包括Awake/OnEnable），不是纯UI创建回调；
其FirstRenderedMs是bootstrap到初始菜单首个受控渲染帧，非进程启动总时间。
LoadScene 请求可能下一帧才激活新场景，旧场景可在当帧先触发渲染；v1没有校验目标场景，
其LoadScene FirstRenderedMs/ReadyRenderedMs在后分析置不可用，只保留请求提交耗时。
v2将请求绑定目标sceneLoaded事件与scene handle；只有该场景激活、有该场景的活动生产Canvas、
并完成实际相机渲染，才记录first/ready。TargetSceneLoadedMs另记录目标sceneLoaded时间。
LoadFixture等目标ready帧后才注入仓库fixture，避免后续Render抢先覆盖scene timing。
CallbackMs始终只是LoadScene请求提交；评价场景加载用TargetSceneLoadedMs/FirstRenderedMs，而非callback。
原始负值sentinel置不可用。六轮比较强制v2探针、同一分析版本、相同分类/occurrence、原始报告SHA核验。
逐指标只取六轮都具备有效数据的相同action IDs；不足完整配对的动作明确计入ExcludedUnpairedActions。

视觉结构解读：UI快照仅枚举活动Graphic，新增绑定用MonoBehaviour本身不会增加Graphic差异。
Prefab包装层/重命名可改变HierarchyPath而像素完全一致，此时检查新旧相应Graphic的屏幕角点、
字体/字号/对齐/文字/颜色/纹理/sprite与可交互性逐项对应，不把路径变化直接当界面回归。
像素差而结构字段相同可能来自动态效果、焦点高亮、CanvasGroup继承透明度、材质/shader或排序：
这些属性未被该快照完整记录，不能只凭UiChanges=0认定视觉一致；仍需检查PNG及差值图。
结构差或像素差均不自动降低通过标准；需用具体差异原因和人工审核结论归档。

渲染与内存：非 Development Windows x64 Mono，隐藏窗口、共同1920×1080 SDR RT，VSync off、60 FPS。
生产 root Canvas 临时转 ScreenSpaceCamera；实际帧计数核验，含相同探针/相机/RT开销。
每稳态 1.5秒 UTC 窗口、外部250ms PrivateBytes/WorkingSet采样；漫画瞬态另标。
无强制 GC、无探针调用 UnloadUnusedAssets。资源枚举/JSON 在窗口结束后执行。
Private commit 和 WorkingSet 不相加；没有独立 VRAM 测量。Release纹理 byte API不完整，不作总显存。
GameObjects/Graphics枚举只统计活动对象；新增的停用缓存不会出现在该数值中，不能据此声称全部对象无增长。
textures/sprites/materials/audio/fonts是已加载资源集合；配合外部内存趋势、缓存专门测试判断生命周期。
本数据不是普通显示窗口绝对RAM；关注相同方法下 A/B 差异、资源数量、稳定性和三轮范围。
截图 ReadPixels/PNG 额外分配污染后续内存，因此视觉轮从性能比较严格排除。
最后菜单截图在所有计时/内存点之后执行；AllProcessPeak可能含截图，GameplayStepPeak排除。
PASS 证明测量完整，不等于没有任何 bug 或无限运行无泄漏。

工具自检
--------
CompileProbe.ps1 -ValidationOnly -Revision v2 可使用已有Player依赖在HarnessValidation/StaticCompile-v2静态编译；
它不是本轮canonical编译，ApplyMeasuredProbe明确拒绝 ValidationOnly manifest。
源文件变化后必须新建校准/canonical证据并重跑，不能替换旧数据后声称相同探针。
