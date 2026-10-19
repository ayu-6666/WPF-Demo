# RebootHMI — 自动化设备 HMI（WPF 复刻）

基于 **WPF + MVVM + .NET Framework 4.8 + MaterialDesignInXamlToolkit 5.2.1**，按参考截图 1:1 复刻的深蓝科技风自动化设备人机界面。

## 技术栈

| 项 | 说明 |
|---|---|
| 框架 | WPF / .NET Framework 4.8（SDK 风格 csproj） |
| 模式 | MVVM（手写 ObservableObject / RelayCommand，无第三方 MVVM 框架） |
| UI 库 | MaterialDesignThemes 5.2.1 + MaterialDesignColors 5.2.1（仅用 PackIcon 图标，主题样式全部自定义） |
| 语言 | C# 8.0 / XAML |

## 界面清单（10 个截图页面 + 外壳）

| 模块 | Key | 还原要点 |
|---|---|---|
| 登录 | `login` | 星空背景、科幻切角登录卡、帐户密码/手机号双 Tab、白色发光输入框、发送验证码、自动登录、忘记密码 |
| 报警 | `alarm` | 故障查询（标签 chips/搜索/料道·望道·AGV·相机列表）、运行日志三色时间线、报警信息红色故障条 + 警灯、日期虚线下拉 |
| 自动模式 | `auto` | 抓手控制（转角缸/防坠落/插销缸）、拆机盘控制（上提/下提/托举/挡停）、料道控制白色下拉（未完成红字）、手控 NG 料道取盘/送盘 |
| 位置参数-工位 | `pos_station` | 工位一/二/三切换、左右切换箭头、X/Y/Z/C 轴位置滑杆(0–136m)、速度滑杆 + 数字微调框、确定按钮 |
| 位置参数-缓存 | `pos_buffer` | OP50/OP60/OP70 缓存台，结构与工位页一致 |
| 位置参数-上料道 | `pos_feed` | 上料道一/二/三 |
| 位置参数-下料道 | `pos_discharge` | 下料道一/二/三（经底部导航子菜单进入） |
| IO-手抓-机器人 | `io_gripper` | 手抓信号 9 行双列、拍照机器人/抓放机器人各 9 项、列间竖向 chevron 装饰 |
| IO-进料道-拆机盘 | `io_feedtray` | 进料道信号 10 行、拆机盘信号 8 行 |
| IO-下料道信号 | `io_discharge` | 下料道控制 10 行、实时-下料道 5 行、实时-进料道表单（拍照状态/外协码/手动读码·输入·备用） |
| IO-NG料道-人检区 | `io_ng` | NG 料道 6 行、人检区 7 行、相机信号三列 4 行 |

主外壳：顶部 Logo + 实时时钟 + 居中“自动化设备”发光科技标题 + 用户/电源按钮；左侧开始/暂停/停止/急停/复位操作栏（可点击切换运行/暂停/急停状态，颜色联动）；底部 7 项胶囊导航栏（位置参数、IO 信号带弹式子菜单）。

## 目录结构

```
RebootHMI/
├─ RebootHMI.csproj            # net48 + UseWPF + MaterialDesignThemes 5.2.1
├─ App.xaml / App.xaml.cs      # 全局资源合并、未处理异常弹窗
├─ Themes/
│  ├─ Colors.xaml              # 深蓝色板、渐变画刷、星空点阵 TileBrush
│  ├─ Controls.xaml            # 全部样式与模板（卡片/按钮/开关/信号灯/滑杆/下拉/导航…）
│  └─ IoTemplates.xaml         # IO 行（两列/三列）共享 DataTemplate
├─ Core/
│  ├─ ObservableObject.cs
│  ├─ RelayCommand.cs
│  └─ Converters.cs            # 相等/取反/空值/前缀/标题图标等转换器
├─ Controls/
│  ├─ SciCard.cs               # 科技卡片：青色描边 + 四角折线 + 三点切角标题栏
│  └─ NumberUpDown.cs          # 速度设定数字微调框
├─ Models/Models.cs            # NavItem/OpButtonModel/IoItem/IoRow/IoColumn/ToggleItem/AxisData…
├─ ViewModels/
│  ├─ MainViewModel.cs         # 时钟、登录态、导航、子菜单、设备运行状态
│  ├─ LoginViewModel.cs
│  ├─ AlarmViewModel.cs
│  ├─ AutoModeViewModel.cs
│  ├─ PositionViewModel.cs     # 工位/缓存/上/下料道四页共用
│  ├─ IoViewModels.cs          # 4 个 IO 页面 ViewModel
│  └─ PlaceholderViewModel.cs
├─ Views/                      # MainWindow + 10 个页面 UserControl
├─ dist/publish/               # 发布产物
└─ dist/RebootHMI-v1.0.0.zip   # 发布压缩包
```

## 构建与发布

要求：Visual Studio 2022（含 .NET 桌面开发）或 .NET SDK（可构建 net48 SDK 工程）。

```bat
:: 1. 还原依赖
msbuild RebootHMI\RebootHMI.csproj /t:Restore

:: 2. Release 编译
msbuild RebootHMI\RebootHMI.csproj /p:Configuration=Release

:: 3. 发布（独立文件夹 + zip）
dotnet publish RebootHMI\RebootHMI.csproj -c Release -o RebootHMI\dist\publish
powershell Compress-Archive RebootHMI\dist\publish\* RebootHMI\dist\RebootHMI-v1.0.0.zip
```

或直接在工作区根目录双击 `build_all.bat`，一键构建并打包全部 13 个工程模块。

## 操作说明

1. 启动后进入登录页，输入任意手机号和验证码（非空校验）点击“登录”进入主界面。
2. 底部导航：手势=手动控制（占位）、齿轮=自动模式、勾选=位置参数（弹子菜单）、列表=辊道（占位）、警灯=报警、小车=组件（占位）、齿轮设置=IO 信号（弹子菜单）。
3. 左侧操作栏可模拟设备状态：开始（绿）/暂停（黄）/急停（红）/停止/复位。
4. IO 信号灯、气缸开关均可点击切换；位置参数滑杆、数字微调、Tab 切换均可交互。
