# 角色与部件背景文本审核

审核日期：2026-10-22。初次审核确认 2 处病句，以及 1 类背景说明与运行时机制不一致的问题。

修正状态：用户随后要求修正，确认的问题现已修复，并补清星盘经历和船壳用词；生产程序集编译通过，隔离 Unity 环境测试 9/9 通过。详见[修正与验证记录](D:/Game/outputs/narrative-text-audit-20261022/fixes.md)。以下保留初次审核证据，其中原文及行号描述的是修正前状态。

## 范围与方法

- 六港 36 位可招募角色的人物背景、关联的 15 篇《第七港口志》。
- 150 件部件：125 条基础记录中，115 条使用 CSV 原文、10 条被世界观目录覆盖；另有 25 条代码定义的扩展部件。
- 核对人物档案、部件来历、异闻、专属卡背景的取值路径，交叉检查世界观目录。
- 静态结构核对：150 个部件 ID、36 个角色 ID 均唯一；各自背景正文没有重复整篇、空串或 Unicode 替换字符；章节引用全部有效；CSV 125 行均为 20 列。
- 本轮为源码和文本静态审核，未启动 Unity，未执行 EditMode/PlayMode 测试，也未重新验证实际屏幕布局。

## 确定问题

### 1. 岸风短帆把晒网架写成了帆的材料

位置：[component_catalog_v2.csv:28](D:/Game/UnityClient/Assets/_Project/Resources/Data/component_catalog_v2.csv:28)

原文：“渔妇用晒网架裁成短帆，最能接住清晨岩壁边的轻风。”

“用晒网架裁成短帆”缺少布料这一对象，按通常语义成了把架子裁成帆。建议改为：

> 渔妇借晒网架铺开旧帆布，裁成短帆，最能接住清晨岩壁边的轻风。

该 ID 为 `sail_coast_runner`，不在 10 条背景覆盖表中，是游戏实际读取的正文。

### 2. 第 02 篇异闻的“擦去”搭配错误

位置：[CrewNarrativeCatalog.cs:120](D:/Game/UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs:120)

原文：“老船长裁下一幅帆，擦去王徽的位置，让每个人签名。”

“擦去”的对象应为王徽，而非位置。建议改为：

> 老船长裁下一幅帆，擦去王徽，让每个人在原处签名。

该篇是人物及部件档案共用的《第一面自由旗》，不是仅供开发参考的文字。

### 3. 14 件新增部件的来历承诺了无法完整触发的最佳环境

背景拼接位置：[ComponentArtCatalog.cs:625](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/ComponentArtCatalog.cs:625)

这些来历正文自动追加：“它在……中最能发挥作用；错过条件时仍保留基础装配属性。”

实际航道转换见 [CoreGameplayModularShipController.cs:230](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CoreGameplayModularShipController.cs:230)：索引 0 为礁区、1 为商贸、2 及以上为深海。沿岸与海峡从未生成；正常非负索引也会覆盖默认的开放海域。索引合法性见 [CoreCardSchoolUnlocks.cs:71](D:/Game/UnityClient/Assets/_Project/Core/Runtime/CoreCardSchoolUnlocks.cs:71)。

| 无法匹配的航道 | 背景受影响的新增部件 |
| --- | --- |
| 沿岸，3 件 | 曙光风筝帆、盐沫浮壳、曙光寻针 |
| 海峡，5 件 | 潮钩导流首、海峡斜桁帆、雾钟短炮、海峡叠板、海峡回声盘 |
| 开放海域，6 件 | 日醒狮首、自由尾流长枪、横风转轴炮、风暴冠列炮、风暴方舟壁、风暴谕盘 |

这些部件的基础属性和时间、天气条件加成仍可生效；问题是航道条件永远不匹配，无法达到来历所宣称的完整最佳状态。计算依据见 [ComponentArtCatalog.cs:241](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/ComponentArtCatalog.cs:241)。

同一问题还影响基础目录 53 件部件的“装配效果”页，共 67 件。这里应先统一实际航道分类与部件条件，再调整说明；仅修改背景措辞不能解决条件无法触发的问题。

## 设定疑点与可选润色

以下没有足够证据认定为硬错误，不应直接覆盖原设定。

| 项目 | 证据与疑点 | 建议 |
| --- | --- | --- |
| 海巨人经历的主体 | [第 06 篇，124 行](D:/Game/UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs:124) 写老船长在巨人背上走七昼夜并获赠石质星盘；[塞勒涅背景，171 行](D:/Game/UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs:171) 也写七昼夜并获赠石盘。 | 补充同行、转赠或两次经历的关系，避免读者以为同一段故事换了主角。 |
| 塞勒涅的身份 | 可招募的“月潮占星师 塞勒涅”与[潮汐镇魂号舰长，85 行](D:/Game/UnityClient/Assets/_Project/Core/Runtime/CoreLegendaryWarships.cs:85) 同名，目前没有说明是否同一人。 | 若为同一人，补身份关联；若不同，区分称呼。 |
| 旧线索与新异闻缺少衔接 | [走私客针首，CSV 第 9 行](D:/Game/UnityClient/Assets/_Project/Resources/Data/component_catalog_v2.csv:9) 讲无旗盐船、盐人，却关联第 04 篇《与神明谈价》；[墓潮骨刺，第 19 行](D:/Game/UnityClient/Assets/_Project/Resources/Data/component_catalog_v2.csv:19) 讲石港升起，却关联第 12 篇《仍在寻找的人》。现行章节未交代这些线索。 | 检查这些内容是否保留为支线；若保留，增加明确衔接，或重新选择关联章节。题材不同本身不能证明矛盾。 |
| 活潮船壳与活龙骨 | [覆盖正文，147 行](D:/Game/UnityClient/Assets/_Project/Core/Runtime/SeventhPortLoreCatalog.cs:147) 将“活潮船壳”写作“这层活龙骨”。 | 若不是有意借代，可改为“这层活潮船壳”，或说明船壳取材于活龙骨。 |

## 未发现的问题及排除项

- 36 篇人物背景未发现可确认的人名、港口或性别代词错配。
- 从源码看，档案按人物或部件 ID 读取对应正文，未发现串对象、开发备注泄漏或正文被主动删字的路径。实际 UI 布局未在本轮运行验证。
- 艾达的“八章旧病历”不应直接改成“八张”：专属卡同样写“第八章病历”，可能指分章的病历册。
- 赫莲的断链经历与第 09 篇更贴合，但目录明确同时关联第 03、09 篇，因此不能据此认定程序绑定错误。
- CSV 第 52 行重复“最后一发空包弹”、第 92 与 97 行关于自愈的旧描述会被运行时覆盖，未计为玩家当前可见错误。

