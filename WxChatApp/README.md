# WxChatApp

基于 **WPF (.NET 8)** + **ASP.NET Core SignalR** 的桌面版仿微信即时通讯示例项目。客户端与服务端在同一目录树下，开箱即可本地双端联调。

> 本项目为学习/演示用途：图像、表情、头像均由程序生成或本地内置，不依赖微信开放平台与任何外部图片资源。

---

## 1. 功能清单

### 1.1 即时通讯

| 功能 | 状态 | 说明 |
|------|------|------|
| 单聊 / 群聊 | ✅ | SignalR 实时收发，支持离线消息拉取 |
| 文本 / 表情 / 粘贴图片 | ✅ | 表情与贴纸内置 |
| 图片 / 文件 / 语音消息 | ✅ | 服务端中转存储；语音用 NAudio 录制播放 |
| 消息右键菜单 | ✅ | 复制、转发、收藏、多选、删除 |
| 多选批量操作 | ✅ | 勾选后批量删除（仅自己发出的可删）、**合并转发** |
| 合并转发 | ✅ | 多选消息合并为一条「聊天记录」卡片，点击查看完整内容 |
| 收藏 | ✅ | 服务端 `favorites` 表持久化 |
| 已读回执 / 正在输入 | ✅ | SignalR 推送 |
| 消息撤回 / 清空会话 | ✅ | 清空仅影响本机视图 |

### 1.2 通讯录与资料

| 功能 | 状态 | 说明 |
|------|------|------|
| 好友申请 / 同意 / 删除 | ✅ | |
| 自定义头像 / 昵称 / 签名 | ✅ | 头像上传后三处 UI 同步刷新 |
| 修改个人资料 | ✅ | 微信号（唯一性/格式校验）、昵称、简介、手机号（格式校验），保存后左侧栏与个人页同步 |
| 好友备注 / 标签 / 分组 | ✅ | 联系人详情页「编辑」弹窗；标签可增删；分组下拉可新建；保存后通讯录与聊天标题按备注优先显示 |
| 联系人详情 | ✅ | 微信号（未设置回退账号）、备注、标签、分组、朋友圈预览图、发消息入口 |
| 语音 / 视频通话 | ✅（信令） | 呼叫、接听、拒接、挂断、通话计时；**受 WPF 限制不传输真实音视频流** |

### 1.3 朋友圈

| 功能 | 状态 | 说明 |
|------|------|------|
| 发布图文 / 点赞 / 评论 | ✅ | |
| 九宫格自适应 | ✅ | 1 张大图；2~3 张一行；4 张两行；5~9 张三列 |
| 图片查看器 | ✅ | 滚轮缩放、拖拽平移、左右切换、ESC 关闭、另存为 |
| 视频查看器 | ✅ | 弹窗自动播放，点击暂停/继续 |
| 联系人预览图 | ✅ | 点击直接打开查看器 |

### 1.4 搜索与公众号

| 功能 | 状态 | 说明 |
|------|------|------|
| 搜一搜 | ✅ | 顶部搜索框 + 分类入口（全部/朋友圈/文章/表情/公众号/小程序/视频号/联系人/聊天记录）；结果分区展示好友、聊天记录、朋友圈，带空状态与搜索发现 |
| 未实现分类 | ✅ | 文章/表情/公众号/小程序/视频号点击后给出「暂未实现」提示，不出现空白页 |
| 结果跳转 | ✅ | 双击好友 → 通讯录详情；朋友圈 → 发现页；消息 → 对应会话提示 |
| 公众号 | ✅ | 本地内置公众号与图文（不依赖微信开放平台），支持按账号筛选、查看全文 |

### 1.5 未实现（保留入口并提示）

| 功能 | 原因 |
|------|------|
| 视频号 | 微信视频号无开放 API |
| 扫一扫 | 未集成扫码 SDK |
| 游戏 / 小程序 | 无对应开放平台能力 |
| 真实音视频流 | WPF 未内置 WebRTC，需额外集成 WebRTC DLL 与 STUN/TURN |

---

## 2. 技术栈与目录结构

