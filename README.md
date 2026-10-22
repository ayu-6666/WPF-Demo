# WPF-Demo
## 1 Getting Project

> git clone https://github.com/ayu-6666/WPF-Demo.git
>
> cd WPF-Demo
>
> git checkout .
>
> git pull
>
> git checkout -b dev origin/master



## 2 Build Project

> WaferApp
<img width="1086" height="793" alt="image" src="https://github.com/user-attachments/assets/a01c06f3-5586-42ee-beff-84fb949d2ad1" />

- Cmp.UTools.GUI
<img width="944" height="841" alt="image" src="https://github.com/user-attachments/assets/1164f3d4-d094-4ffa-b643-6fac075c5a86" />

- BitTomrrorApp
<img width="960" height="840" alt="image" src="https://github.com/user-attachments/assets/35e2e662-3b56-40dd-bc36-9343153cfda7" />

- Radae3DDemo3
<img width="984" height="841" alt="image" src="https://github.com/user-attachments/assets/f68be148-9e08-483f-9fdf-c529f82af6a1" />

- WpfDevDemo
<img width="1198" height="798" alt="image" src="https://github.com/user-attachments/assets/5f1a84ce-c869-4cea-8206-36ae5193154f" />

- CoordinateFunction
<img width="1384" height="851" alt="image" src="https://github.com/user-attachments/assets/7c1d4d69-dbab-4626-9aa2-cc9d22ac60dd" />

- Tang.RadarApplication
<img width="1264" height="811" alt="image" src="https://github.com/user-attachments/assets/2d4d7b13-7113-47ef-8eea-c6e56def7775" />
<img width="1184" height="751" alt="image" src="https://github.com/user-attachments/assets/88001f43-9604-4830-a9a5-fef760026ba5" />
<img width="984" height="691" alt="image" src="https://github.com/user-attachments/assets/c1de4f5d-c458-4047-8e9f-0088b5fde005" />

- 机器人
<img width="1918" height="1072" alt="image" src="https://github.com/user-attachments/assets/4ab08d3e-dc24-4061-9031-7ae2004e69d9" />
<img width="1917" height="1070" alt="image" src="https://github.com/user-attachments/assets/511279a1-b321-4547-8f5f-9f1e2ce2a152" />


- 电源检测
<img width="1920" height="1090" alt="image" src="https://github.com/user-attachments/assets/70ee51ed-4619-4281-8538-98bab42f3e8f" />
<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/c39a6b6d-ded8-45e5-a96c-cb0beab57559" />


- Linux运维系统
  <img width="1920" height="1200" alt="image" src="https://github.com/user-attachments/assets/8403e5f3-ad64-4743-a747-6112ef8ab620" />
  <img width="1920" height="1169" alt="image" src="https://github.com/user-attachments/assets/3b9fb8ab-3f2e-45eb-9b30-aa83ec9a1b6c" />



## 3 Rxbit.Calibration

工业设备标定 / CNC 运动控制 HMI。深灰底 `#1C1F2E` + 紫蓝渐变主色调（`#8E7CFF → #6366F1 → #3B82F6`），哑铃形双色渐变时间球（左紫 `#A78BFA` / 右浅灰蓝 `#E6EDF8`），iOS 风格 ToggleSwitch，左侧图标导航栏 + 底部实时位置状态栏。

| 技术栈 | 版本 |
|---|---|
| .NET Framework | 4.8 |
| WPF SDK | .NET SDK-style csproj |
| MaterialDesignInXamlToolkit | 5.2.1 |
| MVVM | 轻量自实现（ObservableObject / RelayCommand） |
| 编译器 | VS 2022 MSBuild |

**已实现页面（均已 1:1 复刻参考截图）**

1. **主页（Dashboard）** — 哑铃形时间球（世界时间 12:17 / 北京时间 20:17）、输入/输出界面 iOS 开关、报警信息卡（数量/代码）、监视界面 + 运行模式渐变按钮
2. **输出界面** — 左右双表（Y00~Y08）、有效勾选 + iOS 开关、翻页箭头
3. **手动参数** — 6 轴参数卡（X/Y/Z/A/B/C：回零速度、近点速度、回零 Acc、零点偏移）、右侧轴手动控制（+/- 步进 + 数值框）
4. **自动参数** — 6 轴参数卡（P1 速度、Acc、Dec）
5. **授权弹窗** — 请求码 4 格 + 授权码 5 格 + 获取请求码渐变按钮（主窗口 Overlay）
6. **左侧导航栏** — 12 项（主页/设置/用户/权限/通信/输出界面/IO/输入界面/手动参数/自动参数/报警/日志），选中态紫线 `#7B61FF` + 背景 `#2A2D42`
7. **底部状态栏** — X/Y/Z/A/B 实时位置（mm）+ 主轴转速

**目录结构**
```
Rxbit.Calibration/
├─ Rxbit.Calibration.csproj        # SDK-style .NET Framework 4.8 + WPF
├─ App.xaml / App.xaml.cs          # 合并 Themes/Colors + Themes/Controls
├─ Controls/
│  ├─ SciCard.cs                   # 深灰扁平卡片容器（CornerRadius DP）
│  └─ ToggleSwitch.cs              # iOS 风格开关（IsOn DP + 点击切换）
├─ Core/
│  ├─ ObservableObject.cs          # MVVM 基类
│  ├─ RelayCommand.cs              # ICommand 实现
│  └─ Converters.cs                # BoolToVisibility / EqToBool / StringToVisibility
├─ Models/Models.cs                 # NavItem / IoRow / AxisManual / AxisAuto / ManualCtrlAxis / StatusBarInfo
├─ Themes/
│  ├─ Colors.xaml                   # 深灰底 + 紫蓝渐变 + 哑铃球画刷
│  └─ Controls.xaml                # SciCard / RunModeButton / IosToggleSwitch / SideNavItem /
│                                   # AuthDigit / AxisNumberBox / DarkComboBox / StatusChip / PagerArrow
├─ ViewModels/
│  ├─ MainViewModel.cs             # 时钟 + 导航 + 页面缓存 + 底部状态栏 + 授权弹窗
│  ├─ DashboardViewModel.cs        # 时间球绑定 + 开关 + 报警信息
│  ├─ OutputViewModel.cs           # 双表 9 行 IoRow
│  ├─ ManualParamsViewModel.cs     # 6 轴参数 + 轴手动控制
│  └─ AutoParamsViewModel.cs       # 6 轴参数
└─ Views/
   ├─ MainWindow.xaml / .cs        # 左侧导航 + 顶栏 + ContentControl + 底部状态栏 + 授权弹窗 Overlay
   ├─ DashboardView.xaml           # 哑铃时间球 + 输入/输出/报警/监视界面
   ├─ OutputView.xaml              # 双表翻页
   ├─ ManualParamsView.xaml        # 6 轴参数卡 + 轴手动控制
   └─ AutoParamsView.xaml          # 6 轴参数卡

## 3 About project

> Current Version: ```1.0.01```
