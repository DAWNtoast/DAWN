# 船坞落地美术记录

日期：2026-09-07

2026-10-13 清理说明：以下为当时的制作记录。旧 `shipyard_grounded_v2.png` 与输入背景 `shipyard_background.png` 后来移入 `Editor/ArchivedArt/PortFacilities/`，其归档副本已按 B01 经审核清理。当前船坞使用 `UnityClient/Assets/_Project/Resources/PortFacilities/shipyard_water_gallery_v3.png`；下文旧路径与提示词保留为来源说明，不代表旧资源仍在项目中。详见[旧素材清理记录](../../Artifacts/UnusedAssetAudit20261013/index.html)。

模式：内置 imagegen / compositing，未使用 CLI 或程序修改图片。

当时接入资源（已退役）：`D:/Game/UnityClient/Assets/_Project/Resources/PortFacilities/shipyard_grounded_v2.png`

原始生成文件保留在：`C:/Users/Admin/.codex/generated_images/01a050de-d083-7010-bcc7-de18e62c64ab/exec-2f73dfda-f222-4f89-a8dd-cc05580aaff5.png`

输入图：现有 `PortFacilities/shipyard_background.png`（房屋/船台布局）及 `PortArt/ship_roster_sheet.png`（船型身份参考）。

集成：三艘非模块船与船台合绘，建立真实承托、光照及船台前栏遮挡。后排左侧留空，运行时继续渲染可换装的轻帆船。交互、价格、购买和旗舰切换仍由现有按钮处理；不是移除购船功能。

弃用尝试：`exec-63e395e3-33af-48f4-bb9f-6d1de73955ba.png` 是一次透明船只提取尝试，但返回 RGB 棋盘背景，并无真实 alpha；未接入项目。原文件保留，没有把棋盘背景冒充透明资源。

## 最终生成提示词

Use case: compositing. Asset type: finished opaque background for a Unity pirate shipyard shop. Image 1 EDIT TARGET: the interior with four wooden ship cradles. Image 2 SHIP IDENTITY REFERENCE ONLY: four ships.
Keep the entire room, camera, floor, horizon and exact four cradle positions from Image 1. Place THREE ships physically supported IN three existing cradles, matching perspective, scale, lighting, hull contact shadows and wood-beam occlusion.
Leave the REAR LEFT cradle completely EMPTY for a runtime-customized player ship. Rear RIGHT cradle contains Image 2's top-right RED/BLACK pirate galleon. FRONT LEFT cradle contains Image 2's bottom-left BLUE/GOLD royal galleon. FRONT RIGHT cradle contains Image 2's bottom-right PURPLE/TEAL mystical ship. These three dry-docked ships have NO sea foam, no water.
Their keels must rest on cradle crossbeams; the NEAR wooden cradle rails overlap the lower hull naturally. Each ship is a modest shipyard display, scaled to fit its cradle fully, not a huge ship towering out of frame. Keep masts entirely below the top 20% of the picture, so a menu header can sit there. Entire ships visible, front ships larger than rear ship, all angled in the same depth perspective as their cradles.
No floating ships or dark card backgrounds. No new frames, no UI, no text, no additional ships, no people. Preserve rear left empty cradle and its supporting timbers. Preserve detailed warm-lantern pixel-painterly style of original room. Wide landscape image.
