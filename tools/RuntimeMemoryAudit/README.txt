运行时内存探针 · 2026-10-14

工具仅复制进 D:/Game/tmp 下专门的诊断工程；不可放正式 UnityClient/Assets。
PrepareProbe.ps1 -Project D:/Game/tmp/runtime-memory-probe-20261014
Unity构建方法：ShatteredSail.Editor.RuntimeMemoryAuditBuilder.BuildFromCommandLine
参数：-runtimeMemoryBuildOutput D:/Game/Artifacts/RuntimeMemoryOptimization20261014/Baseline/Windows/MemoryAudit.exe
由协调代理串行启动Unity；不得与其他Unity测试/采样并发。

生产公司/产品先备份，构建器使用 ShatteredSailAudit / RuntimeMemoryOptimization20261014；
probe验证实际LocalLow路径后才写独立PlayerPrefs；journal/checkpoints位于本轮报告memory-details内。
基线与优化诊断包必须采用同一probe DLL（RunTrials强制SHA256一致）；正式交付移除诊断文件并核Managed程序集。
构建前须对生产 Assets/Packages/ProjectSettings 建立准确 source manifest，再逐文件复制/核对隔离工程；
不可从旧诊断工程或运行中的测试工程整目录覆盖生产，也不可把旧个人预览混入源清单。
本轮所有输出固定在 Artifacts/RuntimeMemoryOptimization20261014，脚本不是通用安装器。
诊断DLL后处理：ApplyMeasuredProbe.ps1 -BuildName Optimized01；只替换同一测量probe，并记录全部文件前后哈希。
统一probe的Release编译参数、依赖哈希及原始DLL保存在 ProbeTools/MeasuredProbe；
基线完整一致性证据为该目录 baseline-production-file-parity.json、baseline-probe-update.json；
优化证据位于 Optimized01/DiagnosticProbePostprocess。此操作仅用于诊断，绝不应用到正式发布包。

修复了旧工具3处过时入口：Dispatch已退役，改测仓库Storage子页，仍57点；
人物详情图像是Tavern Dossier Parchment；详情返回共用Return To Port Hub（第一次返回列表）。
fixture显式设置WorldEconomyScope，资产参数全通过现役UI或记录的测试反射进入。
首次校准到40点发现旧12秒动画期限失效：现在漫画4页、每页自动4秒。
本探针记录第一张实际漫画后，逐页显示至少0.25秒并点击生产Battle Ram Comic Advance，
不改变游戏计时器，也不依赖隐藏窗口是否收到失焦事件。原失败CalibrationBaseline01保留。

渲染：Hidden窗口，共同1920x1080 SDR RT，生产root Canvas临时改ScreenSpaceCamera，
手动SubmitRenderRequest；endCameraRendering（Built-in则onPostRender）计数。稳态每点至少30帧，全程至少100帧。
图形API、GPU、Unity版本及完整RT清单入报告。这个受控渲染数值包含额外RT/相机开销，不能称正常显示窗口绝对RAM。
生产相机禁用避免不同前台状态多渲染；生产图片/质量导入设置不变。游戏音频仍按生产路径运行。

57点包括：菜单/成就/港口3、9设施+仓库Storage10、设施20循环的4检查点、六港36人物详情与配件店12、
9航行天气/时间、4冲撞的海战/漫画/接舷12、回菜单与3次场景往返7。无强制GC或探针触发卸载。
每稳态先等待生产cleanup/fade，然后1.5s UTC采样窗，再记录资源/写JSON；漫画为transient，保留临近OS样本，不伪称稳态。
最后菜单截图在所有测量窗之后保存并验证非空。AllProcessPeak含截图/探针开销；GameplayStepPeak排除inventory与最后截图。

Measure.ps1 -Executable <exe> -OutputDirectory <新的本轮目录>
外部250ms PrivateBytes/WorkingSet，UTCTimestamp直接对齐probe窗口，不卡OS计数到Unity不可靠接口。
Analyze.ps1验证57点/完成计数/独立身份/非Development/渲染、稳定窗采样数量，输出checkpoints.csv和summary.json。
Runtime纹理byte为release不完整估计，只列明availability，不作总内存。Private与WorkingSet不可相加；未量独立显存。

RunTrials.ps1 -BaselineExecutable <before exe> -OptimizedExecutable <after exe> -TrialsRoot <新目录>
顺序 B1/O1/B2/O2/B3/O3（前后版本交替，连续运行），每版3轮全新Player、同身份串行。
Compare.ps1每检查点计算3轮中位数，报告内存正负变化，不把OS分配器保留直接称泄漏。
完成序列的PASS只证明测量有效；是否优化有效需结合同态物体数/资产名称/三轮趋势人工判断。

未经新的明确授权，绝不生成/更新 UIPreviewsForMe；不调用任何外部图像API。

