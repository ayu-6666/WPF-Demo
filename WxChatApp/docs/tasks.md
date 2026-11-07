# WxChatApp 中英文界面切换任务清单（2026-11-06）

> 对应规格：`docs/spec.md`。执行顺序 L1 → L2 → L3 → L4（分批）→ L5 → 收尾。
> 每批完成后回报一条进展。

## T1 本地化资源框架与切换机制（先跑通）

- [x] 新增 `Resources/Strings.zh-CN.xaml`、`Resources/Strings.en-US.xaml`（键名语义化，覆盖全部页面）。
- [x] 新增 `Services/Localization.cs`：`AppLanguage`、`Loc.T/T(args)`、`Initialize`、`SetLanguage`、`LanguageChanged`、`Culture`。
- [x] `App.xaml` 默认合并中文资源字典；`App.xaml.cs` 启动时 `Loc.Initialize()`。
- [x] `LocalSettings` 增加 `Language`（默认 `zh-Hans`），`LocalStore.Save()` 持久化。
- [x] `SettingsWindow` 增加语言下拉（`AutomationId=LanguageCombo`），切换即 `Loc.SetLanguage(persist:true)`；删除「仅简体中文」。
- [x] `ShellViewModel` 订阅 `LanguageChanged` 刷新派生文案（时间/微信号/状态）。
- [x] 构建通过 + UIA 真实验证：切英文后主界面即时变化；重启保持。

## T2 聊天列表与聊天窗

- [x] `Views/MainWindow.xaml` 聊天列表、搜索、空状态、ToolTip。
- [x] `Views/ChatPaneView.xaml` 全部顶栏/空状态/右键菜单/表情页签/输入区/多选工具栏。
- [x] `ViewModels/ChatPaneViewModel.cs`、`ChatMessageVm.cs`、`ConversationItemVm.cs` 全部弹框/提示/已读/时间。
- [x] `Views/ChatPaneView.xaml.cs` 动态菜单与本地事件文案。
- [x] `TimeFormatter` 中英日期格式；UIA：聊天窗与右键菜单英文正常，发消息链路仍通。

## T3 通讯录、资料页与朋友圈

- [x] `Views/MainWindow.xaml` 通讯录/资料页/我页面。
- [x] `ViewModels/ContactItemVm.cs`、`ShellViewModel.cs` 微信号前缀/未设置/创建时间。
- [x] `Views/MomentsPane.xaml` 封面菜单/空状态/编辑删除 ToolTip/只看。
- [x] `Views/PublishMomentWindow.xaml(.cs)` 发布/编辑标题、按钮、提示、删除确认。
- [x] `ViewModels/MomentsViewModel.cs`、`MomentItemVm.cs` 点赞/评论/删除/时间。
- [x] UIA：资料页字段中英一致；朋友圈发布/编辑/删除确认弹框英文；真实删除/更新仍落库。

## T4 发现页、公众号/小程序、登录页、设置页与全部弹框

- [x] `Views/MainWindow.xaml` 发现菜单、公众号面板、搜一搜、客服/小程序。
- [x] `Views/LoginWindow.xaml(.cs)`、`LoginViewModel.cs`。
- [x] `Views/SettingsWindow.xaml(.cs)` 标题/分组/说明/按钮/快捷键/提示。
- [x] `AppDialog`、`InputDialog`、`CreateGroupWindow`、`EditProfileWindow`、`CallWindow`、`ViewerWindow` 及 `Helpers/Dialogs.cs`。
- [x] `ApiClient`/`ChatConnection` 错误前缀；`AppBackground`/`MomentsCoverStore` 校验错误。
- [x] UIA 抽查并截图：官方账号、搜一搜、登录、设置、群聊、资料编辑、预览、通话。

## T5 收尾

- [x] 构建零错误；全量中文界面字面量扫描（XAML 仅剩注释）。
- [x] 真实重启持久化校验（`settings.json` 的 `Language`）。
- [x] 消息与 SignalR 回归：两个真实账号各建一条 Hub WebSocket，A 调 `SendMessage` → B 收到 `message`/`conversationUpdated`，REST 回读命中，测试消息已删除无残留（朋友圈发布/编辑/删除已在 T3 验证）。
- [x] 清理 `WxChatApp/.tmp-i18n/`（验证脚本与结果已删除）。
- [x] `docs/开发文档.md` 追加「12. 中英文界面切换」；本清单勾选完成。
- [x] 输出「已完成并验证 / 未完成及原因」清单。

## T6 账号信息一致性 + 好友资料编辑

- [x] 资料页移除独立「Account」行，WeChat ID 与头部统一使用 `ContactDto.WxId`。
- [x] 服务端 `friends.remark` 列 + `PUT /api/friends/{id}/profile`（昵称/备注/标签，含长度校验与好友校验）。
- [x] 客户端 `ApiClient.UpdateFriendProfileAsync`、`ShellViewModel.UpdateContactProfileAsync`（写入 + 回读校验 + 刷新通讯录/会话）。
- [x] 新增 `EditContactWindow`（中英文案入字典），资料页新增编辑入口。
- [x] 真实联调：接口写入→回读一致；UIA 验证身份行一致、编辑保存后资料页/通讯录/聊天列表即时更新；测试数据已恢复。
- [x] `docs/开发文档.md` 追加「13. 账号信息一致性与好友资料编辑」。

## T7 服务端单文件分发（2026-11-07）

- [x] 定位根因：旧 `publish\server` 为框架依赖多文件发布，单独拷贝/双击 exe 报 `The application to execute does not exist: ...WxChatServer.dll`。
- [x] `scripts\deploy-server.bat` 改为自包含单文件发布（`--self-contained true` + `PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract`），发布前清理旧多文件产物、保留 `App_Data`。
- [x] 重新发布 `publish\server\WxChatServer.exe`（93.8 MB）；空目录只放该 exe 启动成功，`/api/health` 返回 200。
- [x] 文档更新：`docs/部署文档.md`（发布命令、双击运行、FAQ）、`README.md`（发布/部署/语言切换使用说明）、`docs/开发文档.md` 第 14 节。
