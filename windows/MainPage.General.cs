using System.Text.Json;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Tomatotodo_Windows.Data;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private const string NativeBackupFormat = "tomatotodo-native-backup";
    private Window? _miniWindow;
    private Grid? _miniPanel;
    private TextBlock? _miniTimerText;
    private TextBlock? _miniStatusText;

    private void RenderGeneral()
    {
        if (_generalGroup == "appearance") { RenderAppearance(); return; }
        if (_generalGroup is not null) { RenderGeneralGroup(); return; }
        var page = new StackPanel { Spacing = 8, Margin = new Thickness(0, 2, 0, 32) };
        foreach (var group in SettingsGroups)
        {
            var card = new SettingsCard
            {
                Header = group.Title,
                Description = group.Description,
                HeaderIcon = FluentIcon(group.Icon, 24),
                IsClickEnabled = true,
                MinHeight = 76,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ContentAlignment = ContentAlignment.Right
            };
            card.Click += (_, _) => NavigateSettingsGroup(group.Id);
            page.Children.Add(card);
        }
        PageBody.Children.Add(page);
    }

    private void RenderGeneralGroup()
    {
        var page = new StackPanel { Spacing = 10, Margin = new Thickness(0, 2, 0, 54) };
        if (_generalGroup == "about")
        {
            page.Children.Add(UpdateSettingsCard());
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u516C\u544A"), global::Tomatotodo_Windows.Data.UiText.T("\u67E5\u770B\u5F53\u524D\u7248\u672C\u516C\u544A\uFF0C\u5305\u542B\u5DF2\u8BFB\u516C\u544A"), Action(global::Tomatotodo_Windows.Data.UiText.T("\u67E5\u770B\u516C\u544A"), async (_, _) => await ShowVersionAnnouncementsAsync(true)), IconGlyph.Bell));
            page.Children.Add(BuildAboutCredits());
        }
        if (_generalGroup == "course")
        {
            AddSection(page, IconGlyph.Navigation, global::Tomatotodo_Windows.Data.UiText.T("\u5BFC\u822A\u680F"), global::Tomatotodo_Windows.Data.UiText.T("\u7BA1\u7406\u5DE6\u4FA7\u5BFC\u822A\u5165\u53E3"));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868"), global::Tomatotodo_Windows.Data.UiText.T("\u5728\u5DE6\u4FA7\u663E\u793A\u8BFE\u7A0B\u8868\u5165\u53E3\uFF0C\u67E5\u770B\u672C\u5468\u8BFE\u7A0B\u3001\u6559\u5BA4\u4E0E\u4EFB\u8BFE\u6559\u5E08\u3002"), Toggle(_state.CourseScheduleEnabled, on =>
            { _state.CourseScheduleEnabled = on; SyncCourseNavigation(); Save(); }), IconGlyph.Calendar));
        }

        if (_generalGroup == "common")
        {
            var language = new ComboBox { MinWidth = 180, MaxDropDownHeight = 420 };
            foreach (var code in UiLanguage.Codes)
                language.Items.Add(new ComboBoxItem { Content = UiLanguage.Label(code, Services.LanguagePreference.Resolved), Tag = code });
            language.SelectedIndex = Array.IndexOf(UiLanguage.Codes, Services.LanguagePreference.Selected);
            language.SelectionChanged += (_, _) =>
            {
                if (language.SelectedItem is not ComboBoxItem { Tag: string code }) return;
                Services.LanguagePreference.Set(code);
                if (_appearancePage is not null) PageBody.Children.Remove(_appearancePage);
                _appearancePage = null;
                Render();
            };
            page.Children.Add(GeneralCard(UiLanguage.Title(Services.LanguagePreference.Resolved),
                UiLanguage.Label(Services.LanguagePreference.Selected, Services.LanguagePreference.Resolved), language, IconGlyph.Navigation));
            AddSection(page, IconGlyph.Navigation, global::Tomatotodo_Windows.Data.UiText.T("\u542F\u52A8\u4E0E\u540E\u53F0"), global::Tomatotodo_Windows.Data.UiText.T("\u5173\u95ED\u7A97\u53E3\u65F6\u53EF\u9009\u62E9\u7EE7\u7EED\u8FD0\u884C\u6216\u9000\u51FA\u8F6F\u4EF6"));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u673A\u542F\u52A8"), global::Tomatotodo_Windows.Data.UiText.T("\u767B\u5F55 Windows \u65F6\u81EA\u52A8\u6253\u5F00 Tomatotodo\u3002"), Toggle(Services.StartupService.Enabled,
                async on => await ChangeStartupAsync(on)), IconGlyph.Power));
            var closeBehavior = new ComboBox { MinWidth = 180 };
            closeBehavior.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u9996\u6B21\u5173\u95ED\u65F6\u8BE2\u95EE"));
            closeBehavior.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u9690\u85CF\u5230\u7CFB\u7EDF\u6258\u76D8"));
            closeBehavior.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u9000\u51FA\u8F6F\u4EF6"));
            closeBehavior.SelectedIndex = _state.CloseBehavior == "tray" ? 1 : _state.CloseBehavior == "exit" ? 2 : 0;
            closeBehavior.SelectionChanged += (_, _) => RememberCloseBehavior(closeBehavior.SelectedIndex switch { 1 => "tray", 2 => "exit", _ => "ask" });
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u5173\u95ED\u7A97\u53E3\u65F6"), global::Tomatotodo_Windows.Data.UiText.T("\u542F\u52A8\u540E\u59CB\u7EC8\u4FDD\u7559\u7CFB\u7EDF\u6258\u76D8\u56FE\u6807\uFF1B\u9996\u6B21\u9009\u62E9\u540E\u8BB0\u4F4F\u5173\u95ED\u65B9\u5F0F\u3002"), closeBehavior, IconGlyph.ChromeClose));
            page.Children.Add(GeneralCard("托盘图标黑白样式", "保留番茄与勾号轮廓，跟随 Windows 系统模式：浅色使用黑色，深色使用白色。", Toggle(_state.TrayMonochromeIcon,
                on => { _state.TrayMonochromeIcon = on; Save(); TrayIconStyleChanged?.Invoke(); }), IconGlyph.Contrast));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u6258\u76D8\u663E\u793A\u63A5\u4E0B\u6765\u8BFE\u7A0B"), global::Tomatotodo_Windows.Data.UiText.T("\u5728\u6258\u76D8\u53F3\u952E\u83DC\u5355\u663E\u793A\u4E0B\u4E00\u95E8\u8BFE\u7A0B\u3001\u65E5\u671F\u3001\u65F6\u95F4\u548C\u6559\u5BA4\u3002"), Toggle(_state.TrayShowNextCourse,
                on => { _state.TrayShowNextCourse = on; Save(); }), IconGlyph.Calendar));
            AddSection(page, IconGlyph.FullScreen, global::Tomatotodo_Windows.Data.UiText.T("\u6A21\u5F0F"), global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u65F6\u7684\u7A97\u53E3\u663E\u793A\u65B9\u5F0F"));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u6C89\u6D78\u6A21\u5F0F"), global::Tomatotodo_Windows.Data.UiText.T("\u542F\u7528\u540E\u5C06 Tomatotodo \u5207\u6362\u4E3A\u5168\u5C4F\u6C89\u6D78\u663E\u793A\u3002"), Toggle(_state.ImmersiveMode, on =>
            { _state.ImmersiveMode = on; ApplyImmersiveMode(); Save(); }), IconGlyph.FullScreen));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u5C0F\u7A97\u6A21\u5F0F"), global::Tomatotodo_Windows.Data.UiText.T("\u663E\u793A\u59CB\u7EC8\u7F6E\u9876\u7684\u539F\u751F\u8BA1\u65F6\u5C0F\u7A97\uFF0C\u5E76\u540C\u6B65\u5F53\u524D\u8FD0\u884C\u72B6\u6001\u3002"), Toggle(_state.MiniWindowMode, on =>
            { _state.MiniWindowMode = on; SetMiniWindow(on); Save(); }), IconGlyph.MiniWindow));

            AddSection(page, IconGlyph.Stopwatch, global::Tomatotodo_Windows.Data.UiText.T("\u8BA1\u65F6\u8282\u594F"), global::Tomatotodo_Windows.Data.UiText.T("1\u2013180 \u5206\u949F"));
            Panel rhythm = _compactSettingsLayout ? new StackPanel { Spacing = 10 } : new Grid { ColumnSpacing = 12 };
            if (rhythm is Grid rhythmGrid)
            {
                rhythmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rhythmGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            var focus = GeneralNumber(global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u65F6\u957F"), _state.FocusMinutes, 1, 180, value => { _state.FocusMinutes = value; Save(); });
            var pause = GeneralNumber(global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11\u65F6\u957F"), _state.BreakMinutes, 1, 60, value => { _state.BreakMinutes = value; Save(); });
            rhythm.Children.Add(focus);
            if (rhythm is Grid) Grid.SetColumn(pause, 1);
            rhythm.Children.Add(pause);
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u4E0E\u77ED\u4F11"), global::Tomatotodo_Windows.Data.UiText.T("\u65F6\u957F\u4FEE\u6539\u4F1A\u5728\u4E0B\u4E00\u6B21\u91CD\u7F6E\u8BA1\u65F6\u5668\u540E\u751F\u6548\u3002"), rhythm, IconGlyph.Stopwatch));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u542F\u7528\u77ED\u4F11"), global::Tomatotodo_Windows.Data.UiText.T("\u5B8C\u6210\u4E13\u6CE8\u540E\u5207\u6362\u5230\u77ED\u4F11\u9636\u6BB5\uFF1B\u662F\u5426\u81EA\u52A8\u5F00\u59CB\u7531\u5FAA\u73AF\u8BA1\u65F6\u65B9\u5F0F\u51B3\u5B9A\u3002"), Toggle(_state.EnableShortBreak, on => { _state.EnableShortBreak = on; Save(); Render(); }), IconGlyph.Coffee));
            var cycle = new ComboBox { MinWidth = 180 };
            cycle.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u624B\u52A8\u5FAA\u73AF\u8BA1\u65F6")); cycle.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u52A8\u5FAA\u73AF\u8BA1\u65F6"));
            cycle.SelectedIndex = _state.AutomaticTimerCycle ? 1 : 0;
            cycle.SelectionChanged += (_, _) => { _state.AutomaticTimerCycle = cycle.SelectedIndex == 1; Save(); };
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u5FAA\u73AF\u8BA1\u65F6"), global::Tomatotodo_Windows.Data.UiText.T("\u624B\u52A8\uFF1A\u6BCF\u9636\u6BB5\u7ED3\u675F\u540E\u7B49\u5F85\u5F00\u59CB\u3002\u81EA\u52A8\uFF1A\u4E13\u6CE8\u4E0E\u77ED\u4F11\u8FDE\u7EED\u8FD0\u884C\uFF0C\u4EFB\u52A1\u756A\u8304\u8FBE\u6807\u540E\u5B8C\u6210\u77ED\u4F11\u5E76\u505C\u6B62\u3002\u5173\u95ED\u77ED\u4F11\u5219\u8FDE\u7EED\u4E13\u6CE8\uFF1B\u6B63\u5411\u8BA1\u65F6\u4E0D\u53C2\u4E0E\u5FAA\u73AF\u3002"), cycle, IconGlyph.Refresh));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u6B63\u5411\u8BA1\u65F6"), global::Tomatotodo_Windows.Data.UiText.T("\u4ECE 00:00 \u5411\u4E0A\u7D2F\u8BA1\u4E13\u6CE8\u65F6\u95F4\uFF0C\u4E0D\u81EA\u52A8\u7ED3\u675F\u6216\u5956\u52B1\u756A\u8304\u3002"), Toggle(_state.PositiveCountup, SetPositiveCountup), IconGlyph.Up));
            AddSection(page, IconGlyph.Bell, global::Tomatotodo_Windows.Data.UiText.T("\u8BA1\u65F6\u7ED3\u675F\u63D0\u9192"), global::Tomatotodo_Windows.Data.UiText.T("\u7CFB\u7EDF\u901A\u77E5\u4E0E\u7EA6 1 \u79D2\u7684\u97F3\u6548\u53EF\u5206\u522B\u5F00\u5173\uFF0C\u8F6F\u4EF6\u5185\u63D0\u793A\u59CB\u7EC8\u4FDD\u7559"));
            Panel EndReminder(bool shortBreak)
            {
                var controls = new StackPanel { Orientation = _compactSettingsLayout ? Orientation.Vertical : Orientation.Horizontal, Spacing = 16 };
                controls.Children.Add(LabeledControl(global::Tomatotodo_Windows.Data.UiText.T("\u7CFB\u7EDF\u901A\u77E5"), Toggle(shortBreak ? _state.BreakEndSystemNotification : _state.FocusEndSystemNotification,
                    on => { if (shortBreak) _state.BreakEndSystemNotification = on; else _state.FocusEndSystemNotification = on; Save(); })));
                controls.Children.Add(LabeledControl(global::Tomatotodo_Windows.Data.UiText.T("\u97F3\u6548"), Toggle(shortBreak ? _state.BreakEndSound : _state.FocusEndSound,
                    on => { if (shortBreak) _state.BreakEndSound = on; else _state.FocusEndSound = on; Save(); })));
                return controls;
            }
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u7ED3\u675F\u63D0\u9192"), global::Tomatotodo_Windows.Data.UiText.T("\u5B8C\u6210\u4E13\u6CE8\u5E76\u83B7\u5F97\u756A\u8304\u65F6\u63D0\u9192\u3002"), EndReminder(false), IconGlyph.Stopwatch));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11\u7ED3\u675F\u63D0\u9192"), global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11\u8BA1\u65F6\u7ED3\u675F\u65F6\u63D0\u9192\u3002"), EndReminder(true), IconGlyph.Coffee));
        }

        if (_generalGroup == "dashboard")
        {
            AddSection(page, IconGlyph.Clock, global::Tomatotodo_Windows.Data.UiText.T("\u5706\u8868\u8BBE\u7F6E"), global::Tomatotodo_Windows.Data.UiText.T("\u63A7\u5236\u4EEA\u8868\u76D8\u5706\u8868\u7684\u663E\u793A\u7EC6\u8282"));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u663E\u793A\u523B\u5EA6"), global::Tomatotodo_Windows.Data.UiText.T("\u663E\u793A\u5341\u4E8C\u4E2A\u5C0F\u65F6\u523B\u5EA6\uFF0C\u4FBF\u4E8E\u5FEB\u901F\u8BFB\u53D6\u65F6\u95F4\u3002"), Toggle(_state.ShowClockMarkers, on => { _state.ShowClockMarkers = on; Save(); Render(); }), IconGlyph.Clock));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u663E\u793A\u79D2\u9488"), global::Tomatotodo_Windows.Data.UiText.T("\u5173\u95ED\u540E\u4FDD\u7559\u65F6\u9488\u4E0E\u5206\u9488\uFF0C\u753B\u9762\u66F4\u52A0\u5B89\u9759\u3002"), Toggle(_state.ShowClockSeconds, on => { _state.ShowClockSeconds = on; Save(); Render(); }), IconGlyph.Clock));

            AddSection(page, IconGlyph.CalendarDay, global::Tomatotodo_Windows.Data.UiText.T("\u5012\u6570\u65E5\u8BBE\u7F6E"), CountdownSummary());
            Panel countdown = _compactSettingsLayout ? new StackPanel { Spacing = 10 } : new Grid { ColumnSpacing = 12 };
            if (countdown is Grid countdownGrid)
            {
                countdownGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                countdownGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            var name = new TextBox { Header = global::Tomatotodo_Windows.Data.UiText.T("\u76EE\u6807\u540D\u79F0"), Text = _state.CountdownName, PlaceholderText = global::Tomatotodo_Windows.Data.UiText.T("\u4F8B\u5982\uFF1A\u5047\u671F\u7ED3\u675F") };
            name.TextChanged += (_, _) => { _state.CountdownName = name.Text.Trim(); Save(); };
            countdown.Children.Add(name);
            var date = new CalendarDatePicker { Header = global::Tomatotodo_Windows.Data.UiText.T("\u76EE\u6807\u65E5\u671F"), Date = _state.CountdownDate };
            date.DateChanged += (_, _) => { _state.CountdownDate = date.Date; Save(); Render(); };
            if (countdown is Grid) Grid.SetColumn(date, 1);
            countdown.Children.Add(date);
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u76EE\u6807\u540D\u79F0\u4E0E\u65E5\u671F"), global::Tomatotodo_Windows.Data.UiText.T("\u5012\u6570\u65E5\u4F1A\u540C\u6B65\u663E\u793A\u5728\u4EEA\u8868\u76D8\u7EC4\u4EF6\u4E2D\u3002"), countdown, IconGlyph.CalendarDay));

            AddSection(page, IconGlyph.Bell, global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u63D0\u9192"), global::Tomatotodo_Windows.Data.UiText.T("\u6309\u8BFE\u7A0B\u8868\u65F6\u95F4\u63D0\u524D\u63D0\u9192"));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u542F\u8BFE\u7A0B\u63D0\u9192"), global::Tomatotodo_Windows.Data.UiText.T("\u6839\u636E\u5F53\u524D\u8BFE\u7A0B\u8868\u7684\u51C6\u786E\u5F00\u59CB\u65F6\u95F4\u751F\u6210\u63D0\u9192\u3002"), Toggle(_state.CourseReminderEnabled, on => { _state.CourseReminderEnabled = on; Save(); }), IconGlyph.Bell));
            Panel reminder = _compactSettingsLayout ? new StackPanel { Spacing = 10 } : new Grid { ColumnSpacing = 12 };
            if (reminder is Grid reminderGrid)
                for (var column = 0; column < 3; column++) reminderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            reminder.Children.Add(GeneralNumber(global::Tomatotodo_Windows.Data.UiText.T("\u63D0\u524D\u63D0\u9192 / \u5206\u949F"), _state.CourseReminderLeadMinutes, 1, 120, value => { _state.CourseReminderLeadMinutes = value; Save(); }));
            var toast = Toggle(_state.CourseReminderSystemToast, on => { _state.CourseReminderSystemToast = on; Save(); });
            var sound = Toggle(_state.CourseReminderSound, on => { _state.CourseReminderSound = on; Save(); });
            var toastControl = LabeledControl(global::Tomatotodo_Windows.Data.UiText.T("\u7CFB\u7EDF\u901A\u77E5"), toast);
            var soundControl = LabeledControl(global::Tomatotodo_Windows.Data.UiText.T("\u97F3\u6548"), sound);
            reminder.Children.Add(toastControl); reminder.Children.Add(soundControl);
            if (reminder is Grid)
            {
                Grid.SetColumn(toastControl, 1); Grid.SetColumn(soundControl, 2);
            }
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u63D0\u9192\u65B9\u5F0F"), global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u63D0\u9192\u5F00\u542F\u65F6\u59CB\u7EC8\u663E\u793A\u8F6F\u4EF6\u6D88\u606F\uFF1B\u7CFB\u7EDF\u901A\u77E5\u4E0E\u97F3\u6548\u53EF\u5206\u522B\u5F00\u5173\u3002"), reminder, IconGlyph.Bell));

            AddWeatherSettings(page);
            AddSection(page, IconGlyph.Media, global::Tomatotodo_Windows.Data.UiText.T("\u97F3\u4E50\u8BBE\u7F6E"), _state.MusicMode == "local" ? global::Tomatotodo_Windows.Data.UiText.T("\u672C\u5730\u97F3\u4E50") : global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u5A92\u4F53"));
            var music = new ComboBox { Width = 180, HorizontalAlignment = HorizontalAlignment.Right };
            music.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u5A92\u4F53")); music.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u672C\u5730\u97F3\u4E50")); music.SelectedIndex = _state.MusicMode == "local" ? 1 : 0;
            music.SelectionChanged += (_, _) => { _state.MusicMode = music.SelectedIndex == 1 ? "local" : "system"; Save(); Render(); _ = RefreshMediaAsync(); };
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u5A92\u4F53\u6765\u6E90"), global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u5A92\u4F53\u8BFB\u53D6 Windows \u6B63\u5728\u64AD\u653E\u7684\u97F3\u4E50\u6216\u89C6\u9891\uFF1B\u672C\u5730\u97F3\u4E50\u64AD\u653E\u6240\u9009\u6587\u4EF6\u5939\u4E2D\u7684\u97F3\u9891\u3002"), music, IconGlyph.Media));
            var folderActions = new StackPanel { Orientation = _compactSettingsLayout ? Orientation.Vertical : Orientation.Horizontal, Spacing = 10 };
            folderActions.Children.Add(Action(global::Tomatotodo_Windows.Data.UiText.T("\u9009\u62E9\u97F3\u4E50\u6587\u4EF6\u5939"), async (_, _) => await ChooseMusicFolderAsync(), true));
            folderActions.Children.Add(new TextBlock
            {
                Text = LocalMusicFolderSummary(),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = DashboardBrush("TextFillColorSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u672C\u5730\u97F3\u4E50"), global::Tomatotodo_Windows.Data.UiText.T("\u652F\u6301\u5E38\u89C1\u97F3\u9891\u683C\u5F0F\uFF1B\u6587\u4EF6\u4FDD\u7559\u5728\u539F\u6587\u4EF6\u5939\uFF0C\u4E0D\u4F1A\u4E0A\u4F20\u3002"), folderActions, IconGlyph.Media));
        }

        if (_generalGroup == "course")
        {
            AddSection(page, IconGlyph.Calendar, global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868\u5BFC\u5165\u4E0E\u5BFC\u51FA"), global::Tomatotodo_Windows.Data.UiText.T("\u7EDF\u4E00\u8BFE\u7A0B\u8868 JSON"));
            var courseActions = new StackPanel { Orientation = _compactSettingsLayout ? Orientation.Vertical : Orientation.Horizontal, Spacing = 10 };
            courseActions.Children.Add(Action(global::Tomatotodo_Windows.Data.UiText.T("\u5BFC\u5165 JSON"), async (_, _) => await ImportCourseSchedule(), true));
            courseActions.Children.Add(Action(global::Tomatotodo_Windows.Data.UiText.T("\u5BFC\u51FA JSON"), async (_, _) => await ExportCourseSchedule()));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u8BFE\u7A0B\u8868\u6570\u636E"), global::Tomatotodo_Windows.Data.UiText.T("\u5BFC\u5165\u524D\u4F1A\u9A8C\u8BC1 tomatotodo-course-schedule v1\uFF0C\u5E76\u786E\u8BA4\u8986\u76D6\u3002"), courseActions, IconGlyph.Calendar));
        }

        if (_generalGroup == "account")
        {
            var profile = _accountSession.Current;
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u8D26\u6237\u4E0E\u4E2A\u4EBA\u8D44\u6599"), profile is null ? global::Tomatotodo_Windows.Data.UiText.T("\u767B\u5F55\u3001\u6CE8\u518C\u6216\u7BA1\u7406\u672C\u5730\u4E0E\u4E91\u7AEF\u8D26\u6237\u3002") : global::Tomatotodo_Windows.Data.UiText.F("{0} \u00B7 \u7BA1\u7406\u4E2A\u4EBA\u8D44\u6599\u4E0E\u8D26\u6237\u540C\u6B65\u3002", profile.Nickname),
                Action(profile is null ? global::Tomatotodo_Windows.Data.UiText.T("\u767B\u5F55\u8D26\u6237") : global::Tomatotodo_Windows.Data.UiText.T("\u7BA1\u7406\u8D26\u6237"), AccountEntry_Click), "\uE77B"));
            AddSection(page, IconGlyph.Folder, global::Tomatotodo_Windows.Data.UiText.T("\u7528\u6237\u6570\u636E\u5BFC\u5165\u4E0E\u5BFC\u51FA"), global::Tomatotodo_Windows.Data.UiText.T("Tomatotodo JSON \u5907\u4EFD"));
            var userActions = new StackPanel { Orientation = _compactSettingsLayout ? Orientation.Vertical : Orientation.Horizontal, Spacing = 10 };
            userActions.Children.Add(Action(global::Tomatotodo_Windows.Data.UiText.T("\u5BFC\u51FA JSON"), async (_, _) => await ExportNativeBackup(), true));
            userActions.Children.Add(Action(global::Tomatotodo_Windows.Data.UiText.T("\u5BFC\u5165 JSON"), async (_, _) => await ImportNativeBackup()));
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u5168\u90E8\u672C\u673A\u6570\u636E"), global::Tomatotodo_Windows.Data.UiText.T("\u5305\u542B\u4EFB\u52A1\u3001\u8BFE\u7A0B\u3001\u6863\u6848\u3001\u4E2A\u6027\u5316\u3001\u5E38\u89C4\u8BBE\u7F6E\u4E0E\u901F\u8BB0\uFF1B\u4E0D\u4F1A\u5BFC\u51FA\u4E34\u65F6\u8BA1\u65F6\u72B6\u6001\u3002"), userActions, IconGlyph.Folder));

            AddSection(page, IconGlyph.Refresh, global::Tomatotodo_Windows.Data.UiText.T("\u6062\u590D\u51FA\u5382\u6570\u636E"), global::Tomatotodo_Windows.Data.UiText.T("\u6B64\u64CD\u4F5C\u4E0D\u53EF\u64A4\u9500"));
            var reset = new Button { Content = global::Tomatotodo_Windows.Data.UiText.T("\u6062\u590D\u51FA\u5382\u6570\u636E"), HorizontalAlignment = HorizontalAlignment.Right, Background = new SolidColorBrush(ColorHelper.FromArgb(255, 164, 48, 42)) };
            reset.Click += async (_, _) => await ResetNativeData();
            page.Children.Add(GeneralCard(global::Tomatotodo_Windows.Data.UiText.T("\u6E05\u9664\u539F\u751F\u6570\u636E"), global::Tomatotodo_Windows.Data.UiText.T("\u4EC5\u6E05\u9664 %LOCALAPPDATA%\\Tomatotodo\\Native \u4E2D\u7684\u672C\u673A\u72B6\u6001\uFF0C\u4E0D\u4F1A\u4FEE\u6539\u65E7 Web \u7248\u6570\u636E\u3002"), reset, IconGlyph.Refresh));
        }
        PageBody.Children.Add(page);
    }

    private void SetPositiveCountup(bool on)
    {
        if (_state.PositiveCountup == on) return;
        if (_running && !_breakPhase) CommitFocus();
        _running = false; _breakPhase = false; _state.PositiveCountup = on;
        _remainingSeconds = on ? 0 : _state.FocusMinutes * 60;
        Save(); Render();
    }

    private static ToggleSwitch Toggle(bool initial, Action<bool> update)
    {
        var toggle = new ToggleSwitch { IsOn = initial, OnContent = global::Tomatotodo_Windows.Data.UiText.T("\u5F00"), OffContent = global::Tomatotodo_Windows.Data.UiText.T("\u5173"), MinWidth = 98, HorizontalAlignment = HorizontalAlignment.Right };
        toggle.Toggled += (_, _) => update(toggle.IsOn); return toggle;
    }

    private static FrameworkElement LabeledControl(string label, UIElement control)
    {
        var box = new StackPanel { Spacing = 5 };
        box.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        box.Children.Add(control); return box;
    }

    private static NumberBox GeneralNumber(string title, int value, int minimum, int maximum, Action<int> update)
    {
        var box = new NumberBox
        {
            Header = title,
            Value = value,
            Minimum = minimum,
            Maximum = maximum,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            SmallChange = 1,
            LargeChange = 5
        };
        return box.WithChanged(update);
    }

    private SettingsCard GeneralCard(string header, string description, UIElement content, string glyph)
    {
        var card = CreateSettingsCard(header, description, content, FluentIcon(glyph, 20, new SolidColorBrush(AccentDisplayColor)));
        TrackSettingsSearch(header, description, card);
        return card;
    }

    private static SettingsCard CreateSettingsCard(string header, string description, UIElement content, IconElement icon) => new()
    {
        Header = header,
        Description = description,
        HeaderIcon = icon,
        Content = content,
        ContentAlignment = ContentAlignment.Right,
        IsClickEnabled = false,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Margin = new Thickness(0, 0, 0, 1)
    };

    private void AddSection(Panel page, string glyph, string title, string detail)
    {
        var section = new StackPanel { Spacing = 3, Margin = new Thickness(0, page.Children.Count == 0 ? 0 : 16, 0, 2) };
        section.Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], TextWrapping = TextWrapping.Wrap });
        section.Children.Add(new TextBlock { Text = detail, FontSize = 12, Foreground = DashboardBrush("TextFillColorSecondaryBrush"), TextWrapping = TextWrapping.Wrap });
        page.Children.Add(section);
    }

    private string CountdownSummary() => _state.CountdownDate is not { } date ? global::Tomatotodo_Windows.Data.UiText.T("\u5C1A\u672A\u8BBE\u7F6E") : global::Tomatotodo_Windows.Data.UiText.F("\u8DDD\u79BB {0} \u8FD8\u6709 {1} \u5929", _state.CountdownName, (date.Date - DateTimeOffset.Now.Date).Days);

    private void ApplyImmersiveMode()
    {
        var window = ((App)Application.Current).MainWindowInstance;
        if (window is null) return;
        var kind = _state.ImmersiveMode ? Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen : Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped;
        if (window.AppWindow.Presenter.Kind != kind) window.AppWindow.SetPresenter(kind);
        UpdateImmersiveSurface();
        window.SetTitleBar(_state.ImmersiveMode ? null : ShellCommandBar);
    }

    private bool _miniDragFeedback;

    private void SetMiniWindow(bool open)
    {
        if (!open)
        {
            var closing = _miniWindow;
            _miniWindow = null; _miniPanel = null; _miniTimerText = _miniStatusText = null;
            if (_state.MiniWindowMode) { _state.MiniWindowMode = false; Save(); }
            if (_miniModeToggle?.IsOn == true) _miniModeToggle.IsOn = false;
            closing?.Close();
            return;
        }
        if (_miniWindow is not null) { _miniWindow.Activate(); return; }
        var window = new Window();
        // A companion timer is not a separate taskbar or Alt+Tab destination.
        // Set this before first activation to avoid a transient taskbar button.
        window.AppWindow.IsShownInSwitchers = false;
        var miniRoot = new Grid();
        var panel = new Grid { Padding = new Thickness(18, 8, 12, 8), ColumnSpacing = 10 };
        miniRoot.Children.Add(panel);
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var drag = new Grid { VerticalAlignment = VerticalAlignment.Center, RowSpacing = 2,
            Background = new SolidColorBrush(Colors.Transparent) };
        drag.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        drag.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
        _miniStatusText = new TextBlock { FontSize = 12, IsTextScaleFactorEnabled = false };
        _miniTimerText = new TextBlock { FontSize = 22, IsTextScaleFactorEnabled = false, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        drag.Children.Add(new Viewbox { Child = _miniStatusText, Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left });
        var timerFit = new Viewbox { Child = _miniTimerText, Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left };
        Grid.SetRow(timerFit, 1); drag.Children.Add(timerFit);
        // 长按区域独立于播放按钮；按下立即反馈，350ms 后进入系统拖动。
        var feedback = new Border
        {
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(43),
            BorderBrush = new SolidColorBrush(AccentDisplayColor),
            Background = new SolidColorBrush(ColorHelper.FromArgb(28, AccentDisplayColor.R, AccentDisplayColor.G, AccentDisplayColor.B)),
            Margin = new Thickness(2),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        // Match the native capsule using the actual DIP height; content padding
        // and per-monitor DPI must not influence the feedback outline.
        miniRoot.SizeChanged += (_, e) =>
            feedback.CornerRadius = new CornerRadius(Math.Max(0, (e.NewSize.Height - 4) / 2));
        miniRoot.Children.Add(feedback);
        var hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        bool holding = false;
        bool dragging = false;
        Windows.Graphics.SizeInt32? restingSize = null;
        bool miniClosed = false;
        void SetHoldSize(bool expanded)
        {
            if (miniClosed) return;
            var current = window.AppWindow.Size;
            var position = window.AppWindow.Position;
            if (expanded && restingSize is null) restingSize = current;
            if (restingSize is not { } original) return;
            var width = expanded ? (int)Math.Round(original.Width * 1.04) : original.Width;
            var height = expanded ? (int)Math.Round(original.Height * 1.04) : original.Height;
            // Resize the native capsule as well as XAML so feedback is not
            // clipped by the window region. Preserve its current centre on drop.
            window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                position.X + (current.Width - width) / 2,
                position.Y + (current.Height - height) / 2, width, height));
            if (!expanded) restingSize = null;
        }
        void ResetHold()
        {
            holding = false; _miniDragFeedback = false; hold.Stop();
            feedback.Visibility = Visibility.Collapsed;
            SetHoldSize(false);
            UpdateMiniWindow();
        }
        drag.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(drag).Properties.IsLeftButtonPressed) return;
            holding = true; _miniDragFeedback = true;
            SetHoldSize(true);
            feedback.BorderBrush = new SolidColorBrush(AccentDisplayColor);
            feedback.Visibility = Visibility.Visible;
            if (_miniStatusText is not null) _miniStatusText.Text = global::Tomatotodo_Windows.Data.UiText.T("\u51C6\u5907\u79FB\u52A8");
            hold.Start();
        };
        drag.PointerReleased += (_, _) => { if (!dragging) ResetHold(); };
        drag.PointerCanceled += (_, _) => { if (!dragging) ResetHold(); };
        drag.PointerExited += (_, _) => { if (!dragging) ResetHold(); };
        hold.Tick += async (_, _) =>
        {
            if (!holding) return;
            hold.Stop();
            if (_miniStatusText is not null) _miniStatusText.Text = global::Tomatotodo_Windows.Data.UiText.T("\u79FB\u52A8\u4E2D");
            // Let XAML present the new label before entering the native modal
            // move loop, which otherwise leaves the previous hold prompt onscreen.
            await Task.Delay(32);
            if (!holding) return;
            dragging = true;
            try { Views.FloatingWindowChrome.Drag(window); }
            finally
            {
                dragging = false; ResetHold();
                if (!miniClosed) Views.FloatingWindowChrome.SnapToEdge(window);
            }
        };
        window.Closed += (_, _) => { miniClosed = true; ResetHold(); };
        ToolTipService.SetToolTip(drag, global::Tomatotodo_Windows.Data.UiText.T("\u957F\u6309\u79FB\u52A8\u5C0F\u7A97"));
        panel.Children.Add(drag);
        _miniPlayButton = Action(global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u59CB"), (_, _) => StartPause());
        _miniPlayButton.CornerRadius = new CornerRadius(22);
        _miniPlayButton.Width = _miniPlayButton.Height = 36;
        _miniPlayButton.Margin = new Thickness(0);
        _miniPlayButton.Padding = new Thickness(0);
        _miniPlayButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        _miniPlayButton.VerticalContentAlignment = VerticalAlignment.Center;
        _miniPlayButton.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_miniPlayButton, 1); panel.Children.Add(_miniPlayButton);
        var menu = new MenuFlyout();
        var showMain = new MenuFlyoutItem { Text = global::Tomatotodo_Windows.Data.UiText.T("\u663E\u793A\u4E3B\u754C\u9762"), Icon = new SymbolIcon(Symbol.OpenPane) };
        showMain.Click += (_, _) =>
        {
            var main = ((App)Application.Current).MainWindowInstance;
            if (main is null) return;
            if (main is MainWindow mainWindow) { mainWindow.ShowMainWindow(); return; }
            main.AppWindow.Show();
            if (main.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter mainPresenter &&
                mainPresenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
                mainPresenter.Restore();
            main.Activate();
        };
        var close = new MenuFlyoutItem { Text = global::Tomatotodo_Windows.Data.UiText.T("\u5173\u95ED\u5C0F\u7A97"), Icon = new SymbolIcon(Symbol.Cancel) };
        close.Click += (_, _) => SetMiniWindow(false);
        menu.Items.Add(showMain);
        menu.Items.Add(close);
        panel.ContextFlyout = menu;
        ToolTipService.SetToolTip(drag, global::Tomatotodo_Windows.Data.UiText.T("\u957F\u6309\u79FB\u52A8\u5C0F\u7A97\uFF1B\u53F3\u952E\u663E\u793A\u4E3B\u754C\u9762\u6216\u5173\u95ED\u5C0F\u7A97"));
        window.Content = miniRoot; window.AppWindow.Title = global::Tomatotodo_Windows.Data.UiText.T("Tomatotodo \u5C0F\u7A97");
        var presenter = window.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsAlwaysOnTop = true; presenter.IsResizable = false;
            presenter.IsMaximizable = false; presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(window.AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        var miniScale = Views.FloatingWindowChrome.Scale(window);
        var miniWidth = (int)Math.Round(208 * miniScale);
        var miniHeight = (int)Math.Round(64 * miniScale);
        window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(area.X + (area.Width - miniWidth) / 2, area.Y + 20, miniWidth, miniHeight));
        window.AppWindow.Changed += (_, e) => { if (e.DidSizeChange) Views.FloatingWindowChrome.Round(window); };
        Views.FloatingWindowChrome.Round(window);
        // Activation can restore the compositor's default outline; reapply only
        // on activation/size events, never on timer ticks.
        window.Activated += (_, _) => Views.FloatingWindowChrome.Round(window);
        window.Closed += (_, _) =>
        {
            if (!ReferenceEquals(_miniWindow, window)) return;
            _miniWindow = null; _miniPanel = null; _miniTimerText = _miniStatusText = null;
            if (_state.MiniWindowMode) { _state.MiniWindowMode = false; Save(); }
            if (_miniModeToggle?.IsOn == true) _miniModeToggle.IsOn = false;
        };
        _miniWindow = window; _miniPanel = panel; UpdateMiniWindowTheme(); window.Activate(); UpdateMiniWindow();
    }

    private void UpdateMiniWindowTheme()
    {
        if (_miniPanel is null) return;
        _miniPanel.RequestedTheme = IsDarkAppearance ? ElementTheme.Dark : ElementTheme.Light;
        _miniPanel.Background = new SolidColorBrush(ShellBackgroundColor);
        if (_miniTimerText is not null)
            _miniTimerText.Foreground = new SolidColorBrush(IsDarkAppearance ? Colors.White : Colors.Black);
        if (_miniStatusText is not null)
            _miniStatusText.Foreground = new SolidColorBrush(AccentDisplayColor);
    }

    private void UpdateMiniWindow()
    {
        UpdateImmersiveDigits();
        if (_miniWindow is null || _miniTimerText is null || _miniStatusText is null) return;
        if (_miniPlayButton is not null)
        {
            if (_miniPlayButton.Tag is not bool wasRunning || wasRunning != _running)
            {
                _miniPlayButton.Content = MiniControlIcon(_running ? Symbol.Pause : Symbol.Play);
                _miniPlayButton.Tag = _running;
            }
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_miniPlayButton, _running ? global::Tomatotodo_Windows.Data.UiText.T("\u6682\u505C") : global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u59CB"));
        }
        _miniTimerText.Text = $"{_remainingSeconds / 60:00}:{_remainingSeconds % 60:00}";
        if (_miniDragFeedback) return;
        _miniStatusText.Text = _state.PositiveCountup ? (_running ? global::Tomatotodo_Windows.Data.UiText.T("\u6B63\u5411\u8BA1\u65F6 \u00B7 \u8FDB\u884C\u4E2D") : global::Tomatotodo_Windows.Data.UiText.T("\u6B63\u5411\u8BA1\u65F6 \u00B7 \u5DF2\u6682\u505C")) :
            (_breakPhase ? (_running ? global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11 \u00B7 \u8FDB\u884C\u4E2D") : global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11 \u00B7 \u5DF2\u6682\u505C")) : (_running ? global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8 \u00B7 \u8FDB\u884C\u4E2D") : global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8 \u00B7 \u5DF2\u6682\u505C")));
    }

    private static Viewbox MiniControlIcon(Symbol symbol) => new()
    {
        Width = 16,
        Height = 16,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Child = new SymbolIcon(symbol)
    };

    private async Task ExportNativeBackup()
    {
        var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary, SuggestedFileName = global::Tomatotodo_Windows.Data.UiText.T("Tomatotodo-\u7528\u6237\u6570\u636E") };
        picker.FileTypeChoices.Add("JSON", [".json"]); InitializePicker(picker);
        var file = await picker.PickSaveFileAsync(); if (file is null) return;
        var profile = _accountSession.Current;
        var backup = Accounts.UnifiedUserData.Capture(_state, profile: profile, avatar: profile is null ? null : _accountStorage.Read(profile.Id).Avatar);
        _state.UnifiedData = backup; Save();
        await FileIO.WriteTextAsync(file, backup.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task ImportNativeBackup()
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(".json"); InitializePicker(picker);
            var file = await picker.PickSingleFileAsync(); if (file is null) return;
            var raw = await FileIO.ReadTextAsync(file);
            var document = System.Text.Json.Nodes.JsonNode.Parse(raw)?.AsObject() ?? throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u5907\u4EFD\u5185\u5BB9\u4E3A\u7A7A\u3002"));
            AppState imported;
            if (Accounts.UnifiedUserData.IsDocument(document)) imported = Accounts.UnifiedUserData.Apply(document, _state);
            else
            {
                var backup = JsonSerializer.Deserialize<NativeBackup>(raw);
                if (backup?.Format != NativeBackupFormat || backup.Version != 1 || backup.Data is null) throw new InvalidDataException(global::Tomatotodo_Windows.Data.UiText.T("\u4EC5\u652F\u6301 Tomatotodo \u8DE8\u5E73\u53F0 v2 \u6216\u65E7\u7248\u684C\u9762\u5907\u4EFD\u3002"));
                imported = backup.Data;
            }
            var confirm = new ContentDialog { Title = global::Tomatotodo_Windows.Data.UiText.T("\u8986\u76D6\u5F53\u524D\u672C\u673A\u6570\u636E\uFF1F"), Content = global::Tomatotodo_Windows.Data.UiText.T("\u5C06\u66FF\u6362\u4EFB\u52A1\u3001\u8BFE\u7A0B\u3001\u6863\u6848\u3001\u4E2A\u6027\u5316\u548C\u5E38\u89C4\u8BBE\u7F6E\uFF1B\u4E34\u65F6\u8BA1\u65F6\u4E0D\u4F1A\u6062\u590D\u3002"), PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u8986\u76D6\u5E76\u5BFC\u5165"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), XamlRoot = XamlRoot };
            StyleDestructiveDialog(confirm);
            if (await ShowThemedDialogAsync(confirm) != ContentDialogResult.Primary) return;
            _state = imported;
            if (_accountSession.Current is { } account) _cloudAccounts.AcceptImportedDocument(account.Id, _state);
            _state.CourseSchedule = CourseScheduleCodec.Parse(CourseScheduleCodec.Export(_state.CourseSchedule));
            _remainingSeconds = _state.PositiveCountup ? 0 : _state.FocusMinutes * 60; _running = false; _breakPhase = false; _uncommittedSeconds = 0;
            SyncCourseNavigation(); ApplyTheme(); Save(); Render();
        }
        catch (Exception exception) { await ShowCourseError(global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u5BFC\u5165\u7528\u6237\u6570\u636E"), exception.Message); }
    }

    private async Task ResetNativeData()
    {
        var first = new ContentDialog { Title = global::Tomatotodo_Windows.Data.UiText.T("\u6062\u590D\u51FA\u5382\u6570\u636E\uFF1F"), Content = global::Tomatotodo_Windows.Data.UiText.T("\u8FD9\u4F1A\u6E05\u9664\u5F53\u524D\u539F\u751F\u4EFB\u52A1\u3001\u6863\u6848\u3001\u8BFE\u7A0B\u3001\u4E2A\u6027\u5316\u548C\u5E38\u89C4\u8BBE\u7F6E\u3002\u65E7 Web \u7248\u6570\u636E\u4E0D\u4F1A\u53D7\u5F71\u54CD\u3002"), PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u7EE7\u7EED"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), XamlRoot = XamlRoot };
        StyleDestructiveDialog(first);
        if (await ShowThemedDialogAsync(first) != ContentDialogResult.Primary) return;
        var final = new ContentDialog { Title = global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u518D\u6B21\u786E\u8BA4"), Content = global::Tomatotodo_Windows.Data.UiText.T("\u6B64\u64CD\u4F5C\u4E0D\u80FD\u64A4\u9500\u3002"), PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u6062\u590D"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), XamlRoot = XamlRoot };
        StyleDestructiveDialog(final);
        if (await ShowThemedDialogAsync(final) != ContentDialogResult.Primary) return;
        _state = new AppState(); _remainingSeconds = _state.FocusMinutes * 60; _running = _breakPhase = false; _uncommittedSeconds = 0;
        SyncCourseNavigation(); ApplyTheme(); Save(); Render();
    }

    private sealed class NativeBackup
    {
        public string Format { get; set; } = NativeBackupFormat;
        public int Version { get; set; } = 1;
        public AppState? Data { get; set; }
    }
}

internal static class NumberBoxExtensions
{
    public static NumberBox WithChanged(this NumberBox box, Action<int> update)
    {
        box.ValueChanged += (_, args) => { if (!double.IsNaN(args.NewValue)) update((int)args.NewValue); };
        return box;
    }
}
