// 盯盘行情源：拉腾讯 qt.gtimg.cn（GB18030），交易时段按用户节奏刷新，其余时段降频；只产出快照，不碰视觉树。
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using AFMediaBar.Classes.Models;
using AFMediaBar.Classes.Settings;
using Microsoft.Extensions.Hosting;

namespace AFMediaBar.Classes.Services;

/// <summary>
/// 自选股行情轮询服务。它自己既不认识任务栏，也不认识设置页：
/// 读取 <see cref="SettingsManager.Current"/> 里的自选与节奏，产出 <see cref="Snapshot"/> 并抛出 <see cref="Changed"/>。
/// </summary>
public sealed class StockQuoteService : IHostedService, IDisposable
{
    private const string QuoteEndpoint = "https://qt.gtimg.cn/?q={0}";

    /// <summary>非交易时段的刷新间隔（秒）：收盘后价格不动，没必要按用户节奏打接口。</summary>
    private const int IdleRefreshSeconds = 60;

    /// <summary>单个标的在行情报文里的字段数下限：少于它的行视为不完整。</summary>
    private const int MinimumFieldCount = 39;

    private static readonly HttpClient Http = CreateClient();

    private readonly Lock _gate = new();
    private Timer? _timer;
    private volatile StockTickerSnapshot _snapshot = StockTickerSnapshot.Empty;
    private int _polling;
    private bool _disposed;

    /// <summary>最近一次轮询结果。/ The most recent poll result.</summary>
    public StockTickerSnapshot Snapshot => _snapshot;

    /// <summary>快照更新后触发；可能在后台线程上，订阅方自行切回 UI 线程。/ Raised after a snapshot update; may be on a background thread.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _timer ??= new Timer(OnTick, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        return Task.CompletedTask;
    }

    private void OnTick(object? state)
    {
        if (_disposed) return;
        if (Interlocked.Exchange(ref _polling, 1) == 1) return;
        _ = PollAsync();
    }

    private async Task PollAsync()
    {
        try
        {
            var settings = SettingsManager.Current.StockTicker.Normalize();
            if (settings.Codes.Count == 0)
            {
                Publish(StockTickerSnapshot.Empty);
                return;
            }

            var trading = IsTradingNow();
            using var request = new HttpRequestMessage(HttpMethod.Get,
                string.Format(CultureInfo.InvariantCulture, QuoteEndpoint, string.Join(",", settings.Codes)));
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var text = Encoding.GetEncoding("GB18030").GetString(bytes);
            var quotes = Parse(text, settings.Codes);
            Publish(new StockTickerSnapshot(quotes, DateTime.Now, trading,
                quotes.Count == 0 ? "no-data" : string.Empty));
        }
        catch (Exception exception)
        {
            Publish(new StockTickerSnapshot(_snapshot.Quotes, _snapshot.UpdatedAt, _snapshot.IsTrading, exception.Message));
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
            ScheduleNext();
        }
    }

    private void Publish(StockTickerSnapshot snapshot)
    {
        _snapshot = snapshot;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>按"当前设置 + 是否交易时段"安排下一次轮询；任何异常都不该让轮询停摆。</summary>
    private void ScheduleNext()
    {
        try
        {
            lock (_gate)
            {
                if (_disposed || _timer is null) return;
                var settings = SettingsManager.Current.StockTicker.Normalize();
                var seconds = IsTradingNow()
                    ? Math.Max(StockTickerSettings.MinimumRefreshSeconds, settings.RefreshSeconds)
                    : IdleRefreshSeconds;
                _timer.Change(TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>A 股交易时段（含集合竞价与尾盘缓冲）；节假日靠"价格不动"自然退化。</summary>
    public static bool IsTradingNow()
    {
        var now = DateTime.Now;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        var minute = now.Hour * 60 + now.Minute;
        return minute is (>= 9 * 60 + 15 and <= 11 * 60 + 35) or (>= 12 * 60 + 55 and <= 15 * 60 + 5);
    }

    private static List<StockQuote> Parse(string payload, IReadOnlyList<string> codes)
    {
        var parsed = new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in payload.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var assign = chunk.IndexOf('=');
            if (assign <= 0) continue;
            var key = chunk[..assign].Trim();
            if (!key.StartsWith("v_", StringComparison.Ordinal)) continue;
            var code = key[2..];
            var first = chunk.IndexOf('"', assign);
            var last = chunk.LastIndexOf('"');
            if (first < 0 || last <= first) continue;

            var fields = chunk[(first + 1)..last].Split('~');
            if (fields.Length < MinimumFieldCount) continue;

            var price = ParseDouble(fields[3]);
            if (price <= 0) continue;
            parsed[code] = new StockQuote(
                code,
                fields[1].Trim(),
                price,
                ParseDouble(fields[32]),
                FormatTime(fields[30]));
        }

        var ordered = new List<StockQuote>(codes.Count);
        foreach (var code in codes)
        {
            if (parsed.TryGetValue(code, out var quote)) ordered.Add(quote);
        }

        return ordered;
    }

    /// <summary>腾讯报文的涨跌幅在第 33 个字段（0 基 32），缺失时用价格与昨收自己算。</summary>
    private static string FormatTime(string raw) =>
        raw.Length >= 14 ? raw.Substring(8, 2) + ":" + raw.Substring(10, 2) + ":" + raw.Substring(12, 2) : raw;

    private static double ParseDouble(string raw) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static HttpClient CreateClient()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch (Exception)
        {
            // 已注册过或运行环境自带，忽略。
        }

        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        return client;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
