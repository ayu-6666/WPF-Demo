# MediaStudioVideo（MediaStudioWpf48）

WPF + MVVM + .NET Framework 4.8 的本地多媒体工作台，调用外部 FFmpeg 完成音视频/图片处理。共 12 个工具，支持中英文切换与浅色/深色主题。

## 快速开始

1. 解压发布包，确认 `ffmpeg.exe` 与 `MediaStudioWpf48.exe` 同目录；
2. 双击 `MediaStudioWpf48.exe`；
3. 左侧选工具 → 选择输入/输出 → 点右下角“开始处理”。

开发环境、编译与打包：

```powershell
# Debug 编译
& "C:\Tang\IDE\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" `
  "d:\tang\vs-workspace\WPF-Demo\MediaStudioVideo\MediaStudioWpf48.sln" `
  /m /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /v:minimal

# Release 编译 + 打包（生成 artifacts\*.zip）
.\Build-Package.ps1 -Configuration Release `
  -MSBuildPath "C:\Tang\IDE\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe"
```

## 文档

| 文档 | 面向 | 内容 |
| --- | --- | --- |
| [01-功能使用说明](docs/01-功能使用说明.md) | 最终用户 | 界面分区、按钮状态、12 个工具用法、格式与限制 |
| [02-开发与环境部署](docs/02-开发与环境部署.md) | 开发 / 部署 | 技术栈、目录结构、环境、调试、配置、排错 |
| [03-编译打包发布](docs/03-编译打包发布.md) | 构建 / 发布 | 编译与打包命令、产物结构、检查清单、清理约定 |

## 关键约定

- 改动仅限本模块；技术栈固定为 .NET Framework 4.8 + WPF + MVVM，不引入第三方 UI 框架。
- 颜色、圆角、字号、间距集中在 [App.xaml](App.xaml) 与 [ThemeManager.cs](Services/ThemeManager.cs)，换肤只改资源。
- 中英文文案集中在 [LocalizationManager.cs](Services/LocalizationManager.cs)，新增文案必须两套语言同时补齐。
- 打包暂存目录在压缩成功后自动删除，`artifacts\` 只保留最终 ZIP。

## 界面验证截图

| 截图 | 说明 |
| --- | --- |
| [verify_ui_default.png](verify_ui_default.png) | 主界面默认态（含禁用按钮） |
| [verify_ui_button_hover.png](verify_ui_button_hover.png) | 次按钮悬浮态 |
| [verify_ui_button_pressed.png](verify_ui_button_pressed.png) | 标题栏按钮按下态 |
| [verify_ui_button_disabled.png](verify_ui_button_disabled.png) | “开始处理”禁用态 |
| [verify_ui_list_hover.png](verify_ui_list_hover.png) | 工具列表行悬浮态 |
| [verify_ui_maximized.png](verify_ui_maximized.png) | 最大化态（图标切换为还原） |
| [verify_ui_restored.png](verify_ui_restored.png) | 还原态 |
