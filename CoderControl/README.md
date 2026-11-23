# CoderControl 代码查看器

基于 WPF（.NET 8）+ [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) + Roslyn 的轻量代码查看工具：
深色 IDE 风格界面，左侧解决方案文件树，右侧标签式代码预览，并内置一个 WPF 界面示例页。

![改动前后对比](images/compare-before-after.png)

- 上方：改动前（原始界面，系统默认标题栏、无菜单/工具栏/文件树）
- 下方：改动后（自绘标题栏 + 菜单 + 工具栏 + 解决方案资源管理器 + 标签文档区）
- 离线大图预览：[`docs/preview/index.html`](preview/index.html)（纯本地，双击打开）

---

## 1. 构建与运行

```powershell
cd CoderControl
dotnet build CoderControl.csproj -c Debug
.\bin\Debug\net8.0-windows\CoderControl.exe
```

### 离屏截图模式

```powershell
.\bin\Debug\net8.0-windows\CoderControl.exe --render <输出目录>
```

该模式把窗口放到屏幕外（不弹任务栏），依次渲染并保存 4 张界面截图，便于在无法截屏的环境中生成文档素材：

| 文件 | 内容 |
| --- | --- |
| `after-main.png` | 默认状态（3 个初始标签） |
| `after-maximized.png` | 最大化状态（验证还原图标与边缘补偿） |
| `after-sample.png` | 切到 Sample 示例页 |
| `after-empty.png` | 关闭全部标签后的空状态 |

---

## 2. 目录结构

```text
CoderControl/
├── App.xaml / App.xaml.cs              # 启动入口；--render 离屏截图
├── MainWindow.xaml / .xaml.cs          # 标题栏、菜单、工具栏、三栏布局与全部命令
├── Themes/DarkTheme.xaml               # 深色主题：颜色、菜单/滚动条/按钮/标签/文件树模板
├── Models/
│   ├── FileNode.cs                     # 文件树节点（属性通知、图标颜色、懒加载标记）
│   ├── DocumentTab.cs                  # 文档标签（标题/路径/语言/编码/内容/提示）
│   └── SourceCodeModel.cs              # 初始标签配置（Xaml / CSharp / Sample）
├── Views/
│   ├── SolutionExplorer.xaml / .xaml.cs # 解决方案资源管理器
│   ├── CodeViewer.xaml / .xaml.cs       # 标签文档区 + AvalonEdit + 空状态
│   ├── BreatheLight.xaml / .xaml.cs     # 呼吸灯头像控件
│   └── BreatheLightExample.xaml / .cs   # Sample 示例页（直播卡片）
└── docs/
    ├── images/                          # 本文档使用的截图
    └── preview/index.html               # 离线预览页（双击打开）
```

---

## 3. 界面与功能

### 3.1 标题栏（34px，自绘）

- `WindowStyle="None"` + `WindowChrome`（CaptionHeight=34、ResizeBorderThickness=6）保留**拖动、双击最大化/还原、边框缩放**等系统行为。
- 左侧为 `{}` 代码图标 + 当前文档标题（随标签切换更新，如 `Xaml - CoderControl`）。
- 右侧三个窗口按钮使用 Segoe MDL2 字形：最小化 `E921`、最大化 `E922`、还原 `E923`、关闭 `E8BB`。
- 悬浮/按下有独立背景色反馈；关闭按钮悬浮为 `#E81123` 红色并转白字。
- 最大化后：图标切换为还原、ToolTip 与 `AutomationProperties.Name` 同步为“还原”，“窗口”菜单项文字同步为“还原窗口”。

### 3.2 菜单栏（13 个主菜单）

| 菜单 | 主要功能 |
| --- | --- |
| 文件(F) | 新建、打开、重新加载、关闭标签、关闭所有标签、退出 |
| 编辑(E) | 撤销/重做、剪切/复制/粘贴、全选 |
| 搜索(S) | 查找、查找下一个/上一个、复制当前文件路径 |
| 视图(V) | 显示/隐藏解决方案资源管理器、全部展开/折叠、字号增减与重置 |
| 编码(N) | 以 UTF-8 / GBK 重新加载 |
| 语言(L) | 自动识别、C#、XAML/XML、JavaScript、纯文本 |
| 设置(T) | 自动换行、显示行号、字体大小子菜单 |
| 工具(O) | 格式化 C# 代码、去除行尾空格、打开模块目录 |
| 宏(M) | 一键格式化、删除所有空行 |
| 运行(R) | 运行示例页面、重新加载当前文件 |
| 插件(P) | 源码统计、复制文件路径 |
| 窗口(W) | 最小化、最大化/还原、关闭窗口 |
| 帮助(H) | 关于 CoderControl |

