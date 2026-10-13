# 旧美术质量审计脚本

这组脚本保留 2026-10-12 的人工质量审阅结论。2026-10-13 清理后已同步 A01–A04、B01、I01–I06 的精确文件范围；I07、D01–D06 不在排除范围内。

- `inventory.cjs` 读取当前源文件；归档目录为空或不存在时记录 0。
- `environment-audit.cjs` 删除退役藏宝底图和独立扩展展柜条目，按当前库存检查其负责的资源范围。
- `report.cjs` 只从旧条目移除获批退役路径；未覆盖文件、缺失现役文件、缺失代码证据仍报错。网页统计从库存计算。
- `cleanup-20261013.cjs` 从同目录 `retired-assets-20261013.json` 读取获批的 104 条精确路径及组号；该稳定记录保留授权日期和原始日志位置，运行不依赖被 Git 忽略的 `Artifacts` 报告。旧库存仍包含退役项时要求重新盘点，不能静默跳过任意缺失文件。

需要保留历史报告时，运行前设置 `ART_QUALITY_AUDIT_OUTPUT` 为新目录。报告合并需要已有 `characters-audit.json` 和 `items-ui-audit.json`，可用 `ART_QUALITY_AUDIT_SOURCE` 指定它们所在的历史目录。其余输入读取新输出目录。组件预览脚本 `Artifacts/ArtQualityAudit20261012/contact_items.py`、`compose_icon_audit.py` 同样支持该输出变量。

当前生产资源已超出旧人工审阅范围。生成当前库存和分项报告不等于完成新素材质量审阅；总报告仍会阻止未覆盖资源通过。不要为了生成报告而关闭这些检查，也不要把 2026-10-12 的历史 HTML/JSON 当成当前库存。
