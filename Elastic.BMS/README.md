# Elastic.BMS — 电池监控与检测系统

基于 **WPF + MVVM + .NET Framework 4.8 + MaterialDesignInXamlToolkit 5.2.1**，按参考截图 **1:1 复刻**的深蓝科技风电池监控与检测系统界面。

## 技术栈

| 项 | 说明 |
|---|---|
| 框架 | WPF / .NET Framework 4.8（SDK 风格 csproj） |
| 模式 | MVVM（手写 ObservableObject / RelayCommand，无第三方 MVVM 框架） |
| UI 库 | MaterialDesignThemes 5.2.1 + MaterialDesignColors 5.2.1（仅用 PackIcon 图标，主题样式全部自定义） |
| 语言 | C# 8.0 / XAML |

## 界面清单（4 个页面 + 主外壳）

| 模块 | Key | 还原要点 |
|---|---|---|
| 监控主页 | `dashboard` | 左侧「总体状态」卡（70°C 温度 / 55% 剩余电量 / 1000kW 总电量 三色数值 + 2×2 图标统计框：总电芯量 50000、正在报警 1、已停机 20、总设备数 100）；中间「电池组3D模型」交叉占位区；右侧「当前电芯状态」卡（圆形水位仪表盘 60% + 容量/剩余电量面板 + 运行时长 120.11h / 充放电次数 245 / SOC 40% / SOH 45% 四宫格）；左下「实时数据统计」双折线图（SOC/SOH 图例、X 轴 1–24、Y 轴 0–100）；右下「异常报警」红色关闭按钮列表 |
| 实时监控 | `realtime` | 顶部 5 组描边数据条（总电压/总电流、SOC/SOH、剩余容量/充满容量、最高/最低电压+序号、均衡/环境/MOS 温度）；下方四面板：「电池」电芯1–20 mV 双列、「电芯温度」温度1–20 ℃ 双列、「控制」（Pack 下拉、充电MOS/放电MOS 开·关、解除告警·解除、休眠·关机、8 格绿色状态格）、「故障/告警/保护」三块空描边区 |
| 报警记录 | `alarm` | 筛选栏（时间区间双输入框、类型下拉、查询蓝按钮、重置按钮、右侧导出白按钮）；7 列表格（序号/标题/报警信息/时间/类型/状态/处理）20 行；底部分页「上一页 / 2/5 / 下一页」 |
| 系统设置 | `settings` | 页签「基本设置 / 通信设置」+ 大描边外框。基本设置：总充放电容量、报警设置（单体电压/总电压/电芯温度 上限(充电)/下限(放电)）、电压设置（单体过充/过放、总压过充/过放：保护(V)/延时(ms)/恢复(V)/过流保护次数）、电流设置（充电电流、放电电流1/2、短路电流）、温度设置；右下「保存 / 导入配置 / 导出配置」。通信设置：网络通信设置卡（IP 172.30.12.5 / 端口 502 + 保存/取消）、串口设置卡（端口 COM1 / 波特率 9600 / 数据位 8 / 停止位 1 / 奇偶校验位 None + 保存/取消） |

主外壳：无边框 `WindowChrome` 自绘标题栏——左侧蓝色条形 Logo、居中梯形发光托板标题「电池监控与检测系统」、右上实时日期时间 + 「退出」按钮；深蓝径向渐变 + 星空点阵背景；内容区套一圈带四角折线的发光青色描边外框；底部居中胶囊导航栏（监控主页 / 实时监控 / 报警记录 / 系统设置，选中项白色药丸高亮 + 蓝色方块指示）。

## 目录结构

```
Elastic.BMS/
├─ Elastic.BMS.csproj          # net48 + UseWPF + MaterialDesignThemes 5.2.1
├─ App.xaml / App.xaml.cs      # 全局资源合并、未处理异常弹窗
├─ Themes/
│  ├─ Colors.xaml              # 深蓝色板、渐变画刷、星空点阵 TileBrush
│  └─ Controls.xaml            # 全部样式与模板（卡片/按钮/输入框/下拉/页签/底部导航…）
├─ Core/
│  ├─ ObservableObject.cs
│  ├─ RelayCommand.cs
│  └─ Converters.cs            # bool→可见性、相等→bool、字符串→可见性
├─ Controls/
│  └─ SciCard.cs               # 科技卡片：青色描边 + 四角折线 + 渐变切角标题栏
├─ Models/Models.cs            # NavItem/StatCell/StatGroup/MetricBox/CellRow/StateCell/KeyValue/LegendItem/AlarmEntry/AlarmRecord
├─ ViewModels/
│  ├─ MainViewModel.cs         # 实时时钟、底部导航、页面切换与缓存
│  ├─ DashboardViewModel.cs    # 总体状态/仪表盘/折线图点位/异常报警
│  ├─ RealtimeViewModel.cs     # 顶部数据条、电芯电压/温度、控制状态格
│  ├─ AlarmRecordsViewModel.cs # 筛选条件、20 行记录、分页
│  ├─ SettingsViewModel.cs     # 基本/通信子页切换
│  ├─ SettingsBasicViewModel.cs
│  └─ SettingsCommViewModel.cs
└─ Views/                      # MainWindow + DashboardView / RealtimeView / AlarmRecordsView
                               #            + SettingsView / SettingsBasicView / SettingsCommView
```

## 数据说明

界面为**界面复刻**，各页数值取自参考图（部分实时数据为占位空值），实际通信/采集逻辑未接入。

## 构建与打包

### 单独编译

```bat
"C:\...\MSBuild.exe" Elastic.BMS\Elastic.BMS.csproj /p:Configuration=Release
```

产物：`Elastic.BMS\bin\Release\net48\Elastic.BMS.exe`

### 打包全部模块

在仓库根目录执行 `build_all.bat`（默认 Release）：

```bat
build_all.bat
```

脚本会编译 `MODULES` 列表中的 14 个模块，将每个模块的发布输出打包为 `Publish\<模块名>-<时间戳>.zip`，打包完成后删除各模块临时目录，`Publish` 根目录仅保留 zip 与 `logs` 文件夹。

## 运行

双击 `Elastic.BMS.exe` 即可，无需额外依赖（.NET Framework 4.8 系统组件除外）。
