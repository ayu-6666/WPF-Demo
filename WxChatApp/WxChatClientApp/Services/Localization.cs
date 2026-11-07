using System.Globalization;
using System.IO;
using System.Windows;

namespace WxChatApp.Services;

public enum AppLanguage
{
    ZhHans,
    EnUs
}

/// <summary>
/// 统一本地化入口：XAML 通过 <c>{DynamicResource Key}</c> 取文案，代码通过 <see cref="T(string)"/>。
/// 语言字典在 Application.Resources.MergedDictionaries 中整体替换，切换后所有 DynamicResource 即时刷新。
/// </summary>
public static class Loc
{
    private static readonly CultureInfo ZhCulture = CultureInfo.GetCultureInfo("zh-CN");
    private static readonly CultureInfo EnCulture = CultureInfo.GetCultureInfo("en-US");
    private static ResourceDictionary? _current;
    private static ResourceDictionary? _zhFallback;

    /// <summary>语言变化（已替换字典后触发），供代码侧动态文案刷新。</summary>
    public static event Action? LanguageChanged;

    public static AppLanguage Language { get; private set; } = AppLanguage.ZhHans;

    public static bool IsEnglish => Language == AppLanguage.EnUs;

    public static CultureInfo Culture => IsEnglish ? EnCulture : ZhCulture;

    public static string Code => IsEnglish ? "en-US" : "zh-Hans";

    public static void Initialize()
    {
        var saved = LocalStore.Settings.Language;
        Language = string.Equals(saved, "en-US", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.EnUs
            : AppLanguage.ZhHans;
        Apply(Language);
    }

    public static void SetLanguage(AppLanguage language, bool persist = true)
    {
        if (persist)
        {
            LocalStore.Settings.Language = language == AppLanguage.EnUs ? "en-US" : "zh-Hans";
            LocalStore.Save();
        }

        if (language == Language && _current != null)
        {
            return;
        }

        Language = language;
        Apply(language);
        LanguageChanged?.Invoke();
    }

    /// <summary>取当前语言文案；缺失时回退中文，再回退键名，保证界面不会空白。</summary>
    public static string T(string key)
    {
        try
        {
            if (_current != null && _current.Contains(key))
            {
                return _current[key] as string ?? key;
            }
        }
        catch
        {
            // 资源字典异常时走回退
        }

        if (_current != null)
        {
            try
            {
                return _current[key] as string ?? key;
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            if (_zhFallback != null && _zhFallback.Contains(key))
            {
                return _zhFallback[key] as string ?? key;
            }
        }
        catch
        {
            // ignore
        }

        return key;
    }

    public static string T(string key, params object?[] args)
    {
        try
        {
            return string.Format(Culture, T(key), args);
        }
        catch (FormatException)
        {
            return T(key);
        }
    }

    private static void Apply(AppLanguage language)
    {
        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        var dict = LoadDictionary(language);
        var merged = app.Resources.MergedDictionaries;
        if (_current != null)
        {
            merged.Remove(_current);
        }
        // 追加到末尾：WPF 资源查找从后往前，新语言覆盖 App.xaml 里的默认中文
        merged.Add(dict);
        _current = dict;

        _zhFallback ??= language == AppLanguage.ZhHans ? dict : LoadDictionary(AppLanguage.ZhHans);

        var culture = Culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch
        {
            // 个别线程不允许设置时忽略，界面仍使用 Loc.Culture 做格式化
        }
    }

    private static ResourceDictionary LoadDictionary(AppLanguage language)
    {
        var file = language == AppLanguage.EnUs ? "Strings.en-US.xaml" : "Strings.zh-CN.xaml";
        var uri = new Uri("/WxChatApp;component/Resources/" + file, UriKind.Relative);
        try
        {
            return new ResourceDictionary { Source = uri };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"加载语言资源失败：{file}\n{ex.Message}", ex);
        }
    }
}
