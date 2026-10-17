# IpanelWpf — 1Panel 服务器管理面板 WPF 复刻版

基于 **WPF + MVVM + .NET Framework 4.8 + MaterialDesignInXamlToolkit** 技术框架，对 1Panel（简单好用的 Linux 服务器管理面板）暗色主题界面进行 1:1 复刻的桌面演示工程。界面效果、布局结构、配色样式与原版一致。

## 技术栈

| 技术 | 说明 |
| --- | --- |
| WPF | .NET Framework 4.8 桌面客户端 |
| MVVM | 手写 `ObservableObject` / `RelayCommand` 基类，视图与逻辑解耦 |
| MaterialDesignInXamlToolkit | 5.0.0（`PackIcon` 矢量图标库），MaterialDesignColors 3.0.0 |
| SDK-Style csproj | 支持 `dotnet build` / `dotnet publish` 一键构建 |

## 已复刻页面（对应 8 张参考截图）

| # | 页面 | 视图 | 还原要点 |
| --- | --- | --- | --- |
| 1 | 概览 | `Views/DashboardView.xaml` | 智能体/网站/数据库/已安装应用统计卡、运行消耗·CPU·内存·磁盘 4 个环形仪表、监控曲线图（上行/下行/总发送/总接收）、系统信息卡、应用安装列表卡 |
| 2 | 应用商店 | `Views/AppStoreView.xaml` | 全部/已安装/可升级/设置页签、17 个分类芯片、镜像加速提示条、6 张应用卡片（mysql/redis/nginx-proxy/openvpn/tomcat/bililive-go，含品牌 Logo、已启动徽章、端口芯片、操作组）、分页器 |
| 3 | 数据库 | `Views/DatabaseView.xaml` | MySQL/PostgreSQL/Redis/MongoDB 页签、服务状态栏（停止徽章/版本/停止·重启·设置）、创建/连接信息/用户/从服务器同步等子操作、数据库列表 |
| 4 | 容器 | `Views/ContainerView.xaml` | 概览/容器/编排/镜像/网络/存储卷/仓库/操作模板/配置页签、新建·已启动统计、6 个容器行（镜像、状态徽章、CPU/内存迷你进度条、IP、关联资源、端口芯片组、控制/日志/更多操作） |
| 5 | 主机-防火墙 | `Views/FirewallView.xaml` | 主机防火墙/容器端口防护/端口转发/设置页签、`status docker` 绿色徽章与版本芯片、创建/编辑/导入/导出工具栏、6 条端口防护规则（含绿色放行端口芯片） |
| 6 | 面板设置-面板 | `Views/PanelSettingsView.xaml` | 8 个设置页签、主题颜色三态分段选择、菜单栏固定/隐藏体验计划开关、面板别名/重启倒计时间/默认访问地址等表单（含"设置"按钮与灰色说明文字）、运行环境芯片 |
| 7 | 面板设置-安全 | `Views/PanelSettingsView.xaml` | 面板端口/监听地址/安全入口/未认证页面/授权 IP/域名绑定表单、面板 SSL 与密码复杂度验证拨动开关 |
| 8 | 文件 | `Views/FilesView.xaml` | 文件页签（面板/home/+）、路径导航栏（前进后退/刷新/主目录/子目录开关/AI 搜索）、创建/上传下载/回收站/终端等操作栏、18 行文件表（权限/用户组/大小/修改时间/收藏星标） |

其他侧边栏入口（AI、网站、终端、计划任务、工具箱、高级功能、日志审计等）进入占位提示页，`系统` 菜单支持展开/收起（文件、监控、防火墙、进程管理、SSH 管理、磁盘管理）。

## 界面外观体系

- 主题色板：`Themes/Colors.xaml` — 1Panel 暗色配色（页面底 `#1B2130`、侧边栏 `#171C29`、卡片 `#242B3D`、品牌蓝 `#3370FF`、语义绿 `#23B26D` 等）
- 全量控件样式：`Themes/Controls.xaml` — 页签胶囊、主/次按钮、深色输入框/下拉框/复选框、拨动开关、迷你进度条、绿色徽章、端口芯片、分段单选、信息提示条等
- 通用控件：
  - `Controls/GaugeCircle.xaml` — 环形仪表（描边虚线圆弧按百分比绘制）
  - `Controls/PagerControl.xaml` — "共 N 条 / 条每页 / 前往 N 页" 分页器
  - `Controls/SearchBoxControl.xaml` — 带占位符与放大镜的搜索框
- 导航外壳：`Views/MainWindow.xaml` — 左侧 1Panel Logo + 分组导航（可展开）+ 主节点、右侧内容区 `ContentControl` 页面路由、底部版权页脚与右侧悬浮工具条

## 目录结构

```
IpanelWpf/
├── IpanelWpf.sln                  # 解决方案（VS 可直接打开）
├── IpanelWpf.csproj               # SDK 风格工程文件（net48 + UseWPF）
├── App.xaml / App.xaml.cs         # 应用入口（全局资源合并、异常兜底）
├── Core/                          # MVVM 基础设施
│   ├── ObservableObject.cs        # INotifyPropertyChanged 基类
│   ├── RelayCommand.cs            # 命令（含泛型版本）
│   └── Converters.cs              # 枚举/索引/布尔转可见性、导航配色等转换器
├── Themes/
│   ├── Colors.xaml                # 暗色主题色板
│   └── Controls.xaml              # 全量控件样式库
├── Controls/                      # 通用控件（环形仪表、分页器、搜索框）
├── Models/
│   └── Models.cs                  # 导航项、应用/容器/防火墙/文件等数据模型
├── ViewModels/                    # 主控 + 8 个页面 ViewModel（纯 MVVM）
├── Views/                         # MainWindow 外壳 + 8 个页面视图
└── dist/
    ├── publish/                   # 发布产物（IpanelWpf.exe 及依赖）
    └── IpanelWpf-v1.3.2.zip       # 打包压缩包
```

## 构建与运行

环境要求：Windows 10/11 + .NET Framework 4.8 运行时（系统内置），安装 [.NET SDK](https://dotnet.microsoft.com/download)（8.0+）或 Visual Studio 2022。

```bash
# 命令行构建（Release）
dotnet build IpanelWpf/IpanelWpf.csproj -c Release

# 直接运行
IpanelWpf/bin/Release/net48/IpanelWpf.exe
```

Visual Studio 用户：双击打开 `IpanelWpf.sln`，选择 Release/Debug 后按 `F5` 运行。

## 发布打包

```bash
# 发布（框架依赖，输出到 dist/publish）
dotnet publish IpanelWpf/IpanelWpf.csproj -c Release -o IpanelWpf/dist/publish

# 压缩为发布包
Compress-Archive -Path IpanelWpf/dist/publish/* -DestinationPath IpanelWpf/dist/IpanelWpf-v1.3.2.zip -Force
```

发布包解压后双击 `IpanelWpf.exe` 即可运行，无需安装任何依赖（目标机器需已启用 .NET Framework 4.8，Windows 10 1903+ 默认内置）。

## 说明

- 本工程为界面复刻演示，数据均为静态模拟数据（与参考截图一致），不包含真实服务器操作逻辑；业务动作（创建、重启、删除等）以界面状态呈现。
- 界面默认启动即进入"概览"页，最大化的 1440×820 设计基准，最小窗口 1280×720。
- 全局未处理异常通过 `App.xaml.cs` 捕获并弹窗提示，避免直接崩溃。
- 中英文界面切换、真实后端对接可作为后续扩展方向。
