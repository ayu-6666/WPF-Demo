using Microsoft.Data.Sqlite;
using WxChatServer.Models;

namespace WxChatServer.Data;

/// <summary>
/// 轻量 SQLite 持久化封装：所有数据落在本机 wxchat.db，服务重启后仍保留。
/// </summary>
public sealed class Db
{
    private readonly string _connString;
    public string DataDir { get; }
    public string UploadDir { get; }

    public Db()
    {
        DataDir = Environment.GetEnvironmentVariable("WXCHAT_DATA")
                  ?? Path.Combine(AppContext.BaseDirectory, "App_Data");
        Directory.CreateDirectory(DataDir);
        UploadDir = Path.Combine(DataDir, "uploads");
        Directory.CreateDirectory(UploadDir);
        _connString = $"Data Source={Path.Combine(DataDir, "wxchat.db")}";
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connString);
        conn.Open();
        return conn;
    }

    public void Init()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS users(
                id TEXT PRIMARY KEY,
                account TEXT NOT NULL UNIQUE COLLATE NOCASE,
                nickname TEXT NOT NULL,
                signature TEXT NOT NULL DEFAULT '',
                wxid TEXT NOT NULL UNIQUE,
                region TEXT NOT NULL DEFAULT '',
                avatar TEXT NOT NULL DEFAULT '',
                phone TEXT NOT NULL DEFAULT '',
                password_hash TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS sessions(
                token TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS friends(
                owner_id TEXT NOT NULL,
                friend_id TEXT NOT NULL,
                tag TEXT NOT NULL DEFAULT '',
                remark TEXT NOT NULL DEFAULT '',
                friend_group TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL,
                PRIMARY KEY(owner_id, friend_id)
            );
            CREATE TABLE IF NOT EXISTS groups(
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                owner_id TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS group_members(
                group_id TEXT NOT NULL,
                user_id TEXT NOT NULL,
                joined_at TEXT NOT NULL,
                PRIMARY KEY(group_id, user_id)
            );
            CREATE TABLE IF NOT EXISTS messages(
                id TEXT PRIMARY KEY,
                conv_type TEXT NOT NULL,
                conv_id TEXT NOT NULL,
                sender_id TEXT NOT NULL,
                type TEXT NOT NULL,
                content TEXT NOT NULL,
                image_url TEXT,
                at_users TEXT,
                created_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_messages_conv ON messages(conv_type, conv_id, created_at);
            CREATE TABLE IF NOT EXISTS message_reads(
                message_id TEXT NOT NULL,
                user_id TEXT NOT NULL,
                read_at TEXT NOT NULL,
                PRIMARY KEY(message_id, user_id)
            );
            CREATE TABLE IF NOT EXISTS moments(
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                content TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS moment_images(
                post_id TEXT NOT NULL,
                seq INTEGER NOT NULL,
                url TEXT NOT NULL,
                PRIMARY KEY(post_id, seq)
            );
            CREATE TABLE IF NOT EXISTS moment_likes(
                post_id TEXT NOT NULL,
                user_id TEXT NOT NULL,
                created_at TEXT NOT NULL,
                PRIMARY KEY(post_id, user_id)
            );
            CREATE TABLE IF NOT EXISTS moment_comments(
                id TEXT PRIMARY KEY,
                post_id TEXT NOT NULL,
                user_id TEXT NOT NULL,
                content TEXT NOT NULL,
                reply_to TEXT,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS favorites(
                id TEXT PRIMARY KEY,
                owner_id TEXT NOT NULL,
                message_id TEXT NOT NULL,
                conv_type TEXT NOT NULL,
                conv_id TEXT NOT NULL,
                msg_type TEXT NOT NULL,
                content TEXT NOT NULL,
                image_url TEXT,
                created_at TEXT NOT NULL,
                favorited_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_favorites_owner ON favorites(owner_id, favorited_at DESC);

            CREATE TABLE IF NOT EXISTS official_accounts(
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                avatar TEXT NOT NULL DEFAULT '',
                description TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS official_messages(
                id TEXT PRIMARY KEY,
                account_id TEXT NOT NULL,
                title TEXT NOT NULL,
                content TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_official_msg_account ON official_messages(account_id, created_at DESC);
            """;
        cmd.ExecuteNonQuery();

        // ---- 兼容旧库的列增补（SQLite 仅支持 ADD COLUMN，用 PRAGMA 判定避免重复添加）----
        EnsureColumn(conn, "users", "avatar", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "users", "phone", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "friends", "tag", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "friends", "remark", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "friends", "friend_group", "TEXT NOT NULL DEFAULT ''");

        SeedOfficialAccounts(conn);
    }

    /// <summary>首次建库时写入内置公众号与示例图文（本地模拟，不依赖微信开放平台）。</summary>
    private static void SeedOfficialAccounts(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM official_accounts";
        if (Convert.ToInt64(check.ExecuteScalar()) > 0)
        {
            return;
        }

        var accounts = new (string Id, string Name, string Desc)[]
        {
            ("oa_wxchat_team", "WxChat 团队", "产品公告与版本更新说明"),
            ("oa_helper", "WxChat 使用小助手", "聊天、朋友圈与安全使用技巧"),
            ("oa_daily", "每日一文", "每天一篇短文，记录生活与技术")
        };
        var now = DateTime.UtcNow;

        using var insertAccount = conn.CreateCommand();
        insertAccount.CommandText =
            "INSERT INTO official_accounts(id, name, avatar, description, created_at) VALUES(@id, @name, '', @desc, @t)";
        using var insertMessage = conn.CreateCommand();
        insertMessage.CommandText =
            "INSERT INTO official_messages(id, account_id, title, content, created_at) VALUES(@id, @acc, @title, @content, @t)";

        foreach (var a in accounts)
        {
            insertAccount.Parameters.Clear();
            insertAccount.Parameters.AddWithValue("@id", a.Id);
            insertAccount.Parameters.AddWithValue("@name", a.Name);
            insertAccount.Parameters.AddWithValue("@desc", a.Desc);
            insertAccount.Parameters.AddWithValue("@t", now.ToString("o"));
            insertAccount.ExecuteNonQuery();
        }

        var messages = new (string AccountId, string Title, string Content)[]
        {
            ("oa_wxchat_team", "WxChat 桌面版 3.0 更新说明",
             "本次更新新增：消息多选与批量删除、合并转发、好友标签、站内搜索、朋友圈九宫格与图片查看器等。\n\n" +
             "如果你在使用中遇到问题，可以在「发现 → 搜一搜」中检索历史消息，或在通讯录中查看好友详情。"),
            ("oa_wxchat_team", "关于音视频通话能力的说明",
             "当前版本的语音/视频通话已完成呼叫、接听、拒接与通话计时的界面与信令链路，" +
             "但受限于 WPF 平台未内置 WebRTC，暂不传输真实音视频流。后续将评估接入 WebRTC 的方案。"),
            ("oa_helper", "如何给好友设置标签",
             "进入「通讯录」，选中好友后，在右侧详情面板找到「标签」一行，点击「编辑」输入标签并保存即可。" +
             "标签同时会参与站内搜索，方便你按分组快速找到联系人。"),
            ("oa_helper", "朋友圈图片查看技巧",
             "朋友圈支持 1~9 张图片的九宫格自动排版。点击任意图片会打开查看器：滚轮缩放、按住拖动，" +
             "左右方向键切换，ESC 关闭；如果内容是视频，会弹窗自动播放，点击画面暂停/继续。"),
            ("oa_daily", "第一篇：慢下来",
             "写代码久了，容易只盯着屏幕。今天试着把窗口最小化十分钟，去窗边站一会儿。\n\n" +
             "很多卡住的问题，答案往往在离开键盘之后自己浮现。"),
            ("oa_daily", "第二篇：关于记录",
             "把每天遇到的一个小问题记下来，一年后就是一本属于自己的排错手册。\n\n" +
             "记录不需要漂亮，只需要真实。")
        };

        for (var i = 0; i < messages.Length; i++)
        {
            var m = messages[i];
            insertMessage.Parameters.Clear();
            insertMessage.Parameters.AddWithValue("@id", "om_" + i.ToString("D2"));
            insertMessage.Parameters.AddWithValue("@acc", m.AccountId);
            insertMessage.Parameters.AddWithValue("@title", m.Title);
            insertMessage.Parameters.AddWithValue("@content", m.Content);
            insertMessage.Parameters.AddWithValue("@t", now.AddHours(-i * 3).ToString("o"));
            insertMessage.ExecuteNonQuery();
        }
    }

    public sealed class OfficialAccountRecord
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Avatar { get; set; } = "";
        public string Description { get; set; } = "";
        public int MessageCount { get; set; }
    }

    public sealed class OfficialMessageRecord
    {
        public string Id { get; set; } = "";
        public string AccountId { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string CreatedAt { get; set; } = "";
    }

    /// <summary>公众号列表（含图文数量）。</summary>
    public List<OfficialAccountRecord> GetOfficialAccounts()
    {
        var list = new List<OfficialAccountRecord>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT a.*, (SELECT COUNT(*) FROM official_messages m WHERE m.account_id = a.id) AS msg_count
            FROM official_accounts a ORDER BY a.created_at
            """;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new OfficialAccountRecord
            {
                Id = (string)r["id"],
                Name = (string)r["name"],
                Avatar = (string)r["avatar"],
                Description = (string)r["description"],
                MessageCount = Convert.ToInt32(r["msg_count"])
            });
        }
        return list;
    }

    /// <summary>公众号图文列表；accountId 为空时返回全部（按时间倒序）。</summary>
    public List<OfficialMessageRecord> GetOfficialMessages(string? accountId, int limit = 50)
    {
        var list = new List<OfficialMessageRecord>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT m.*, a.name AS account_name FROM official_messages m
            JOIN official_accounts a ON a.id = m.account_id
            WHERE (@acc = '' OR m.account_id = @acc)
            ORDER BY m.created_at DESC LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@acc", accountId ?? "");
        cmd.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 200));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new OfficialMessageRecord
            {
                Id = (string)r["id"],
                AccountId = (string)r["account_id"],
                AccountName = (string)r["account_name"],
                Title = (string)r["title"],
                Content = (string)r["content"],
                CreatedAt = (string)r["created_at"]
            });
        }
        return list;
    }

    private static void EnsureColumn(SqliteConnection conn, string table, string column, string def)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var r = cmd.ExecuteReader();
        var exists = false;
        while (r.Read())
        {
            if ((string)r["name"] == column)
            {
                exists = true;
                break;
            }
        }
        if (!exists)
        {
            using var a = conn.CreateCommand();
            a.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {def}";
            a.ExecuteNonQuery();
        }
    }

    private static string Now() => DateTime.UtcNow.ToString("o");

    // ---------------- 用户 ----------------

    public UserRecord CreateUser(string account, string nickname, string passwordHash)
    {
        var user = new UserRecord
        {
            Id = Guid.NewGuid().ToString("D"),
            Account = account,
            NickName = nickname,
            Signature = "这个人很懒，什么都没写",
            WxId = Auth.AuthService.NewWxId(),
            Region = "中国 上海",
            Avatar = "",
            PasswordHash = passwordHash,
            CreatedAt = Now()
        };
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO users(id, account, nickname, signature, wxid, region, avatar, password_hash, created_at)
            VALUES(@id, @account, @nick, @sig, @wxid, @region, @avatar, @pwd, @created)
            """;
        cmd.Parameters.AddWithValue("@id", user.Id);
        cmd.Parameters.AddWithValue("@account", user.Account);
        cmd.Parameters.AddWithValue("@nick", user.NickName);
        cmd.Parameters.AddWithValue("@sig", user.Signature);
        cmd.Parameters.AddWithValue("@wxid", user.WxId);
        cmd.Parameters.AddWithValue("@region", user.Region);
        cmd.Parameters.AddWithValue("@avatar", user.Avatar);
        cmd.Parameters.AddWithValue("@pwd", user.PasswordHash);
        cmd.Parameters.AddWithValue("@created", user.CreatedAt);
        cmd.ExecuteNonQuery();
        return user;
    }

    public UserRecord? GetUserByAccount(string account)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM users WHERE account = @a COLLATE NOCASE LIMIT 1";
        cmd.Parameters.AddWithValue("@a", account);
        using var r = cmd.ExecuteReader();
        return r.Read() ? MapUser(r) : null;
    }

    public UserRecord? GetUserById(string id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM users WHERE id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? MapUser(r) : null;
    }

    private static UserRecord MapUser(SqliteDataReader r)
    {
        return new UserRecord
        {
            Id = (string)r["id"],
            Account = (string)r["account"],
            NickName = (string)r["nickname"],
            Signature = (string)r["signature"],
            WxId = (string)r["wxid"],
            Region = (string)r["region"],
            Avatar = r["avatar"] is string a ? a : "",
            Phone = r["phone"] is string ph ? ph : "",
            PasswordHash = (string)r["password_hash"],
            CreatedAt = (string)r["created_at"]
        };
    }

    /// <summary>更新个人资料；avatarUrl 为 null 表示不改头像。</summary>
    public void UpdateProfile(string userId, string nickname, string signature, string wxid, string phone, string? avatarUrl = null)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = avatarUrl == null
            ? "UPDATE users SET nickname = @n, signature = @s, wxid = @w, phone = @p WHERE id = @id"
            : "UPDATE users SET nickname = @n, signature = @s, wxid = @w, phone = @p, avatar = @a WHERE id = @id";
        cmd.Parameters.AddWithValue("@n", nickname);
        cmd.Parameters.AddWithValue("@s", signature);
        cmd.Parameters.AddWithValue("@w", wxid);
        cmd.Parameters.AddWithValue("@p", phone);
        if (avatarUrl != null)
        {
            cmd.Parameters.AddWithValue("@a", avatarUrl);
        }
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>微信号是否已被他人占用（不区分大小写）。</summary>
    public bool IsWxIdTaken(string wxid, string excludeUserId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM users WHERE wxid = @w COLLATE NOCASE AND id <> @id";
        cmd.Parameters.AddWithValue("@w", wxid);
        cmd.Parameters.AddWithValue("@id", excludeUserId);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    public List<(UserRecord User, bool IsFriend)> SearchUsers(string keyword, string meId)
    {
        var result = new List<(UserRecord, bool)>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.*, f.created_at AS friend_since
            FROM users u
            LEFT JOIN friends f ON f.owner_id = @me AND f.friend_id = u.id
            WHERE u.id <> @me AND (u.account LIKE @kw OR u.nickname LIKE @kw)
            ORDER BY u.nickname LIMIT 20
            """;
        cmd.Parameters.AddWithValue("@me", meId);
        cmd.Parameters.AddWithValue("@kw", "%" + keyword + "%");
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            result.Add((MapUser(r), r["friend_since"] is string));
        }
        return result;
    }

    // ---------------- 会话令牌 ----------------

    public void CreateSession(string token, string userId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO sessions(token, user_id, created_at) VALUES(@t, @u, @c)";
        cmd.Parameters.AddWithValue("@t", token);
        cmd.Parameters.AddWithValue("@u", userId);
        cmd.Parameters.AddWithValue("@c", Now());
        cmd.ExecuteNonQuery();
    }

    public UserRecord? GetUserByToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.* FROM sessions s JOIN users u ON u.id = s.user_id
            WHERE s.token = @t LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@t", token);
        using var r = cmd.ExecuteReader();
        return r.Read() ? MapUser(r) : null;
    }

    public void DeleteSession(string token)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE token = @t";
        cmd.Parameters.AddWithValue("@t", token);
        cmd.ExecuteNonQuery();
    }

    // ---------------- 好友 ----------------

    public static string DirectConversationId(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? a + "_" + b : b + "_" + a;

    public bool AreFriends(string a, string b)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM friends WHERE owner_id = @a AND friend_id = @b LIMIT 1";
        cmd.Parameters.AddWithValue("@a", a);
        cmd.Parameters.AddWithValue("@b", b);
        using var r = cmd.ExecuteReader();
        return r.Read();
    }

    /// <summary>写入双向好友关系（幂等）。返回是否本次新建立。</summary>
    public bool AddFriendPair(string a, string b)
    {
        var existed = AreFriends(a, b);
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO friends(owner_id, friend_id, created_at) VALUES(@a, @b, @now);
            INSERT OR IGNORE INTO friends(owner_id, friend_id, created_at) VALUES(@b, @a, @now);
            """;
        cmd.Parameters.AddWithValue("@a", a);
        cmd.Parameters.AddWithValue("@b", b);
        cmd.Parameters.AddWithValue("@now", Now());
        cmd.ExecuteNonQuery();
        return !existed;
    }

    public List<ContactDto> GetContacts(string userId)
    {
        var list = new List<ContactDto>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.*, f.created_at AS friend_since, f.tag AS friend_tag,
                   f.remark AS friend_remark, f.friend_group AS friend_group
            FROM friends f JOIN users u ON u.id = f.friend_id
            WHERE f.owner_id = @me
            ORDER BY u.nickname COLLATE NOCASE
            """;
        cmd.Parameters.AddWithValue("@me", userId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new ContactDto
            {
                Id = (string)r["id"],
                Account = (string)r["account"],
                NickName = (string)r["nickname"],
                Remark = r["friend_remark"] is string rm ? rm : "",
                Signature = (string)r["signature"],
                WxId = (string)r["wxid"],
                Region = (string)r["region"],
                Avatar = r["avatar"] is string av ? av : "",
                Tag = r["friend_tag"] is string t ? t : "",
                Group = r["friend_group"] is string g ? g : "",
                CreatedAt = (string)r["friend_since"]
            });
        }
        return list;
    }

    public void UpdateFriendTag(string ownerId, string friendId, string tag)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE friends SET tag = @tag WHERE owner_id = @me AND friend_id = @fid";
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.Parameters.AddWithValue("@me", ownerId);
        cmd.Parameters.AddWithValue("@fid", friendId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateFriendProfile(string ownerId, string friendId, string remark, string tag, string group)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE friends SET remark = @remark, tag = @tag, friend_group = @grp WHERE owner_id = @me AND friend_id = @fid";
        cmd.Parameters.AddWithValue("@remark", remark);
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.Parameters.AddWithValue("@grp", group);
        cmd.Parameters.AddWithValue("@me", ownerId);
        cmd.Parameters.AddWithValue("@fid", friendId);
        cmd.ExecuteNonQuery();
    }

    public string GetFriendTag(string ownerId, string friendId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT tag FROM friends WHERE owner_id = @me AND friend_id = @fid";
        cmd.Parameters.AddWithValue("@me", ownerId);
        cmd.Parameters.AddWithValue("@fid", friendId);
        using var r = cmd.ExecuteReader();
        return r.Read() && r["tag"] is string s ? s : "";
    }

    public List<string> GetFriendIds(string userId)
    {
        var ids = new List<string>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT friend_id FROM friends WHERE owner_id = @me";
        cmd.Parameters.AddWithValue("@me", userId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            ids.Add((string)r["friend_id"]);
        }
        return ids;
    }

    // ---------------- 群组 ----------------

    public string CreateGroup(string name, string ownerId, IEnumerable<string> memberIds)
    {
        var groupId = Guid.NewGuid().ToString("D");
        var now = Now();
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO groups(id, name, owner_id, created_at) VALUES(@id, @n, @o, @now)";
        cmd.Parameters.AddWithValue("@id", groupId);
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@o", ownerId);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.ExecuteNonQuery();

        var distinct = new HashSet<string>(memberIds) { ownerId };
        foreach (var memberId in distinct)
        {
            using var m = conn.CreateCommand();
            m.Transaction = tx;
            m.CommandText = "INSERT OR IGNORE INTO group_members(group_id, user_id, joined_at) VALUES(@g, @u, @now)";
            m.Parameters.AddWithValue("@g", groupId);
            m.Parameters.AddWithValue("@u", memberId);
            m.Parameters.AddWithValue("@now", now);
            m.ExecuteNonQuery();
        }
        tx.Commit();
        return groupId;
    }

    public string? GetGroupName(string groupId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM groups WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", groupId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? (string)r["name"] : null;
    }

    public string? GetGroupOwner(string groupId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT owner_id FROM groups WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", groupId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? (string)r["owner_id"] : null;
    }

    public bool IsGroupMember(string groupId, string userId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM group_members WHERE group_id = @g AND user_id = @u LIMIT 1";
        cmd.Parameters.AddWithValue("@g", groupId);
        cmd.Parameters.AddWithValue("@u", userId);
        using var r = cmd.ExecuteReader();
        return r.Read();
    }

    public List<UserRecord> GetGroupMembers(string groupId)
    {
        var list = new List<UserRecord>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT u.* FROM group_members gm JOIN users u ON u.id = gm.user_id
            WHERE gm.group_id = @g ORDER BY gm.joined_at
            """;
        cmd.Parameters.AddWithValue("@g", groupId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(MapUser(r));
        }
        return list;
    }

    public bool AddGroupMember(string groupId, string userId)
    {
        if (IsGroupMember(groupId, userId))
        {
            return false;
        }
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO group_members(group_id, user_id, joined_at) VALUES(@g, @u, @now)";
        cmd.Parameters.AddWithValue("@g", groupId);
        cmd.Parameters.AddWithValue("@u", userId);
        cmd.Parameters.AddWithValue("@now", Now());
        cmd.ExecuteNonQuery();
        return true;
    }

    public bool RemoveGroupMember(string groupId, string userId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM group_members WHERE group_id = @g AND user_id = @u";
        cmd.Parameters.AddWithValue("@g", groupId);
        cmd.Parameters.AddWithValue("@u", userId);
        return cmd.ExecuteNonQuery() > 0;
    }

    // ---------------- 消息 ----------------

    public MessageRecord InsertMessage(string convType, string convId, string senderId, string type,
        string content, string? imageUrl, IEnumerable<string>? atUserIds)
    {
        var msg = new MessageRecord
        {
            Id = Guid.NewGuid().ToString("D"),
            ConvType = convType,
            ConvId = convId,
            SenderId = senderId,
            Type = type,
            Content = content,
            ImageUrl = imageUrl,
            CreatedAt = Now()
        };
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO messages(id, conv_type, conv_id, sender_id, type, content, image_url, at_users, created_at)
            VALUES(@id, @t, @c, @s, @ty, @content, @img, @at, @created)
            """;
        cmd.Parameters.AddWithValue("@id", msg.Id);
        cmd.Parameters.AddWithValue("@t", convType);
        cmd.Parameters.AddWithValue("@c", convId);
        cmd.Parameters.AddWithValue("@s", senderId);
        cmd.Parameters.AddWithValue("@ty", type);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@img", (object?)imageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@at", atUserIds == null ? "" : string.Join(",", atUserIds));
        cmd.Parameters.AddWithValue("@created", msg.CreatedAt);
        cmd.ExecuteNonQuery();
        return msg;
    }

    public List<MessageRecord> GetMessages(string convType, string convId, int limit, string? afterIso)
    {
        var list = new List<MessageRecord>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        if (afterIso != null)
        {
            cmd.CommandText = """
                SELECT * FROM messages WHERE conv_type = @t AND conv_id = @c AND created_at > @after
                ORDER BY created_at ASC LIMIT @limit
                """;
            cmd.Parameters.AddWithValue("@after", afterIso);
        }
        else
        {
            cmd.CommandText = """
                SELECT * FROM (SELECT * FROM messages WHERE conv_type = @t AND conv_id = @c
                ORDER BY created_at DESC LIMIT @limit) ORDER BY created_at ASC
                """;
        }
        cmd.Parameters.AddWithValue("@t", convType);
        cmd.Parameters.AddWithValue("@c", convId);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(MapMessage(r));
        }
        return list;
    }

    private static MessageRecord MapMessage(SqliteDataReader r)
    {
        return new MessageRecord
        {
            Id = (string)r["id"],
            ConvType = (string)r["conv_type"],
            ConvId = (string)r["conv_id"],
            SenderId = (string)r["sender_id"],
            Type = (string)r["type"],
            Content = (string)r["content"],
            ImageUrl = r["image_url"] is string s ? s : null,
            CreatedAt = (string)r["created_at"]
        };
    }

    public MessageRecord? GetLastMessage(string convType, string convId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM messages WHERE conv_type = @t AND conv_id = @c
            ORDER BY created_at DESC LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@t", convType);
        cmd.Parameters.AddWithValue("@c", convId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? MapMessage(r) : null;
    }

    public int GetUnreadCount(string convType, string convId, string userId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM messages m
            WHERE m.conv_type = @t AND m.conv_id = @c AND m.sender_id <> @me
              AND NOT EXISTS (SELECT 1 FROM message_reads r WHERE r.message_id = m.id AND r.user_id = @me)
            """;
        cmd.Parameters.AddWithValue("@t", convType);
        cmd.Parameters.AddWithValue("@c", convId);
        cmd.Parameters.AddWithValue("@me", userId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? (int)(long)r[0] : 0;
    }

    public HashSet<string> GetReadMessageIds(IEnumerable<string> messageIds)
    {
        var set = new HashSet<string>();
        var ids = messageIds.ToList();
        if (ids.Count == 0)
        {
            return set;
        }
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        var placeholders = string.Join(",", ids.Select((_, i) => "@p" + i));
        cmd.CommandText = $"SELECT DISTINCT message_id FROM message_reads WHERE message_id IN ({placeholders})";
        for (var i = 0; i < ids.Count; i++)
        {
            cmd.Parameters.AddWithValue("@p" + i, ids[i]);
        }
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            set.Add((string)r[0]);
        }
        return set;
    }

    /// <summary>将会话中他人发来的消息标记为已读，返回需要通知的对方用户 Id。</summary>
    public List<string> MarkRead(string convType, string convId, string readerId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO message_reads(message_id, user_id, read_at)
            SELECT m.id, @me, @now FROM messages m
            WHERE m.conv_type = @t AND m.conv_id = @c AND m.sender_id <> @me
              AND NOT EXISTS (SELECT 1 FROM message_reads r WHERE r.message_id = m.id AND r.user_id = @me)
            """;
        cmd.Parameters.AddWithValue("@me", readerId);
        cmd.Parameters.AddWithValue("@t", convType);
        cmd.Parameters.AddWithValue("@c", convId);
        cmd.Parameters.AddWithValue("@now", Now());
        cmd.ExecuteNonQuery();
        return GetConversationMembers(convType, convId).Where(x => x != readerId).ToList();
    }

    public List<string> GetConversationMembers(string convType, string convId)
    {
        if (convType == "single")
        {
            return convId.Split('_', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        var list = new List<string>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT user_id FROM group_members WHERE group_id = @g";
        cmd.Parameters.AddWithValue("@g", convId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add((string)r[0]);
        }
        return list;
    }

    public List<ConversationDto> GetConversations(string meId)
    {
        var list = new List<ConversationDto>();

        // 单聊：以好友关系为基线，仅展示已有消息的会话
        foreach (var contact in GetContacts(meId))
        {
            var convId = DirectConversationId(meId, contact.Id);
            var last = GetLastMessage("single", convId);
            if (last == null)
            {
                continue;
            }
            var sender = GetUserById(last.SenderId);
            list.Add(new ConversationDto
            {
                Type = "single",
                Id = convId,
                Title = string.IsNullOrWhiteSpace(contact.Remark) ? contact.NickName : contact.Remark,
                Avatar = contact.Avatar,
                LastMessage = Preview(last),
                LastMessageTime = last.CreatedAt,
                LastSenderName = sender?.NickName ?? "",
                Unread = GetUnreadCount("single", convId, meId),
                IsGroup = false,
                MemberCount = 2
            });
        }

        // 群聊
        using (var conn = Open())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT g.id, g.name, (SELECT COUNT(*) FROM group_members x WHERE x.group_id = g.id) AS mc
                FROM groups g JOIN group_members gm ON gm.group_id = g.id
                WHERE gm.user_id = @me
                """;
            cmd.Parameters.AddWithValue("@me", meId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var groupId = (string)r["id"];
                var groupName = (string)r["name"];
                var memberCount = (int)(long)r["mc"];
                var last = GetLastMessage("group", groupId);
                if (last == null)
                {
                    continue;
                }
                var sender = GetUserById(last.SenderId);
                list.Add(new ConversationDto
                {
                    Type = "group",
                    Id = groupId,
                    Title = groupName,
                    LastMessage = Preview(last),
                    LastMessageTime = last.CreatedAt,
                    LastSenderName = sender?.NickName ?? "",
                    Unread = GetUnreadCount("group", groupId, meId),
                    IsGroup = true,
                    MemberCount = memberCount
                });
            }
        }

        return list.OrderByDescending(c => c.LastMessageTime).ToList();
    }

    private static string Preview(MessageRecord msg)
    {
        switch (msg.Type)
        {
            case "image":
                return "[图片]";
            case "voice":
                return "[语音]";
            case "file":
            {
                var name = msg.Content.Split('|')[0];
                return "[文件] " + (name.Length > 30 ? name[..30] + "…" : name);
            }
            case "merge":
                return "[聊天记录]";
            default:
                break;
        }
        return msg.Content.Length > 60 ? msg.Content[..60] + "…" : msg.Content;
    }

    public bool DeleteMessage(string messageId, string requesterId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sender_id FROM messages WHERE id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("@id", messageId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return false;
        if ((string)r["sender_id"] != requesterId) return false;
        using var del = conn.CreateCommand();
        del.CommandText = "DELETE FROM messages WHERE id = @id";
        del.Parameters.AddWithValue("@id", messageId);
        del.ExecuteNonQuery();
        using var rd = conn.CreateCommand();
        rd.CommandText = "DELETE FROM message_reads WHERE message_id = @id";
        rd.Parameters.AddWithValue("@id", messageId);
        rd.ExecuteNonQuery();
        return true;
    }

    /// <summary>按关键字搜索当前用户可见会话中的文本消息。</summary>
    public List<MessageRecord> SearchMessages(string userId, string keyword, int limit = 50)
    {
        var list = new List<MessageRecord>();
        var k = keyword.Trim();
        if (k.Length == 0) return list;
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM messages
            WHERE type = 'text' AND content LIKE @kw AND (
                (conv_type = 'single' AND (
                    conv_id LIKE @me_a ESCAPE '\'
                    OR conv_id LIKE @me_b ESCAPE '\'
                ))
                OR (conv_type = 'group' AND EXISTS (
                    SELECT 1 FROM group_members gm WHERE gm.group_id = conv_id AND gm.user_id = @me
                ))
            )
            ORDER BY created_at DESC LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@kw", "%" + k + "%");
        // 单聊会话 id 形如「我的id_对方id」，用前缀/后缀匹配，_ 需转义
        cmd.Parameters.AddWithValue("@me_a", userId + @"\_%");
        cmd.Parameters.AddWithValue("@me_b", @"%\_" + userId);
        cmd.Parameters.AddWithValue("@me", userId);
        cmd.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 300));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(MapMessage(r));
        }
        return list;
    }

    /// <summary>按关键字搜索当前用户可见的朋友圈（自己 + 好友发布的内容）。</summary>
    public List<MomentDto> SearchMoments(string meId, string keyword, int limit = 50)
    {
        var posts = new List<MomentDto>();
        var k = keyword.Trim();
        if (k.Length == 0) return posts;
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.*, u.nickname AS author_name, u.avatar AS author_avatar
            FROM moments p JOIN users u ON u.id = p.user_id
            WHERE p.content LIKE @kw AND (
                p.user_id = @me OR p.user_id IN (SELECT friend_id FROM friends WHERE owner_id = @me)
            )
            ORDER BY p.created_at DESC LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@kw", "%" + k + "%");
        cmd.Parameters.AddWithValue("@me", meId);
        cmd.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 200));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            posts.Add(new MomentDto
            {
                Id = (string)r["id"],
                UserId = (string)r["user_id"],
                UserName = (string)r["author_name"],
                UserAvatar = r["author_avatar"] is string av ? av : "",
                Content = (string)r["content"],
                CreatedAt = (string)r["created_at"],
                IsMine = (string)r["user_id"] == meId
            });
        }
        foreach (var post in posts)
        {
            post.Images = GetMomentImages(post.Id);
        }
        return posts;
    }

    // ---------------- 收藏 ----------------

    public class FavoriteRecord
    {
        public string Id { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public string MessageId { get; set; } = "";
        public string ConvType { get; set; } = "";
        public string ConvId { get; set; } = "";
        public string MsgType { get; set; } = "";
        public string Content { get; set; } = "";
        public string? ImageUrl { get; set; }
        public string MessageCreatedAt { get; set; } = "";
        public string FavoritedAt { get; set; } = "";
    }

    public List<FavoriteRecord> GetFavorites(string ownerId)
    {
        var list = new List<FavoriteRecord>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM favorites WHERE owner_id = @me ORDER BY favorited_at DESC LIMIT 200";
        cmd.Parameters.AddWithValue("@me", ownerId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new FavoriteRecord
            {
                Id = (string)r["id"],
                OwnerId = (string)r["owner_id"],
                MessageId = (string)r["message_id"],
                ConvType = (string)r["conv_type"],
                ConvId = (string)r["conv_id"],
                MsgType = (string)r["msg_type"],
                Content = (string)r["content"],
                ImageUrl = r["image_url"] is string s ? s : null,
                MessageCreatedAt = (string)r["created_at"],
                FavoritedAt = (string)r["favorited_at"]
            });
        }
        return list;
    }

    public FavoriteRecord? AddFavorite(string ownerId, MessageRecord msg)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO favorites(id, owner_id, message_id, conv_type, conv_id, msg_type, content, image_url, created_at, favorited_at)
            VALUES(@id, @me, @mid, @t, @cid, @mt, @content, @img, @created, @now)
            """;
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("D"));
        cmd.Parameters.AddWithValue("@me", ownerId);
        cmd.Parameters.AddWithValue("@mid", msg.Id);
        cmd.Parameters.AddWithValue("@t", msg.ConvType);
        cmd.Parameters.AddWithValue("@cid", msg.ConvId);
        cmd.Parameters.AddWithValue("@mt", msg.Type);
        cmd.Parameters.AddWithValue("@content", msg.Content);
        cmd.Parameters.AddWithValue("@img", (object?)msg.ImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", msg.CreatedAt);
        cmd.Parameters.AddWithValue("@now", Now());
        if (cmd.ExecuteNonQuery() == 0) return null;
        return GetFavorites(ownerId).FirstOrDefault(f => f.MessageId == msg.Id);
    }

    public bool RemoveFavorite(string ownerId, string favoriteId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM favorites WHERE id = @id AND owner_id = @me";
        cmd.Parameters.AddWithValue("@id", favoriteId);
        cmd.Parameters.AddWithValue("@me", ownerId);
        return cmd.ExecuteNonQuery() > 0;
    }

    // ---------------- 朋友圈 ----------------

    public List<MomentDto> GetMoments(string meId)
    {
        var posts = new List<MomentDto>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.*, u.nickname AS author_name, u.avatar AS author_avatar
            FROM moments p JOIN users u ON u.id = p.user_id
            WHERE p.user_id = @me OR p.user_id IN (SELECT friend_id FROM friends WHERE owner_id = @me)
            ORDER BY p.created_at DESC LIMIT 100
            """;
        cmd.Parameters.AddWithValue("@me", meId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var dto = new MomentDto
            {
                Id = (string)r["id"],
                UserId = (string)r["user_id"],
                UserName = (string)r["author_name"],
                UserAvatar = r["author_avatar"] is string av ? av : "",
                Content = (string)r["content"],
                CreatedAt = (string)r["created_at"],
                IsMine = (string)r["user_id"] == meId
            };
            posts.Add(dto);
        }
        foreach (var post in posts)
        {
            post.Images = GetMomentImages(post.Id);
            post.Likes = GetMomentLikes(post.Id);
            post.Comments = GetMomentComments(post.Id);
            post.LikedByMe = post.Likes.Any(l => l.UserId == meId);
        }
        return posts;
    }

    private List<string> GetMomentImages(string postId)
    {
        var list = new List<string>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT url FROM moment_images WHERE post_id = @p ORDER BY seq";
        cmd.Parameters.AddWithValue("@p", postId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add((string)r[0]);
        }
        return list;
    }

    private List<MomentLikeDto> GetMomentLikes(string postId)
    {
        var list = new List<MomentLikeDto>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT l.user_id, u.nickname FROM moment_likes l JOIN users u ON u.id = l.user_id
            WHERE l.post_id = @p ORDER BY l.created_at
            """;
        cmd.Parameters.AddWithValue("@p", postId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new MomentLikeDto { UserId = (string)r["user_id"], UserName = (string)r["nickname"] });
        }
        return list;
    }

    private List<MomentCommentDto> GetMomentComments(string postId)
    {
        var list = new List<MomentCommentDto>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.*, u.nickname AS commenter, ru.nickname AS reply_name
            FROM moment_comments c
            JOIN users u ON u.id = c.user_id
            LEFT JOIN moment_comments rc ON rc.id = c.reply_to
            LEFT JOIN users ru ON ru.id = rc.user_id
            WHERE c.post_id = @p ORDER BY c.created_at
            """;
        cmd.Parameters.AddWithValue("@p", postId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new MomentCommentDto
            {
                Id = (string)r["id"],
                UserId = (string)r["user_id"],
                UserName = (string)r["commenter"],
                Content = (string)r["content"],
                ReplyToName = r["reply_name"] is string s ? s : null,
                CreatedAt = (string)r["created_at"]
            });
        }
        return list;
    }

    public void PublishMoment(string postId, string userId, string content, IEnumerable<string> imageUrls)
    {
        var now = Now();
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO moments(id, user_id, content, created_at) VALUES(@id, @u, @c, @now)";
        cmd.Parameters.AddWithValue("@id", postId);
        cmd.Parameters.AddWithValue("@u", userId);
        cmd.Parameters.AddWithValue("@c", content);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.ExecuteNonQuery();

        var seq = 0;
        foreach (var url in imageUrls)
        {
            using var img = conn.CreateCommand();
            img.Transaction = tx;
            img.CommandText = "INSERT INTO moment_images(post_id, seq, url) VALUES(@p, @s, @u)";
            img.Parameters.AddWithValue("@p", postId);
            img.Parameters.AddWithValue("@s", seq++);
            img.Parameters.AddWithValue("@u", url);
            img.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>删除本人动态（连同图片、点赞、评论），作者不符则返回 false。</summary>
    public bool DeleteMoment(string postId, string userId)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using var check = conn.CreateCommand();
        check.Transaction = tx;
        check.CommandText = "SELECT user_id FROM moments WHERE id = @id";
        check.Parameters.AddWithValue("@id", postId);
        var author = check.ExecuteScalar() as string;
        if (author == null || author != userId)
        {
            tx.Rollback();
            return false;
        }
        foreach (var table in new[] { "moment_images", "moment_likes", "moment_comments" })
        {
            using var del = conn.CreateCommand();
            del.Transaction = tx;
            del.CommandText = $"DELETE FROM {table} WHERE post_id = @id";
            del.Parameters.AddWithValue("@id", postId);
            del.ExecuteNonQuery();
        }
        using var delPost = conn.CreateCommand();
        delPost.Transaction = tx;
        delPost.CommandText = "DELETE FROM moments WHERE id = @id";
        delPost.Parameters.AddWithValue("@id", postId);
        delPost.ExecuteNonQuery();
        tx.Commit();
        return true;
    }

    /// <summary>修改本人动态的文字与图片（图片整体替换），作者不符则返回 false。</summary>
    public bool UpdateMoment(string postId, string userId, string content, IEnumerable<string> imageUrls)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using var check = conn.CreateCommand();
        check.Transaction = tx;
        check.CommandText = "SELECT user_id FROM moments WHERE id = @id";
        check.Parameters.AddWithValue("@id", postId);
        var author = check.ExecuteScalar() as string;
        if (author == null || author != userId)
        {
            tx.Rollback();
            return false;
        }
        using var upd = conn.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = "UPDATE moments SET content = @c WHERE id = @id";
        upd.Parameters.AddWithValue("@id", postId);
        upd.Parameters.AddWithValue("@c", content);
        upd.ExecuteNonQuery();

        using var delImgs = conn.CreateCommand();
        delImgs.Transaction = tx;
        delImgs.CommandText = "DELETE FROM moment_images WHERE post_id = @id";
        delImgs.Parameters.AddWithValue("@id", postId);
        delImgs.ExecuteNonQuery();

        var seq = 0;
        foreach (var url in imageUrls)
        {
            using var img = conn.CreateCommand();
            img.Transaction = tx;
            img.CommandText = "INSERT INTO moment_images(post_id, seq, url) VALUES(@p, @s, @u)";
            img.Parameters.AddWithValue("@p", postId);
            img.Parameters.AddWithValue("@s", seq++);
            img.Parameters.AddWithValue("@u", url);
            img.ExecuteNonQuery();
        }
        tx.Commit();
        return true;
    }

    public bool ToggleLike(string postId, string userId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM moment_likes WHERE post_id = @p AND user_id = @u";
        cmd.Parameters.AddWithValue("@p", postId);
        cmd.Parameters.AddWithValue("@u", userId);
        var deleted = cmd.ExecuteNonQuery();
        if (deleted > 0)
        {
            return false;
        }
        using var insert = conn.CreateCommand();
        insert.CommandText = "INSERT INTO moment_likes(post_id, user_id, created_at) VALUES(@p, @u, @now)";
        insert.Parameters.AddWithValue("@p", postId);
        insert.Parameters.AddWithValue("@u", userId);
        insert.Parameters.AddWithValue("@now", Now());
        insert.ExecuteNonQuery();
        return true;
    }

    public MomentCommentDto AddComment(string postId, string userId, string content, string? replyToId)
    {
        var id = Guid.NewGuid().ToString("D");
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO moment_comments(id, post_id, user_id, content, reply_to, created_at)
            VALUES(@id, @p, @u, @c, @r, @now)
            """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@p", postId);
        cmd.Parameters.AddWithValue("@u", userId);
        cmd.Parameters.AddWithValue("@c", content);
        cmd.Parameters.AddWithValue("@r", (object?)replyToId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@now", Now());
        cmd.ExecuteNonQuery();

        var user = GetUserById(userId);
        string? replyName = null;
        if (replyToId != null)
        {
            using var rc = conn.CreateCommand();
            rc.CommandText = """
                SELECT u.nickname FROM moment_comments c JOIN users u ON u.id = c.user_id WHERE c.id = @id
                """;
            rc.Parameters.AddWithValue("@id", replyToId);
            using var rr = rc.ExecuteReader();
            replyName = rr.Read() ? (string)rr[0] : null;
        }
        return new MomentCommentDto
        {
            Id = id,
            UserId = userId,
            UserName = user?.NickName ?? "",
            Content = content,
            ReplyToName = replyName,
            CreatedAt = Now()
        };
    }

    public string? GetMomentAuthor(string postId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT user_id FROM moments WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", postId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? (string)r[0] : null;
    }

    /// <summary>朋友圈可见范围（作者 + 作者好友），用于实时推送。</summary>
    public List<string> GetMomentAudience(string postId)
    {
        var author = GetMomentAuthor(postId);
        if (author == null)
        {
            return new List<string>();
        }
        var audience = GetFriendIds(author);
        audience.Add(author);
        return audience.Distinct().ToList();
    }
}
