# 六港功能页顶栏

已接入 `D:/Game/UnityClient`。酒馆、商店、后勤等九个港口功能页共用同一套导航逻辑，按当前港口切换独立美术。港口总览和航行界面保持原有 HUD。

## 显示与交互

从左到右：**返回港口 ｜ ← ｜ 当前功能名称 ｜ → ｜ 本机时间 ｜ 港口名称 ｜ 总金币 ｜ 设置**。

- 新顶栏替换功能页原蓝色透明条，同时隐藏上方重复的原型状态栏；场景内容的坐标、尺寸和支撑点不变。
- 左右按钮循环浏览九个设施；返回按钮回到当前港口。按钮有悬停、按下、键盘焦点反馈，边框本身不拦截点击。
- 时间为本机时间 `yyyy-MM-dd HH:mm`，按分钟刷新。当前项目商店库存使用真实时间，尚无独立的持久游戏日历，故没有用每次出航重置的航时冒充日期。
- 总金币读取当前 `ProfileGold`，发生收支后刷新，不是单次航行随船金币。
- 设置打开独立羊皮纸弹窗：主音量、音乐、音效、全屏。与主菜单共用设置，点击“应用设置”才保存；关闭不保存草稿。打开/关闭不会重建下层功能页，不清空采购数量或卡组。
- 设置遮罩隔离下层点击与键盘导航；同帧关闭再打开可安全复用交互隔离组件。空白处点击后恢复弹窗内部焦点，可以继续使用取消键。
- 编辑模式的设置仅预览，禁止写入设置或改变音量、全屏。

## 六套独立素材

| 港口 | 边框特色 | 资源名 |
| --- | --- | --- |
| 断锚港 | 黑铁断锚、链条、修补木饰 | broken_anchor_header_v1.png |
| 白贝港 | 白贝贝雕、珍珠母装饰 | white_shell_header_v1.png |
| 铜钟港 | 黄铜钟饰、齿轮纹样 | bronze_bell_header_v1.png |
| 盐冠港 | 鎏金冠饰、象牙色饰线与深红木底 | salt_crown_header_v1.png |
| 暮潮港 | 银色月纹、紫晶与暗木 | twilight_tide_header_v1.png |
| 灰鸥港 | 鸥翼木雕、绳结与海港旧木 | gray_gull_header_v1.png |

使用内置 imagegen 独立生成六张位图，没有用同一图换色。完整提示词与生成方式见 [ART_PROMPTS.md](D:/Game/outputs/port_headers/ART_PROMPTS.md)。资源位于 `D:/Game/UnityClient/Assets/_Project/Resources/PortHeaders/`。

原件不改写像素；`PortHeaderArtCatalog` 按每张源图的选区创建 Sprite，再由 Unity 九宫格缩放边框，保留角饰比例。纹理设置为无 mipmap、无压缩、不缩放非二次幂尺寸。

## 查看效果

- 打开既有 `D:/Game/UnityClient/Assets/_Project/Scenes/UIPreviews/` 中任一港口功能预览场景，等待 Unity 编译后重新打开场景即可在非运行状态查看。
- [六港 13 张实机预览对照](D:/Game/UnityClient/Artifacts/PortHeadersV1/index.html)：六港酒馆/后勤各一张，加设置弹窗。
- [灰鸥港后勤](D:/Game/UnityClient/Artifacts/PortHeadersV1/GrayGull_Maintenance.png)、[暮潮港酒馆](D:/Game/UnityClient/Artifacts/PortHeadersV1/TwilightTide_Tavern.png)、[设置弹窗](D:/Game/UnityClient/Artifacts/PortHeadersV1/GrayGull_Maintenance_Settings.png)。

## 验证

- 最终 EditMode：89/89 通过，覆盖六港 × 九功能页、左右循环/返回、实时读数、布局不重叠、独立边框，以及设置编辑保护与页面草稿保留。[结果](D:/Game/UnityClient/Artifacts/port-header-editmode-final.xml)。
- PlayMode：20/20 通过，包含新增两项设置生命周期/焦点回归。[结果](D:/Game/UnityClient/Artifacts/port-header-playmode-v1.xml)。
- 六港 13 张 1920×1080 截图全部生成并逐张看图检查：文字清晰、无顶部裁切和重复蓝条，主题装饰不同，原人物/船只/陈列物布局未移动。鼠标悬停/键盘焦点逻辑由控件和测试验证，静态截图不代表完整人工游玩。
- 在 `D:/Game/tmp/regional-ports-check-20260906` 隔离副本验证；未关闭或操作用户当前 Unity 编辑器，未覆盖用户场景或玩家存档。

## 主要代码

- `Presentation/Runtime/CoreGameplayPortController.cs`：顶栏布局、原功能页导航。
- `Presentation/Runtime/CoreGameplayPortHeaderController.cs`：按钮反馈、时间/金币刷新。
- `Presentation/Runtime/PortHeaderArtCatalog.cs`：六港边框资源与九宫格切片。
- `Presentation/Runtime/CoreGameplayPortSettingsController.cs`：设置草稿弹窗与应用。
- `Editor/PortHeaderAcceptance.cs`：独立预览截图，不应用设置、不保存场景。

上述代码位于 `D:/Game/UnityClient/Assets/_Project/`。主控制器的编辑模式清理改为倒序即时销毁；运行模式仍延迟销毁，以修复在编辑预览中切换设施时的对象残留。
