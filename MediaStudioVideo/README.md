# MediaStudioVideo（MediaStudioWpf48）

本地多媒体工作台：WPF + MVVM + .NET Framework 4.8，调用随包 FFmpeg 完成音视频/图片处理。共 12 个工具，支持中英文切换与浅色/深色主题，全部处理在本机完成，不上传任何文件。

- 技术栈：.NET Framework 4.8 / WPF / MVVM（手写 `RelayCommand` + `INotifyPropertyChanged`）
- 依赖：仅系统程序集 + `ffmpeg.exe`，无 NuGet、无第三方 UI 框架、无需联网还原
- 语言版本：`LangVersion=7.3`（不要使用 C# 8+ 语法）

---

## 1. 项目背景

日常的转码、抽音轨、拼接、截取、去水印、图片转视频等需求，用命令行操作 FFmpeg 参数多、易出错，也不方便批量复用。本项目把这些高频操作收敛成统一的图形界面：

- 工具化：每个场景一个菜单项，参数只暴露必要字段；
- 本地化：文件不出本机，适合内网/离线环境；
- 可发布：单 exe + FFmpeg，解压即用，便于分发到测试机与现场；
- 可维护：颜色、字号、文案、FFmpeg 参数分别集中管理。

---

## 2. 快速开始

1. 解压发布包，确认 `ffmpeg.exe` 与 `MediaStudioWpf48.exe` 在同一目录；
2. 双击 `MediaStudioWpf48.exe`；
3. 左侧选择工具 → 选择输入 / 输出 → 按需填参数 → 点击右下角“开始处理”。

---

## 3. 功能概览

| 工具 | 说明 | 输入 | 默认输出 |
| --- | --- | --- | --- |
| 音乐提取 | 从视频导出 MP3 音轨（libmp3lame -q:a 2） | 视频文件 | `.mp3` |
| 添加背景音乐 | 循环背景音乐、音量 0.25，按视频时长截断 | 视频 + 音频 | `.mp4` |
| 视频拼接 | 按文件名顺序拼接同规格视频（concat） | 视频文件夹 | `.mp4` |
| 视频剪辑 | 按开始时间与时长截取并重编码 | 视频文件 | `.mp4` |
| M3U8 转 MP4 | 下载 HLS 流并封装 MP4 | `.m3u8` 地址/文件 | `.mp4` |
| 文件夹图片合成视频 | 图片序列按 `1/帧率` 停留合成无声视频 | 图片文件夹 | `.mp4` |
| 视频去码 / 去水印 | `delogo` 修补指定矩形区域 | 视频文件 | `.mp4` |
| 视频画质增强 | 降噪 + 2 倍 Lanczos 放大 + 锐化 | 视频文件 | `.mp4` |
| 视频提取 GIF | 调色板方案生成 480px 循环 GIF | 视频文件 | `.gif` |
| 旋转 / 水平翻转 | `hflip` / `vflip` / `transpose` | 视频文件 | `.mp4` |
| 图片去码 / 去水印 | 图片矩形区域修补 | 图片文件 | `.png` |
| 图片高清增强 | 2 倍 Lanczos 放大 + 锐化 | 图片文件 | `.png` |

---

## 4. 本次功能修复

