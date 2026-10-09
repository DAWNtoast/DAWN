# TutorialVoyage 新手引导预览

此文件夹收纳新手引导阶段所有可编辑的画面状态。每个 `.unity` 文件都是 `UIPreviewsForMe` 中的独立副本，可单独打开、调整 Canvas 层级、控件位置、尺寸、文字和颜色。

- `00_TutorialVoyage_Runtime.unity`：独立 `TutorialVoyage` 运行时海图演示，运行时会生成海图、航线节点、状态栏、日志、事件卡和操作按钮。
- 现存编号文件对应当前引导状态。副本按真实游戏中该状态所使用的界面模板建立，文件头包含状态 ID、中文说明和模板来源；同一模板的不同状态仍然分别保留，便于针对该步骤记录布局修改。第二航程的随机讯息、伏击和战斗撤退三个旧节点已从流程和预览中移除。

| 文件 | 状态 | 预览画面 |
|---|---|---|
| 01_FirstDeparture | FirstDeparture | 首航选择目的港与海图（`UI_VoyageDeparture`） |
| 02_FirstSail | FirstSail | 首航航行输入（`UI_Navigation`） |
| 03_FirstMandatory | FirstMandatory | 强制事件弹窗（`UI_Encounter`） |
| 04_FirstOptional | FirstOptional | 可选事件弹窗（`UI_Encounter`） |
| 05_FirstCrewEvent | FirstCrewEvent | 航海士事件（`UI_Encounter`） |
| 06_FirstReturn | FirstReturn | 主动返航（`UI_Navigation`） |
| 07_FirstSettlement | FirstSettlement | 首航结算（`UI_Settlement`） |
| 08_Tavern | Tavern | 酒馆（`UI_Tavern`） |
| 09_RecruitNavigator | RecruitNavigator | 航海士详情与招募（`UI_TavernCrewDetail`） |
| 10_SecondPier | SecondPier | 第二航程进入出航码头（`UI_VoyageDeparture`） |
| 11_SecondRoute | SecondRoute | 第二航程选择路线（`UI_VoyageDeparture`） |
| 12_SecondDeparture | SecondDeparture | 第二航程确认出航（`UI_VoyageDeparture`） |
| 13_SecondSail | SecondSail | 第二航程航行（`UI_Navigation`） |
| 14_SecondCrewEvent | SecondCrewEvent | 特殊船员事件（`UI_Encounter`） |
| 18_SecondSettlement | SecondSettlement | 第二航程结算（`UI_Settlement`） |
| 19_Workshop | Workshop | 卡组工坊（`UI_CardWorkshop`） |
| 20_BuildDeck | BuildDeck | 卡组构筑（`UI_DeckBuilder`） |
| 21_ThirdPier | ThirdPier | 第三航程进入出航码头（`UI_VoyageDeparture`） |
| 22_ThirdRoute | ThirdRoute | 第三航程选择首领路线（`UI_VoyageDeparture`） |
| 23_ThirdDeparture | ThirdDeparture | 第三航程确认出航（`UI_VoyageDeparture`） |
| 24_ThirdSail | ThirdSail | 第三航程驶向首领信号（`UI_Navigation`） |
| 25_TutorialBoss | TutorialBoss | 首领战（`UI_Battle`） |
| 26_ThirdReturn | ThirdReturn | 首领战胜利返航（`UI_Navigation`） |
| 27_ThirdSettlement | ThirdSettlement | 首胜结算（`UI_Settlement`） |
| 28_DrawChart | DrawChart | 在首胜结算页绘制海图（`UI_Settlement`，含「航海士 · 绘制海图」按钮） |
| 29_Warehouse | Warehouse | 进入船长收藏室（`UI_Warehouse`） |
| 30_SelectTreasure | SelectTreasure | 选择破浪者的罗盘（`UI_Warehouse`） |
| 31_DisplayTreasure | DisplayTreasure | 陈列首胜藏品（`UI_Warehouse`） |
| 32_Complete | Complete | 引导完成返回港口（`UI_PortHub`） |
| 33_Shipyard | Shipyard | 进入皇家船坞（`UI_Shipyard`） |
| 34_ShipEquipment | ShipEquipment | 打开配件页签（`UI_ShipyardDetail`） |
| 35_UnequipRewardComponent | UnequipRewardComponent | 卸下旧部件（`UI_ShipyardDetail`） |
| 36_SelectRewardComponent | SelectRewardComponent | 选择首胜部件（`UI_ComponentDetail`） |
| 37_EquipRewardComponent | EquipRewardComponent | 装备首胜部件（`UI_ShipyardDetail`） |

这些场景是编辑参考副本。运行时生成的实际引导状态仍由 `CoreGameplayGuidedTutorialController` 和 `CoreGameplayGuidedTutorialUI` 驱动；完成布局调整后，请将最终参数同步回对应的运行时代码或公共预制体。

`28_DrawChart` 以已保存的 `27_ThirdSettlement` 布局为基础，绘图按钮使用真实结算界面的控件工厂、尺寸与位置。此步骤仍处于结算阶段；藏宝图桌是在完成仓库陈列和首胜部件装配后，从港口进入的另一页面。需要修复旧的错误藏宝图桌副本时，可运行编辑器入口 `ShatteredSail.Editor.GuidedTutorialPreviewSceneBuilder.RepairDrawChartFromCommandLine`，它只处理 `28_DrawChart`，不会重新生成其余可编辑场景。
