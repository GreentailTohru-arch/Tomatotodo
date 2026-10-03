using Microsoft.UI.Xaml.Controls;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    internal bool UseMonochromeTrayIcon => _state.TrayMonochromeIcon;
    internal event Action? TrayIconStyleChanged;
    internal string CloseBehavior => _state.CloseBehavior;
    internal void RememberCloseBehavior(string behavior)
    {
        _state.CloseBehavior = behavior;
        Save();
    }
    internal IReadOnlyList<Services.TrayEntry> BuildTrayEntries()
    {
        var phase = _breakPhase ? global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11") : global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8");
        var entries = new List<Services.TrayEntry>
        {
            new($"{(_running ? "暂停" : "开始")}{phase}  {_remainingSeconds / 60:00}:{_remainingSeconds % 60:00}",
                () => DispatcherQueue.TryEnqueue(StartPause)),
            new(global::Tomatotodo_Windows.Data.UiText.T("\u91CD\u7F6E\u65F6\u95F4"), () => DispatcherQueue.TryEnqueue(ResetTimer)),
            new(global::Tomatotodo_Windows.Data.UiText.F("\u5F53\u524D\u6E05\u5355\uFF1A{0}", ActivePreset.Name)),
            new(global::Tomatotodo_Windows.Data.UiText.F("\u5F53\u524D\u4EFB\u52A1\uFF1A{0}", ActiveTask?.Title ?? "未选择任务"))
        };
        if (_state.TrayShowNextCourse)
        {
            var next = NextCourse(_state.CourseSchedule);
            if (next is { } upcoming)
            {
                entries.Add(new(global::Tomatotodo_Windows.Data.UiText.F("\u63A5\u4E0B\u6765\u8BFE\u7A0B\uFF1A{0}", upcoming.Course.Name)));
                entries.Add(new($"{upcoming.Date:M/d}  {CourseTime(upcoming.Course)}  {upcoming.Course.Room}"));
            }
            else entries.Add(new(global::Tomatotodo_Windows.Data.UiText.T("\u63A5\u4E0B\u6765\u8BFE\u7A0B\uFF1A\u6682\u65E0\u8BFE\u7A0B")));
        }
        return entries;
    }
    internal void LeaveImmersiveForBackground()
    {
        if (_state.ImmersiveMode) ExitImmersive();
    }
    internal async Task<ContentDialogResult> AskCloseAsync() => await ShowThemedDialogAsync(new ContentDialog
    {
        Title = global::Tomatotodo_Windows.Data.UiText.T("\u5173\u95ED Tomatotodo"),
        Content = global::Tomatotodo_Windows.Data.UiText.T("\u9690\u85CF\u5230\u7CFB\u7EDF\u6258\u76D8\u540E\u4ECD\u4F1A\u7EE7\u7EED\u8BA1\u65F6\u548C\u63D0\u9192\uFF0C\u70B9\u51FB\u4EFB\u52A1\u680F\u53F3\u4FA7\u56FE\u6807\u53EF\u6062\u590D\u7A97\u53E3\u3002\u5C06\u8BB0\u4F4F\u672C\u6B21\u9009\u62E9\uFF0C\u53EF\u5728\u5E38\u89C4\u8BBE\u7F6E\u4E2D\u4FEE\u6539\u3002"),
        PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u9690\u85CF\u5230\u7CFB\u7EDF\u6258\u76D8"), SecondaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u9000\u51FA\u8F6F\u4EF6"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"),
        DefaultButton = ContentDialogButton.Primary
    });
    private async Task ChangeStartupAsync(bool enabled)
    {
        try { Services.StartupService.SetEnabled(enabled); }
        catch (Exception error)
        {
            await ShowThemedDialogAsync(new ContentDialog { Title = global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u66F4\u6539\u5F00\u673A\u542F\u52A8"), Content = error.Message, CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u786E\u5B9A") });
            Render();
        }
    }
}
