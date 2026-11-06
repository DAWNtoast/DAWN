# 美术、部件与界面整合发布

版本 0.3.20261025.1。使用 Release.ps1 的 Plan、Snapshot、EditMode、PlayMode、Verify、Windows、Smoke、Package、Seal、Cleanup 阶段；参数 Revision 默认01，修复后须换新 revision，不能覆盖已有证据。

主工程输入冻结到 Artifacts/IntegratedArtRelease20261024/FrozenSourceXX，发布输出为 Builds/第七港口-美术与界面整合20261025-rXX。使用现有 tmp/shipyard-detail-clean-20261011 预热工程；占用时不得强行启动或终止其他任务进程。

EditMode/PlayMode必须为相同快照的完整套件，不带过滤。默认不启用 CaptureVisuals：该开关还会解锁整战役专项。美术截图使用单独的过滤PlayMode运行加 CaptureVisuals，保存到独立 VisualXX 证据目录，不能替代全量回归。

Smoke通过后运行 VerifyContent.ps1 -Revision XX。它核对实际Player输出的55份美术和31件部件、冻结源码与原稿的SHA256，以及像素一致的41图标图集，并将版本说明与内容清单放进Windows包。随后再Package、Seal，确保这些说明也被压缩和逐文件核验。

Seal之后运行Cleanup。仅在无进程占用且没有目录链接时清理共用验证工程Temp；Library是其他发布/诊断脚本的共用预热缓存，保留并记录原因。主工程Assets/Packages/ProjectSettings清理前后哈希必须不变。冻结源码、必要日志、截图、最终EXE/ZIP全部保留。

本任务不修改卡牌规则定义，不调用图像生成API，不触碰退役UIPreviewsForMe，不提交/推送Git。