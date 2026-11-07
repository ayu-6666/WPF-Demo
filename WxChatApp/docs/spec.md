# WxChatApp 中英文界面切换规格（2026-11-06）

> 本轮目标：替换「仅简体中文」实现，建立统一本地化资源层，支持界面内即时切换 + 重启保持。
> 上一轮（第四轮 4 项修复）的规格与结果已归档在 `docs/开发文档.md` 第 11 节。

## 1. 目标与范围

只改 `WxChatApp`（含项目内 `WxChatApp/WxChatServer` 仅限必要服务端文案/接口兼容），不改其它工程模块。

| 编号 | 需求 | 验收标准 |
|---|---|---|
| L1 | 统一本地化资源层 | 界面文案来源集中在 `Resources/Strings.zh-CN.xaml` / `Strings.en-US.xaml`；XAML 不再出现中文界面字面量（注释除外） |
| L2 | 设置页语言切换（简体中文 / English） | 切换后当前已打开的主界面、设置页、菜单与弹框文案立即变化，无需重启 |
| L3 | 语言持久化 | 选择写入 `%LocalAppData%\WxChatApp\settings.json` 的 `Language`；重启客户端后仍为上次语言 |
| L4 | 覆盖范围 | 聊天列表与聊天窗、通讯录与资料页、朋友圈（含发布/编辑弹框、删除确认）、发现页与公众号/小程序、设置页、登录页、以及全部弹框/菜单/工具提示 |
| L5 | 日期/数字格式 | 时间文案与格式化随语言切换（中：`M月d日`、`昨天`；英：`MMM d`、`Yesterday`），数字使用对应 `CultureInfo` |

## 2. 现状与根因（已核实）

- 13 个 XAML 文件直接写中文（`MainWindow.xaml`、`ChatPaneView.xaml`、`MomentsPane.xaml`、`SettingsWindow.xaml`、`LoginWindow.xaml`、`AppDialog.xaml`、`InputDialog.xaml`、`CreateGroupWindow.xaml`、`EditProfileWindow.xaml`、`PublishMomentWindow.xaml`、`CallWindow.xaml`、`ViewerWindow.xaml`）。
- 代码侧大量用户可见文案写在 `ViewModels/*.cs`、`Views/*.xaml.cs`、`Helpers/Dialogs.cs`、`Helpers/TimeFormatter.cs`。
- `SettingsWindow.xaml` 明确写「语言：当前仅提供简体中文界面」。
- `LocalSettings` 无语言字段；`TimeFormatter` 固定中文格式。
- 自定义弹框 `AppDialog` 的标题/按钮文案由调用方传入，`Dialogs` 的默认参数为中文。

## 3. 方案（决策完成）

### 3.1 资源层：ResourceDictionary + Loc 单例

- 新增 `Resources/Strings.zh-CN.xaml`、`Resources/Strings.en-US.xaml`，条目为 `sys:String`，键名语义化（如 `App.Title`、`Nav.Contacts`、`Chat.Send`、`Settings.Language`、`Common.Cancel`）。
- 在 `App.xaml` 的 `MergedDictionaries` 中默认合并中文资源字典（启动后由 `Loc.Initialize()` 按持久化语言替换）。
- 新增 `Services/Localization.cs`：
  - `enum AppLanguage { ZhHans, EnUs }`
  - `static class Loc`：
    - `Language`（当前语言）、`Culture`（`zh-CN` / `en-US`）、`IsEnglish`
    - `event Action? LanguageChanged`
    - `Initialize()`：读取 `LocalStore.Settings.Language`，加载对应字典并设置线程 Culture
    - `SetLanguage(AppLanguage, bool persist = true)`：替换 `Application.Resources.MergedDictionaries` 中的字符串字典，设置 `CultureInfo`，按需持久化，触发 `LanguageChanged`
    - `string T(string key)`：从当前字典取值；缺失时回退中文，再回退键名
    - `string T(string key, params object[] args)`：按当前 Culture 格式化
- XAML 用 `{DynamicResource Key}` 引用文案。`DynamicResource` 支持 `TextBlock.Text`、`Button.Content`、`Window.Title`、`ToolTip`、`MenuItem.Header`、`TextBox.Tag`，字典替换后自动刷新，无需重启。
- 代码用 `Loc.T(key)` / `Loc.T(key, args)`；动态计算文案（时间、未读、数量、按钮文案）改用 `Loc.T` 并在 `LanguageChanged` 时刷新。
- 带格式的动态文案（如「已选 {0} 条」「共 {0} 条」）改为 ViewModel 计算属性，避免 XAML `StringFormat` 写死中文。

### 3.2 设置页语言切换

- `SettingsWindow.xaml` 通用区：
  - 删除「当前仅提供简体中文界面」。
  - 新增「界面语言」行：`ComboBox`（`AutomationId=LanguageCombo`），选项「简体中文 / English」。
  - 说明文案「切换后立即生效，并保存到本机」。
