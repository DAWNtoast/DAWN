# 六港区域化与「第七码头」内容扩展

## 使用与预览
在 Unity 中打开 `Assets/_Project/Scenes/UIPreviews/UI_Tavern.unity`、`UI_ComponentShop.unity` 或 `UI_Maintenance.unity`，选中层级里的 `UI Preview` 对象，在 Inspector 切换 `Preview Port`。不进入 Play Mode 也能查看布局。运行时按照实际所在港口自动加载。
- 人物：把鼠标移到入座船员或姓名上，点击打开羊皮纸个人档案；可以签约或切换同职业代表。
- 部件：点击模型或前方名牌进入羊皮纸介绍，显示专精、真实装配数值、部件来历；「阅读关联篇章」切换到所属短篇。
- 后勤：中央船只下水，左侧六项检查，右侧采购单可逐项调整数量并核对费用和舱容。

## 本轮落地范围
- 6 个港口 × 酒馆／部件商店／港口后勤，共 18 张重新绘制的场景背景。建筑、道具与空间材质不同，保留入口与陈列区的共同布局，不使用同图换色。
- 36 位具名船员：6 港口 × 6 职业，具有独立身份、外貌说明、招募状态、人物背景与地域肖像。酒馆人物在背景中就座；档案使用新绘羊皮纸胸像图。
- 5 类部件各 25 件，共 125 件；每类每个稀有度各 5 件。各具名称、数值取舍、专精方向和来历，保留地区限售、月度库存规则。
- 15 篇连贯短篇《航海异闻：第七码头》，通过六港人物与部件物证串联。女巫、海妖、海巨人、归潮舰队共同构成主线。
- 兼容原有部件 ID 与船员职业存档；地区招募不会误登记为灰鸥港人物；派遣人员在出发时确定，不被后续招募改名。

## 六港视觉主题
| 港口 | 场景辨识 |
| --- | --- |
| 断锚港 | 废船肋骨、锈链、回收旧木、失事遗物 |
| 白贝港 | 贝壳白拱、珊瑚石、珍珠、明亮的海岛建筑 |
| 铜钟港 | 铜钟、齿轮、熔炉、红砖与机械工坊 |
| 盐冠港 | 盐白石材、王冠装饰、商会旗帜、宫廷仓廊 |
| 暮潮港 | 黑色遗迹、月轮、蓝紫灯火、深海神秘器物 |
| 灰鸥港 | 白墙杉木、渔网、浮标、海鸥与日常渔港生活 |

## 资源与来源
本轮使用内置 ImageGen 生成区域场景和人物图集，再由运行时资源目录加载。没有使用程序换色代替区域重绘。
- 场景：`UnityClient/Assets/_Project/Resources/PortFacilities/Regional/`
- 人物：`UnityClient/Assets/_Project/Resources/PortArt/CrewRegional/crew_*_portraits_v2.png`
- 场景提示词：[regional_art_prompts.json](regional_art_prompts.json)
- 人物提示词：[regional_portrait_prompts.json](regional_portrait_prompts.json)
- 原始生成文件、最终路径：[regional_art_manifest.json](regional_art_manifest.json)
- 未通过验收的棋盘格底试作已移至 `outputs/narrative/rejected-art/`，不再位于游戏资源目录，保留可恢复副本。

## 数值与美术边界
125 件部件的玩法数据、名称和背景各自独立；实物模型与船体拼装图仍复用现有的「5 类 × 5 稀有度」25 套造型，并非 125 套新模型。稀有度环绕光和类型支架保持。
同职业地区角色目前共享已实现的职业能力，人物身份、外貌和背景不同；不会把尚未实现的地域特技写成已生效技能。故事属于世界背景，并非本轮新增的可接取任务链。

## 验收入口
- 30 张实际 Unity 场景／档案截图：[区域场景画廊](../../UnityClient/Artifacts/RegionalPortsV1/index.html)
- 资源完整性与截图记录：[report.json](../../UnityClient/Artifacts/RegionalPortsV1/report.json)
- 短篇及人物索引：[seventh_berth_anthology.md](seventh_berth_anthology.md)
- 125 部件设定：[component_anthology_125.md](component_anthology_125.md)
- 测试输出位于 `UnityClient/TestResults-RegionalPorts-EditMode.xml` 与 `UnityClient/TestResults-RegionalPorts-PlayMode.xml`。
最终自动化回归：EditMode 59/59 通过，PlayMode 18/18 通过，合计 77/77（2026-09-07）。覆盖区域资源与标题、采购牌布局、125 件部件的数量／真实数值／故事关联、换船后装配保留、跨月报价过期、区域招募独立、派遣身份快照及旧存档兼容。

测试与截图在隔离副本和独立存档命名空间执行，不覆盖玩家正在打开的场景或个人进度。
