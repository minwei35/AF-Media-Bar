// 盯盘组件只订阅并呈现行情快照；它不建 HTTP 客户端、不解析报文，也不拥有轮询服务。
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using AFMediaBar.Classes.Models;
using AFMediaBar.Classes.Services;
using AFMediaBar.Classes.Settings;
using AFMediaBar.Resources;

namespace AFMediaBar.Components;

public partial class TaskBarMediaControl
{
    /// <summary>横向任务栏上盯盘组件占用的固定宽度（DIP）。</summary>
    private const double StockSurfaceWidth = 96;

    /// <summary>竖向任务栏上盯盘组件占用的长度（DIP）。</summary>
    private const double StockVerticalLength = 54;

    private static readonly Brush StockUpBrush = CreateFrozen(0xF2, 0x5B, 0x5B);
    private static readonly Brush StockDownBrush = CreateFrozen(0x2F, 0xC1, 0x70);

    private StockQuoteService? _stockSource;
    private bool _stockSubscribed;
    private DispatcherTimer? _stockRotation;
    private StockTickerSnapshot _stockSnapshot = StockTickerSnapshot.Empty;
    private int _stockIndex;

    /// <summary>绑定行情源：只换订阅，不接管服务的启动与释放。</summary>
    /// <param name="source">行情源。/ The quote source.</param>
    public void BindStockSource(StockQuoteService source)
    {
        DetachStockSource();
        _stockSource = source;
        Loaded -= StockLoaded;
        Unloaded -= StockUnloaded;
        Loaded += StockLoaded;
        Unloaded += StockUnloaded;
        if (IsLoaded) StockLoaded(this, new RoutedEventArgs());
    }

    private void StockLoaded(object sender, RoutedEventArgs args)
    {
        if (_stockSource is null || _stockSubscribed) return;
        _stockSource.Changed += StockChanged;
        _stockSubscribed = true;
        _stockRotation ??= new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
        _stockRotation.Tick -= StockRotation_Tick;
        _stockRotation.Tick += StockRotation_Tick;
        ApplyStockSettings();
        StockChanged(this, EventArgs.Empty);
    }

    private void StockUnloaded(object sender, RoutedEventArgs args) => DetachStockSource();

    private void DetachStockSource()
    {
        if (_stockSubscribed && _stockSource is not null) _stockSource.Changed -= StockChanged;
        _stockSubscribed = false;
        if (_stockRotation is not null)
        {
            _stockRotation.Stop();
            _stockRotation.Tick -= StockRotation_Tick;
        }
    }

    private void StockChanged(object? sender, EventArgs args)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(RefreshStockReadout), DispatcherPriority.Background);
            return;
        }

        _stockSnapshot = _stockSource?.Snapshot ?? StockTickerSnapshot.Empty;
        ApplyStockSettings();
        RefreshStockReadout();
    }

    /// <summary>按当前设置对齐轮换节奏与显示项；设置改动在下一次快照刷新时生效。</summary>
    private void ApplyStockSettings()
    {
        var settings = PresentationSettings.StockTicker.Normalize();
        TaskbarStockName.Visibility = settings.ShowName ? Visibility.Visible : Visibility.Collapsed;

        if (_stockRotation is null) return;
        if (settings.RotateSeconds <= 0 || _stockSnapshot.Quotes.Count <= 1)
        {
            _stockRotation.Stop();
            return;
        }

        var interval = TimeSpan.FromSeconds(settings.RotateSeconds);
        if (_stockRotation.Interval != interval) _stockRotation.Interval = interval;
        if (!_stockRotation.IsEnabled) _stockRotation.Start();
    }

    private void StockRotation_Tick(object? sender, EventArgs args)
    {
        var count = _stockSnapshot.Quotes.Count;
        if (count <= 1) return;
        _stockIndex = (_stockIndex + 1) % count;
        RefreshStockReadout();
    }

    /// <summary>把当前这一只的读数写进两行文字与提示；没有数据时给一个中性占位符。</summary>
    private void RefreshStockReadout()
    {
        var settings = PresentationSettings.StockTicker.Normalize();
        var quotes = _stockSnapshot.Quotes;
        if (quotes.Count == 0)
        {
            _stockIndex = 0;
            TaskbarStockName.Text = settings.ShowName ? Translations.Get("Stock.Placeholder") : string.Empty;
            TaskbarStockValue.Text = "—";
            TaskbarStockValue.Foreground = StockNeutralBrush();
            TaskbarStockSurface.ToolTip = DescribeEmpty(settings, _stockSnapshot);
            AutomationProperties.SetName(TaskbarStockSurface, TaskbarStockSurface.ToolTip as string ?? string.Empty);
            return;
        }

        if (_stockIndex >= quotes.Count) _stockIndex = 0;
        var quote = quotes[_stockIndex];
        TaskbarStockName.Text = settings.ShowName ? quote.Name : string.Empty;

        var price = quote.Price.ToString(quote.Price >= 100 ? "F2" : "F3", CultureInfo.InvariantCulture);
        var change = quote.ChangePercent.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
        TaskbarStockValue.Text = settings.ShowPercent ? price + " " + change + "%" : price;
        TaskbarStockValue.Foreground = ResolveStockBrush(settings, quote);

        var summary = quote.Name + " " + price + (settings.ShowPercent ? " (" + change + "%)" : string.Empty) +
                      (string.IsNullOrEmpty(quote.Time) ? string.Empty : " · " + quote.Time);
        var position = quotes.Count > 1 ? " · " + (_stockIndex + 1) + "/" + quotes.Count : string.Empty;
        TaskbarStockSurface.ToolTip = summary + position;
        AutomationProperties.SetName(TaskbarStockSurface, summary);
    }

    private string DescribeEmpty(StockTickerSettings settings, StockTickerSnapshot snapshot)
    {
        if (!settings.Enabled || settings.Codes.Count == 0) return Translations.Get("Stock.NoWatchlist");
        if (string.IsNullOrEmpty(snapshot.Error)) return Translations.Get("Stock.Loading");
        return Translations.Get("Stock.Unavailable") + " · " + snapshot.Error;
    }

    private Brush ResolveStockBrush(StockTickerSettings settings, in StockQuote quote)
    {
        if (quote.IsUp) return settings.SwapColors ? StockDownBrush : StockUpBrush;
        if (quote.IsDown) return settings.SwapColors ? StockUpBrush : StockDownBrush;
        return StockNeutralBrush();
    }

    private Brush StockNeutralBrush() =>
        SongTitle.TryFindResource("TextFillColorSecondaryBrush") as Brush
        ?? (SongTitle.Foreground is { } foreground ? foreground : Brushes.Gray);

    /// <summary>点击盯盘组件：手动翻到下一只自选，不打断自动轮换。</summary>
    private void TaskbarStockSurface_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs args)
    {
        var count = _stockSnapshot.Quotes.Count;
        if (count <= 1) return;
        _stockIndex = (_stockIndex + 1) % count;
        RefreshStockReadout();
    }

    private void TaskbarStockSurface_MouseEnter(object sender, System.Windows.Input.MouseEventArgs args) =>
        AnimateComponentHover(TaskbarStockSurface, true);

    private void TaskbarStockSurface_MouseLeave(object sender, System.Windows.Input.MouseEventArgs args) =>
        AnimateComponentHover(TaskbarStockSurface, false);

    private static Brush CreateFrozen(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
