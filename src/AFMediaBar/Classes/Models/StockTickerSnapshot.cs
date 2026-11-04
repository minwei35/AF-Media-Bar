// 盯盘组件的只读快照：控件只呈现，不解析行情报文。
namespace AFMediaBar.Classes.Models;

/// <summary>一只标的的一次读数。 / One reading of one instrument.</summary>
/// <param name="Code">带市场前缀的代码。/ Code with the market prefix.</param>
/// <param name="Name">证券简称。/ Instrument name.</param>
/// <param name="Price">最新价。/ Last price.</param>
/// <param name="ChangePercent">涨跌幅，百分数值（-2.7 表示 -2.70%）。/ Change in percent, as a percentage value.</param>
/// <param name="Time">行情时间（HH:mm:ss）。/ Quote time.</param>
public readonly record struct StockQuote(string Code, string Name, double Price, double ChangePercent, string Time)
{
    /// <summary>是否上涨。/ Whether the price is up.</summary>
    public bool IsUp => ChangePercent > 0;

    /// <summary>是否下跌。/ Whether the price is down.</summary>
    public bool IsDown => ChangePercent < 0;
}

/// <summary>一次完整轮询的结果。 / The result of one full poll.</summary>
/// <param name="Quotes">按自选顺序排列的读数；取不到行情的标的不会出现在这里。/ Quotes in watchlist order; instruments with no data are absent.</param>
/// <param name="UpdatedAt">这次结果的本地时间。/ Local time of this result.</param>
/// <param name="IsTrading">拉取时是否处于交易时段。/ Whether the poll ran during trading hours.</param>
/// <param name="Error">失败原因；成功时为空。/ Failure reason, empty on success.</param>
public sealed record StockTickerSnapshot(
    IReadOnlyList<StockQuote> Quotes,
    DateTime UpdatedAt,
    bool IsTrading,
    string Error = "")
{
    /// <summary>空快照：服务还没拿到第一批数据时使用。 / The empty snapshot used before the first poll returns.</summary>
    public static StockTickerSnapshot Empty { get; } = new([], DateTime.MinValue, false);
}
