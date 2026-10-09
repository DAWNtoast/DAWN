# Windows 测试构建与成品启动验收

在独立 Unity 验证工程运行，避免锁定玩家正在使用的主工程。先通过 EditMode / PlayMode，再构建；不要把 UIPreviews 或 SampleScene 加入成品。

```powershell
& 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' `
  -batchmode -quit `
  -projectPath D:/Game/tmp/regional-ports-check-20260906 `
  -executeMethod ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine `
  -releaseOutput D:/Game/Builds/ShatteredSail-Windows-20260916/ShatteredSail.exe `
  -logFile D:/Game/UnityClient/Artifacts/ReleaseAudit20260916/Build.log
```

构建入口拒绝覆盖已有 exe，也拒绝非空输出目录，防止混入旧版文件。重建请使用新的输出目录。设置以 `try/finally` 恢复；只在构建时使用独立测试产品身份、窗口模式和 Mono Win64。输出旁的 `build-report.txt` 是 Unity BuildReport 摘要。通过 `Start-Process` 后台调用时使用 `-WindowStyle Hidden`。

成品验收使用实际 exe，不能拿 Editor 截图冒充成品：

```powershell
& 'D:/Game/Builds/ShatteredSail-Windows-20260916/ShatteredSail.exe' `
  -screen-fullscreen 0 -screen-width 1280 -screen-height 720 `
  -shatteredSailSmokeTest D:/Game/UnityClient/Artifacts/ReleaseAudit20260916/PlayerSmoke `
  -logFile D:/Game/UnityClient/Artifacts/ReleaseAudit20260916/Player.log
```

`-shatteredSailSmokeTest` 是显式只读验收开关：检查启动场景、资源、中文字体引用、输入系统、设置打开/关闭、绘制帧和打包场景；还用成品 Shader 离屏绘制四艘船的完整图块、镜像及不同转向，检查边缘透明和船体可见。写出截图与报告后以 0（通过）或 1（失败）退出。不会开始新游戏、应用设置或执行 GM，不写 PlayerPrefs。普通双击不创建此检查器。

隐藏窗口没有可读屏幕缓冲区时，成品中的真实主菜单 Canvas 通过当前渲染管线输出到 1920×1080 RenderTexture；这是打包后的 UI 渲染，不是 Windows 桌面截图，也不使用 Editor 预览素材。

完整游戏与 GM 交互由 PlayMode 回归覆盖；上述成品验收只证明该机器的启动/渲染/主菜单交互，不是全流程成品人工通关。

`Package.ps1` 的 ZIP 路径必须在构建目录之外，脚本会拒绝自包含路径及覆盖旧 ZIP。压缩结束后逐文件读取 ZIP 内数据并比对 SHA-256，确认完整运行依赖没有遗漏或损坏。

默认可执行文件名仍为 `ShatteredSail.exe`；如果发布包需要使用其他名称，可传入
`-ExecutableName`。例如第七港口 v1 的目录、程序和 ZIP 可以这样命名：

```powershell
& 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath D:/Game/UnityClient `
  -executeMethod ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine `
  -releaseOutput 'D:/Game/Builds/第七港口v1/第七港口v1.exe' `
  -logFile 'D:/Game/Builds/第七港口v1-build.log'

& 'D:/Game/tools/ReleaseBuild/Package.ps1' `
  -BuildDirectory 'D:/Game/Builds/第七港口v1' `
  -ZipPath 'D:/Game/Builds/第七港口v1.zip' `
  -ExecutableName '第七港口v1.exe'
```
