# 接舷静态特殊船员验证

2026-10-27 的最终要求：双方角色始终静止，我方按已参战特殊船员显示身份；没有特殊船员时回退原普通水手组。仅保留学校出牌特效。原学校预览 `outputs/boarding-school-fx-20261026/动画预览.html` 内容不变。

最新验收在 `outputs/boarding-actors-20261026/static-specialists`：242 项 EditMode、13 项 PlayMode 全通过。两张 `play1/Artifacts/Static_SpecialCrew_fire.png` / `Static_SpecialCrew_thunder.png` 展示三个真实特殊身份与原学校特效同屏。逐帧回归比较角色网格和身体/队伍位姿，覆盖六系、辅助牌、普通近战、敌方命中、延迟伤害、暂停和结算淡出。

复用 `tmp/shipyard-assembly-20261017` 预热 Unity 工程，不复制 Library。Prepare 确认无租约/进程，备份差异并记录 journal。默认 EvidenceSet 为 `static-specialists`；复跑须指定未使用的新名字。`static-*` 自动跳过历史动作 Capture helper，且禁止动作 Capture。

```powershell
./tools/BoardingActors20261026/Validate.ps1 -Stage Prepare -EvidenceSet static-rerun
./tools/BoardingActors20261026/Validate.ps1 -Stage Test -EvidenceSet static-rerun -Run edit1 -TestFilter 'BoardingActorGraphicTests;BoardingPartyPresentationTests;BoardingBattleArtCatalogTests;VoyageArtCacheReleaseTests;BattleResolutionMotionTests;BattleSchoolAttackGraphicTests;BattleSchoolCastPresentationTests'
./tools/BoardingActors20261026/Validate.ps1 -Stage Test -EvidenceSet static-rerun -Run play1 -TestPlatform PlayMode -TestFilter 'BoardingStatic_;BoardingCinematic_'
./tools/BoardingActors20261026/Validate.ps1 -Stage Cleanup -EvidenceSet static-rerun
```

每轮 Unity 完成后才能启动下一阶段。PlayMode 保存真实游戏静态 PNG；不生成新角色动作预览。Cleanup 核对绝对路径、链接和进程，恢复租用源码、清除本轮新增文件与 Temp；保留 SmallTextAudit、WindowFit、ShipyardAssembly 使用的共享 Library，并核验主工程清理前后哈希。源码变动用 `Sync -Run 唯一名称` 保留输入记录。

`validation`、`card-actions`、`card-actions-final` 及 Capture/BuildPreview/VerifyPreview/CompareStages/ArchiveFrames 为已取消角色动画方案的历史证据与工具，不代表最终玩法。