- `SettingsWindow.xaml.cs`：
  - 打开时按 `Loc.Language` 选中项。
  - `SelectionChanged` 立即调用 `Loc.SetLanguage(..., persist: true)`；不等待「保存」。
  - 「取消/关闭」不回滚语言（语言是即时生效项；其余缓存/背景/快捷键仍按原快照回滚）。
- `LocalSettings` 增加 `string Language { get; set; } = "zh-Hans"`；`LocalStore.Save()` 负责落盘（JSON 自动序列化）。

### 3.3 启动与即时刷新

- `App.OnStartup` 在 `ShowLogin()` 之前调用 `Loc.Initialize()`。
- `ShellViewModel` 订阅 `Loc.LanguageChanged`，统一刷新列表项派生文案（时间、微信号前缀、未读/连接状态等），并通知 `ChatPane`、`Moments` 重新计算派生属性；不重新请求服务端。
- `ChatPaneViewModel` 暴露 `NotifyLanguageChanged()`，刷新语音按住文案、多选计数文案、错误前缀等派生属性。
- `MomentsViewModel` 同理刷新「只看/查看全部」、点赞/评论/删除提示。
- `TimeFormatter` 所有格式分支按 `Loc.Culture` 区分中英格式。
- 已打开的 `CallWindow/ViewerWindow` 订阅 `Loc.LanguageChanged` 刷新代码设置的文案（不改变通话/预览状态）。

### 3.4 文案键清单（按文件分组）

| 分组 | 使用位置 | 说明 |
|---|---|---|
| `App.*` | 窗口标题、品牌名 | 微信 / WeChat；微信登录 / WeChat Sign In |
| `Common.*` | 全部页面与弹框 | 确定/取消/关闭/保存/删除/编辑/返回/更多/搜索/发送/复制/转发/收藏/多选/重试/知道了等 |
| `Login.*` | 登录页与 VM | 标题、副标题、账号、昵称、密码、记住账号、状态灯、错误提示 |
| `Nav.*` / `Main.*` | 主窗口 | 微信/通讯录/发现、搜一搜、朋友圈、公众号、视频号、扫一扫、游戏、小程序、离线提示 |
| `Contact.*` | 资料页 | 朋友资料、备注、昵称、账号、微信号、标签、朋友圈、更多信息、个性签名、来源、添加时间、未设置、发消息、语音/视频通话 |
| `Chat.*` | 聊天窗 | 输入提示、空状态、右键菜单、多选工具栏、发送/表情/图片/文件/语音/@、已读/未读、转发/合并转发、系统提示 |
| `Moments.*` | 朋友圈 | 发布/编辑标题、空状态、点赞/评论/编辑/删除、封面菜单、删除确认、编辑提示、图片数量/格式提示 |
| `Official.*` / `Search.*` | 发现页 | 公众号筛选、全部图文、图文消息、返回列表、搜索提示与结果类型/详情 |
| `Me.*` | 我页面 | 账号资料、我的朋友圈、消息通知、清空聊天记录、退出登录、二维码占位、连接状态 |
| `Settings.*` | 设置页 | 缓存、路径、图片/文件、背景、通用、通知、自启、主题、语言、快捷键、保存/取消、各类确认与错误 |
| `Dialog.*` | 弹框 | 提示/错误/完成/确认、知道了、输入、群聊、资料、通话 |
| `Viewer.*` | 图片/视频预览 | 关闭、上一张/下一张、缩小/适应窗口/放大、另存为、播放状态 |
| `Time.*` | 时间文案 | 昨天、刚刚、N 分钟前、今天 |
| `Error.*` | 网络/上传/目录 | 连接超时、服务不可用、上传/下载失败前缀、目录不可写 |

### 3.5 不在本轮范围

- 服务端返回的中文业务错误（如「账号或密码错误」）仍原样展示，属于服务端契约字段；最终清单注明。
- 用户输入内容、昵称、聊天正文、朋友圈正文不翻译。
- 深色主题、其它语言（仅简体中文与 English）。

## 4. 逐文件改动清单

### 4.1 新增

- `Services/Localization.cs`
- `Resources/Strings.zh-CN.xaml`
- `Resources/Strings.en-US.xaml`
- `WxChatApp/.tmp-i18n/`（临时联调脚本与截图，完成后删除）

### 4.2 修改（XAML）

- `App.xaml`：合并默认语言字典。
- `Views/MainWindow.xaml`：`Title`、导航 ToolTip、搜索水印、发现菜单、资料页、公众号、搜一搜、我页面、离线/Toast、动态菜单项。
- `Views/ChatPaneView.xaml`：顶栏 ToolTip、空状态、右键菜单、合并记录、表情页签、输入区按钮、多选工具栏、发送按钮。
- `Views/MomentsPane.xaml`：封面 ToolTip/菜单、空状态、编辑/删除 ToolTip、评论/发表、只看按钮。
- `Views/SettingsWindow.xaml`：标题、各组标题/说明、按钮、语言下拉。
- `Views/LoginWindow.xaml`：标题、标签、记住账号、状态灯 ToolTip。
- `Views/AppDialog.xaml`、`Views/InputDialog.xaml`、`Views/CreateGroupWindow.xaml`、`Views/EditProfileWindow.xaml`、`Views/PublishMomentWindow.xaml`、`Views/CallWindow.xaml`、`Views/ViewerWindow.xaml`：标题、按钮、提示、ToolTip。

