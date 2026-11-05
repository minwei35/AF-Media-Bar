// 盯盘组件的设置编辑器：只写当前活动上下文，页面失效后不再提交旧的编辑结果。
using System.Globalization;
using AFMediaBar.Classes.Abstractions;
using AFMediaBar.Classes.Services.Localization;
using AFMediaBar.Classes.Settings;
using AFMediaBar.Resources;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AFMediaBar.ViewModels.Pages;

/// <summary>盯盘组件参数编辑器：自选股、刷新与轮换节奏、读数显示项。</summary>
public sealed partial class StockTickerSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsConfiguration _configuration;
    private readonly LocalizationService _localization;
    private bool _disposed;

    private StockTickerSettings Current => SettingsManager.Current.StockTicker;

    /// <summary>是否在任务栏显示盯盘组件。/ Whether the ticker shows on the taskbar.</summary>
    public bool Enabled { get => Current.Enabled; set => Update(Current with { Enabled = value }); }
    /// <summary>是否只在显示时段（周一至周五 09:00–16:00）显示。/ Whether to show only inside the display window.</summary>
    public bool ScheduleEnabled { get => Current.ScheduleEnabled; set => Update(Current with { ScheduleEnabled = value }); }


    /// <summary>是否显示证券简称。/ Whether the instrument name is shown.</summary>
    public bool ShowName { get => Current.ShowName; set => Update(Current with { ShowName = value }); }

    /// <summary>是否显示涨跌幅。/ Whether the change in percent is shown.</summary>
    public bool ShowPercent { get => Current.ShowPercent; set => Update(Current with { ShowPercent = value }); }

    /// <summary>是否改用绿涨红跌。/ Whether the up/down colours are swapped.</summary>
    public bool SwapColors { get => Current.SwapColors; set => Update(Current with { SwapColors = value }); }

    /// <summary>自选股（以空格或逗号分隔）。/ The watchlist, separated by spaces or commas.</summary>
    public string WatchlistText
    {
        get => string.Join(" ", Current.Codes);
        set => Update(Current with { Codes = ParseCodes(value) });
    }

    /// <summary>自选股数量提示。/ A hint about how many instruments are watched.</summary>
    public string WatchlistSummary =>
        string.Format(CultureInfo.CurrentCulture, Translations.Get("Stock.Watchlist.Summary"), Current.Codes.Count);

    /// <summary>刷新间隔（秒）。/ Refresh interval in seconds.</summary>
    public double RefreshSeconds
    {
        get => Current.RefreshSeconds;
        set => Update(Current with { RefreshSeconds = (int)Math.Round(value) });
    }

    /// <summary>轮换间隔（秒）。/ Rotation interval in seconds.</summary>
    public double RotateSeconds
    {
        get => Current.RotateSeconds;
        set => Update(Current with { RotateSeconds = (int)Math.Round(value) });
    }

    /// <summary>刷新间隔下限（秒）。/ Lower bound of the refresh interval.</summary>
    public double MinimumRefreshSeconds => StockTickerSettings.MinimumRefreshSeconds;

    /// <summary>刷新间隔上限（秒）。/ Upper bound of the refresh interval.</summary>
    public double MaximumRefreshSeconds => StockTickerSettings.MaximumRefreshSeconds;

    /// <summary>刷新间隔步长（秒）。/ Step of the refresh interval.</summary>
    public double RefreshStepSeconds => 1;

    /// <summary>轮换间隔下限（秒）。/ Lower bound of the rotation interval.</summary>
    public double MinimumRotateSeconds => StockTickerSettings.MinimumRotateSeconds;

    /// <summary>轮换间隔上限（秒）。/ Upper bound of the rotation interval.</summary>
    public double MaximumRotateSeconds => StockTickerSettings.MaximumRotateSeconds;

    /// <summary>轮换间隔步长（秒）。/ Step of the rotation interval.</summary>
    public double RotateStepSeconds => 1;

    public StockTickerSettingsViewModel(ISettingsConfiguration configuration, LocalizationService localization)
    {
        _configuration = configuration;
        _localization = localization;
        SettingsManager.SettingsChanged += OnSettingsChanged;
        localization.LanguageChanged += OnLanguageChanged;
    }

    private void Update(StockTickerSettings value)
    {
        if (_disposed || !_configuration.IsActive) return;
        _configuration.SetStockTicker(value);
        OnPropertyChanged(string.Empty);
    }

    private static List<string> ParseCodes(string? text)
    {
        var codes = new List<string>(StockTickerSettings.MaximumCodes);
        foreach (var part in (text ?? string.Empty).Split([' ', ',', '，', ';', '、'], StringSplitOptions.RemoveEmptyEntries))
        {
            var code = StockTickerSettings.NormalizeCode(part);
            if (code.Length == 0 || codes.Contains(code, StringComparer.OrdinalIgnoreCase)) continue;
            codes.Add(code);
            if (codes.Count >= StockTickerSettings.MaximumCodes) break;
        }

        return codes;
    }

    private void OnSettingsChanged(object? sender, EventArgs args)
    {
        if (!_disposed) OnPropertyChanged(string.Empty);
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        if (!_disposed) OnPropertyChanged(nameof(WatchlistSummary));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SettingsManager.SettingsChanged -= OnSettingsChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
    }
}
