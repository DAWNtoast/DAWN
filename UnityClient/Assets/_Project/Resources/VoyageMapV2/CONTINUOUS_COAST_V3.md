# 海图连续海岸 V3

## 修复内容

旧版 `terrain_strait_v2` 的两岸一直延伸到素材边界。直接旋转、拉伸整张图，便会把素材边界画成笔直断岸；按地形组包围盒贴整张岛屿图也会覆盖原本的凹湾和水道。

新版先保留固定事件与地形占格生成流程，再统一读取全图 `Island` 格：

- `VoyageChartCoastField` 以每格 12×12 子采样生成共享海岸距离场（577×385），加入有界、种子驱动的岸线扰动，形成完整闭合的岸线。
- `VoyageChartCoast` 在同一个全图坐标域中绘制地表、礁岸、沙滩、浅水和断续浪花，不再贴矩形峡谷或岛屿插画。地表沿世界坐标连续铺设，不按事件单独拉伸。
- `BuildStraitBankCells` 给峡谷生成连续主脊、宽窄变化、两岸不对称且两端收窄的轮廓，保持原最大占地包络与中间可直行水道。
- 水面纹理使用重叠、带扰动的透明边缘；水面/地形层受海图边界裁切。弱化网格和统一岸边亮圈。
- 岸线只将不可通行 `Island` 作为陆地；`Beach` / `Strait` 仍是水面。视觉采样不修改事件、碰撞、探索或洋流数组。
- 只缓存最近一张地图的 CPU 海岸数据。探索或改变航向不重新计算；实际地形/种子变更使缓存失效。每个显示对象仍独立释放自己创建的材质和数据贴图。

## 查看效果

等待 Unity 编译后重新打开 `Assets/_Project/Scenes/UIPreviews/UI_Navigation.unity`，无需进入 Play；可在预览控制器中切换港口/海域。正式游戏重新出航后使用新版生成器。

相同种子在新版中仍可重现同一结果。峡谷形态已升级，不能保证与旧版种子生成结果逐格相同；固定事件避让可能因此调整位置。不会重置存档、设置、金币或船员。

菜单 `Shattered Sail/Art Review/Capture Voyage Charts` 可以输出真实 Unity 渲染的 18 组港口/海域全图与近景；命令行入口为 `ShatteredSail.Editor.VoyageChartAcceptance.CaptureFromCommandLine`，参数 `-voyageChartCapturePath` 指定新空目录，不能加 `-nographics`。该工具使用临时编辑预览场景，不进入游戏、不写存档和场景。

## 美术资源

新增 `terrain_ground_detail_v3.png`，1254×1254，不透明岩层/植被连续地表材质。使用 **imagegen 技能的内置图像生成模式**制作，原图未经重新切割、换色或锐化。Unity 以无压缩、MipMap、Repeat 导入；着色器使用镜像采样以保证相邻纹理段数值连续。保留全部原始 V2 素材，不删除旧资源。

最终生成提示词（请求 2048，实际返回 1254；以实际文件为准）：

> Create a production-ready seamless square texture tile, 2048x2048, for the LAND INTERIOR of a highly detailed top-down pirate nautical strategy map. Orthographic directly overhead view, no perspective horizon. Edge-to-edge OPAQUE terrain, NO sea, NO coastline, NO islands as isolated shapes, NO transparency. It must tile seamlessly across both opposite edges. Depict richly detailed irregular patches of lush olive and emerald subtropical forest canopy interspersed with weathered warm-grey coastal rock outcrops, tiny fissures, moss, exposed brown earth and scattered small palm crowns. Roughly 55% vegetation, 35% rugged rock, 10% earth. Natural irregular continuous geology at several scales, not polka dots, no repetitive discrete oval objects. High-quality finely painted realistic game environment texture with crisp miniature foliage, layered rock strata, gentle three-dimensional relief, maritime adventure atmosphere. Neutral even overhead daylight, restrained warm highlights and cool dark crevices, visible sharp microtexture throughout, no strong directional cast shadows, no haze or blur. Avoid black outlines, avoid decorative borders, avoid text, labels, UI, paths, buildings, water, sand rings, luminous effects, vignette, or empty margins. This is a repeatable continuous ground material, not a completed map illustration.

## 回归测试

新增 CoastField、BlendGraphic、CoastCache、StraitBankGeometry 测试；覆盖种子确定性、不同 seed 差异、凹湾/分离岛、横竖峡谷、格心视觉与通行一致、边界透明、UV 翻转、顶点上限、资源释放和缓存失效。最终测试与真实渲染报告位于项目 `Artifacts/VoyageCoastV3-Final`。
