using System.Net;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WxChatApp.Models;

namespace WxChatApp.Services;

public sealed class ApiException : Exception
{
    public ApiException(string message) : base(message)
    {
    }
}

/// <summary>REST 接口客户端；所有业务数据都来自真实服务端。</summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private HttpClient _http = CreateHttpClient(AppConfig.ServerBaseUrl);

    public string? Token { get; private set; }

    private static HttpClient CreateHttpClient(string baseUrl)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            // 大文件上传/下载可能持续较久，单个请求给足 10 分钟
            Timeout = TimeSpan.FromMinutes(10)
        };
        return client;
    }

    /// <summary>切换服务端地址（设置页保存后调用），并保留当前登录令牌。</summary>
    public void SetServerBaseUrl(string baseUrl)
    {
        var old = _http;
        var next = CreateHttpClient(baseUrl);
        if (!string.IsNullOrWhiteSpace(Token))
        {
            next.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }
        _http = next;
        try
        {
            old.Dispose();
        }
        catch
        {
            // 旧客户端可能仍有请求在途，忽略释放异常
        }
    }

    public void SetToken(string? token)
    {
        Token = token;
        _http.DefaultRequestHeaders.Authorization =
            token == null ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>把网络层异常转换成可读的中文原因，避免只看到 "An error occurred..."。</summary>
    private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage req)
    {
        try
        {
            return await _http.SendAsync(req);
        }
        catch (TaskCanceledException)
        {
            throw new ApiException(Loc.T("Api.Timeout", _http.BaseAddress?.ToString() ?? ""));
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(Loc.T("Api.NetworkError", _http.BaseAddress?.ToString() ?? "", ex.Message));
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var req = new HttpRequestMessage(method, path);
        if (body != null)
        {
            req.Content = JsonContent.Create(body, options: JsonOptions);
        }
        var resp = await SendRawAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var message = Loc.T("Api.RequestFail", (int)resp.StatusCode);
            try
            {
                var json = await resp.Content.ReadAsStringAsync();
                var err = JsonSerializer.Deserialize<ErrorDto>(json, JsonOptions);
                if (!string.IsNullOrWhiteSpace(err?.Code))
                {
                    message = ErrorCodeText(err!.Code!) ?? message;
                }
                else if (!string.IsNullOrWhiteSpace(err?.Error))
                {
                    message = err!.Error!;
                }
            }
            catch
            {
                // 保留默认错误信息
            }
            resp.Dispose();
            throw new ApiException(message);
        }
        return resp;
    }

    private static string? ErrorCodeText(string code) => code switch
    {
        "wxid_taken" => Loc.T("Api.Error.WxIdTaken"),
        "wxid_invalid" => Loc.T("Api.Error.WxIdInvalid"),
        "phone_invalid" => Loc.T("Api.Error.PhoneInvalid"),
        "nickname_invalid" => Loc.T("Api.Error.NicknameInvalid"),
        "signature_invalid" => Loc.T("Api.Error.SignatureInvalid"),
        "remark_invalid" => Loc.T("Api.Error.RemarkInvalid"),
        "tag_invalid" => Loc.T("Api.Error.TagInvalid"),
        "group_invalid" => Loc.T("Api.Error.GroupInvalid"),
        _ => null
    };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage resp)
    {
        var json = await resp.Content.ReadAsStringAsync();
        var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
        if (value == null)
        {
            throw new ApiException(Loc.T("Api.EmptyResponse"));
        }
        return value;
    }

    public async Task<AuthResult> RegisterAsync(string account, string password, string nickname)
    {
        using var resp = await SendAsync(HttpMethod.Post, "/api/auth/register",
            new { account, password, nickName = nickname });
        return await ReadAsync<AuthResult>(resp);
    }

    public async Task<AuthResult> LoginAsync(string account, string password)
    {
        using var resp = await SendAsync(HttpMethod.Post, "/api/auth/login", new { account, password });
        return await ReadAsync<AuthResult>(resp);
    }

    public async Task LogoutAsync()
    {
        try
        {
            using var resp = await SendAsync(HttpMethod.Post, "/api/auth/logout");
        }
        finally
        {
            SetToken(null);
        }
    }

    public async Task<UserDto> GetMeAsync()
    {
        using var resp = await SendAsync(HttpMethod.Get, "/api/me");
        return await ReadAsync<UserDto>(resp);
    }

    public async Task<UserDto> UpdateProfileAsync(string nickname, string signature, string wxid, string phone, string? avatarUrl = null)
    {
        object body = avatarUrl != null
            ? new { nickName = nickname, signature, wxId = wxid, phone, avatarUrl }
            : new { nickName = nickname, signature, wxId = wxid, phone };
        using var resp = await SendAsync(HttpMethod.Put, "/api/me", body);
        return await ReadAsync<UserDto>(resp);
    }

    public async Task<UserDto?> GetUserByIdAsync(string id)
    {
        using var resp = await SendAsync(HttpMethod.Get, "/api/users/" + id);
        return await ReadAsync<UserDto>(resp);
    }

    public async Task UpdateFriendTagAsync(string friendId, string tag)
    {
        using var _ = await SendAsync(HttpMethod.Put, $"/api/friends/{friendId}/tag", new { tag });
    }

    /// <summary>更新好友资料（备注/标签/分组），返回服务端回写后的最新资料。</summary>
    public async Task<ContactDto> UpdateFriendProfileAsync(string friendId, string remark, string tag, string group)
    {
        using var resp = await SendAsync(HttpMethod.Put, $"/api/friends/{friendId}/profile",
            new { remark, tag, group });
        return await ReadAsync<ContactDto>(resp);
    }

    public async Task<bool> DeleteMessageAsync(string messageId)
    {
        try
        {
            using var _ = await SendAsync(HttpMethod.Delete, "/api/messages/" + messageId);
            return true;
        }
        catch (ApiException)
        {
            return false;
        }
    }

    public async Task<List<MessageDto>> SearchMessagesAsync(string keyword, int limit = 50)
    {
        var url = "/api/messages/search?q=" + Uri.EscapeDataString(keyword) + "&limit=" + limit;
        using var resp = await SendAsync(HttpMethod.Get, url);
        return await ReadAsync<List<MessageDto>>(resp);
    }

    public async Task<List<MomentDto>> SearchMomentsAsync(string keyword, int limit = 50)
    {
        var url = "/api/moments/search?q=" + Uri.EscapeDataString(keyword) + "&limit=" + limit;
        using var resp = await SendAsync(HttpMethod.Get, url);
        return await ReadAsync<List<MomentDto>>(resp);
    }

    public async Task<List<OfficialAccountDto>> GetOfficialAccountsAsync()
    {
        using var resp = await SendAsync(HttpMethod.Get, "/api/official/accounts");
        return await ReadAsync<List<OfficialAccountDto>>(resp);
    }

    public async Task<List<OfficialMessageDto>> GetOfficialMessagesAsync(string? accountId = null, int limit = 50)
    {
        var url = "/api/official/messages?limit=" + limit;
        if (!string.IsNullOrEmpty(accountId))
        {
            url += "&accountId=" + Uri.EscapeDataString(accountId);
        }
        using var resp = await SendAsync(HttpMethod.Get, url);
        return await ReadAsync<List<OfficialMessageDto>>(resp);
    }

    public async Task<List<FavoriteItem>> GetFavoritesAsync()
    {
        using var resp = await SendAsync(HttpMethod.Get, "/api/favorites");
        return await ReadAsync<List<FavoriteItem>>(resp);
    }

    public async Task<FavoriteItem?> AddFavoriteAsync(string messageId)
    {
        try
        {
            using var resp = await SendAsync(HttpMethod.Post, "/api/favorites", new { messageId });
            return await ReadAsync<FavoriteItem>(resp);
        }
        catch (ApiException)
        {
            return null; // 409 已收藏等
        }
    }

    public async Task<bool> RemoveFavoriteAsync(string favoriteId)
    {
        try
        {
            using var _ = await SendAsync(HttpMethod.Delete, "/api/favorites/" + favoriteId);
            return true;
        }
        catch (ApiException)
        {
            return false;
        }
    }

    public async Task<List<UserDto>> SearchUsersAsync(string keyword)
    {
        using var resp = await SendAsync(HttpMethod.Get,
            "/api/users/search?q=" + Uri.EscapeDataString(keyword));
        return await ReadAsync<List<UserDto>>(resp);
    }

    public async Task<UserDto> AddFriendAsync(string account)
    {
        using var resp = await SendAsync(HttpMethod.Post, "/api/friends", new { account });
        return await ReadAsync<UserDto>(resp);
    }

    public async Task<List<ContactDto>> GetContactsAsync()
    {
        using var resp = await SendAsync(HttpMethod.Get, "/api/contacts");
        return await ReadAsync<List<ContactDto>>(resp);
    }

    public async Task<List<ConversationDto>> GetConversationsAsync()
    {
        using var resp = await SendAsync(HttpMethod.Get, "/api/conversations");
        return await ReadAsync<List<ConversationDto>>(resp);
    }

    public async Task<List<MessageDto>> GetMessagesAsync(string convType, string convId,
        int limit = 100, string? after = null)
    {
        var url = $"/api/messages?type={convType}&id={Uri.EscapeDataString(convId)}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(after))
        {
            url += "&after=" + Uri.EscapeDataString(after);
        }
        using var resp = await SendAsync(HttpMethod.Get, url);
        return await ReadAsync<List<MessageDto>>(resp);
    }

    public async Task<GroupDto> CreateGroupAsync(string name, IEnumerable<string> memberIds)
    {
        using var resp = await SendAsync(HttpMethod.Post, "/api/groups",
            new { name, memberIds = memberIds.ToList() });
        return await ReadAsync<GroupDto>(resp);
    }

    public async Task<GroupDto> GetGroupAsync(string groupId)
    {
        using var resp = await SendAsync(HttpMethod.Get, "/api/groups/" + groupId);
        return await ReadAsync<GroupDto>(resp);
    }

    public async Task AddGroupMemberAsync(string groupId, string account)
    {
        using var _ = await SendAsync(HttpMethod.Post, $"/api/groups/{groupId}/members", new { account });
    }

    public async Task<List<MomentDto>> GetMomentsAsync()
    {
        using var resp = await SendAsync(HttpMethod.Get, "/api/moments");
        return await ReadAsync<List<MomentDto>>(resp);
    }

    public async Task<MomentDto> PublishMomentAsync(string content, IEnumerable<string> images)
    {
        using var resp = await SendAsync(HttpMethod.Post, "/api/moments",
            new { content, images = images.ToList() });
        return await ReadAsync<MomentDto>(resp);
    }

    public async Task<bool> ToggleLikeAsync(string postId)
    {
        using var resp = await SendAsync(HttpMethod.Post, $"/api/moments/{postId}/like");
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("liked", out var liked) && liked.GetBoolean();
    }

    public async Task<MomentCommentDto> CommentMomentAsync(string postId, string content, string? replyToId)
    {
        using var resp = await SendAsync(HttpMethod.Post, $"/api/moments/{postId}/comments",
            new { content, replyToId });
        return await ReadAsync<MomentCommentDto>(resp);
    }

    public async Task DeleteMomentAsync(string postId)
    {
        using var _ = await SendAsync(HttpMethod.Delete, "/api/moments/" + postId);
    }

    public async Task<MomentDto> UpdateMomentAsync(string postId, string content, IEnumerable<string> images)
    {
        using var resp = await SendAsync(HttpMethod.Put, "/api/moments/" + postId,
            new { content, images = images.ToList() });
        return await ReadAsync<MomentDto>(resp);
    }

    public async Task<string> UploadImageAsync(string filePath) => await UploadFileAsync(filePath, Loc.T("Api.ImageUploadFail"));

    /// <summary>通用文件上传（图片/语音/文件共用 /api/upload），返回可访问的 URL。</summary>
    public async Task<string> UploadFileAsync(string filePath, string? errorPrefix = null)
    {
        errorPrefix ??= Loc.T("Api.FileUploadFail");
        var info = new FileInfo(filePath);
        if (info.Length > AppConfig.MaxUploadBytes)
        {
            throw new ApiException(Loc.T("Chat.FileTooLarge", info.Name,
                (AppConfig.MaxUploadBytes / 1024 / 1024).ToString()));
        }
        using var form = new MultipartFormDataContent();
        // 流式上传，避免把几十 MB 的视频整体读进内存
        await using var stream = File.OpenRead(filePath);
        var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(GuessContentType(filePath));
        form.Add(content, "file", Path.GetFileName(filePath));
        HttpResponseMessage resp;
        try
        {
            resp = await _http.PostAsync("/api/upload", form);
        }
        catch (TaskCanceledException)
        {
            throw new ApiException(Loc.T("Api.Timeout", _http.BaseAddress?.ToString() ?? ""));
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(Loc.T("Api.NetworkError", _http.BaseAddress?.ToString() ?? "", ex.Message));
        }
        if (!resp.IsSuccessStatusCode)
        {
            var status = resp.StatusCode;
            var text = await resp.Content.ReadAsStringAsync();
            resp.Dispose();
            throw new ApiException(errorPrefix +
                                   (string.IsNullOrWhiteSpace(text) ? status.ToString() : text));
        }
        using (resp)
        {
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("url").GetString() ?? throw new ApiException(Loc.T("Api.InvalidFileUrl"));
        }
    }

    /// <summary>把服务端媒体文件下载到指定路径。</summary>
    public async Task DownloadFileAsync(string url, string destPath)
    {
        HttpResponseMessage resp;
        try
        {
            resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (TaskCanceledException)
        {
            throw new ApiException(Loc.T("Api.Timeout", _http.BaseAddress?.ToString() ?? ""));
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(Loc.T("Api.NetworkError", _http.BaseAddress?.ToString() ?? "", ex.Message));
        }
        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                throw new ApiException(Loc.T("Api.DownloadFail", (int)resp.StatusCode));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(destPath);
            await src.CopyToAsync(dst);
        }
    }

    /// <summary>把消息里的媒体地址解析成绝对地址（兼容服务端返回相对路径）。</summary>
    public Uri ResolveUri(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var abs))
        {
            return abs;
        }
        return new Uri(_http.BaseAddress!, url.TrimStart('/'));
    }

    private static string GuessContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".svg" => "image/svg+xml",
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        ".m4a" => "audio/mp4",
        ".aac" => "audio/aac",
        ".ogg" => "audio/ogg",
        ".flac" => "audio/flac",
        ".mp4" or ".m4v" => "video/mp4",
        ".mov" => "video/quicktime",
        ".avi" => "video/x-msvideo",
        ".mkv" => "video/x-matroska",
        ".wmv" => "video/x-ms-wmv",
        ".webm" => "video/webm",
        ".flv" => "video/x-flv",
        ".txt" => "text/plain",
        ".pdf" => "application/pdf",
        ".zip" => "application/zip",
        ".json" => "application/json",
        _ => "application/octet-stream"
    };
}
