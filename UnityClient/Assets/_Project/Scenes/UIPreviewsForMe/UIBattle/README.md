# UIBattle 战斗状态预览

这些场景都复制自 `../UI_Battle.unity`，用于在 Unity 中分别调整海战中不同的舰船相对位置、朝向和接舷提示。原始的 `UI_Battle.unity` 保持不变。

| 场景 | 展示内容 |
| --- | --- |
| `UIBattle_BowToBow` | 船头对船头，双方正面相向，可用于撞角和正面火力布局 |
| `UIBattle_BowToBroadside` | 船头切入敌舰侧舷，展示侧舷射击角度 |
| `UIBattle_Parallel` | 平行航行，双方保持并行距离并以侧舷交火 |
| `UIBattle_BoardingHook` | 接舷·钩索，侧舷贴近后以钩索固定 |
| `UIBattle_BoardingRam` | 接舷·撞角，船头正面对撞后进入登船阶段 |
| `UIBattle_BoardingLadder` | 接舷·舷梯，两船并行后放下舷梯 |

每个场景都保留完整的战斗 HUD、船体状态、意图提示、手牌和背景图。`Battle Alignment` 标签和战斗意图说明用于帮助识别该预览的状态，均可直接在 Hierarchy/Inspector 中继续调整。
