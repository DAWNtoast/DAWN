# 黑雾走私航线像素风美术资源

> 归档说明：这是早期 10 事件航线方案，已被 `outputs/routes_v2` 的三条 20 事件航线取代，不参与当前 Demo、数值配置或航线选择。保留本目录仅供美术参考。

本目录包含“黑雾走私航线”和 10 个事件对应的像素风美术资源。

## 文件清单

- `black_fog_route_map.png`
  - 内容：黑雾海域航线图，包含礁群、走私藏湾、黑市浮标、帝国沉舰、终点缉私舰等元素。
  - 建议用途：航线选择界面、海图背景、章节入口图。
  - 尺寸：1672 x 941。

- `black_fog_event_sheet.png`
  - 内容：10 个事件插画。
  - 建议用途：事件弹窗插画、事件卡面、剧情节点预览。
  - 建议切分：5 x 2。
  - 尺寸：1774 x 887。

- `black_fog_node_icon_sheet.png`
  - 内容：16 个航线节点、状态和奖励图标。
  - 建议用途：海图节点、状态提示、奖励入口、路线预览。
  - 建议切分：4 x 4。
  - 尺寸：1254 x 1254。

## 事件插画板映射

`black_fog_event_sheet.png` 采用 5 x 2 排布，索引从 0 开始。

| row | col | event_id | 事件名 |
|---:|---:|---|---|
| 0 | 0 | event_smuggle_signal | 暗灯信号 |
| 0 | 1 | event_floating_cargo | 漂浮货箱 |
| 0 | 2 | event_navy_inspection | 海军临检 |
| 0 | 3 | event_black_market_buoy | 黑市浮标 |
| 0 | 4 | event_sick_stowaway | 病弱偷渡客 |
| 1 | 0 | event_fog_reef | 黑雾礁群 |
| 1 | 1 | event_cursed_chest | 沉银宝箱 |
| 1 | 2 | event_mutiny_whisper | 低语叛乱 |
| 1 | 3 | event_smuggler_hideout | 走私者藏湾 |
| 1 | 4 | event_imperial_wreck | 帝国沉舰 |

## 节点图标板映射

`black_fog_node_icon_sheet.png` 采用 4 x 4 排布，索引从 0 开始。

| row | col | icon_id | 用途 |
|---:|---:|---|---|
| 0 | 0 | node_normal_battle | 普通海战 |
| 0 | 1 | node_elite_navy | 精英海军战 |
| 0 | 2 | node_boss_warship | 首领缉私舰 |
| 0 | 3 | node_event | 通用事件 |
| 1 | 0 | node_black_market | 黑市浮标 |
| 1 | 1 | node_smuggler_cove | 走私藏湾 |
| 1 | 2 | node_floating_cargo | 漂浮货箱 |
| 1 | 3 | node_navy_inspection | 海军临检 |
| 2 | 0 | node_fog_reef | 黑雾礁群 |
| 2 | 1 | node_cursed_chest | 诅咒宝箱 |
| 2 | 2 | node_stowaway_map | 偷渡客海图 |
| 2 | 3 | node_mutiny_whisper | 低语叛乱 |
| 3 | 0 | node_imperial_wreck | 帝国沉舰 |
| 3 | 1 | status_pursuit | 海军追击 |
| 3 | 2 | action_safe_return | 安全返航 |
| 3 | 3 | node_hidden_route | 隐藏航线 |

## 风格说明

- 统一像素风，适配前面生成的船只、船员、卡牌和资源图标。
- 图中尽量不包含文字、数字和 UI 标签，方便程序层后续接入本地化文本。
- 主色调：深海军蓝、黑绿雾、黄铜金、帆布米白、暗红危险色、青绿色诅咒光。

