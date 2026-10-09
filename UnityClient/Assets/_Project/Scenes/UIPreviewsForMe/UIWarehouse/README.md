# UIWarehouse

从 UI_Warehouse.unity 复制的可编辑状态预览。打开对应场景后，可直接在 Hierarchy / Inspector 调整布局；场景根节点和航行标题状态会标出当前状态。

状态：
- EmptySlots
- MultipleCollectibles
- CollectionDetail
- Storage：九种基础物资逐行展示本港库存、船舱数量及占舱空间。
- AdjustCargo：双向装载滑条，负数存仓、正数装船，实时显示舱位变化。
- SellCargo：分别选择本港仓储和船舱出售量，按本港当天价格合并结算。

后三页为明确的界面样本，不写入玩家存档。可使用 `GameplayPreviewSceneBuilder.SyncAndCaptureWarehouseCargoFromCommandLine` 同步运行时控件并截图核对。

静态场景保留前三件主线遗物作为布局基准；运行时仓库目录扩展为 40 种，贵重品页签每页显示 6 件，编辑器预览会拥有完整目录并陈列前三件。