干净 Git 克隆的前提与重新生成步骤
--------------------------------
Git 只保存源脚本和本说明；Artifacts、诊断 Player、MeasuredProbe DLL/PDB、rsp、原始测量均不随 Git 分发。
因此 ApplyMeasuredProbe.ps1 不能在只有 tools 源码的干净克隆中直接运行；它依赖先构建的基线 Player 和下列编译证据。
本轮固定 Windows 路径 D:/Game，以及 Unity 6000.3.23f1 (09d2ecc7fb28)。换机器/SDK/路径得到的新哈希不视为本轮同一个二进制。
本轮原始基线构建的 bundleVersion 诊断标记为 0.3.20261014-memory-audit，优化构建为 0.3.20261014；
归档构建器保存后一值。若需重建完全相同的旧基线 Player 元数据，只在该临时诊断构建时恢复旧标记，
随后恢复归档构建器；二者的公司/产品隔离身份一致，正式六轮已使用字节完全相同的运行时 probe DLL。

1. 将 tools/RuntimeMemoryAudit 的白名单源文件逐字节复制到本轮 ProbeTools 目录。
2. 在全新、独占的 tmp/runtime-memory-probe-20261014 中准备准确 Assets/Packages/ProjectSettings：
   基线来自 a239d860b539a201b4c67191f2e746501198029d；优化来自要比较的明确源码版本。
   对三目录全部文件建立 SHA256 清单，复制后逐文件核验，记录生产源与诊断 clone 一致性；不可复制 ForMe 内容。
   确认上述临时工程是此次创建且完全归此次诊断使用，再写 .runtime-memory-probe-owned.json 所有权标记。
   运行 PrepareProbe.ps1，Unity/Player 全部停止后运行 Build.ps1 -Name Baseline。
   固化基线构建；之后将优化版本准确同步到同一诊断工程、保留相同 probe，运行 Build.ps1 -Name Optimized01。
3. 统一 probe 由实际基线 Player Managed 依赖编译，而非借用 Editor 程序集。下列 PowerShell 对应本轮真实参数；
   仅在 MeasuredProbe 尚不存在的新审计中执行，绝不覆盖已有证据：

$ErrorActionPreference = 'Stop'
$audit = 'D:\Game\Artifacts\RuntimeMemoryOptimization20261014'
$probeTools = Join-Path $audit 'ProbeTools'
$measured = Join-Path $probeTools 'MeasuredProbe'
$managed = Join-Path $audit 'Baseline\Windows\MemoryAudit_Data\Managed'
if (Test-Path -LiteralPath $measured) { throw 'Preserve existing measured probe evidence.' }
New-Item -ItemType Directory -Path $measured | Out-Null
$probe = 'ShatteredSail.RuntimeMemoryAudit.dll'
$source = Join-Path $probeTools 'RuntimeMemoryPlayerProbe.cs'
$refs = @(Get-ChildItem -LiteralPath $managed -Filter '*.dll' -File | Where-Object Name -ne $probe | Sort-Object Name)
$arguments = @('-target:library','-nostdlib+','-langversion:9.0','-deterministic+','-optimize+','-debug:portable','-nologo','-define:UNITY_6000_3,UNITY_STANDALONE_WIN,UNITY_STANDALONE')
$arguments += @($refs | ForEach-Object { '-r:"' + $_.FullName + '"' })
$arguments += '-out:"' + (Join-Path $measured $probe) + '"'
$arguments += '"' + $source + '"'
$rsp = Join-Path $measured 'Probe.rsp'
$arguments | Set-Content -LiteralPath $rsp -Encoding utf8
& 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Data\NetCoreRuntime\dotnet.exe' 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Data\DotNetSdkRoslyn\csc.dll' ('@' + $rsp) *> (Join-Path $measured 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Probe compilation failed; retain log.' }
[pscustomobject]@{
    Scope = 'Diagnostic probe only; exact baseline nonDevelopment Player Managed references'
    SourceSHA256 = (Get-FileHash -LiteralPath $source).Hash
    ProbeSHA256 = (Get-FileHash -LiteralPath (Join-Path $measured $probe)).Hash
    ReferencedAssemblies = @($refs | ForEach-Object { [pscustomobject]@{ Name=$_.Name; SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash } })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $measured 'compile-parity.json') -Encoding utf8

4. 本轮实测源码 SHA256 为 5CC873AD7431E279A0AA03E58D45AE6007C5F30C4EB4A33FD496937054C5EF71，
   canonical DLL 为 76EFEF3390FB528424A0111F38CD7097D1C2BB692EC127255C59953F13AA7301。
   ApplyMeasuredProbe.ps1 固定校验该 DLL、源文件以及 Bootstrap 初始化入口；依次对 Baseline、Optimized01 执行。
   它保留原 DLL 和全构建文件清单，再证实只有诊断 DLL 改变。若校验不符，先检查源码/编译器/依赖差异，
   不要关闭校验或把新二进制冒充本轮已测版本。新版本须建立新的同 DLL 前后测试证据。
5. 运行 RunTrials.ps1，复现 B1/O1/B2/O2/B3/O3。不要把任何诊断程序集或其改包操作带入正式发布包。
