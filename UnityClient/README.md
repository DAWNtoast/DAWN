# 第七港口 Unity Client

Unity 6.3 LTS / Universal 2D client for 《第七港口》.

## Source of truth

- Gameplay rules: `../游戏策划案.md`
- Balance and route data: `../outputs/**/*.csv`
- Unity-generated assets: `Assets/_Project/Generated` (created by editor tooling; do not hand-edit)

The Unity Core rules, runtime content catalogs and accepted scene tests define current behavior. Shared CSV files remain content inputs and historical balance references; they do not override newer runtime definitions. The current design baseline is dated 2026-10-09.

## Project layout

- `Assets/_Project/Core`: deterministic gameplay rules with no `UnityEngine` dependency.
- `Assets/_Project/Content`: runtime content contracts and stable content identifiers.
- `Assets/_Project/Persistence`: save schema and migrations.
- `Assets/_Project/Presentation`: Unity scenes, UI, rendering, and input adapters.
- `Assets/_Project/Editor`: importers, validators, and build automation.
- `Assets/_Project/Tests`: EditMode and PlayMode tests.

## Validation

From the Unity menu, run `Shattered Sail > Content > Validate Legacy Sources`.

Batch mode:

```powershell
& 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'D:\Game\UnityClient' `
  -executeMethod ShatteredSail.Editor.LegacyContentValidator.ValidateFromCommandLine `
  -logFile 'D:\Game\UnityClient\Logs\content-validation.log' `
  -quit
```

EditMode tests (the Unity Test Runner exits automatically, so do not add `-quit`):

```powershell
& 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'D:\Game\UnityClient' `
  -runTests -testPlatform EditMode `
  -testResults 'D:\Game\UnityClient\TestResults-EditMode.xml' `
  -logFile 'D:\Game\UnityClient\Logs\editmode-tests.log'
```

## Legacy tutorial voyage scene (developer test only)

The original vertical slice is retained for developer testing. Its older lesson
sequence is not the player tutorial; current players enter through MainMenu and
CoreGameplayDemo. See the design document for the current guided sequence.

In the Unity Editor, use:

`Shattered Sail > Scenes > Create or Refresh Tutorial Voyage`

The command creates `Assets/_Project/Scenes/TutorialVoyage.unity`, adds it to
Build Settings, opens it, and selects the runtime controller. Press Play and
use the primary button at the bottom of the Game view to progress.

For command-line generation:

```powershell
& 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'D:\Game\UnityClient' `
  -executeMethod ShatteredSail.Editor.TutorialVoyageSceneBuilder.CreateFromCommandLine `
  -logFile 'D:\Game\UnityClient\Logs\tutorial-scene-build.log' `
  -quit
