# 事件插画映射修订记录

日期：2026-10-23。新绘制资源统一置于 `UnityClient/Assets/_Project/Resources/EncounterIllustrations/Reviewed20261023/<key>.png`。

## 接入范围

事件弹窗与海图预览现在传递实际 `CoreEncounter`，不再丢失同 ID 的故事变体。传奇战斗前奏仍按唯一 ID 解析完整海景；海上/战斗中的透明舰船资源不变。

| 实际事件 ID | 实际标题或范围 | 新资源 key |
| --- | --- | --- |
| treasure_core | 沉船工坊主机舱 | core_weathered |
| treasure_core | 商盟封存总仓 | core_gilded |
| treasure_core | 三相月蚀圣所 | core_moonlit |
| tutorial_crew_requirement_event | 无法辨认的潮汐笔记 | tutorial_note |
| guided_tutorial_random_event | 顺风捎来的讯息 | tutorial_message_ship |
| guided_tutorial_random_event | 水手发现漂流信筒 | tutorial_message_tube |
| tutorial_navigator_event | 潮汐暗标 | tutorial_tide_buoy |
| event_reef_hidden_cache | 潮痕秘藏 | reef_tide_cache |
| event_reef_broken_mast | 断桅求援 | reef_broken_mast |
| event_reef_sharp_current | 暗流推船 | reef_chart_current |
| common_storm_debris | 风暴残骸 | storm_debris |
| common_reef_divers | 珊瑚采珠人 | pearl_divers |
| weathered_chain_gate | 横海沉链 | weathered_chain |
| weathered_keel_vault | 龙骨封舱 | weathered_keel |
| weathered_rustyard | 锈锚拆船场 | weathered_rustyard |
| weathered_signal_buoy | 沉没测深浮标 | weathered_buoy |
| weathered_broken_caisson | 裂缝沉箱 | weathered_caisson |
| gilded_witness | 失踪押运员 | gilded_witness |
| gilded_relief | 赈济交割船 | gilded_relief |
| gilded_auction | 封缄拍卖艇 | gilded_auction |
| gilded_convoy | 逆风商队 | gilded_convoy |
| gilded_ledger_buoy | 海底账册浮标 | gilded_ledger_buoy |
| moonlit_dawn | 昼辉祭坛 | moonlit_dawn |
| moonlit_dusk | 暮潮祭坛 | moonlit_dusk |
| moonlit_midnight | 夜幕祭坛 | moonlit_midnight |
| moonlit_choir | 无声唱诗礁 | moonlit_choir |
| moonlit_sleeping_guardian | 沉睡守月像 | moonlit_guardian |
| moonlit_moon_well | 不落月井 | moonlit_well |
| treasure_boss_moon_vault | 沉月王庭 · 守藏舰 | treasure_guard_ship |
| event_trade_debt_collector | 债务小艇 | event_trade_debt_collector |
| event_trade_merchant_distress | 商船求援 | event_trade_merchant_distress |
| event_moon_mirror_pool | 月镜潮池 | event_moon_mirror_pool |
| event_moon_spectral_debris | 幽灵残骸 | event_moon_spectral_debris |
| event_moon_lunar_observatory | 月相观测台 | event_moon_lunar_observatory |
| moon_ghost | 无灯幽舰 | moon_ghost |
| event_moon_v2_borrowed_shadow | 借影摆渡人 | event_moon_v2_borrowed_shadow |
| event_moon_drowned_library | 沉没书库 | event_moon_drowned_library |
| event_moon_healing_spring / moonlit_cleanse | 月光泉 / 洗月圣泉 | event_moon_healing_spring |

全量实际绑定见 [illustration-audit.csv](illustration-audit.csv)：直接调用当前 Core 工厂与正式插画目录导出 210 条记录，含 195 个 ID、201 种文本。每行保留分类、航域/档位/分支、事件描述、资源路径、完整 UV 及映射类型。[illustration-coverage.txt](illustration-coverage.txt) 和 [illustration-resource-paths.txt](illustration-resource-paths.txt) 记录实际去重计数与路径，没有继续使用旧的资源总数常量。

