# 可编辑 UI 预览

此文件夹中的场景保存了完整的 Canvas 层级，可以直接在 Unity 的 Hierarchy 和 Inspector 中调整布局、尺寸、层级、文字与颜色，然后使用 Ctrl+S 保存。

这些场景与 `Assets/_Project/Scenes/UIPreviews` 中的运行时预览分开。

新手引导的完整状态预览单独收纳在 `TutorialVoyage` 子文件夹中，包含当前三阶段流程的 34 个 `GuidedTutorialStep` 状态和独立 `TutorialVoyage` 运行时海图演示。

这里的 Canvas 不使用 `DontSaveInEditor`，打开、编译或关闭场景时也不会自动删除。需要恢复代码默认布局时，请使用菜单 `Shattered Sail > Scenes > Create or Refresh Editable UI Preview Scenes`，该操作会重新生成并覆盖当前编辑场景。

场景由同一个 `CoreGameplayDemoController` 提供数据与美术资源，保存后的 Canvas 是独立的编辑副本。修改完成后，请把需要长期保留的布局调整同步回对应的 `CoreGameplay*` 构建代码；后续优化时可直接以此文件夹中你保存的场景为准。

战斗界面的状态预览位于 `UIBattle/`：其中包含船头对船头、船头对侧舷、平行航行，以及钩索、撞角、舷梯三种接舷方式。每个场景均为独立可编辑副本，原始 `UI_Battle.unity` 保持为通用基准。

仓库运行时预览会载入 40 种收藏品目录，默认陈列前三件主线遗物；库存每页显示 6 件，可在贵重品页签用「上一页／下一页」查看完整目录。六个展柜位置仍保持与静态仓库场景一致。

其余功能页的状态副本索引见 UIStatePreviews.md。其中 UIBattle/ 和 TutorialVoyage/ 分别收纳海战姿态/接舷变体与完整新手引导状态；其他功能页按玩法状态拆分，纯素材替换不重复复制。

主菜单和独立弹窗状态位于 UIMainMenu/，包含主菜单、设置、成就和新游戏确认。UICardUpgrade/ 与 UISpecialCardCollection/ 补充编辑器枚举中独立的卡牌强化和专属卡牌收藏页。