| 层 | 技术 |
|----|------|
| 客户端 | WPF、CommunityToolkit.Mvvm、NAudio、SignalR Client |
| 服务端 | ASP.NET Core Minimal API、SignalR、Microsoft.Data.Sqlite |
| 存储 | SQLite（WAL 模式）+ 本地文件（上传媒体） |

```
WxChatApp/
├── WxChatClientApp/                    WPF 客户端工程（WxChatApp.csproj）
│   ├── App.xaml / App.xaml.cs          应用入口、登录流程
│   ├── Models/Dtos.cs                  客户端 DTO
│   ├── Services/
│   │   ├── ApiClient.cs                REST 客户端（认证、资料、搜索、公众号等）
│   │   ├── ChatConnection.cs           SignalR 连接（消息、回执、通话信令）
│   │   ├── VoiceRecorder.cs / VoicePlayer.cs
│   │   └── AppServices.cs              全局单例（Api / Chat / Voice / CurrentUser）
│   ├── ViewModels/                     ShellViewModel、ChatPaneViewModel 等 MVVM
│   ├── Views/                          MainWindow、ChatPaneView、MomentsPane、
│   │                                   ViewerWindow、CallWindow、EditProfileWindow…
│   └── Converters/Resources/           转换器与样式主题（无外部图片）
├── WxChatServer/                       ASP.NET 服务端工程（WxChatServer.csproj）
│   ├── Program.cs                      Minimal API 路由
│   ├── Hubs/ChatHub.cs                 SignalR 中心（消息 + 通话信令）
│   ├── Data/Db.cs                      SQLite 建表/迁移/查询
│   └── bin/Debug/net8.0/App_Data/      wxchat.db 与上传文件（运行时生成）
├── scripts/deploy-server.bat           服务端发布/启停脚本
├── publish/                            发布产物（server\、client\）
└── docs/
    ├── 开发文档.md
    └── 部署文档.md
```

---

## 3. 开发运行

### 3.1 环境要求

- Windows 10 1809+ / Windows 11
- **.NET 8.0 SDK**（客户端需 Desktop Runtime，服务端需 ASP.NET Core Runtime）
- 端口 **5210** 可被占用

### 3.2 启动服务端

```powershell
# 编译
dotnet build WxChatApp\WxChatServer\WxChatServer.csproj

# 运行（首次启动自动建库、补列、创建内置公众号）
dotnet run --project WxChatApp\WxChatServer\WxChatServer.csproj
# 或直接运行产物
.\WxChatApp\WxChatServer\bin\Debug\net8.0\WxChatServer.exe
```

服务端监听 `http://0.0.0.0:5210`，健康检查：`GET /api/health`。

### 3.3 启动客户端

```powershell
dotnet run --project WxChatApp\WxChatClientApp\WxChatApp.csproj
# 或
.\WxChatApp\WxChatClientApp\bin\Debug\net8.0-windows\WxChatApp.exe
```

> 同时启动两个客户端即可双端联调（不同账号登录）。

### 3.4 测试账号

| 账号 | 昵称 | 密码 |
|------|------|------|
| `wxtest_a` | 测试甲 | `test123456` |
| `wxtest_b` | 测试乙 | `test123456` |

---

## 4. 使用操作指南（新增功能）

