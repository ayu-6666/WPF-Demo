using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace BitTomrrorApp.Services;

public class DownloadErrorException : Exception
{
    public string Code { get; }

    public DownloadErrorException(string code, string message, Exception inner = null) : base(message, inner)
    {
        Code = code;
    }
}

public static class DownloadErrors
{
    public static DownloadErrorException FromHttp(Exception ex, string prefix = "HTTP")
    {
        if (ex is DownloadErrorException d) return d;

        if (ex is HttpRequestException hre)
        {
            if (hre.StatusCode.HasValue)
            {
                var code = (int)hre.StatusCode.Value;
                return new DownloadErrorException(
                    $"{prefix}_{code}",
                    HttpMessage(hre.StatusCode.Value),
                    ex);
            }
        }

        var socket = ex.InnerException as SocketException ?? ex as SocketException;
        if (socket != null)
        {
            return socket.SocketErrorCode switch
            {
                SocketError.HostNotFound or SocketError.NoData =>
                    new DownloadErrorException("DNS_FAILED", "无法解析服务器域名（DNS 失败）", ex),
                SocketError.ConnectionRefused =>
                    new DownloadErrorException("CONNECTION_REFUSED", "服务器拒绝连接（端口未开放）", ex),
                SocketError.TimedOut =>
                    new DownloadErrorException("CONNECTION_TIMEOUT", "连接服务器超时", ex),
                _ => new DownloadErrorException("NETWORK_FAILED", $"网络连接失败：{socket.Message}", ex)
            };
        }

        if (ex is TaskCanceledException)
            return new DownloadErrorException("TIMEOUT", "请求超时或被取消", ex);

        return new DownloadErrorException($"{prefix}_FAILED", ex.Message, ex);
    }

    public static string HttpMessage(HttpStatusCode status)
    {
        return status switch
        {
            HttpStatusCode.Unauthorized => "服务器返回 401：需要身份验证",
            HttpStatusCode.Forbidden => "服务器返回 403：拒绝访问（可能需要 Referer/Cookie 或链接已失效）",
            HttpStatusCode.NotFound => "服务器返回 404：资源不存在",
            HttpStatusCode.Gone => "服务器返回 410：资源已删除",
            HttpStatusCode.TooManyRequests => "服务器返回 429：请求过于频繁，被限流",
            HttpStatusCode.InternalServerError => "服务器返回 500：服务器内部错误",
            HttpStatusCode.BadGateway => "服务器返回 502：网关错误",
            HttpStatusCode.ServiceUnavailable => "服务器返回 503：服务不可用",
            _ => $"服务器返回 {(int)status}：{status}"
        };
    }
}