菜单样式（`DarkMenuItemStyle`）要点：

- 深色弹出层 `#1B1B1C`、1px 边框 `#3F3F46`、4px 圆角、投影、`PopupAnimation="Fade"`。
- 悬浮 `IsHighlighted` 与展开 `IsSubmenuOpen` 均高亮为 `#3E3E42`。
- 快捷键提示右对齐、灰色 `#8A8A8A`；分隔线为 1px `#3F3F46`。
- 含子菜单项右侧显示 `>` 箭头，子菜单从右侧弹出并带偏移。
- 可勾选项（自动换行、显示行号、解决方案资源管理器）左侧显示青绿色 `✓`。
- 禁用项文字变灰。

![文件菜单](images/menu-file.png)
![设置菜单（勾选与子菜单箭头）](images/menu-settings.png)

### 3.3 工具栏（8 个图标按钮）

新建、打开、重新加载、关闭当前标签、关闭所有标签、查找、运行示例、关于。
按钮 `30×26`，悬浮 `#3E3E42`、按下 `#4A4A4D`，分组之间用 1px 竖线分隔，全部带 ToolTip 与自动化名称。

### 3.4 解决方案资源管理器

- 解析 `IpanelWpf.sln`，只列出 `.csproj` 工程（跳过解决方案文件夹），根节点显示项目数量。
- 启动默认展开“解决方案 → CoderControl → Models / Views”，与参考图一致。
- **懒加载**：节点首次展开时才枚举子项；第二次直接复用。
- 过滤 `bin/obj/.git/.vs/packages/publish/node_modules/.tmp_ref` 等目录，只显示可预览的文本扩展名。
- 展开/折叠：18px 箭头，展开时 0.12s 旋转 90° 动画；文件节点隐藏箭头。
- 行高 25px、缩进 16px；文件夹与不同扩展名文件使用不同颜色图标（C# 紫色 `{}`、XAML 蓝色、工程绿色、配置黄色）。
- 悬浮 `#2A2D2E`、选中 `#094771` + 白字；滚动条为 10px 深色细滚动条。
- 点击文件节点 → 右侧打开/切换到对应文档；**同一节点重复点击也能重新打开**。

### 3.5 文档标签区

- 选中标签：背景 `#1E1E1E`、标题青绿色 `#4EC9B0`、底部 2px 蓝色下划线 `#007ACC`；
  未选中：`#2D2D30`，悬浮 `#333337`。
- 标签高 34px，标题左右 `14/10` 内边距，右侧 18×18 关闭按钮（悬浮红底白 X）。
- 关闭后自动选中相邻标签；关闭最后一个标签后显示空状态：`{}` 图标 + “没有打开的文档” + 操作提示。
- 打开文件按完整路径去重，已存在则仅切换，不重复创建标签。
- Sample 标签直接承载 `BreatheLightExample` 界面实例（4 张直播卡片 + 呼吸灯头像），切回即时可见。

![默认界面](images/after-main.png)
![Sample 示例页](images/after-sample.png)
![空状态](images/after-empty.png)

### 3.6 编辑器

- AvalonEdit：行号、自动换行、10/6 内边距、查询面板（`SearchPanel`）。
- 语法高亮：C#、XAML/XML、JavaScript/JSON、HTML、CSS、SQL、纯文本；C# 采用 VS 深色配色。
- 编码：自动识别 BOM，支持按 UTF-8 / GBK(936) 重新加载。
- 工具：Roslyn `NormalizeWhitespace` 格式化、去行尾空格、删空行、源码统计、复制路径。
- 单文件超过 4MB 时跳过预览并提示。

### 3.7 快捷键

| 快捷键 | 功能 |
| --- | --- |
| Ctrl+N / Ctrl+O | 新建 / 打开文件 |
| Ctrl+W / Ctrl+Shift+W | 关闭当前标签 / 关闭所有标签 |
| Ctrl+F、F3、Shift+F3 | 查找、下一个、上一个 |
| F5 / F6 | 重新加载当前文件 / 运行示例页面 |
| Ctrl+= / Ctrl+- | 增大 / 减小字号 |
| Ctrl+Alt+L | 显示/隐藏解决方案资源管理器 |

---

## 4. 本轮更新记录（含改动理由）

### 4.1 顶部菜单：补全功能 + 重做样式

**改动**

- 新增完整 13 个主菜单及全部子项（见 3.2），并为每个命令接上实际处理器（窗口、编辑器、视图、编码、语言、字体、格式化等）。
- 新增 `Themes/DarkTheme.xaml` 中的 `DarkMenuItemStyle`、`DarkMenuSeparatorStyle`，替换系统浅色菜单模板。
- 按 `MenuItem.Role`（TopLevelHeader / SubmenuHeader / SubmenuItem）区分内边距、最小宽度、`InputGestureText` 显示、子菜单箭头与弹出方向。
- 增加勾选标记（`IsCheckable/IsChecked`）、悬浮/展开高亮、禁用置灰、Fade 弹出与投影。

