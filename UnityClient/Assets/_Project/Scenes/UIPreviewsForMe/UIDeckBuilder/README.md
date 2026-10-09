# UIDeckBuilder

从 UI_DeckBuilder.unity 复制的可编辑状态预览。打开对应场景后，可直接在 Hierarchy / Inspector 调整布局；场景根节点和航行标题状态会标出当前状态。

状态：
- DeckEmpty
- DeckBuilding
- DeckReady

卡牌名称和效果在运行时使用 Wrap + Truncate 与自适应字号；静态牌组预览中的效果文本也保留了按语义分句的显式换行，距离数值与“米”单位不会被拆开。