### 4.3 修改（C#）

- `Services/AppConfig.cs`：`LocalSettings.Language` 默认 `zh-Hans`。
- `App.xaml.cs`：启动初始化 `Loc`；异常弹框文案改用 `Loc.T`。
- `Helpers/Dialogs.cs`：默认标题/按钮改用 `Loc.T`（默认值不能是编译期常量时改为 `null` 后在方法体内替换）。
- `Helpers/TimeFormatter.cs`：按语言输出日期/时间格式。
- `Helpers/AppBackground.cs`、`Helpers/MomentsCoverStore.cs`：校验错误文案本地化。
- `ViewModels/ShellViewModel.cs`：全部 Toast/连接/搜索结果文案；`MeWxId`、`MeName` 派生刷新；语言变更刷新。
- `ViewModels/ChatPaneViewModel.cs`：全部 Toast/错误/对话框标题/合并摘要/按住说话文案。
- `ViewModels/MomentsViewModel.cs`：只看/查看全部、错误前缀、提示。
- `ViewModels/LoginViewModel.cs`：全部校验/错误/按钮/副标题。
- `ViewModels/ChatMessageVm.cs`：已读/未读、合并标题、时间文案刷新。
- `ViewModels/ConversationItemVm.cs`：时间文案刷新。
- `ViewModels/ContactItemVm.cs`：未设置/微信号前缀/创建时间刷新。
- `ViewModels/MomentItemVm.cs`：点赞/评论/时间文案刷新。
- `Services/ApiClient.cs`、`Services/ChatConnection.cs`：错误前缀本地化。
- `Views/MainWindow.xaml.cs`、`Views/ChatPaneView.xaml.cs`、`Views/LoginWindow.xaml.cs`、`Views/SettingsWindow.xaml.cs`、`Views/MomentsPane.xaml.cs`、`Views/PublishMomentWindow.xaml.cs`、`Views/EditProfileWindow.xaml.cs`、`Views/CreateGroupWindow.xaml.cs`、`Views/CallWindow.xaml.cs`、`Views/ViewerWindow.xaml.cs`、`Views/AppDialog.xaml.cs`、`Views/InputDialog.xaml.cs`：用户可见文案与默认值改为 `Loc.T`；动态菜单构造时取当前语言。
- `ViewModels/ChatPaneViewModel.cs` 增加 `SelectedCountText`；`ViewModels/ShellViewModel.cs` 增加 `SearchCountText`。

## 5. 验收与验证步骤（真实联调，禁止模拟数据）

1. **构建**：`dotnet build WxChatApp/WxChatApp.csproj -v q --nologo` 零错误。
2. **启动**：服务端 `http://127.0.0.1:5210/api/health` 返回 ok；客户端 autotest01 登录成功。
3. **即时切换**：设置页将语言切到 English → 不点保存 → 主窗口导航 ToolTip/空状态/设置页标题/按钮、聊天窗、资料页、朋友圈、公众号、搜索页立即变为英文；再切回简体中文立即恢复。
4. **覆盖抽查**：逐页截图（聊天列表、聊天窗右键菜单、通讯录资料页、朋友圈发布/编辑弹框与删除确认、公众号、设置页、登录页、图片预览）确认无中文界面残留（用户数据/服务端错误除外）。
5. **持久化**：选择 English 后退出客户端 → 检查 `settings.json` 的 `Language` 为 `en-US` → 重启客户端，登录页与主界面仍为英文。
6. **日期/数字**：切换语言后会话列表时间、朋友圈时间由「昨天/M月d日」变为「Yesterday/MMM d」；数量文案（已选 N 条 / 共 N 条 / 已清理 N 个）随语言变化。
7. **链路兼容**：双实例 autotest01 ↔ autotest02 发送消息、朋友圈点赞/评论仍正常；SignalR 断线重连文案随语言显示。
8. **清理**：删除 `WxChatApp/.tmp-i18n/`；`docs/开发文档.md` 追加「12. 中英文界面切换」，`docs/tasks.md` 勾选完成项；输出「已完成并验证 / 未完成及原因」清单。

## 6. 约束

- 只改 `WxChatApp` 相关模块；不改仓库内其它工程。
- 真实联调，不使用模拟数据/假填充。
- 与控制台/SignalR 现有链路兼容：不改变协议、不改服务端接口行为。
- 临时脚本/截图放 `WxChatApp/.tmp-i18n/`，完成后清理。
- 文档同步更新：`docs/spec.md`、`docs/tasks.md`、`docs/开发文档.md`。

## 7. 需要用户确认的决策

- 语言切换在设置页**即时生效并立即持久化**，「取消」不回滚语言（否则无法满足“切换立即生效 + 重启保持”）。其余设置仍保持“取消回滚”。
- 服务端业务错误消息本轮不翻译（避免改服务端契约），在最终清单注明。