**理由**

- 原界面完全没有菜单，功能只能靠代码入口，用户无法发现；参考图为 Notepad++/VS 风格，需要菜单承担全部操作入口。
- 系统默认 MenuItem 模板在深色背景下是浅灰底 + 无圆角，与 IDE 深色主题割裂，必须整模板重绘才能统一配色和交互反馈。
- 用 `Role` 分派而非三套模板，避免重复代码，同时保证顶级菜单、子菜单父项、普通子项三种形态各自正确。

### 4.2 窗口按钮：图标、状态与行为

**改动**

- 标题栏改为 34px 自绘，使用 `WindowChrome` 保留系统拖动/双击最大化/缩放；按钮加 `IsHitTestVisibleInChrome="True"` 保证可点击。
- 三个按钮使用 Segoe MDL2 图标，增加悬浮/按下反馈与关闭红色悬浮态。
- `Window_StateChanged` 中同步：最大化图标 `E923`、ToolTip/自动化名称“还原”、菜单项“还原窗口”。
- 新增 `UpdateMaximizePadding`：最大化时读取窗口实际矩形与监视器工作区，把差值补偿到根容器 `Margin`。

**理由**

- 原界面使用系统标题栏，样式无法与深色主题统一，也没有图标级悬浮反馈。
- WindowChrome 最大化时窗口会向屏幕外扩 7px（实测 `x=-7,y=-7,1934×1214`），不补偿会导致标题栏/内容被屏幕边缘裁切。
- 最大化后按钮必须切换为“还原”语义，否则图标与行为不一致；同步自动化名称便于 UI 自动化测试读取。

### 4.3 左侧文件树：样式与交互

**改动**

- 新建 `SolutionExplorer` + `FileNode`：解析解决方案、懒加载、默认展开、过滤目录与扩展名。
- 新建 `DarkTreeViewItemStyle`：25px 行高、16px 缩进、箭头旋转动画、悬浮/选中态、文件节点隐藏箭头。
- 新建 `DarkScrollBarStyle` 统一深色细滚动条；节点图标按类型着色。
- 点击文件触发 `FileOpenRequested` → `CodeViewer.OpenFile`（按路径去重）。
- `PreviewMouseLeftButtonUp` 补充处理“已选中节点再次点击”：`SelectedItemChanged` 不会重复触发，命中容器后主动打开。

**理由**

- 原界面左侧是一块无意义的青绿色区域，没有文件导航能力。
- 树控件默认模板在深色下选中为系统蓝、无缩进参考线、无展开动画，与参考图差距大。
- 默认展开常用目录可减少首次点击成本；白名单 + 排除目录避免把二进制产物铺满树。
- 去重保证“已存在则切换”，重复点击修复保证用户直觉行为一致。

### 4.4 右侧标签：标题、布局、关闭与示例页

**改动**

- `DocumentTabItemStyle`：选中/未选中/悬浮三态、蓝色下划线、关闭按钮样式。
- 标签头改为 34px、左右 14/10 间距；关闭按钮 18×18、悬浮红底白 X。
- `CloseTab` 关闭后自动选中相邻标签；`UpdateEmptyState` 在 0 标签时显示空状态并隐藏 TabControl。
- Sample 标签创建 `BreatheLightExample` 实例；`OpenSample` 按 `sample://` 键去重。
- 修复 Sample 页空白/头像重叠：设置 `ItemsSource`、动态生成渐变头像、`BreatheLight` 由 StackPanel 改 Grid。

**理由**

- 原标签标题直接使用默认 `TabItem`，选中态灰白、无下划线，关闭 X 只是文字，视觉与参考图不符。
- 关闭最后一个标签后若不移除 TabControl，会留下空白灰块；空状态给用户明确下一步提示。
- 示例页需要真实承载一个 XAML 界面对象才能满足“点击 Sample 显示对应界面内容”的要求，且不能依赖外部图片资源。

### 4.5 其他修复

- `App.xaml` 移除 `StartupUri`：运行时把 `StartupUri` 置空会抛异常导致启动崩溃，改为 `OnStartup` 手动创建主窗口。
- `SourceCodeModel.ImageSource` 改为 `ImageSource?`，直接承载运行时生成的图像。
- `BreatheLight` 去掉调试用 `Background="LightGray"`，恢复透明背景。

---

## 5. 异常问题记录

