# 角色与部件背景修正记录

日期：2026-10-22。已修复审核中确认的两处病句、部件环境判定问题，并补充星盘经历的衔接与船壳用词。

## 已完成

- [岸风短帆](D:/Game/UnityClient/Assets/_Project/Resources/Data/component_catalog_v2.csv:28)：改为“渔妇借晒网架铺开旧帆布，裁成短帆”。CSV 只有此行的 description 字段改变，125 条记录的其他字段保持一致。
- [第 02 篇异闻](D:/Game/UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs:120)：改为“擦去王徽，让每个人在原处签名”。
- [塞勒涅背景](D:/Game/UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs:171)：明确她随老船长在巨人背上度过七昼夜；巨人将石盘赠给老船长，归船后由她保管。与第 06 篇及她的专属卡背景相衔接。
- [活潮船壳](D:/Game/UnityClient/Assets/_Project/Core/Runtime/SeventhPortLoreCatalog.cs:147)：改为船匠“用活龙骨的木料制成这层船壳”，区分材料与部件。
- [适航条件解析](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/ComponentVoyageContextResolver.cs:8)：从真实会话读取当前地形，修复沿岸、海峡和开放海域条件不可达的问题；[实际装配入口](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/CoreGameplayModularShipController.cs:224) 已接入。

## 环境判定规则

| 当前环境 | 部件航道条件 |
| --- | --- |
| 海岸、岛屿 | 沿岸 |
| 海峡 | 海峡 |
| 礁石、浅水滩、海藻区 | 礁区 |
| 无上述局部地形的普通水面 | 按所选航线区分开放海域、商贸、深海 |

沿用 Core 的地形优先级：当前事件有明确地形时使用事件地形，否则读取船只当前位置。雾与风暴保留天气分类；时间与天气的数值规则未调整。已有航行及事件界面刷新会重新计算条件，因此不会锁定在出航位置。

原审核中 14 件新增部件的来历、以及基础目录另外 53 件部件的环境说明，现可匹配对应航道条件。没有修改部件基础数值、加成公式、价格或存档 ID。

## 验证

- 当前全部 Core、Content、Persistence、Presentation 程序集及环境测试编译通过。
- 隔离 Unity EditMode：**9/9 通过，0 失败**。覆盖三条航线下局部地形优先级、普通水面分类、雾/风暴保留、事件地形覆盖及恢复、真实 Sail() 移动后重新解析环境。
- [测试摘要](D:/Game/Artifacts/NarrativeTextFix20261022/Tests02/summary.json) · [完整 NUnit 结果](D:/Game/Artifacts/NarrativeTextFix20261022/Tests02/results.xml)。
- 最小测试工程只导入本轮目录、解析器、最新 Core/Content 程序集与 CSV；依赖全部美术贴图的可用性测试未纳入本次过滤范围。
- 没有生成或恢复退役预览目录，未进行画面重新截图。

## 保留的设定问题

可招募塞勒涅与传说舰长是否同一人、旧归潮舰队线索如何进一步融入主线，目前没有明确设定依据。本次保留原有姓名、身份和支线，仅修正已确认问题及星盘段落的具体衔接。

