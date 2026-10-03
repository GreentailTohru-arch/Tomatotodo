using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Tomatotodo_Windows.Data;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private readonly Queue<(string Title, string Message, InfoBarSeverity Severity)> _messages = new();
    private readonly DispatcherTimer _messageTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private Storyboard? _messageStoryboard;
    private bool _messageClosing;
    private bool _messageSwiping;
    private DateTimeOffset _nextCourseReminderCheck;
    private MediaPlayer? _reminderPlayer;
    private MediaPlayer? _phaseEndPlayer;

    private void InitializeNotifications()
    {
        _messageTimer.Tick += (_, _) => DismissAppMessage();
        AppMessage.PointerEntered += (_, _) => _messageTimer.Stop();
        AppMessage.PointerExited += (_, _) => { if (AppMessage.IsOpen && !_messageSwiping) _messageTimer.Start(); };
        AppMessage.ManipulationStarted += (_, _) => { _messageSwiping = true; _messageTimer.Stop(); _messageStoryboard?.Stop(); };
        AppMessage.ManipulationDelta += (_, args) => AppMessageTransform.X = Math.Max(0, Math.Min(260, AppMessageTransform.X + args.Delta.Translation.X));
        AppMessage.ManipulationCompleted += (_, _) =>
        {
            _messageSwiping = false;
            if (AppMessageTransform.X >= 96) DismissAppMessage(swiped: true);
            else
            {
                AnimateMessage("X", AppMessageTransform.X, 0, 160);
                if (AppMessage.IsOpen) _messageTimer.Start();
            }
        };
        PresetDrawerWorkspace.SizeChanged += (_, args) => AppMessage.Width = Math.Min(620, Math.Max(200, args.NewSize.Width - 32));
    }

    private void ShowAppMessage(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        if (_messages.Count >= 20) _messages.Dequeue();
        _messages.Enqueue((title, message, severity));
        if (!AppMessage.IsOpen && !_messageClosing) ShowNextMessage();
    }

    private void ShowNextMessage()
    {
        if (_messages.TryDequeue(out var message))
        {
            AppMessage.Title = message.Title;
            AppMessage.Message = message.Message;
            AppMessage.Severity = message.Severity == InfoBarSeverity.Error ? InfoBarSeverity.Error : InfoBarSeverity.Success;
            AppMessage.Visibility = Visibility.Visible;
            AppMessage.IsOpen = true;
            AppMessageTransform.X = 0;
            AppMessageTransform.Y = -72;
            AnimateMessage("Y", -72, 0, 260);
            _messageTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(3.8 + message.Message.Length * .045, 4.5, 7));
            _messageTimer.Start();
        }
        else { AppMessage.IsOpen = false; AppMessage.Visibility = Visibility.Collapsed; }
    }

    private void DismissAppMessage(bool swiped = false)
    {
        if (!AppMessage.IsOpen || _messageClosing) return;
        _messageClosing = true;
        _messageSwiping = false;
        _messageTimer.Stop();
        _messageStoryboard?.Stop();
        var animation = AnimateMessage(swiped ? "X" : "Y", swiped ? AppMessageTransform.X : 0,
            swiped ? Math.Max(620, AppMessage.Width) : -72, swiped ? 150 : 170);
        animation.Completed += (_, _) =>
        {
            AppMessage.IsOpen = false;
            AppMessage.Visibility = Visibility.Collapsed;
            AppMessageTransform.X = AppMessageTransform.Y = 0;
            _messageClosing = false;
            ShowNextMessage();
        };
    }

    private Storyboard AnimateMessage(string property, double from, double to, int milliseconds)
    {
        _messageStoryboard?.Stop();
        var animation = new DoubleAnimation
        {
            From = from, To = to, Duration = new Duration(TimeSpan.FromMilliseconds(milliseconds)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, AppMessageTransform);
        Storyboard.SetTargetProperty(animation, property);
        var storyboard = new Storyboard(); storyboard.Children.Add(animation);
        _messageStoryboard = storyboard;
        storyboard.Begin();
        return storyboard;
    }

    private void NotifyFocus(string title, string message, bool success = false)
    {
        ShowAppMessage(title, message, success ? InfoBarSeverity.Success : InfoBarSeverity.Informational);
        ((App)Application.Current).Notifications.Show(title, message);
    }

    private void NotifyPhaseEnd(string title, string message, bool shortBreak)
    {
        ShowAppMessage(title, message, InfoBarSeverity.Success);
        var notify = shortBreak ? _state.BreakEndSystemNotification : _state.FocusEndSystemNotification;
        var sound = shortBreak ? _state.BreakEndSound : _state.FocusEndSound;
        if (notify) ((App)Application.Current).Notifications.Show(title, message, silent: true);
        if (sound) PlayPhaseEndSound();
    }

    private void PlayPhaseEndSound()
    {
        try
        {
            if (_phaseEndPlayer is null)
            {
                _phaseEndPlayer = new MediaPlayer { IsLoopingEnabled = false, Volume = 0.7 };
                _phaseEndPlayer.CommandManager.IsEnabled = false;
                _phaseEndPlayer.SystemMediaTransportControls.IsEnabled = false;
                // Limit the media itself, so loading time does not shorten audible playback.
                _phaseEndPlayer.Source = new MediaPlaybackItem(
                    MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/CourseReminder.m4a")),
                    TimeSpan.Zero, TimeSpan.FromSeconds(1));
            }
            _phaseEndPlayer.PlaybackSession.Position = TimeSpan.Zero;
            _phaseEndPlayer.Play();
        }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine($"Timer completion audio: {error}"); }
    }

    private void CheckCourseReminders()
    {
        var now = DateTimeOffset.Now;
        if (now < _nextCourseReminderCheck) return;
        _nextCourseReminderCheck = now.AddSeconds(5);
        if (!_state.CourseReminderEnabled) return;
        _state.DeliveredCourseReminders ??= [];
        var due = CourseReminder.Due(_state.CourseSchedule, now, _state.CourseReminderLeadMinutes,
            _state.DeliveredCourseReminders).DistinctBy(reminder => reminder.Key).ToArray();
        if (due.Length == 0) return;
        foreach (var reminder in due) _state.DeliveredCourseReminders[reminder.Key] = now;
        foreach (var key in _state.DeliveredCourseReminders.Where(pair => pair.Value < now.AddDays(-8)).Select(pair => pair.Key).ToArray())
            _state.DeliveredCourseReminders.Remove(key);
        Save();
        foreach (var reminder in due)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((reminder.StartsAt -
                TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, ResolveCourseTimeZone()).DateTime).TotalMinutes));
            var details = string.Join(" · ", new[] { reminder.Course.Name, reminder.Course.Room, reminder.Course.Teacher }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            var message = global::Tomatotodo_Windows.Data.UiText.F("{0}\uFF0C{1} \u4E0A\u8BFE\uFF08\u8FD8\u6709 {2} \u5206\u949F\uFF09\u3002", details, $"{reminder.StartsAt:HH:mm}", minutes);
            ShowAppMessage(global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u5373\u5C06\u5F00\u59CB"), message);
            // Course sound is independently controlled and played once, not again by the system toast.
            if (_state.CourseReminderSystemToast) ((App)Application.Current).Notifications.Show(global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u5373\u5C06\u5F00\u59CB"), message, silent: true);
        }
        if (_state.CourseReminderSound) PlayCourseReminderSound();
    }

    private string ResolveCourseTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(_state.CourseSchedule.Term.Timezone).Id; }
        catch { return TimeZoneInfo.Local.Id; }
    }

    private void PlayCourseReminderSound()
    {
        try
        {
            if (_reminderPlayer is null)
            {
                _reminderPlayer = new MediaPlayer { IsLoopingEnabled = false, Volume = 0.7 };
                _reminderPlayer.CommandManager.IsEnabled = false;
                _reminderPlayer.SystemMediaTransportControls.IsEnabled = false;
                _reminderPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/CourseReminder.m4a"));
                _reminderPlayer.MediaOpened += (_, _) => Services.NotificationService.Trace("Reminder audio opened");
                _reminderPlayer.MediaEnded += (_, _) => Services.NotificationService.Trace("Reminder audio playback completed");
                _reminderPlayer.MediaFailed += (_, args) => Services.NotificationService.Trace($"Reminder audio failed: {args.ErrorMessage}");
            }
            _reminderPlayer.PlaybackSession.Position = TimeSpan.Zero;
            _reminderPlayer.Play();
        }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine($"Reminder audio: {error}"); }
    }

#if DEBUG
    internal async void PreviewNotifications()
    {
        // Explicit opt-in runtime smoke check; never changes timer, tasks or focus statistics.
        await Task.Delay(1500);
        ShowAppMessage("提醒测试", "专注结束，获得 1 个番茄。", InfoBarSeverity.Success);
        ((App)Application.Current).Notifications.Show("课程提醒测试", "电子表闹钟音效与 Windows 通知已接入。", silent: true);
        PlayCourseReminderSound();
    }
#endif
}
