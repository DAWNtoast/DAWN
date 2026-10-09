# 第七港口

《第七港口》是一款单机航海探索与回合制卡牌游戏。玩家整备船只、招募特殊船员，在未知海域处理事件与海战，通过撞击转入接舷战，带回物资、海图与老船长留下的线索，逐步建立六港之间的航路。

当前开发与发布主体为 Unity 桌面客户端，面向 Windows 与 macOS。完整玩法、内容范围和后续计划见 [游戏策划案](游戏策划案.md)。

## 运行游戏

本次发布版本：`0.3.20261009`，最新包统一准备出航的六港显示，当前港置暗；包含此前航海、战斗和各功能页的界面优化。

- [Windows 64 位压缩包](Builds/第七港口-六港出航20261009/第七港口-Windows.zip)：完整解压后运行 `第七港口.exe`，保留同目录的数据文件。
- [macOS 通用压缩包](Builds/第七港口-六港出航20261009/第七港口-macOS.zip)：适用于 Intel 和 Apple 芯片，最低 macOS 12.0；在 Mac 上解压并打开 `第七港口.app`。
- [发布说明、体积与验证范围](Builds/第七港口-六港出航20261009/发布说明.txt)：Windows 已完成成品启动检查，macOS 尚未进行真机启动测试及 Apple 公证。此前版本保留在 `Builds/第七港口-20261009`、`Builds/第七港口-精简版20261009`、`Builds/第七港口-航海景色20261009`、`Builds/第七港口-UI优化20261009` 与 `Builds/第七港口-UI布局20261009`。
- [界面优化预览](Artifacts/UIPolishFollowup20261009/Screenshots-Release/index.html)：包含航海、海战、接舷、港口设施、仓库、卡组、市场和人物详情等界面，共 55 张。

- [六港出航界面预览](Artifacts/DeparturePorts20261009/Screenshots/index.html)：六处出发港、随机与已绘海图两种模式，1080p / 720p。

## 打开工程

使用 Unity `6000.3.23f1` 打开 `UnityClient`。

- 玩家入口：`UnityClient/Assets/_Project/Scenes/MainMenu.unity`。
- 正式玩法场景：`UnityClient/Assets/_Project/Scenes/CoreGameplayDemo.unity`。文件名中的 `Demo` 是保留的内部名称，该场景属于当前桌面游戏。
- 代码结构、验证方式与开发入口见 [Unity 工程说明](UnityClient/README.md)。

## 工作区结构

| 路径 | 用途 |
| --- | --- |
| `游戏策划案.md` | 当前版本策划基线，区分现有玩法与后续计划 |
| `UnityClient/` | 正式源码、场景、资源、测试与编辑器工具 |
| `outputs/` | 共享 CSV、航线和事件美术、角色及世界观资料 |
| `work/cargo_config_builder/` | 共享配置维护、检查、模拟和 Unity 部件同步工具 |
| `tools/` | Unity 专项验证、平衡评估与发布工具 |
| `Builds/` | 已生成的桌面版本 |
| `Artifacts/` | 构建、测试、截图及变更记录 |
| `tmp/`、`Unity*Tests_*`、`UnityClientValidation/` | 隔离验证工程及临时工作目录 |

## 内容维护

当前运行规则以 `UnityClient/Assets/_Project/Core`、`Content` 和 `Resources/Data` 为准。共享 CSV 中有沿用的配置与资料，不能将旧表格中的船只、船员数量直接当作客户端现状。

`outputs/routes_v2`、`outputs/balance_configs` 以及共享美术继续用于内容维护。部件源表通过 `work/cargo_config_builder/sync_component_catalog_to_unity.mjs` 同步到 Unity。修改规则后运行相关 EditMode 测试；修改场景、交互或表现后运行对应 PlayMode 检查；发布后检查真实成品。

共享内容中的 `Legacy` 目录和类名属于仍在使用的资源、内容接口或兼容代码。清理文件时应检查实际引用，不按名称判断是否废弃。
