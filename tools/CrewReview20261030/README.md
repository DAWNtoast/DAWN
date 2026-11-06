# 全部船员审核网页

固定入口：`outputs/crew-review-20261030/全部船员审核.html`。可直接在浏览器打开，无需网络服务。图片目录和页面需一同保留。

36 名船员按 6 港、6 职业整理，包含生产定义中的人物资料、现用档案立绘、酒馆人物裁切、通用画像及已有的旧全身参考图。游戏四页使用当前生产 `CoreGameplayDemoController` 在隔离 Unity 预览场景实际渲染，共 144 张 1920×1080 原图，另有 6 张酒馆全景。15 张专属卡牌页有生产滚动区，截图保留初始位置，完整文字在页面展开资料内。

## 审核记录

- 每人下方勾选审核完成并填写建议，自动保存到当前浏览器。
- 点击“导出审核记录”备份 JSON；“导入记录”可恢复或合并建议。不同建议追加保留。
- 固定存储键：`shattered-sail:crew-review:v1:<crew_id>`，稳定船员 ID 取自生产目录。不得更改存储键或删除用户导出的审核文件。
- 浏览器或打开方式（本地文件/HTTP 地址）变化时存储空间可能不同，应先导出记录再导入。禁止用测试数据写入用户浏览器。
- 已有异常存储原文保留，导出包含恢复数据；存储不可用时页面明确提示先导出。

## 更新和验收

只重建网页：`powershell -File tools/CrewReview20261030/ExportReview.ps1`。

从当前源码和素材重新生成：`powershell -File tools/CrewReview20261030/ExportReview.ps1 -RefreshInputs`。数据和截图导出会核验源文件哈希，并安全清理本次临时编译/Unity 工程；不会写入或清理主工程，不会使用退役的 `UIPreviewsForMe`。

`verify_review.cjs` 在内存中验证实际审核脚本的存储/恢复/导入导出/异常数据/跨窗口合并，并核对36人、144截图的链接、尺寸和哈希；不创建用户审核记录。

素材证据见 `data-audit.json`，截图证据见 `capture-manifest.json`、`capture-evidence/`，网页检查见 `web-validation.json`。临时预览服务只用于开发验收；交付使用固定本地 HTML 入口。

网页默认使用轻量预览图，点击放大加载未压缩的原图。`CaptureThumbnails.ps1` 与 `CaptureArtThumbnails.ps1` 只新增预览与索引，不覆盖原图。
