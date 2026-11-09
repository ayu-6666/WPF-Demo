namespace GenaralDBTools.Models;

public sealed record RedisKeySummary(string Key, string Type, long Length, TimeSpan? Ttl)
{
    public string TtlText => Ttl is null
        ? "不存在"
        : Ttl.Value.TotalSeconds < 0
            ? "永久"
            : Ttl.Value.TotalHours >= 24
                ? $"{Ttl.Value.TotalDays:0.0} 天"
                : $"{Ttl.Value.TotalSeconds:0} 秒";

    public string LengthText => Type switch
    {
        "string" => $"{(Length > 0 ? Length : 0)} 字节",
        "hash" or "list" or "set" or "zset" => $"{Length} 个元素",
        "stream" => $"{Length} 条消息",
        _ => Length.ToString()
    };
}

public sealed record RedisKeyDetail(string Key, string Type, TimeSpan? Ttl, string ValueText, long Length)
{
    public string TtlText => Ttl is null
        ? "不存在"
        : Ttl.Value.TotalSeconds < 0
            ? "永久"
            : $"{Ttl.Value.TotalSeconds:0} 秒";
}
