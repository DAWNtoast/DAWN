# 全游戏字体替换 · 2026-09-07

## 效果与范围

按用户参考中的尖角、木刻、复古中文方向，使用作者 Miiiller 的香萃刻宋 003（2023-03-07）。这是风格匹配，不是对参考图原字体的精确识别或逐字复刻；不额外叠加参考图那样的纸张磨损遮罩，以保持小字号和动态数值的清晰度。

字体完整内置到 Unity Resources，主菜单、教程、各港功能页、角色与部件故事、卡组、航海、战斗、设置及编辑预览共用，不依赖玩家电脑安装字体。保留各界面字号、布局与文字颜色；取消伪粗体/斜体。战斗卡牌名改用金色原生字重，避免额外加粗堵塞字腔。首领标记使用字库支持的“首”，替代缺失的交叉刀剑字符。

- [实际 Unity 字样样张](D:/Game/UnityClient/Artifacts/TypographyFinal/17_TypeSpecimen.png)
- [17 页完整截图画廊](D:/Game/UnityClient/Artifacts/TypographyFinal/index.html)
- [主菜单](D:/Game/UnityClient/Artifacts/TypographyFinal/01_MainMenu.png)
- [船员档案](D:/Game/UnityClient/Artifacts/TypographyFinal/04_TavernCrewDetail.png)
- [部件档案](D:/Game/UnityClient/Artifacts/TypographyFinal/07_ComponentShopDetail.png)
- [海战](D:/Game/UnityClient/Artifacts/TypographyFinal/14_Battle.png)

等待 Unity 完成资源导入和编译后，重新打开 MainMenu 或任意 UIPreviews 场景即可查看。编辑器的菜单和 Inspector 字体不变，本次只调整游戏内容。

## 验证

在独立 Unity 副本 `D:/Game/tmp/regional-ports-check-20260906` 执行，没有启动第二个编辑器访问用户主工程，也没有改动用户存档或设置。

- EditMode：105 / 105 通过（其中新增字体测试 16 项）。
- PlayMode：20 / 20 通过。
- 六港 × 18 类界面预览检查均使用同一字体，包括隐藏文字、输入框、富文本和重建/销毁后的资源生命周期。
- 最终截图 17 / 17 成功，自动诊断无缺字和竖向裁切提示；人工查看全部代表页面，并复查移除粗体后的战斗卡牌。
- 字库支持 31,170 个码点。只读 cmap 审计：角色故事 1,063 个不同汉字、部件 CSV 1,246 个不同汉字、Runtime 字串 1,341 个不同汉字均有覆盖。符号支持以真实字库为准，不依赖系统字体回退。

报告：[EditMode XML](D:/Game/UnityClient/Artifacts/typography-editmode-final.xml)、[PlayMode XML](D:/Game/UnityClient/Artifacts/typography-playmode-final.xml)、[截图诊断](D:/Game/UnityClient/Artifacts/TypographyFinal/report.json)。

## 维护

- [字体统一入口](D:/Game/UnityClient/Assets/_Project/Presentation/Runtime/GameTypography.cs)：共享 Resources 资产不得 Destroy。
- [字体版本与授权说明](D:/Game/UnityClient/Assets/_Project/Resources/Fonts/README.md)：包含固定版本、SHA256 和原始 OFL 授权。
- [回归测试](D:/Game/UnityClient/Assets/_Project/Tests/EditMode/GameTypographyTests.cs)。
- [只读字库审计脚本](D:/Game/tools/audit_typography_cmap.py)：Python 标准库，JSON 输出到 stdout。
- [Unity 截图工具](D:/Game/UnityClient/Assets/_Project/Editor/TypographyAcceptance.cs)：菜单 `Shattered Sail / Art Review / Capture Game Typography`；在临时编辑预览中生成截图，不进入 Play Mode，不保存场景或游戏进度。

字体来源：[作者官方仓库](https://github.com/Miiiller/Xiangcui-Kesong)。