```

Do not run Unity with administrator privileges.

## Main menu

The project ships with 21 procedural background music loops for the six ports,
three routes, three treasure-map ranks, three route bosses, ordinary combat,
and five legendary warships. Battle presentation also includes a fresh set of
procedural sound effects for cards, maneuvering, cannon fire, impacts, ramming,
melee, guarding, repairs, and battle resolution. The music, battle-effects,
and master-volume settings remain available, and old audio preference keys stay
compatible with local saves. Non-battle transitions and committed port actions
use separate `WorldSfx`, `InterfaceSfx`, and low-volume `Ambience` banks, covering
departure, sailing, encounters, settlement, trading, equipment, recruitment,
dispatch, investment, treasure maps, chart drawing, curse purification, and
monthly shop refresh.

The battle bank can be regenerated deterministically with
`node Tools/generate_battle_sound_effects.js` from the project root.
The non-battle banks can be regenerated with
`node Tools/generate_nonbattle_sound_effects.js`.

`Assets/_Project/Scenes/MainMenu.unity` is the first enabled player scene. It
uses the generated pixel-art harbor background in
`Assets/_Project/Resources/MainMenu`, then presents new game, continue,
settings, achievements, and exit actions. Continue reads the existing local
profile; new game asks for confirmation before resetting gameplay progress.

The 50 achievements are permanent milestones within that save. Committed gameplay
captures qualifying state; spending money, leaving a port, changing equipment, or
claiming a dispatch never revokes an unlocked achievement. Starting a new game
clears the achievement history along with gameplay progress. Existing saves import
evidence from their current state, collection travel history, and legacy records;
editor previews and achievement-panel queries do not write progress.

Previously tutorial-only actions can also be completed after graduation: edit a
deck, obtain a spare component, return with hull damage, or complete a successful
voyage with a navigator. Long dispatch requires 12 hours, and dispatch completion
requires a successful reward claim. Visiting all ports is independent of investing;
the six-port investment achievement requires an investment in each port. Crew
milestones count individual recruits, and the regional crew milestone requires
recruits from all six ports. Equipment achievements read the current per-ship
inventory, with four occupied slots required on one ship.

Open it with `Shattered Sail > Scenes > Open Main Menu`. The companion create
or refresh command also restores the intended player build order: main menu,
then the unified core-game scene.

## Core gameplay

`Assets/_Project/Scenes/CoreGameplayDemo.unity` is the current playable main
loop. Its port opens as an illustrated, interactive harbor overview with nine
clickable facilities: warehouse, shipyard, tavern, component shop, card
workshop, dispatch desk, treasure-map table, logistics, and the voyage pier.
Facility labels sit above their harbor icons, hover states turn gold, and each
screen has a dedicated background plus previous/next and return-to-port
navigation. The voyage pier includes three route profiles, deterministic seeded maps,
eight-way sailing, currents and provisions, fog-of-war, fixed and random
encounters, crew-gated choices, card combat, a route boss, safe return, arrival,
and protected-loot settlement.

This is the unified tutorial and core-game scene, loaded from the main menu.
New saves first learn sailing and events, then return to recruit a navigator.
The next voyage continues through the special-crew event, an ambush that the
player leaves, and the tutorial boss. This boss has a fixed three-turn naval
lesson followed by a fixed three-turn boarding lesson. Card order, enemy
actions and damage are authored for instruction. Red outlines and arrows mark
the current action without dimming the scene; unrelated input is guarded.

The world is built around the old captain who left home young, united sailors
from six ports, and fought storms, unequal tolls and the sea gods while trying
to establish the freely accessible Seventh Harbor. The victory opened a route
but did not settle its meaning: some lieutenants turned the freedom flag into a
black sail and became predatory legendary fleets, while others still follow the
old route looking for him. Items, ships, crew biographies and keepsakes split
their evidence between signed records and clues that can only be inferred. The
player is asked to decide what the old captain meant by freedom, defeat the
fallen, and build the Seventh Harbor as a responsibility rather than inherit a
throne.

Port checkpoints persist recruitment, deck changes and settled rewards. Stable
sailing and encounter states can be suspended through the menu and resumed from
a voyage checkpoint; in-battle resume is not supported. Failed attempts retain normal losses and open the real
provisioning, warehouse and deck services needed to retry. The first component
is granted once, and completing the final settlement unlocks free exploration.
Existing saves that already completed the tutorial remain unrestricted. The
port's tutorial information button explains how to start a new guided save; it
does not skip or replay a simulated graduation reward. See
[the guided tutorial verification notes](Documentation/GuidedTutorial.md).

The scene uses the shared pixel-art route maps, event illustrations, ship
sprites, crew portraits, resource icons, and card art from
`Assets/_Project/Resources/LegacyDemo`. `LegacyDemoArtCatalog` keeps the source
sprite sheets intact and selects cells through UV coordinates at runtime.

Port preparation and naval combat additionally use a generated production-art
set from `Assets/_Project/Resources/PortArt`: a harbor shipyard backdrop, four
ship presentations, six specialist portraits, six enhancement-module icons,
and four route-aware battle backdrops. `PortArtCatalog` indexes the fixed atlas
grids without duplicating the source PNG files. The port screen shows the new
ship, specialist, and module art; combat automatically chooses a background and
enemy ship by route, with a dedicated boss scene.

The facility hub and its nine interior scenes use the generated pixel-art set
in `Assets/_Project/Resources/PortFacilities`. `PortFacilityArtCatalog` loads
the icon atlas and dedicated warehouse, shipyard, tavern, shop, workshop,
dispatch, treasure, logistics, and voyage backgrounds. Warehouse and logistics
restocking call the real core session; the voyage pier retains the complete
route-selection and departure flow, while other management screens expose the
current prototype data and selection states.

Open it from the Project window or use:

`Shattered Sail > Scenes > Open Core Gameplay Demo`

Press Play, choose a route and seed, then use the direction grid or discovered
event markers to set a course. The navigation chart also accepts clicks anywhere
in unexplored water: the click creates a precise free-course target and updates
heading and distance. Clicks within the event snap radius lock onto the nearest
known, unvisited event; choosing an eight-way heading or moving the distance
slider returns to manual navigation. The chart uses a 48-by-32 exploration grid.
Seeded shoals, reefs, kelp, fog banks, and
storm fronts are distributed around islands and event types; line-of-sight samples
those cells and deterministic edge noise to produce an irregular, reproducible fog
boundary. A deterministic local wind-and-current field also affects travel time:
tailwinds and following currents increase effective speed, while headwinds and
opposing currents reduce it. Deep-blue arrows mark current direction and light-blue
arrows mark wind direction across explored water; the navigation forecast lists
their direction, strength, relation to the course, and separate speed modifiers.
The scene is also enabled in Build Settings.

`TutorialVoyage.unity` remains only as a legacy developer test scene; players
no longer need to open it separately.

The core rules are intentionally independent from `UnityEngine`; run EditMode
tests for deterministic rules and PlayMode tests for scene/UI smoke coverage.

## 桌面包体优化

`DesktopBackgroundCompressionPolicy` 采用明确的背景路径白名单；当前有 42 张
符合条件的 Default 类型背景在桌面平台使用高质量 BC7。导入时将宽高调整至
最近的 4 倍数，按整图双线性重采样保留归一化 UV；源 PNG 不改，原有尺寸上限、
mipmap、Read/Write 和采样设置保持不变。Sprite、可读家具、人物立绘、图标和
图集不在本轮处理范围。不要用全目录压缩替代此白名单。

28 张退役图片已移入 `Assets/_Project/Editor/ArchivedArt`，保留原图及 GUID，
作为编辑器美术档案，不进入 Player。不得将归档目录全部拷回 `Resources`；
确需恢复单个资源时，应先核对正式引用和重复打包风险。

策略没有启动时批量重导逻辑。需要显式应用时，在目标桌面平台的隔离工程中执行：

```text
Unity.exe -batchmode -quit -projectPath "<工程绝对路径>" -buildTarget Win64 -executeMethod ShatteredSail.Editor.DesktopBackgroundCompressionPolicy.ApplyAllFromCommandLine -desktopBackgroundCompressionReport "<全新绝对JSON路径>"
```

报告核验最终 BC7 格式、尺寸、受保护设置及源 PNG 哈希，缺失路径会记录并跳过，
失败会报错；报告不能覆盖已有文件。应用后仍须检查背景画质和实际构建体积，
不能仅凭导入选项认定优化成功。

航海前方视野使用晴海、海雾、风暴各自的日／暮／夜景色，晴昼保留原图，
其余八张位于 `Resources/NavigationLookout/Environments`。天气取船只所在海格，
远处天气仍由视野投影显示；晨昏通过两张实景淡化，动态岛礁与船只同步改变明暗。
背景按窗口等比例裁切，保持海平线位置，不拉伸太阳或月亮；前景投影不参与裁切。
不要恢复 `Lookout Time Overlay` 整屏色罩。图片采用内置 imagegen 生成，提示词与
生成记录保存在 `../Artifacts/NavigationLookoutWeather20261009/imagegen-prompts.json`。


## 界面字体与层级

大标题保留 `GameTypography.InterfaceFont`（香萃刻宋），正文与小字号控件使用
内置 `GameTypography.BodyFont`（Seventh Harbor Sans Regular）。后者基于 Noto Sans SC
官方简中完整字库调整行高，未裁剪字符；来源、哈希与派生过程见
`../Artifacts/UIPolish20261009/font-source.md`，许可证随 Resources/Fonts 保存。
不要直接换回原始 1448/em 的行高，否则紧凑标题栏会在 UGUI 垂直截断时消失。

1080p 参考画布采用标题 32–40、按钮和页签 22–26、正文 22–24、辅助说明
18–20 的基础字号。主菜单和游戏内设置提供标准、大字、特大字，对应 1、1.25、
1.5 倍；点击“应用”后保存并重建当前界面。`GameReadability.Body/Heading`
始终从基础字号计算，不叠乘当前字体。文字空间不足时扩大行高、分页或滚动，
`WoodButtonSkin` 不再启用自动缩字。

市场在三档模式下分别每页显示 4、3、2 项，同类文字在所有页上使用相同字号，
末页不单独放大或缩小；翻页保留数量、收货方式及报价校验。出航左右栏改为
900 / 950 的基础宽度，右侧船只与船员说明可滚动，出航按钮固定可见。
仓库、卡组、酒馆、船坞、部件详情及事件长文保留完整内容并按需滚动。

`WoodButtonSkin.SetRole` 可明确指定 Primary / Secondary / Danger；页面标签的选中
状态独立保存。默认返回、分页、设置为次操作，确认与出航为主操作。卡牌和透明
场景热点保持原有样式与点击区域。普通 Header 基础高108、教程基础高148，
随字号档位增加高度以容纳上下提示。
背景扩展使用同一UV坐标换算；航海四区读取实际Header高度。

航海页把预计耗时、航速和补给消耗放在常驻摘要里；风流修正通过“风流详情”
展开。该操作不推进航程，拖动航程时只更新原控件和航线。港口名称使用小木牌，
点击木牌与点击建筑共用同一入口。

战斗决策信息由 `CoreGameplayBattleDecisionLayout` 在初次构建和保留视图刷新时
定位：资源位于手牌旁，敌方意图避开舰船、人物和血条。每十点生命一格，格挡仍
使用白色护甲边缘。不要改变用于动画和刷新查找的控件父子路径。

卡组右栏必须按海盗框图片内的实际可见纸面检查，单纯“在 RectTransform 内”
不能证明文字未被装饰遮挡。市场初建和数量实时编辑共用 `MarketTradeButtonText`
格式化报价，金额仍按实际结算精度显示。功能页验证和界面预览见
`../Artifacts/UIPolishFollowup20261009`。

准备出航的六港名称按固定顺序常驻，随机航线与已绘海图共用同一排港牌。
当前港用不透明暗色牌面与灰字表示且不可选择；已绘海图仍锁定目的港并保留
目的港高亮。不要再通过隐藏按钮或降低整块透明度来表现当前港。
