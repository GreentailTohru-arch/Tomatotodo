namespace Tomatotodo_Windows.Data;

public sealed record WidgetDefinition(string Id, string LabelKey, string DetailKey, int Width, int Height, bool InitiallyVisible)
{
    // Resolve at display time: the definitions outlive changes to the interface language.
    public string Label => UiText.T(LabelKey);
    public string Detail => UiText.T(DetailKey);
}
public sealed record WidgetPlacement(string Id, int Column, int Row, int Width, int Height);

public sealed class DashboardLayout
{
    public List<string> Order { get; set; } = [];
    public Dictionary<string, bool> Visible { get; set; } = [];

    public DashboardLayout Clone() => new()
    {
        Order = [.. Order],
        Visible = new Dictionary<string, bool>(Visible)
    };
}

public static class DashboardWidgets
{
    public const int Columns = 6;
    public static readonly IReadOnlyList<WidgetDefinition> All =
    [
        new("timer", "\u756A\u8304\u4E13\u6CE8", "\u6A2A\u7248\u8BA1\u65F6\u5668", 4, 2, true),
        new("mode", "\u8BA1\u5212\u65F6\u6BB5", "\u4E13\u6CE8\u4E0E\u77ED\u4F11", 2, 1, true),
        new("miniwindow", "\u5C0F\u7A97\u6A21\u5F0F", "\u7F6E\u9876\u8BA1\u65F6\u7A97", 1, 1, true),
        new("immersive", "\u6C89\u6D78\u6A21\u5F0F", "\u539F\u751F\u5168\u5C4F", 1, 1, true),
        new("tasks", "\u4EFB\u52A1\u6E05\u5355", "\u4ECA\u65E5\u4EFB\u52A1\u53CA\u8FDB\u5EA6", 2, 2, true),
        new("date", "\u65E5\u671F\u4E0E\u65F6\u95F4", "\u5B9E\u65F6\u65F6\u949F", 2, 1, true),
        new("calendar", "\u65E5\u5386", "\u672C\u6708\u65E5\u671F", 2, 2, true),
        new("weather", "\u5B9E\u65F6\u5929\u6C14", "\u5F53\u524D\u4F4D\u7F6E\u5929\u6C14", 2, 1, true),
        new("quote", "\u540D\u8A00\u8B66\u53E5", "\u4E13\u6CE8\u77ED\u53E5", 2, 1, true),
        new("courseForecast", "\u8BFE\u7A0B\u9884\u62A5", "\u63A5\u4E0B\u6765\u8BFE\u7A0B", 2, 1, true),
        new("active", "\u6B63\u5728\u8FDB\u884C", "\u5F53\u524D\u4EFB\u52A1", 2, 1, false),
        new("todayCourses", "\u4ECA\u65E5\u8BFE\u7A0B", "\u5F53\u5929\u5168\u90E8\u8BFE\u7A0B", 1, 2, false),
        new("analogclock", "\u5706\u8868", "\u65F6\u9488\u3001\u5206\u9488\u4E0E\u79D2\u9488", 1, 2, false),
        new("countup", "\u6B63\u5411\u8BA1\u65F6", "\u788E\u7247\u65F6\u95F4\u8BB0\u5F55", 1, 1, false),
        new("media", "\u5F53\u524D\u5A92\u4F53", "\u6B63\u5728\u64AD\u653E", 2, 1, false),
        new("countdown", "\u5012\u6570\u65E5", "\u8DDD\u79BB\u76EE\u6807\u65E5\u671F", 2, 1, false)
    ];

    private static readonly Dictionary<string, WidgetDefinition> ById = All.ToDictionary(widget => widget.Id);

    public static WidgetDefinition Get(string id) => ById[id];

    public static DashboardLayout Default() => new()
    {
        Order = All.Select(widget => widget.Id).ToList(),
        Visible = All.ToDictionary(widget => widget.Id, widget => widget.InitiallyVisible)
    };

    public static DashboardLayout Normalize(DashboardLayout? saved)
    {
        if (saved is null) return Default();
        var order = (saved.Order ?? []).Where(ById.ContainsKey).Distinct().ToList();
        order.AddRange(All.Select(widget => widget.Id).Where(id => !order.Contains(id)));
        return new DashboardLayout
        {
            Order = order,
            Visible = All.ToDictionary(widget => widget.Id,
                widget => saved.Visible?.GetValueOrDefault(widget.Id) ?? widget.InitiallyVisible)
        };
    }

    public static int ColumnsForWidth(double width) => width >= 1040 ? 6 : width >= 688 ? 4 : width >= 320 ? 2 : 1;

    public static IReadOnlyList<WidgetPlacement> Place(DashboardLayout layout, int columns = Columns)
    {
        columns = Math.Clamp(columns, 1, Columns);
        var result = new List<WidgetPlacement>();
        var occupied = new HashSet<(int Column, int Row)>();
        foreach (var id in layout.Order)
        {
            if (!layout.Visible.GetValueOrDefault(id) || !ById.TryGetValue(id, out var widget)) continue;
            var width = Math.Min(widget.Width, columns);
            for (var row = 0; ; row++)
            {
                var found = false;
                for (var column = 0; column <= columns - width; column++)
                {
                    if (Enumerable.Range(row, widget.Height).Any(y =>
                        Enumerable.Range(column, width).Any(x => occupied.Contains((x, y))))) continue;
                    result.Add(new WidgetPlacement(id, column, row, width, widget.Height));
                    foreach (var y in Enumerable.Range(row, widget.Height))
                        foreach (var x in Enumerable.Range(column, width)) occupied.Add((x, y));
                    found = true;
                    break;
                }
                if (found) break;
            }
        }
        return result;
    }

    public static void MoveBefore(DashboardLayout layout, string sourceId, string targetId)
    {
        if (sourceId == targetId || !layout.Order.Contains(sourceId) || !layout.Order.Contains(targetId)) return;
        layout.Order.Remove(sourceId);
        layout.Order.Insert(layout.Order.IndexOf(targetId), sourceId);
    }

    public static void MoveRelative(DashboardLayout layout, string sourceId, string targetId, bool after)
    {
        if (sourceId == targetId || !layout.Order.Contains(sourceId) || !layout.Order.Contains(targetId)) return;
        layout.Order.Remove(sourceId);
        layout.Order.Insert(layout.Order.IndexOf(targetId) + (after ? 1 : 0), sourceId);
    }
}