| # | 现象 | 根因 | 修复方案 |
| --- | --- | --- | --- |
| 1 | 基于图片文件夹生成视频失败：`Error opening input file ...\*.jpg`、`Function not implemented` | Windows 版 FFmpeg 不支持 `-pattern_type glob` 通配符输入 | 改为 **concat demuxer + 图片清单**：枚举 `.jpg/.jpeg/.png/.bmp/.webp`，按文件名排序，每项写入 `file` 与 `duration=1/帧率`，末尾重复最后一帧；补充偶数尺寸 `scale`，无图片时弹出提示 |
| 2 | 输入框/输出框的灰色占位提示与输入内容叠加显示 | 占位 `TextBlock` 的可见性依赖 `ControlTemplate.Triggers` 中绑定 `TemplatedParent.Text` 的 `DataTrigger`，实际未随文本更新 | 改为占位元素自身 `Style.Triggers`，绑定 `AncestorType=TextBox` 的 `Text`；仅空串 / null 时显示占位 |
| 3 | 文件选择框不区分当前工具，视频工具也会列出图片 | 所有工具共用一套 `PickMedia` 过滤串 | 按工具分类：视频类工具只列视频格式，图片类工具只列图片格式；输出保存框按工具给 `.mp4/.mp3/.gif/.png` 过滤 |
| 4 | 日志无法区分错误、追加后不滚动、“摘要”模式丢失关键信息 | 日志是单个 `TextBox.Text` 字符串拼接，不支持着色与滚动 | 改为 `ObservableCollection<LogEntry>` + `ListBox` 模板：错误行红色、成功行绿色；`CollectionChanged` 后自动 `ScrollIntoView` 到底部；“摘要”模式仍保留错误行；上限 2000 条防止内存膨胀 |
| 5 | 原生 `MessageBox` 与界面风格不统一 | 系统消息框不跟随主题 | 新增自定义弹框：成功（绿色勾）、警告（黄色三角 + 感叹号）、错误（红色 × + 信息），图标取主题色，支持 Enter / Esc / 确定关闭，居中于主窗口 |
| 6 | 新增的 XAML/C# 文件未参与编译 | 老式（非 SDK）csproj 需手工登记 | 在 `MediaStudioWpf48.csproj` 中登记 `Models\LogEntry.cs`、`Views\MessageDialog.xaml(.cs)` |

> 本次仅修改脚本拼装、日志与界面交互相关代码，未改动其他模块。

---

## 5. 开发说明

### 5.1 目录结构

```text
MediaStudioVideo/
├─ App.xaml / App.xaml.cs        # 设计令牌、控件样式、启动与全局异常
├─ MainWindow.xaml(.cs)          # 主界面、标题栏、日志自动滚动
├─ Commands/RelayCommand.cs      # 轻量 ICommand
├─ Models/
│  ├─ ToolItem.cs                # 工具项与本地化
│  └─ LogEntry.cs                # 日志条目（文本/错误/成功）
├─ ViewModels/MainViewModel.cs   # 状态、参数拼装、任务执行、日志集合
├─ Views/MessageDialog.xaml(.cs) # 自定义消息弹框
├─ Services/
│  ├─ FfmpegService.cs           # 查找/执行 ffmpeg，日志带错误标记
│  ├─ LocalizationManager.cs     # 中英文词典 + Loc 标记扩展
│  └─ ThemeManager.cs            # 浅色/深色主题画刷
├─ docs/                         # 使用 / 开发 / 打包文档
├─ Build-Package.ps1             # 编译 + 打包脚本
└─ MediaStudioWpf48.sln / .csproj
```

### 5.2 关键约定

- 老式 csproj：新增 `Page` / `Compile` 必须手工登记，否则不参与编译与打包；
- `LangVersion=7.3`：不要使用 `using var`、switch 表达式、可空引用类型等 C# 8+ 语法；
- 颜色与尺寸统一放在 `App.xaml`，运行时主题覆盖在 `ThemeManager`（新增颜色键需两套主题同时补齐）；
- 文案统一放 `LocalizationManager`，新增键必须同时补 `zh-CN` 与 `en-US`；
- FFmpeg 参数集中在 `MainViewModel.Args()`，参数显隐集中在 `UpdateToolState()`；
- FFmpeg 执行期间的中间文件（concat/图片清单）记录在 `_tempFiles`，任务结束清理；
- 日志错误判定在 `FfmpegService.LooksLikeError()`，仅用于界面着色，不影响任务结果。

---

## 6. 构建与部署

### 6.1 单模块编译与打包

```powershell
# Debug 编译
& "C:\Tang\IDE\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" `
  "d:\tang\vs-workspace\WPF-Demo\MediaStudioVideo\MediaStudioWpf48.sln" `
  /m /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /v:minimal

# Release 编译 + 打包（生成 artifacts\*.zip）
.\Build-Package.ps1 -Configuration Release `
  -MSBuildPath "C:\Tang\IDE\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe"