| # | 现象 | 原因 | 处理 |
| --- | --- | --- | --- |
| 1 | 启动即崩溃 `0xE0434352` | `App.xaml` 的 `StartupUri` 在代码中置 `null` 不被允许 | 删除 `StartupUri`，`OnStartup` 手动 `new MainWindow().Show()` |
| 2 | XAML 编译报 `GridSplitter` 不是 `ContentControl` | `GridSplitter` 不支持内容 | 分割线改为独立 1px `Border`（不参与命中测试）+ 透明 `GridSplitter` |
| 3 | 找不到 `TextEditor` | 缺少 AvalonEdit 命名空间 | 补 `using ICSharpCode.AvalonEdit;` |
| 4 | 找不到 `NormalizeWhitespace` | 扩展方法在 `Microsoft.CodeAnalysis` | 补 `using Microsoft.CodeAnalysis;` |
| 5 | 文件树默认目录不展开 | `FileNode` 无属性通知，容器先以 `false` 建立绑定后不更新 | `FileNode` 实现 `INotifyPropertyChanged` |
| 6 | 最大化后窗口四周被裁切 | WindowChrome 最大化外扩约 7px | `GetWindowRect` + 监视器工作区计算 `RootBorder.Margin` 补偿（含 DPI 换算） |
| 7 | Sample 页空白、头像堆在一起 | 未设置 `ItemsSource`；`BreatheLight` 用 `StackPanel` 导致子元素重叠 | 构造时填充 4 条示例数据并生成渐变头像；`StackPanel` 改为 `Grid` |
| 8 | UI 自动化读到的节点名是类型名 | `FileNode`/`DocumentTab` 未重写 `ToString` | 重写 `ToString`，并为无文本按钮补 `AutomationProperties.Name` |
| 9 | 已选中的树节点再次点击无反应 | 选中未变化不触发 `SelectedItemChanged` | `PreviewMouseLeftButtonUp` 命中容器后再次触发打开 |
| 10 | 可勾选菜单项一直显示对勾 | 模板未处理未勾选状态 | `IsCheckable=True` 时默认 `Opacity=0`，`IsChecked=True` 再置 1 |
| 11 | 树展开箭头动画无效 | 动画目标不在同一模板 namescope | 动画移入 `ToggleButton` 模板内部，绑定 `TemplatedItem.IsExpanded` 双向 |
| 12 | 锁屏状态下全屏截图纯白 | 锁屏截不到应用窗口 | 增加 `--render` 离屏 `RenderTargetBitmap`；弹层用 `PrintWindow` 单独抓取 |

---

## 6. 截图索引

离线预览页：[`docs/preview/index.html`](preview/index.html)（纯本地、双击打开，含大图与图注）。

| 文件 | 说明 |
| --- | --- |
| [`images/compare-before-after.png`](images/compare-before-after.png) | 前后对比（上：改动前，下：改动后，每张 1600px 宽） |
| [`images/before.png`](images/before.png) | 改动前原始界面 |
| [`images/after-main.png`](images/after-main.png) | 改动后默认界面（1600×1000） |
| [`images/after-maximized.png`](images/after-maximized.png) | 最大化（还原图标、边缘补偿） |
| [`images/after-sample.png`](images/after-sample.png) | Sample 示例页（1600×1000） |
| [`images/after-empty.png`](images/after-empty.png) | 关闭全部标签后的空状态 |
| [`images/menu-file.png`](images/menu-file.png) | 文件菜单（快捷键、分隔线） |
| [`images/menu-settings.png`](images/menu-settings.png) | 设置菜单（勾选、子菜单箭头） |

---

## 7. 验证结果

UI Automation 实测（真实进程，非离屏）：

- 初始标签为 `Xaml / CSharp / Sample`。
- 点击树节点 `DocumentTab.cs` → 新增标签；切到 Sample 后再次点击同一文件 → 标签数仍为 4（去重生效）。
- Sample 页文本包含“产品发布会直播 / 正在直播 / WPF 动画技术分享”等卡片内容。
- 点击标签内 X 关闭 `DocumentTab.cs` → 剩 3 个标签且选中相邻标签。
- “文件 → 关闭所有标签” → 标签数 0，空状态“没有打开的文档”出现。
- 最大化：`WindowVisualState=Maximized`，窗口按钮自动化名称变为“还原”。

已知限制：

- 锁屏状态下无法模拟鼠标悬浮，菜单/树/标签的悬浮配色通过样式触发器与 `PrintWindow` 弹层截图核对，建议解锁后人工复核一次。
- 拖动标题栏、双击标题栏最大化依赖 `WindowChrome` 原生行为，需人工操作确认手感。
- `PrintWindow` 抓取的弹层不包含窗口投影（阴影为 `AllowsTransparency` 合成层）。