| 操作 | 路径 |
|------|------|
| 搜索好友 / 消息 / 朋友圈 | 发现 → **搜一搜** → 输入 ≥2 字符（回车或点搜索）→ 双击结果跳转 |
| 多选 + 批量删除 | 右键任意消息 → **多选** → 勾选 → 底部工具栏「批量删除」 |
| 合并转发 | 右键 → **多选** → 勾选多条 → 工具栏「**合并转发**」→ 输入目标账号 → 发送 |
| 查看合并聊天记录 | 点击收到的「聊天记录」卡片 → 弹出完整条目 |
| 设置头像 / 昵称 / 签名 | 我 → 编辑资料 → 更换头像（jpg/png/gif/bmp，≤2MB）→ 保存 |
| 设置好友标签 | 通讯录 → 选中好友 → 详情「标签」行 → 编辑 → 保存（≤30 字） |
| 查看公众号 | 发现 → **公众号** → 左侧选择账号 / 「全部图文」 → 点击图文查看全文 |
| 发起语音/视频通话 | 聊天窗右上角麦克风/摄像机按钮，或联系人详情「语音通话 / 视频通话」 |
| 接听来电 | 被叫方自动弹出通话窗口 → 「接听」/「拒接」 |
| 发送图片 | 聊天输入框上方工具栏 → 图片按钮 → 选择本地图片（≤20MB）→ 自动压缩为适合显示的尺寸 |
| 发送文件 | 聊天输入框上方工具栏 → 文件按钮 → 选择本地文件（≤20MB，排除 .exe/.bat/.cmd/.com/.scr/.msi/.js/.vbs/.ps1/.sh 等危险类型） |
| 发送语音 | 工具栏切换为语音模式 → 「按住 说话」≥1 秒松开 → 自动发送；长按不足 1 秒提示太短 |
| 播放语音 | 点击对方发来的语音气泡 → 自动播放；支持倍速（长按可取消） |
| 朋友圈九宫格 | 朋友圈 → 添加 1~9 张图片 → 自动排版 → 点击任意图片打开查看器 |
| 查看视频 | 点击朋友圈视频 → 弹窗自动播放 → 点击画面暂停/继续 |
| 切换界面语言 | 设置 → 通用 → **界面语言** → 简体中文 / English；切换后全部页面与弹框立即生效，选择保存在 `%LOCALAPPDATA%\WxChatApp\settings.json` 的 `Language`，重启保持 |

---

## 5. 部署

### 5.1 发布

```powershell
# 服务端：自包含单文件（可单独拷贝/双击运行，无需目标机安装 .NET）
dotnet publish WxChatApp\WxChatServer\WxChatServer.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish\server
dotnet publish WxChatApp\WxChatClientApp\WxChatApp.csproj -c Release -o publish\client

# 或直接用脚本（自动定位工程，输出 WxChatApp\publish\server）
WxChatApp\scripts\deploy-server.bat
```

> 模块已改为多工程结构（`WxChatClientApp\` + `WxChatServer\`），模块根目录不再有 `.csproj` / `.sln`。
> 仓库根的 `build_all.bat` 会分别构建客户端与服务端，并打成一个 `WxChatApp-<时间戳>.zip`（内含 `client\`、`server\`）。

### 5.2 服务端

1. 将 `publish\server` 复制到目标机器（如 `D:\WxChatServer`）；只拷贝 `WxChatServer.exe` 一个文件也能运行（首次启动会在同目录创建 `App_Data`）。
2. 开放防火墙 TCP 5210。
3. **双击 `WxChatServer.exe`**（或在终端运行 `.\\WxChatServer.exe`）；关闭：`Ctrl+C` 或关闭控制台窗口。
4. 数据位于 `App_Data\wxchat.db`（SQLite）与 `App_Data\uploads\`（媒体文件），**备份只需复制整个 `App_Data` 目录**；可用环境变量 `WXCHAT_DATA` 指定其它数据目录。

> 如需修改端口，可在 `Program.cs` / 启动配置中调整；客户端 `AppServices.Api` 的基地址需同步修改。

### 5.3 客户端

1. 将 `publish\client` 复制到用户机器。
2. 目标机器安装 **.NET 8 Desktop Runtime**。
3. 运行 `WxChatApp.exe`。

> 登录窗口**左下角有服务状态信号灯**：进入登录页即请求 `/api/health`，绿灯「服务正常」、红灯「服务不可用」，此后每 5 秒自动刷新，点击文字可立即重新检测。因此服务端未启动时可以直接从登录页判断问题出在哪一端。

### 5.4 一键部署脚本（`scripts/deploy-server.bat`）

无需手动 publish / 杀进程 / 开端口，双击或命令行运行即可：

```batch
cd WxChatApp\scripts
deploy-server.bat                       :: 发布并重启（默认 5210）
deploy-server.bat 5210                  :: 指定端口
deploy-server.bat 5210 install-service  :: 注册为 Windows 服务（需管理员）
deploy-server.bat status                :: 查看端口占用 + 健康检查
deploy-server.bat stop                  :: 停止服务
```

脚本执行顺序（顺序是刻意的，不可颠倒）：

1. 检查 `dotnet` 与 `WxChatServer.csproj`；
2. **先停止旧服务进程**（否则 exe 被占用会导致 publish 卡住）；
3. `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\server`（发布前清理旧多文件产物，保留 `App_Data`）；
4. 添加防火墙入站规则（非管理员仅提示，不中断）；
5. 以 `WXCHAT_PORT` / `WXCHAT_DATA` 环境变量启动 `publish\server\WxChatServer.exe`；
6. 轮询 `/api/health` 最长 25 秒，输出成功或失败结论。

说明：

- 数据目录默认 `publish\server\App_Data`；若启动脚本前已设置 `WXCHAT_DATA`，脚本会沿用该值，便于接管既有的历史数据。
- 脚本为 UTF-8（无 BOM）+ CRLF，请勿用会改动换行符的编辑器保存，否则 `cmd` 解析会出错。
- `install-service` 使用 `setx /M` 写入机器级环境变量，需要管理员权限。

更详细的环境、配置与排障说明见 [docs/部署文档.md](docs/部署文档.md)。

---

## 6. 图标与品牌资源

图标文件位于 `WxChatApp/Resources/Brand/`，为本项目自有品牌图形（渐变圆角方底 + 白色聊天气泡 + “WX”字标），不含任何第三方商标元素：

| 文件 | 用途 |
|------|------|
| `logo.png` / `logo-256.png` | 主 Logo（256×256），界面大图引用 |
| `logo-16/24/32/48/64/128/512.png` | 同一设计的多尺寸导出，供不同显示场景按 DPI 选用 |
| `logo.ico` | EXE 文件图标、任务栏与窗口标题栏图标（内嵌 16/24/32/48/64/128/256 七帧），在 `.csproj` 里通过 `<ApplicationIcon>` 声明 |

引用位置：

| 位置 | 引用 |
|------|------|
| 登录页顶部 | `pack://application:,,,/Resources/Brand/logo-128.png` |
| 主界面空状态、发消息按钮 | `logo-128.png` / `logo-32.png` |
| 左侧导航图标 | 统一改为 Segoe MDL2 Assets 字体图标（不再使用位图 logo） |
| 任务栏 / 窗口标题栏 | csproj `<ApplicationIcon>Resources\Brand\logo.ico</ApplicationIcon>` |