```

### 6.2 仓库全量构建

在仓库根目录双击或执行 `build_all.bat`，脚本会依次编译并打包 19 个模块（含本模块），产物统一放在各模块/根目录的 `artifacts` 下。

### 6.3 运行要求

- Windows 10 1809+ / Windows 11，已安装 .NET Framework 4.8 Runtime；
- `ffmpeg.exe` 与主程序同目录，或通过环境变量 `FFMPEG_PATH` 指向 exe / 所在目录，或加入 `PATH`；
- 发布包内已附带 `ffmpeg.exe`，解压即可运行。

### 6.4 手动部署清单

| 项目 | 要求 |
| --- | --- |
| 必需文件 | `MediaStudioWpf48.exe`、`MediaStudioWpf48.exe.config`、`ffmpeg.exe` |
| 可选文件 | `ffprobe.exe`、`ffplay.exe` |
| 不可分发 | `startup-error.log`、`bin/`、`obj/`、打包暂存目录 |
| 校验 | 启动后标题为 `Media Studio`，跑一次“音乐提取”确认输出非 0 字节 |

---

## 7. 异常问题总结

| 现象 | 根因 | 处理方式 |
| --- | --- | --- |
| `Error opening input file ...\*.jpg` / `Function not implemented` | Windows 版 FFmpeg 不支持 glob 通配符输入 | 已改为 concat 清单方案，见第 4 节第 1 条；若旧包仍报错，请更新到本次版本 |
| 启动即提示找不到 FFmpeg | exe 目录无 `ffmpeg.exe` 且未配置 `FFMPEG_PATH` | 放置 `ffmpeg.exe` 到同目录，或设置 `FFMPEG_PATH` 后重启程序 |
| FFmpeg 退出码非 0 | 参数不兼容、编码/容器不匹配、源文件损坏 | 查看日志中的红色错误行；确认输出扩展名与编码匹配（如 `.gif` 需 GIF 编码、`.mp3` 需音频源） |
| 输出文件为 0 字节或中途中断 | 输出目录不存在 / 无写权限 / 磁盘空间不足 | 先在资源管理器中确认目录可写，再重试 |
| 中文或含空格路径处理失败 | 路径未正确加引号 | 程序会自动为输入/输出加引号；若手动粘贴，请保留完整路径，不要自行加引号 |
| 图片合成后时长或顺序不对 | 文件名排序为字符串升序；每张图停留 `1/帧率` 秒 | 统一命名为 `001.jpg`、`002.jpg`；帧率越高单张停留越短 |
| 图片合成报尺寸/参数不一致 | 同一文件夹混入不同分辨率图片 | 先统一尺寸再合成 |
| 日志中文乱码 | 控制台编码不一致 | 服务已按 UTF-8 读取 FFmpeg 输出；如外部替换 FFmpeg 版本仍乱码，优先使用随包版本 |
| 弹框看起来“没有出现” | 弹框为模态窗口并居中于主窗口，可能被其他置顶窗口遮挡 | 使用 Alt+Tab 切回 `Media Studio`，或最小化其他窗口 |
| 打开程序立即退出 | 启动异常 | 查看 exe 同目录 `startup-error.log`，按堆栈定位 |
| 新增界面文件不生效 | 老式 csproj 未登记文件 | 在 `MediaStudioWpf48.csproj` 的 `ItemGroup` 中补 `Page` / `Compile` 后重新编译 |

---

## 8. 文档导航

| 文档 | 面向 | 内容 |
| --- | --- | --- |
| [01-功能使用说明](docs/01-功能使用说明.md) | 最终用户 | 界面分区、按钮状态、12 个工具用法、格式与限制 |
| [02-开发与环境部署](docs/02-开发与环境部署.md) | 开发 / 部署 | 技术栈、目录结构、环境、调试、配置、排错 |
| [03-编译打包发布](docs/03-编译打包发布.md) | 构建 / 发布 | 编译与打包命令、产物结构、检查清单、清理约定 |

界面布局与交互细节见 `docs/01-功能使用说明.md` 第 2、3 节；实际外观以本机运行为准。
