# 第 5 批事件插画视觉验收

日期：2026-10-23。范围仅为下列 7 张，均使用内置 image_gen。新创作后将最终选定图片安装至 `UnityClient/Assets/_Project/Resources/EncounterIllustrations/Reviewed20261023`；逐张通过 `view_image` 以原尺寸查看了该目录中的实际 PNG。

| key | 画面核对 | 结果 |
| --- | --- | --- |
| moonlit_dawn | 蓝天日光、太阳纹祭坛、未点燃的中心祭器、远船与完整海面；昼景辨识明确 | 通过 |
| moonlit_dusk | 海平面落日、橙紫暮色、琥珀色潮纹和中心祭器；与昼／夜祭坛清楚区分 | 通过 |
| moonlit_midnight | 午夜蓝色海景、月亮与新月形祭坛、星形纹样、冷蓝祭器辉光；无暮色阳光 | 通过 |
| moonlit_choir | 中央岩缝嵌入骨笛，孔洞可辨，周边石面有潮纹和流经裂隙的浪水；无金属管风琴 | 通过 |
| moonlit_guardian | 闭眼安坐的巨大石像、单颗月核、双手托护、海上遗迹与完整背景 | 通过，采用手部修订版 |
| moonlit_well | 圆形有井壁的小井、井中月影与银砂、井旁水罐及粮袋；海面和码头背景完整 | 通过 |
| treasure_guard_ship | 无眼石像船首、矿物质感深色船体、月纹旧帆、王庭港口与近景小船，低视角体现尺度 | 通过 |

各图均为一幅完整场景，无文字、UI、边框、水印或拼图。三祭坛的时段及场景主体一致且可辨，其他四张的叙事物件也与计划一致。

守月像初稿的手指交叠形成额外手部轮廓。已用内置 image_gen 局部修正，最终只有清楚的两只手托护月核；最终来源见 `generated-source-moonlit_guardian-revision-1.json`，初稿来源记录保留以便追溯。第 5 批清单和安装清单均引用修订版。

- 生成与选用记录：[generated-batch-5.json](generated-batch-5.json)。
- 来源、提示词及工具返回 hint：各 `generated-source-<key>.json`；守月像另有修订记录。
- 安装命令：`tools/EventDescriptionCatalog20261023/InstallReviewedArt.ps1 -AllowPartial`，退出码 0，运行时共安装 21 张已记录图片，包含本批 7 张；没有生成或修改其他批次的图片内容。
- 安装脚本核对每张为 1536×1024，按 16 px 网格抽样 alpha=255，创建或保留对应 Unity meta，并记录 SHA-256。
- 本记录只确认本批 7 张的视觉检查和本地安装；不代替全部 36 张图片的最终资源导入／加载测试。