当前运行时引用 **142 个路径**，其中 **38 个 Reviewed 路径对应 43 条目录记录**；现役与兼容合并后同为 142 个路径。原借影摆渡人独立图片不再被事件引用，因此新添 38 张并不等于原 105 路径简单相加。旧文件是否仍在磁盘不影响引用统计，也没有在本任务中删除旧资源。

## 兼容与回退

- 三档旧版 `treasure_core` 的实际标题均为“秘藏核心”，继续使用重绘藏宝图集的石门场景。不会根据当前选中的档位误配新图。
- 旧独立教程的 `legacyTutorial: true` 保留“陌生海图暗语”的既有映射；“偏航的星盘”使用新增海面月相观测台场景。现役笔记与暗标浮标不会覆盖它们。
- 藏宝 `moonlit_mirror_pool` 继续使用原月影图集场景；普通航行的 `event_moon_mirror_pool` 单独使用新海面镜池场景，避免共享 ID 别名更新误伤其他叙事。
- `moonlit_cleanse` 与新浅滩月光泉共用资源；`moonlit_echo_library` 保留原水下藏书场景，普通沉没书库改用半淹石阶与密封书匣的新图。
- 两个多语义 ID 依据实际事件 ID 与精确标题选择变体。工厂标题发生变更时，29 场景对应回归会提示漏项；仅有 ID 或未知标题时仍采用既有兼容映射。
- 已知插画缺失时返回空图，不加载另一故事的图片或透明舰船。缓存释放机制不变。
- 新图全部使用完整 UV；图集原有 5.5% 内缩不应用于新独立图。

## 已完成验证

- 新鲜编译 Core、Content、Persistence、Presentation 与插画测试程序集全部通过。
- `tools/EventIllustrations20261022/RunTests.ps1 -IncludeResourceValidation`：**16/16 通过**，XML 结束时间 `2026-10-23 11:33:01Z`。覆盖当前 195 个 ID、210 条实际工厂记录、38 张修订场景、五个同 ID 变体、旧版兼容、缓存和缺图行为。全部 **142 个实际引用资源**的 PNG、meta、唯一 GUID、Unity 导入和实际加载检查通过。[结果 XML](../../Artifacts/EventIllustrations20261022/Tests/results.xml)。
- `tools/BossEncounterScenes20261023/RunTests.ps1 -IncludeResourceValidation`：**27/27 通过**，XML 结束时间 `2026-10-23 11:35:01Z`。12 个 Boss 的字符串/实际事件解析都优先选择海景，并验证五个变体经真实控制器传递后仍使用正确图片；海景和原透明战舰分别加载。[结果 XML](../../Artifacts/BossEncounterScenes20261023/Tests/results.xml)。
- `git diff --check` 通过；仅有既存文件 LF/CRLF 转换提示。
- [代表性生产 UI 检查](ui-preview-review.md)：5 张实际事件弹窗与 2 张海图截图均验证、目检通过，不保存场景。

## 最终交付与复核命令

38 张新图及其 meta 已全部安装，完整资源验证顺序执行并成功退出。总计 **43 项插画与解析器测试通过，0 失败、0 跳过**；新鲜 XML 均由本轮生成。全部生成来源、提示词和最终 SHA256 见 [总生成清单](all-generated-art.json)，视觉核查见 [视觉报告](visual-review.md)。

```powershell
& 'D:/Game/tools/EventIllustrations20261022/RunTests.ps1' -IncludeResourceValidation
& 'D:/Game/tools/BossEncounterScenes20261023/RunTests.ps1' -IncludeResourceValidation
```

以上命令为本轮实际执行命令；运行时统计保存在 [覆盖计数](illustration-coverage.txt)，资源路径不使用历史总数常量。
