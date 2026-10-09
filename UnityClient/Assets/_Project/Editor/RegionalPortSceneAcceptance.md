# 港口区域场景验收

菜单：`Shattered Sail / Art Review / Capture All Regional Facilities and Dossiers`。

命令行入口：

```text
-executeMethod ShatteredSail.Editor.RegionalPortSceneAcceptance.CaptureFromCommandLine
-regionalCapturePath D:/Game/UnityClient/Artifacts/RegionalPortsV1
```

使用 `-batchmode`，不要使用 `-nographics`。工具不需要运行游戏，不需要 `MainCamera` 标签；在私有预览场景中创建临时相机和画布，不保存场景、不修改 PlayerPrefs。输出路径可为绝对路径，适用于临时项目副本执行、结果直接保存到主项目。没有指定路径时，保存到当前项目 `Artifacts/RegionalPorts/review-时间戳`。

每港捕获酒馆、部件商店、港口投资建设、船员档案、部件档案，共 30 张 1920 × 1080 PNG，另有 `index.html` 和 `report.json`。即使仍有美术缺图，也先写出截图和报告，再以异常报告缺图；不得把回退到泛用背景误记为完成。报告检查 18 个独立资源路径、图片尺寸与原始文件 SHA-256；哈希只能发现同文件复用，是否只做了换色必须逐图检查建筑、材质、道具与构图。

只检资源（无需图形设备）：

```text
-executeMethod ShatteredSail.Editor.RegionalPortSceneAcceptance.ValidateAssetsFromCommandLine
```

背景交互锚点均以 1920 × 990 内容区为准，原点在中心：

- 酒馆：左侧吧台两席中心 `(-790,55)`、`(-585,50)`；中央桌四席 `(-365,-70)`、`(-120,-35)`、`(135,-35)`、`(405,-60)`。顶部约 125 像素留给导航。现有角色被绘入背景，透明热区与档案负责交互，不能另绘一组通用角色覆盖地域人物。
- 商店：独立商人位于 `(735,-71)`、大小 `220 × 330`（房屋高度约三分之一）。背景不要再画一个商人。中央与左侧下半部留作展品地面；部件模型、支架、台座、铭牌是独立图层。
- 后勤：浅水船坞约位于 `(-5,-85)`、范围 `850 × 500`；不要把当前旗舰画进背景。左立板在 `(-680,-60)`、`430 × 610`；右采购牌在 `(640,-60)`、`540 × 620`，两侧留出牌面空间，不在背景重复画立板。水线透明叠加保留地区水色，不用单色椭圆遮挡。

进入各个 `UI_Tavern`、`UI_ComponentShop`、`UI_Maintenance` 预览场景后，在控制器 Inspector 的 `Editor Preview Port` 切换六港即可直接检查，无需 Play。