> **注意**：WPF 中 `WindowStyle="None"` 或 `AllowsTransparency="True"` 的窗口**不能**在 XAML 里设置 `Icon="..."` 属性，否则启动即崩溃（CLR 0xe0434352）。图标通过 csproj 的 `<ApplicationIcon>` 注入即可在任务栏和 Alt+Tab 正常显示，界面 logo 统一用 Image 控件引用 png。

如需替换为其它品牌图标：按同一命名规则覆盖 `Resources/Brand/` 下的 png 与 ico（png 保持透明背景；ico 需包含 16/24/32/48/64/128/256 多尺寸，Windows Vista+ 支持内嵌 PNG 帧）。

---

## 7. 数据库与迁移

- 服务端启动时 `Db.Init()` 自动建表，并通过 `EnsureColumn` 对旧库安全补列（`ALTER TABLE ... ADD COLUMN`）。
- 新增表：`favorites`（收藏）、`official_accounts` / `official_messages`（公众号）。
- 内置公众号为**首次建库时写入的示例数据**，不依赖外部接口。

---

## 8. 常见问题

| 现象 | 处理 |
|------|------|
| 客户端提示无法连接 | 确认服务端已启动、端口 5210 未被占用、防火墙放行 |
| 端口被占用 | `Get-NetTCPConnection -LocalPort 5210` 找到 PID 后 `Stop-Process -Id <pid> -Force` |
| 搜索中文无结果 | 确认客户端使用 UTF-8 请求（`Uri.EscapeDataString`）；PowerShell 手工测试时 `Invoke-RestMethod` 可能误解码中文 URL |
| 头像/图片不显示 | 检查服务端 `App_Data\uploads` 目录是否存在且客户端能访问服务端地址 |
| 通话窗口打不开 | 当前仅支持**单聊**好友通话；群聊会提示不支持 |
| 旧库缺字段 | 启动一次新版服务端即自动补列/建表，无需手工迁移 |

---

## 9. 相关文档

- [开发文档.md](docs/开发文档.md)：架构、模块、关键实现与扩展指引
- [部署文档.md](docs/部署文档.md)：环境、编译、部署、运维与排障
