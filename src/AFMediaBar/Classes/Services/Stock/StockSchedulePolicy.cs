// 盯盘组件的显示时段策略：纯计算，不读时钟以外的东西，也不碰视觉树。
using AFMediaBar.Classes.Settings;

namespace AFMediaBar.Classes.Services;

/// <summary>
/// 盯盘组件的显示时段：周一至周五 09:00–16:00（本机时间，右端开区间）。
/// 收盘后到次日开盘前的行情没有意义，隐藏它可以让任务栏把位置还给别的东西。
/// </summary>
public static class StockSchedulePolicy
{
    /// <summary>时段起点（含）。/ Window start, inclusive.</summary>
    public const int StartHour = 9;

    /// <summary>时段终点（不含）。/ Window end, exclusive.</summary>
    public const int EndHour = 16;

    /// <summary>现在是否落在显示时段里（不考虑开关）。/ Whether the current local time falls inside the window, ignoring the switch.</summary>
    public static bool IsWithinWindow(DateTime now) =>
        now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) &&
        now.Hour >= StartHour && now.Hour < EndHour;

    /// <summary>组件此刻是否应当显示：开关关闭时恒定显示。/ Whether the component should be shown right now; always true while the switch is off.</summary>
    public static bool IsVisibleNow(StockTickerSettings settings, DateTime now) =>
        !settings.ScheduleEnabled || IsWithinWindow(now);

    /// <summary>组件此刻是否应当显示（取本机时间）。/ Whether the component should be shown right now, using local time.</summary>
    public static bool IsVisibleNow(StockTickerSettings settings) => IsVisibleNow(settings, DateTime.Now);
}
