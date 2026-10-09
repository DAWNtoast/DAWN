# UITreasureMap

可编辑藏宝图状态预览。各场景保留基础藏宝室的背景、展板和布局，只使用独立样本更新藏宝工作台与藏图册；样本不读取或写入玩家存档。

状态：
- MapUnopened：等级 2，尚无图碎片或藏宝图，显示收集条件。
- FragmentsReady：已收集 4 个图碎片，可拼合首张藏宝图。
- MapDecoded：已持有并装备残旧藏宝图，展示种子、线索、战力与出航消耗规则。
- RewardClaim：永久完成回执；奖励在返港结算中保存，此页不能重复领取。

定向同步入口：`ShatteredSail.Editor.GameplayPreviewSceneBuilder.SyncTreasureStatesFromCommandLine`。执行前按场景路径备份到 `Artifacts/TreasureAdventure20260930/preview-backup`，不会重建其他功能页或替换藏宝室的原始 Canvas。
