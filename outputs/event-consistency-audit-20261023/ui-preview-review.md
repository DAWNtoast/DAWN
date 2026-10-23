# 代表性生产 UI 插画检查

日期：2026-10-23。通过新鲜生产 Core / Presentation 程序集、实际事件工厂及现役 `CoreGameplayDemoController` 创建临时预览场景，使用相机渲染 1920×1080 截图。没有保存场景、读取玩家存档或调用退役预览入口。

结果：**7 屏均完成实际纹理/标记验证，并逐张打开目检。**

| 截图 | 实际内容 | 结果 |
| --- | --- | --- |
| [沉船工坊主机舱](../../Artifacts/EventConsistencyAudit20261023/Previews/core_weathered_event.png) | 真实残旧藏宝航次中的核心事件 | `core_weathered` 正确；水下工坊、吊装设备和潜水钟主体可见 |
| [商盟封存总仓](../../Artifacts/EventConsistencyAudit20261023/Previews/core_gilded_event.png) | 真实鎏金藏宝航次中的核心事件 | `core_gilded` 正确；封存仓库、箱柜和通道可见 |
| [三相月蚀圣所](../../Artifacts/EventConsistencyAudit20261023/Previews/core_moonlit_event.png) | 真实月蚀藏宝航次中的核心事件 | `core_moonlit` 正确；三处仪式结构、光束和月蚀主体可见 |
| [顺风捎来的讯息](../../Artifacts/EventConsistencyAudit20261023/Previews/tutorial_message_ship_event.png) | `RandomEvent(true)` 实际工厂对象 | `tutorial_message_ship` 正确；两船接近并交接讯息 |
| [水手发现漂流信筒](../../Artifacts/EventConsistencyAudit20261023/Previews/tutorial_message_tube_event.png) | `RandomEvent(false)` 实际工厂对象 | `tutorial_message_tube` 正确；船侧水手、船钩和信筒可见 |
| [藏宝核心海图](../../Artifacts/EventConsistencyAudit20261023/Previews/core_weathered_map.png) | 真实残旧藏宝图上的核心节点 | 生产地图显示“X”，没有错误添加卡图 |
| [断桅求援海图](../../Artifacts/EventConsistencyAudit20261023/Previews/reef_broken_mast_map.png) | 普通航行事件的实际地图标记渲染 | `reef_broken_mast` 正确，地图标记使用完整 UV |

五张弹窗都从实际 `CoreEncounter` 解析图片。三个核心 ID 相同但图片互不串用；两个讯息 ID 相同且选项一致，图片仍随实际文本分支切换。新图不经过透明舰船专用遮罩，也没有额外海水层。界面既有 4:3 框对 1536×1024 来源图进行 cover 显示：UV 横向约 `0.05599–0.94401`，纵向近完整，关键主体未裁断。

海图保留生产规则：藏宝核心专用“X”标记跳过卡图装饰；两个随机教程事件不属于海图节点。没有为了截图把它们改成地图事件。普通事件图标仍是小型标记；截图与 `report.json` 同时证明它使用了新来源纹理。

截图中的不可选项来自真实预览航次尚未取得工具、信用、共鸣或容量不足的状态，未改动选项可用性。四选项核心使用既有滚动列表，截图只记录当前滚动位置。本次不扩大为事件面板布局改造。

证据：[机器检查 report.json](../../Artifacts/EventConsistencyAudit20261023/Previews/report.json)、[Editor 日志](../../Artifacts/EventConsistencyAudit20261023/Previews/editor.log)。Editor 以退出码 0 结束。脚本：`tools/EventConsistencyAudit20261023/Preview.ps1` 与 `Capture.cs`。只覆盖上述代表界面，不据此宣称其余全部事件都已截图验收。
