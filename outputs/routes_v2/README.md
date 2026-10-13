# 航线、事件与共享美术资料

本目录保留三条常规航线和藏宝图海域的源配置、美术图集及映射。它们是 Unity 内容维护所需的共享资料。

## 2026-10-13 旧素材清理

经审核按 I03 清理以下 6 张第一版旧图，保留对应 `map_v2`、`sheet_v2`、`expansion_v3`、`unique_v4` 图及全部 CSV。

| 航线 | 已清理旧图 |
| --- | --- |
| 黄金潮汐商路 | `golden_tide_route_map.png`、`golden_tide_event_sheet.png` |
| 月潮遗迹海 | `moon_tide_route_map.png`、`moon_tide_event_sheet.png` |
| 碎帆礁海 | `shattered_sail_route_map.png`、`shattered_sail_event_sheet.png` |

已经退役的 `outputs/route_event_assets/` 黑雾航线素材包及旧说明、映射按 I01 清理。本目录的 `treasure_hunt_route_map.png` 和 `treasure_hunt_event_sheet.png` 仍保留供源表与生成器使用；A03 仅清理 Unity 资源目录中的旧寻宝路线底图副本。详见[旧素材清理记录](../../Artifacts/UnusedAssetAudit20261013/index.html)。

## 主要文件

| 文件 | 内容 |
| --- | --- |
| `route_config.csv` | 三条常规航线的源配置 |
| `event_point_config.csv` | 固定、随机事件模板及结构化选项 |
| `event_chain_config.csv` | 事件链阶段、解锁条件与结局 |
| `art_manifest.csv` | 航线地图、事件图集与格子映射 |
| `../balance_configs/treasure_map_config.csv` | 藏宝图品级源配置 |
| `../balance_configs/treasure_event_config.csv` | 藏宝图事件源配置 |

三条航线为碎帆礁海、黄金潮汐商路、月潮遗迹海。图集文件使用 `shattered_sail`、`golden_tide`、`moon_tide` 前缀。`grid_row`、`grid_col` 从 0 开始，读取图集时应同时使用各自的行列数。

## 与客户端的关系

Unity 编辑器的 `LegacyContentValidator` 仍检查本目录和 `balance_configs` 中的必要 CSV。运行时航线数值、地图规则、船只和船员目录以 Unity 的 Core 与内容代码为准；这些源表与旧模拟报告不能替代当前运行规则。

正式游戏使用的美术已经位于 Unity 资源目录中，`LegacyDemoArtCatalog` 等保留名称仍是有效接口。不能仅依据 `Legacy` 或 `Demo` 字样删除资源、场景或类。

## 配置维护

`work/cargo_config_builder` 中保留 CSV 生成、检查及模拟工具。运行会改写配置的历史生成器前，应先核对其字段与当前内容目录；生成器的存在不代表应在每次发布时重建全部表格。

事件选项应维护结构化条件和效果，并同步核对玩家看到的文字。新增或调整事件后，在 Unity 中验证出现条件、可支付性、离开路径、收益记录和重复触发行为。

当前完整规则见 [游戏策划案](../../游戏策划案.md)。
