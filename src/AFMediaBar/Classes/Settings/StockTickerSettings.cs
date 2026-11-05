// 盯盘组件的设置契约：自选股、刷新节奏与轮换节奏；缺字段保持关闭语义只在显式关闭时生效。
namespace AFMediaBar.Classes.Settings;

/// <summary>
/// 任务栏盯盘组件的设置。代码为带市场前缀的写法（sh600519 / sz000001 / sh000001），
/// 用户只填数字时按首位推断市场，推断不出来的原样保留，由数据源回空。
/// </summary>
public sealed record StockTickerSettings
{
    /// <summary>刷新间隔下限（秒）。行情源本身按秒级推送，再快没有意义。</summary>
    public const int MinimumRefreshSeconds = 2;

    /// <summary>刷新间隔上限（秒）。</summary>
    public const int MaximumRefreshSeconds = 60;

    /// <summary>轮换间隔下限（秒）。</summary>
    public const int MinimumRotateSeconds = 2;

    /// <summary>轮换间隔上限（秒）。</summary>
    public const int MaximumRotateSeconds = 120;

    /// <summary>自选股数量上限，避免一条组件把整条媒体栏吃掉。</summary>
    public const int MaximumCodes = 12;

    /// <summary>默认刷新间隔（秒）。</summary>
    public const int DefaultRefreshSeconds = 5;

    /// <summary>默认轮换间隔（秒）。</summary>
    public const int DefaultRotateSeconds = 6;

    /// <summary>是否在任务栏显示盯盘组件。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>自选股列表；为空时组件显示占位符而不是消失。</summary>
    public IReadOnlyList<string> Codes { get; init; } = DefaultCodes;

    /// <summary>行情刷新间隔（秒）。</summary>
    public int RefreshSeconds { get; init; } = DefaultRefreshSeconds;

    /// <summary>组件里轮流显示每只股票的时间（秒）；小于等于 0 表示停在第一只。</summary>
    public int RotateSeconds { get; init; } = DefaultRotateSeconds;
    /// <summary>
    /// 是否只在显示时段（周一至周五 09:00–16:00）显示。关闭后恒显。
    /// Whether to show only inside the display window (Mon–Fri 09:00–16:00). When off the component is always shown.
    /// </summary>
    public bool ScheduleEnabled { get; init; } = true;

    /// <summary>是否显示证券简称；关掉只留价格，给极窄的媒体栏留出空间。</summary>
    public bool ShowName { get; init; } = true;

    /// <summary>是否显示涨跌幅。</summary>
    public bool ShowPercent { get; init; } = true;

    /// <summary>
    /// 是否改用"绿涨红跌"。默认按 A 股习惯红涨绿跌；开启后反过来，方便同时盯美股的用法。
    /// </summary>
    public bool SwapColors { get; init; }

    /// <summary>默认自选：上证指数、贵州茅台、宁德时代。</summary>
    public static IReadOnlyList<string> DefaultCodes { get; } = ["sh000001", "sh600519", "sz300750"];

    /// <summary>默认设置：开启、三只自选、5 秒刷新、6 秒轮换。</summary>
    public static StockTickerSettings Default { get; } = new();

    /// <summary>归一化：去空、去重、限流、夹取节奏区间。</summary>
    public StockTickerSettings Normalize()
    {
        var codes = new List<string>(MaximumCodes);
        foreach (var raw in Codes ?? [])
        {
            var code = NormalizeCode(raw);
            if (code.Length == 0 || codes.Contains(code, StringComparer.OrdinalIgnoreCase)) continue;
            codes.Add(code);
            if (codes.Count >= MaximumCodes) break;
        }

        return this with
        {
            Codes = codes.Count == 0 ? DefaultCodes : codes,
            RefreshSeconds = Math.Clamp(RefreshSeconds, MinimumRefreshSeconds, MaximumRefreshSeconds),
            RotateSeconds = RotateSeconds <= 0 ? 0 : Math.Clamp(RotateSeconds, MinimumRotateSeconds, MaximumRotateSeconds)
        };
    }

    /// <summary>
    /// 把用户输入统一成数据源接受的代码：数字按首位补市场前缀，带前缀的转小写，
    /// 其余（美股代码等）原样保留，由数据源决定能否取到行情。
    /// </summary>
    /// <param name="raw">用户输入的代码。/ The code the user typed.</param>
    public static string NormalizeCode(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0) return string.Empty;

        var lower = text.ToLowerInvariant();
        if (lower.StartsWith("sh") || lower.StartsWith("sz") || lower.StartsWith("bj") ||
            lower.StartsWith("hk") || lower.StartsWith("us"))
        {
            return lower;
        }

        if (lower.EndsWith(".ss") || lower.EndsWith(".sh")) return "sh" + Digits(lower[..^3]);
        if (lower.EndsWith(".sz")) return "sz" + Digits(lower[..^3]);
        if (lower.EndsWith(".hk")) return "hk" + Digits(lower[..^3]);

        if (text.All(char.IsDigit))
        {
            return text.Length switch
            {
                6 when text[0] is '6' or '9' or '5' => "sh" + text,
                6 when text[0] is '0' or '1' or '2' or '3' => "sz" + text,
                6 when text[0] is '4' or '8' => "bj" + text,
                5 => "hk" + text,
                _ => text
            };
        }

        return text;
    }

    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());
}
