using System.Text.Json;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Tomatotodo_Windows.Data;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private TaskPreset? _presetDraft;
    private TaskPreset? _editingPreset;
    private bool _taskDragArmed;
    private readonly HashSet<Guid> _selectedHistory = [];
    private double _presetRenderedViewportWidth;
    private const double PresetGridSideGutter = 20;
    private const double PresetGridGap = 16;
    private const double PresetCardMinimumWidth = 340;
    private const double PresetCardMaximumWidth = 440;
    private const int PresetMaximumColumns = 4;
    private const double PresetFooterActionWidth = 200;
    private const double PresetFooterActionHeight = 48;
    private static TextBlock PresetText(string text, double size = 13, bool bold = false) => new()
    {
        Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap,
        FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
    };

    private static Button PresetFooterAction(string text, RoutedEventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            Width = PresetFooterActionWidth,
            Height = PresetFooterActionHeight,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        if (primary) button.Style = (Style)Application.Current.Resources["TomatotodoAccentButtonStyle"];
        button.Click += click;
        return button;
    }

    private void FitPresetFooterActions(StackPanel actions)
    {
        void Fit()
        {
            var buttons = actions.Children.OfType<Button>().ToArray();
            if (buttons.Length == 0) return;
            var available = PresetDrawerPanel.ActualWidth - 48 - actions.Spacing * (buttons.Length - 1);
            var width = Math.Min(PresetFooterActionWidth, Math.Max(72, available / buttons.Length));
            foreach (var button in buttons) button.Width = width;
        }
        SizeChangedEventHandler onPanelSize = (_, _) => Fit();
        actions.Loaded += (_, _) => { PresetDrawerPanel.SizeChanged += onPanelSize; Fit(); };
        actions.Unloaded += (_, _) => PresetDrawerPanel.SizeChanged -= onPanelSize;
    }

    private void RenderPresets()
    {
        PageBody.MaxWidth = double.PositiveInfinity;
        var viewportWidth = PageScroll.ActualWidth > 0 ? PageScroll.ActualWidth : 960;
        _presetRenderedViewportWidth = PageScroll.ActualWidth;
        var safeWidth = Math.Max(0, viewportWidth - 2 * PresetGridSideGutter);

        // Use a true responsive card grid: cards never become narrower than a readable WinUI setting
        // panel, yet remain capped at a comfortable desktop width. Extra window width becomes columns,
        // not one overlong list card. The grid is centered inside the persistent safety gutters.
        var columns = Math.Clamp(
            (int)Math.Floor((safeWidth + PresetGridGap) / (PresetCardMinimumWidth + PresetGridGap)),
            1,
            PresetMaximumColumns);
        var maxGridWidth = columns * PresetCardMaximumWidth + (columns - 1) * PresetGridGap;
        var gridWidth = Math.Min(safeWidth, maxGridWidth);
        var grid = new Grid
        {
            Width = gridWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            ColumnSpacing = PresetGridGap,
            RowSpacing = PresetGridGap,
            Margin = new Thickness(0, 8, 0, 90)
        };
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < (int)Math.Ceiling(_state.Presets.Count / (double)columns); i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // Non-popup ThemeShadow casters need a sibling receiver. Share one receiver and
        // shadow across the preset cards so their elevation stays visually consistent.
        var shadowReceiver = new Grid { IsHitTestVisible = false };
        Grid.SetColumnSpan(shadowReceiver, columns);
        Grid.SetRowSpan(shadowReceiver, Math.Max(1, grid.RowDefinitions.Count));
        grid.Children.Add(shadowReceiver);
        var cardShadow = new ThemeShadow();
        cardShadow.Receivers.Add(shadowReceiver);
        for (var index = 0; index < _state.Presets.Count; index++)
        {
            var card = PresetCard(_state.Presets[index], cardShadow);
            Grid.SetColumn(card, index % columns);
            Grid.SetRow(card, index / columns);
            grid.Children.Add(card);
        }
        PageBody.Children.Add(grid);
    }

    private Grid PresetCard(TaskPreset preset, ThemeShadow cardShadow)
    {
        var current = preset.Id == ActivePreset.Id;
        var heading = new Grid { MinHeight = 36, Margin = new Thickness(0, 0, 52, 0) };
        var titleLine = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleLine.Children.Add(new TextBlock
        {
            Text = preset.Name,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (current)
        {
            titleLine.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = global::Tomatotodo_Windows.Data.UiText.T("\u5F53\u524D\u6E05\u5355"),
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                }
            });
        }
        heading.Children.Add(titleLine);
        var menu = new Button
        {
            Width = 36, Height = 36, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 16, 16, 0),
            Translation = new System.Numerics.Vector3(0, 0, 33),
            Style = (Style)Application.Current.Resources["ShellIconButtonStyle"],
            Content = new SymbolIcon { Symbol = Symbol.More }
        };
        AutomationProperties.SetName(menu, global::Tomatotodo_Windows.Data.UiText.F("{0} \u66F4\u591A\u64CD\u4F5C", preset.Name));
        ToolTipService.SetToolTip(menu, global::Tomatotodo_Windows.Data.UiText.T("\u66F4\u591A\u64CD\u4F5C"));
        menu.Flyout = PresetMenu(preset);

        var content = new StackPanel { Spacing = 12 };

        var schedule = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (preset.DueAt is { } due)
            schedule.Children.Add(PresetMetadataChip(IconGlyph.CalendarDay, global::Tomatotodo_Windows.Data.UiText.F("\u622A\u6B62 {0}", $"{due:MM/dd}")));
        if (preset.RemindAt is { } remind)
            schedule.Children.Add(PresetMetadataChip(IconGlyph.Bell, global::Tomatotodo_Windows.Data.UiText.F("\u63D0\u9192 {0}", $"{remind:MM/dd HH:mm}")));
        if (preset.Repeat != "none")
            schedule.Children.Add(PresetMetadataChip(IconGlyph.Repeat, global::Tomatotodo_Windows.Data.UiText.F("\u91CD\u590D \u00B7 {0}", PresetRepeatText(preset.Repeat))));
        if (schedule.Children.Count > 0)
        {
            var scheduleScroll = new ScrollViewer
            {
                Content = schedule,
                HorizontalScrollMode = ScrollMode.Enabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollMode = ScrollMode.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            EnablePresetMetadataAutoScroll(scheduleScroll);
            content.Children.Add(scheduleScroll);
        }

        var completeCount = preset.Tasks.Count(t => t.IsComplete);
        var completionBar = new ProgressBar
        {
            Minimum = 0, Maximum = Math.Max(1, preset.Tasks.Count), Value = completeCount,
            Height = 4, HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 2, 0, 0)
        };
        NormalizeProgressBarLayers(completionBar);
        AutomationProperties.SetName(completionBar, global::Tomatotodo_Windows.Data.UiText.F("\u4EFB\u52A1\u5B8C\u6210\u8FDB\u5EA6 {0}/{1}", completeCount, preset.Tasks.Count));
        ToolTipService.SetToolTip(completionBar, global::Tomatotodo_Windows.Data.UiText.F("\u5DF2\u5B8C\u6210 {0}/{1}", completeCount, preset.Tasks.Count));
        content.Children.Add(completionBar);

        var preview = new StackPanel { Spacing = 0 };
        if (preset.Tasks.Count == 0)
            preview.Children.Add(new TextBlock
            {
                Text = global::Tomatotodo_Windows.Data.UiText.T("\u8FD8\u6CA1\u6709\u4EFB\u52A1\u3002\u4F7F\u7528\u201C\u66F4\u591A\u201D\u83DC\u5355\u5F00\u59CB\u7F16\u8F91\u3002"), FontSize = 13,
                Opacity = .72, TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(12, 16, 12, 16)
            });
        for (var i = 0; i < Math.Min(3, preset.Tasks.Count); i++)
        {
            var task = preset.Tasks[i];
            var line = new Grid { ColumnSpacing = 8, MinHeight = 44, Padding = new Thickness(12, 8, 12, 8) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.Children.Add(new TextBlock { Text = $"{i + 1:00}", FontSize = 11, Opacity = .62,
                VerticalAlignment = VerticalAlignment.Center });
            var taskCopy = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            taskCopy.Children.Add(new TextBlock { Text = task.Title, FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis,
                TextDecorations = task.IsComplete ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None });
            if (!string.IsNullOrWhiteSpace(task.Subtitle))
                taskCopy.Children.Add(new TextBlock { Text = task.Subtitle, FontSize = 11, Opacity = .72,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextDecorations = task.IsComplete ? Windows.UI.Text.TextDecorations.Strikethrough : Windows.UI.Text.TextDecorations.None });
            Grid.SetColumn(taskCopy, 1);
            line.Children.Add(taskCopy);
            var progress = new TextBlock
            {
                Text = task.EstimatedPomodoros is > 0
                    ? $"{task.CompletedPomodoros}/{task.EstimatedPomodoros}  🍅" : "",
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(progress, 2);
            line.Children.Add(progress);
            preview.Children.Add(new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                BorderThickness = i == 0 ? new Thickness(0) : new Thickness(0, 1, 0, 0),
                Child = line
            });
        }
        if (preset.Tasks.Count > 3)
        {
            var remaining = new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.F("\u53E6\u6709 {0} \u9879\u4EFB\u52A1", preset.Tasks.Count - 3), FontSize = 12,
                Opacity = .72, Padding = new Thickness(12, 8, 12, 8) };
            preview.Children.Add(new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = remaining
            });
        }
        content.Children.Add(new Border
        {
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(4),
            Child = preview
        });

        var total = preset.Tasks.Sum(t => t.EstimatedPomodoros ?? 0);
        var footer = new Grid { Padding = new Thickness(0, 12, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = global::Tomatotodo_Windows.Data.UiText.F("{0}/{1} \u5DF2\u5B8C\u6210", preset.Tasks.Count(t => t.IsComplete), preset.Tasks.Count),
            FontSize = 12, Opacity = .72
        });
        var estimate = new TextBlock { Text = total > 0 ? global::Tomatotodo_Windows.Data.UiText.F("\u9884\u8BA1 {0} \u4E2A\u756A\u8304", total) : global::Tomatotodo_Windows.Data.UiText.T("\u672A\u8BBE\u7F6E\u756A\u8304\u9884\u7B97"), FontSize = 12, Opacity = .72 };
        Grid.SetColumn(estimate, 1);
        footer.Children.Add(estimate);
        content.Children.Add(new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = footer
        });

        // SettingsCard reserves a trailing presenter column even in vertical content mode. A
        // native theme-resource card keeps the same WinUI anatomy while letting list content,
        // right-aligned counts, and the footer use the complete width inside the 16 epx inset.
        var cardBody = new StackPanel { Spacing = 12 };
        cardBody.Children.Add(heading);
        cardBody.Children.Add(content);
        var card = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = current
                ? new SolidColorBrush(AccentDisplayColor)
                : (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Shadow = cardShadow,
            Translation = new System.Numerics.Vector3(0, 0, 32),
            Child = cardBody
        };
        var container = new Grid();
        container.Children.Add(card);
        container.Children.Add(menu);
        return container;
    }

    private static void EnablePresetMetadataAutoScroll(ScrollViewer scroll)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        var pointerOver = false;
        var animationsEnabled = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
        var direction = 1d;
        var pauseTicks = 20;
        scroll.PointerEntered += (_, _) => pointerOver = true;
        scroll.PointerExited += (_, _) => { pointerOver = false; pauseTicks = 12; };
        scroll.Loaded += (_, _) => timer.Start();
        scroll.Unloaded += (_, _) => timer.Stop();
        timer.Tick += (_, _) =>
        {
            if (pointerOver || !animationsEnabled) return;
            var maxOffset = scroll.ExtentWidth - scroll.ViewportWidth;
            if (maxOffset <= 1)
            {
                if (scroll.HorizontalOffset > 0) scroll.ChangeView(0, null, null, true);
                direction = 1;
                return;
            }
            if (pauseTicks > 0) { pauseTicks--; return; }
            var offset = Math.Clamp(scroll.HorizontalOffset + direction * 0.8, 0, maxOffset);
            scroll.ChangeView(offset, null, null, true);
            if (offset <= 0 || offset >= maxOffset)
            {
                direction = -direction;
                pauseTicks = 20;
            }
        };
    }

    private Border PresetMetadataChip(string? symbol, string text, bool accented = true)
    {
        var accent = AccentDisplayColor;
        var chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (symbol is { } iconSymbol)
        {
            chip.Children.Add(FluentIcon(iconSymbol, 16, accented ? new SolidColorBrush(accent) : null));
        }
        chip.Children.Add(new TextBlock
        {
            Text = text, FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            Foreground = accented ? new SolidColorBrush(accent) : null
        });
        return new Border
        {
            Child = chip, Padding = new Thickness(8, 4, 8, 4), MinHeight = 28,
            CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left,
            Background = accented
                ? new SolidColorBrush(ColorHelper.FromArgb(18, accent.R, accent.G, accent.B))
                : (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = accented
                ? new SolidColorBrush(ColorHelper.FromArgb(64, accent.R, accent.G, accent.B))
                : (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1)
        };
    }

    private static string PresetRepeatText(string repeat) => repeat switch
    {
        "daily" => global::Tomatotodo_Windows.Data.UiText.T("\u6BCF\u5929"),
        "workday" => global::Tomatotodo_Windows.Data.UiText.T("\u5DE5\u4F5C\u65E5"),
        "weekly" => global::Tomatotodo_Windows.Data.UiText.T("\u6BCF\u5468"),
        "monthly" => global::Tomatotodo_Windows.Data.UiText.T("\u6BCF\u6708"),
        "yearly" => global::Tomatotodo_Windows.Data.UiText.T("\u6BCF\u5E74"),
        "custom" => global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u5B9A\u4E49"),
        _ => global::Tomatotodo_Windows.Data.UiText.T("\u4E0D\u91CD\u590D")
    };

    private SolidColorBrush PresetPopupBackground() => new(IsDarkAppearance
        ? Microsoft.UI.ColorHelper.FromArgb(255, 44, 44, 44)
        : Microsoft.UI.ColorHelper.FromArgb(255, 249, 249, 249));

    private void MakePresetPopupOpaque(FrameworkElement element)
    {
        if (new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast) return;
        // 仅清单日期/提醒/重复编辑层禁用亚克力，保留主窗口材质和原生交互状态。
        foreach (var key in new[] { "ContentDialogBackground", "ContentDialogTopOverlay",
            "ComboBoxDropDownBackground", "CalendarViewBackground", "FlyoutPresenterBackground",
            "DatePickerFlyoutPresenterBackground", "TimePickerFlyoutPresenterBackground" })
            element.Resources[key] = PresetPopupBackground();
        if (element is ContentDialog dialog && dialog.Content is FrameworkElement content)
        {
            dialog.Background = PresetPopupBackground();
            MakePresetPopupOpaque(content);
        }
        else if (element is Panel panel)
            foreach (var child in panel.Children.OfType<FrameworkElement>()) MakePresetPopupOpaque(child);
        else if (element is Border { Child: FrameworkElement child }) MakePresetPopupOpaque(child);
    }

    private MenuFlyout PresetMenu(TaskPreset preset)
    {
        var flyout = new MenuFlyout();
        // MenuFlyout 独立呈现，跟随应用外观而非 Windows 的默认外观。
        var presenterStyle = new Style(typeof(MenuFlyoutPresenter));
        presenterStyle.Setters.Add(new Setter(FrameworkElement.RequestedThemeProperty,
            IsDarkAppearance ? ElementTheme.Dark : ElementTheme.Light));
        if (!new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast)
            presenterStyle.Setters.Add(new Setter(Control.BackgroundProperty, PresetPopupBackground()));
        flyout.MenuFlyoutPresenterStyle = presenterStyle;
        var current = new MenuFlyoutItem { Text = global::Tomatotodo_Windows.Data.UiText.T("\u8BBE\u4E3A\u5F53\u524D"), Icon = new SymbolIcon(Symbol.Accept) };
        current.Click += (_, _) => SetCurrentPreset(preset);
        flyout.Items.Add(current);
        var edit = new MenuFlyoutItem { Text = global::Tomatotodo_Windows.Data.UiText.T("\u7F16\u8F91"), Icon = new SymbolIcon(Symbol.Edit) };
        edit.Click += (_, _) => OpenPresetEditor(preset);
        flyout.Items.Add(edit);
        var due = new MenuFlyoutItem { Text = global::Tomatotodo_Windows.Data.UiText.T("\u622A\u6B62\u65E5\u671F") + (preset.DueAt is null ? "" : $" · {preset.DueAt:MM/dd}") };
        due.Click += async (_, _) => await ShowScheduleDialog(preset, "due");
        flyout.Items.Add(due);
        var remind = new MenuFlyoutItem { Text = global::Tomatotodo_Windows.Data.UiText.T("\u63D0\u9192\u6211") + (preset.RemindAt is null ? "" : $" · {preset.RemindAt:MM/dd HH:mm}") };
        remind.Click += async (_, _) => await ShowScheduleDialog(preset, "remind");
        flyout.Items.Add(remind);
        var repeat = new MenuFlyoutItem { Text = global::Tomatotodo_Windows.Data.UiText.T("\u91CD\u590D") + (preset.Repeat == "none" ? "" : $" · {preset.Repeat}") };
        repeat.Click += async (_, _) => await ShowRepeatDialog(preset);
        flyout.Items.Add(repeat);
        flyout.Items.Add(new MenuFlyoutSeparator());
        var delete = new MenuFlyoutItem { Text = global::Tomatotodo_Windows.Data.UiText.T("\u5220\u9664"), Icon = new SymbolIcon(Symbol.Delete) };
        delete.Click += async (_, _) => await DeletePreset(preset);
        flyout.Items.Add(delete);
        return flyout;
    }

    private void SetCurrentPreset(TaskPreset preset)
    {
        if (_running && !_breakPhase) CommitFocus();
        _state.ActivePresetId = preset.Id;
        _state.ActiveTaskId = preset.Tasks.FirstOrDefault(t => !t.IsComplete)?.Id;
        Save(); Render();
    }

    private void CompleteTask(TaskPreset preset, TodoTask task)
    {
        if (task.IsComplete) return;
        if (_state.ActiveTaskId == task.Id && _running && !_breakPhase) CommitFocus();
        task.IsComplete = true;
        _state.ArchivedTasks.Add(new ArchivedTask { PresetId = preset.Id,
            Task = JsonSerializer.Deserialize<TodoTask>(JsonSerializer.Serialize(task))! });
        if (_state.ActiveTaskId == task.Id)
            _state.ActiveTaskId = preset.Tasks.FirstOrDefault(t => !t.IsComplete)?.Id;
    }

    private void CheckPresetSchedules()
    {
        var changed = false;
        var now = DateTimeOffset.Now;
        foreach (var preset in _state.Presets)
        {
            if (preset.DueAt is { } due && due < now)
            {
                foreach (var task in preset.Tasks.Where(t => !t.IsComplete).ToArray())
                {
                    _state.ArchivedTasks.Add(new ArchivedTask { PresetId = preset.Id,
                        Status = "expired", Task = JsonSerializer.Deserialize<TodoTask>(JsonSerializer.Serialize(task))! });
                    preset.Tasks.Remove(task);
                    changed = true;
                }
                var next = PresetSchedule.Next(due, preset);
                if (next is null) preset.DueAt = null;
                else
                {
                    while (next < now) next = PresetSchedule.Next(next.Value, preset);
                    preset.DueAt = next;
                    if (preset.RemindAt is { } remindAt)
                    {
                        preset.RemindAt = next + (remindAt - due);
                        preset.LastRemindedAt = null;
                    }
                    foreach (var task in preset.Tasks) task.IsComplete = false;
                }
                changed = true;
            }
        }
        if (changed)
        {
            if (ActiveTask is null) _state.ActiveTaskId = ActivePreset.Tasks.FirstOrDefault()?.Id;
            Save(); Render();
        }
        var reminder = _state.Presets.FirstOrDefault(p => p.RemindAt is { } at && at <= now && p.LastRemindedAt != p.RemindAt);
        if (reminder is null) return;
        reminder.LastRemindedAt = reminder.RemindAt;
        Save();
        ShowAppMessage(global::Tomatotodo_Windows.Data.UiText.T("\u4EFB\u52A1\u63D0\u9192"), global::Tomatotodo_Windows.Data.UiText.F("{0} \u00B7 \u8BA1\u5212\u65F6\u95F4\u5230\u4E86", reminder.Name));
    }

    private void PresetAdd_Click(object sender, RoutedEventArgs e) => OpenPresetEditor(null);
    private void PresetSort_Click(object sender, RoutedEventArgs e) => OpenPresetSort();
    private void PresetHistory_Click(object sender, RoutedEventArgs e) => OpenPresetHistory();
    private void PresetDrawerClose_Click(object sender, RoutedEventArgs e) => PresetDrawerLayer.Visibility = Visibility.Collapsed;

    private void PresetDrawerWorkspace_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (PresetDrawerLayer.Visibility == Visibility.Visible) UpdatePresetDrawerWidth();
    }

    private void UpdatePresetDrawerWidth()
    {
        var available = PresetDrawerWorkspace.ActualWidth;
        if (available <= 0) return;
        PresetDrawerPanel.Width = Math.Min(available, Math.Clamp(available * .53, 480, 620));
    }

    private void OpenPresetDrawer(string eyebrow, string title)
    {
        PresetDrawerEyebrow.Text = eyebrow;
        PresetDrawerEyebrow.Visibility = string.IsNullOrWhiteSpace(eyebrow) ? Visibility.Collapsed : Visibility.Visible;
        PresetDrawerTitle.Text = title;
        PresetDrawerBody.Children.Clear();
        PresetDrawerFooter.Children.Clear();
        PresetDrawerLayer.Visibility = Visibility.Visible;
        UpdatePresetDrawerWidth();
        AnimatePresetDrawerOpen();
    }

    private void AnimatePresetDrawerOpen()
    {
        // Animate XAML properties rather than the element's composition Translation property:
        // the latter can fault in Microsoft.UI.Xaml.dll on this Windows App SDK runtime.
        PresetDrawerTranslate.X = 64;
        PresetDrawerPanel.Opacity = 0;
        var storyboard = new Storyboard();
        var slide = new DoubleAnimation
        {
            From = 64, To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(260)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slide, PresetDrawerTranslate);
        Storyboard.SetTargetProperty(slide, "X");
        storyboard.Children.Add(slide);
        var fade = new DoubleAnimation
        {
            From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(180))
        };
        Storyboard.SetTarget(fade, PresetDrawerPanel);
        Storyboard.SetTargetProperty(fade, "Opacity");
        storyboard.Children.Add(fade);
        storyboard.Begin();
    }

    private void OpenPresetEditor(TaskPreset? original)
    {
        _editingPreset = original;
        _presetDraft = original is null ? new TaskPreset { Name = "" } :
            JsonSerializer.Deserialize<TaskPreset>(JsonSerializer.Serialize(original))!;
        OpenPresetDrawer(global::Tomatotodo_Windows.Data.UiText.T("\u4EFB\u52A1\u6E05\u5355"), original is null ? global::Tomatotodo_Windows.Data.UiText.T("\u6DFB\u52A0\u9884\u8BBE") : global::Tomatotodo_Windows.Data.UiText.T("\u7F16\u8F91\u9884\u8BBE"));
        var name = new TextBox { Header = global::Tomatotodo_Windows.Data.UiText.T("\u6E05\u5355\u540D\u79F0"), Text = _presetDraft.Name,
            PlaceholderText = global::Tomatotodo_Windows.Data.UiText.T("\u4F8B\u5982\uFF1A\u6211\u7684\u4E00\u5929"), MaxLength = 80 };
        name.TextChanged += (_, _) => _presetDraft.Name = name.Text;
        PresetDrawerBody.Children.Add(name);
        var section = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        section.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        section.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        section.Children.Add(PresetText(global::Tomatotodo_Windows.Data.UiText.T("\u4EFB\u52A1\u5185\u5BB9"), 13, true));
        var count = PresetText(global::Tomatotodo_Windows.Data.UiText.F("{0} \u9879 \u00B7 \u53EF\u8C03\u6574\u987A\u5E8F", _presetDraft.Tasks.Count), 11);
        Grid.SetColumn(count, 1); section.Children.Add(count);
        PresetDrawerBody.Children.Add(section);
        var list = new ListView { CanDragItems = true, CanReorderItems = true, AllowDrop = true,
            ReorderMode = ListViewReorderMode.Enabled, MinHeight = 260, SelectionMode = ListViewSelectionMode.None,
            Padding = new Thickness(0) };
        void RefreshTasks()
        {
            list.Items.Clear();
            count.Text = global::Tomatotodo_Windows.Data.UiText.F("{0} \u9879 \u00B7 \u53EF\u8C03\u6574\u987A\u5E8F", _presetDraft.Tasks.Count);
            if (_presetDraft.Tasks.Count == 0)
            {
                list.Items.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u4ECE\u7B2C\u4E00\u9879\u4EFB\u52A1\u5F00\u59CB\n\u4EFB\u52A1\u4F1A\u6309\u8FD9\u91CC\u7684\u987A\u5E8F\u663E\u793A\u5728\u4E3B\u9875\u3002"),
                    TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 58, 0, 0),
                    Opacity = .72, TextWrapping = TextWrapping.Wrap });
                return;
            }
            foreach (var task in _presetDraft.Tasks.ToArray())
            {
                var item = new ListViewItem
                {
                    Tag = task.Id,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 0, 8),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0)
                };
                var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(16, 12, 12, 12) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var handle = new FontIcon
                {
                    Glyph = IconGlyph.Drag, FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"], FontSize = 20, Width = 24, Height = 24, Opacity = .72,
                    VerticalAlignment = VerticalAlignment.Center
                };
                handle.PointerPressed += (_, _) => _taskDragArmed = true;
                handle.PointerReleased += (_, _) => _taskDragArmed = false;
                row.Children.Add(handle);
                ToolTipService.SetToolTip(handle, global::Tomatotodo_Windows.Data.UiText.T("\u62D6\u52A8\u8C03\u6574\u4EFB\u52A1\u987A\u5E8F"));
                var fields = new Grid { RowSpacing = 8, ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Center };
                fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(116) });
                var taskName = new TextBox { Text = task.Title, PlaceholderText = global::Tomatotodo_Windows.Data.UiText.T("\u4EFB\u52A1\u540D\u79F0") };
                taskName.TextChanged += (_, _) => task.Title = taskName.Text;
                Grid.SetColumnSpan(taskName, 2);
                fields.Children.Add(taskName);
                var subtitle = new TextBox { Text = task.Subtitle, PlaceholderText = global::Tomatotodo_Windows.Data.UiText.T("\u5C0F\u6807\u9898\uFF08\u53EF\u9009\uFF09") };
                subtitle.TextChanged += (_, _) => task.Subtitle = subtitle.Text;
                Grid.SetRow(subtitle, 1);
                fields.Children.Add(subtitle);
                var estimate = new NumberBox { PlaceholderText = global::Tomatotodo_Windows.Data.UiText.T("\u756A\u8304\u6570"), Width = 116,
                    Value = task.EstimatedPomodoros ?? double.NaN, Minimum = 1, Maximum = 99,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                    VerticalAlignment = VerticalAlignment.Stretch };
                estimate.ValueChanged += (_, e) => task.EstimatedPomodoros = double.IsNaN(e.NewValue) ? null : (int)e.NewValue;
                AutomationProperties.SetName(estimate, global::Tomatotodo_Windows.Data.UiText.T("\u756A\u8304\u6570"));
                Grid.SetRow(estimate, 1);
                Grid.SetColumn(estimate, 1);
                fields.Children.Add(estimate);
                Grid.SetColumn(fields, 1); row.Children.Add(fields);
                var remove = new Button
                {
                    Content = new SymbolIcon { Symbol = Symbol.Delete }, Width = 36, Height = 36,
                    VerticalAlignment = VerticalAlignment.Center,
                    Style = (Style)Application.Current.Resources["ShellIconButtonStyle"]
                };
                ToolTipService.SetToolTip(remove, global::Tomatotodo_Windows.Data.UiText.T("\u5220\u9664\u4EFB\u52A1"));
                remove.Click += (_, _) => { _presetDraft.Tasks.Remove(task); RefreshTasks(); };
                Grid.SetColumn(remove, 2); row.Children.Add(remove);
                item.Content = new Border
                {
                    Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Child = row
                };
                list.Items.Add(item);
            }
        }
        list.DragItemsStarting += (_, e) =>
        {
            if (!_taskDragArmed) e.Cancel = true;
        };
        list.DragItemsCompleted += (_, _) =>
        {
            _taskDragArmed = false;
            var ordered = list.Items.OfType<ListViewItem>().Select(item => (Guid)item.Tag).ToList();
            if (ordered.Count != _presetDraft.Tasks.Count) return;
            _presetDraft.Tasks = ordered.Select(id => _presetDraft.Tasks.First(t => t.Id == id)).ToList();
        };
        RefreshTasks();
        PresetDrawerBody.Children.Add(list);
        var entry = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        entry.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        entry.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(116) });
        entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        var newTitle = new TextBox { PlaceholderText = global::Tomatotodo_Windows.Data.UiText.T("\u4EFB\u52A1\u540D\u79F0") };
        var newSubtitle = new TextBox { PlaceholderText = global::Tomatotodo_Windows.Data.UiText.T("\u5C0F\u6807\u9898\uFF08\u53EF\u9009\uFF09") };
        Grid.SetColumnSpan(newTitle, 2);
        entry.Children.Add(newTitle);
        Grid.SetRow(newSubtitle, 1);
        entry.Children.Add(newSubtitle);
        var newEstimate = new NumberBox { PlaceholderText = global::Tomatotodo_Windows.Data.UiText.T("\u756A\u8304\u6570"), Width = 116,
            Minimum = 1, Maximum = 99, Value = double.NaN,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            VerticalAlignment = VerticalAlignment.Stretch };
        AutomationProperties.SetName(newEstimate, global::Tomatotodo_Windows.Data.UiText.T("\u756A\u8304\u6570"));
        Grid.SetRow(newEstimate, 1);
        Grid.SetColumn(newEstimate, 1); entry.Children.Add(newEstimate);
        var add = new Button
        {
            Content = new SymbolIcon { Symbol = Symbol.Add }, Height = 44, Width = 44,
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["TomatotodoAccentButtonStyle"]
        };
        ToolTipService.SetToolTip(add, global::Tomatotodo_Windows.Data.UiText.T("\u6DFB\u52A0\u4EFB\u52A1"));
        add.Click += (_, _) =>
        {
            var title = newTitle.Text.Trim();
            if (title.Length == 0) { newTitle.Focus(FocusState.Programmatic); return; }
            _presetDraft.Tasks.Add(new TodoTask { Title = title, Subtitle = newSubtitle.Text.Trim(),
                EstimatedPomodoros = double.IsNaN(newEstimate.Value) ? null : (int)newEstimate.Value });
            newTitle.Text = newSubtitle.Text = ""; newEstimate.Value = double.NaN;
            RefreshTasks();
        };
        Grid.SetColumn(add, 2);
        Grid.SetRowSpan(add, 2);
        entry.Children.Add(add);
        PresetDrawerFooter.Children.Add(new SettingsCard
        {
            Header = global::Tomatotodo_Windows.Data.UiText.T("\u6DFB\u52A0\u4EFB\u52A1"), Description = global::Tomatotodo_Windows.Data.UiText.T("\u586B\u5199\u4EFB\u52A1\u540D\u79F0\u540E\u6DFB\u52A0\u5230\u6B64\u6E05\u5355\u3002"),
            Content = entry, ContentAlignment = ContentAlignment.Vertical,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, IsClickEnabled = false
        });
        var bottom = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.Children.Add(PresetText(global::Tomatotodo_Windows.Data.UiText.T("\u53EF\u4EE5\u4FDD\u5B58\u7A7A\u767D\u6E05\u5355"), 11));
        var save = PresetFooterAction(global::Tomatotodo_Windows.Data.UiText.T("\u4FDD\u5B58"), (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_presetDraft.Name)) { name.Focus(FocusState.Programmatic); return; }
            _presetDraft.Name = _presetDraft.Name.Trim();
            if (_editingPreset is null) _state.Presets.Add(_presetDraft);
            else
            {
                var index = _state.Presets.FindIndex(p => p.Id == _editingPreset.Id);
                if (index >= 0) _state.Presets[index] = _presetDraft;
            }
            Save(); PresetDrawerLayer.Visibility = Visibility.Collapsed; Render();
        }, true);
        Grid.SetColumn(save, 1); bottom.Children.Add(save);
        PresetDrawerFooter.Children.Add(bottom);
    }

    private void OpenPresetSort()
    {
        OpenPresetDrawer(global::Tomatotodo_Windows.Data.UiText.T("\u4EFB\u52A1\u6E05\u5355"), global::Tomatotodo_Windows.Data.UiText.T("\u914D\u7F6E\u6392\u5E8F"));
        var order = _state.Presets.Select(p => p.Id).ToList();
        var positionLabels = new Dictionary<Guid, TextBlock>();
        var list = new ListView { CanDragItems = true, CanReorderItems = true, AllowDrop = true,
            ReorderMode = ListViewReorderMode.Enabled, SelectionMode = ListViewSelectionMode.None,
            Padding = new Thickness(0) };
        foreach (var preset in _state.Presets)
        {
            var item = new ListViewItem
            {
                Tag = preset.Id,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 8),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0)
            };
            var row = new Grid { Padding = new Thickness(16, 12, 12, 12), ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var handle = new FontIcon { Glyph = IconGlyph.Drag, FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"], FontSize = 20, Width = 24, Height = 24,
                Opacity = .72, VerticalAlignment = VerticalAlignment.Center };
            handle.PointerPressed += (_, _) => _taskDragArmed = true;
            handle.PointerReleased += (_, _) => _taskDragArmed = false;
            ToolTipService.SetToolTip(handle, global::Tomatotodo_Windows.Data.UiText.T("\u62D6\u52A8\u8C03\u6574\u6E05\u5355\u987A\u5E8F"));
            row.Children.Add(handle);
            var copy = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            copy.Children.Add(PresetText(preset.Name, 14, true));
            copy.Children.Add(PresetText(global::Tomatotodo_Windows.Data.UiText.F("{0} \u9879\u4EFB\u52A1{1}", preset.Tasks.Count, (preset.Id == ActivePreset.Id ? " · 当前清单" : "")), 12));
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);
            var position = new TextBlock { Text = $"{list.Items.Count + 1:00}", FontSize = 12,
                Opacity = .62, VerticalAlignment = VerticalAlignment.Center };
            positionLabels[preset.Id] = position;
            Grid.SetColumn(position, 2);
            row.Children.Add(position);
            item.Content = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = row
            };
            list.Items.Add(item);
        }
        list.DragItemsStarting += (_, e) =>
        {
            if (!_taskDragArmed) e.Cancel = true;
        };
        list.DragItemsCompleted += (_, _) =>
        {
            _taskDragArmed = false;
            order = list.Items.OfType<ListViewItem>().Select(i => (Guid)i.Tag).ToList();
            for (var index = 0; index < order.Count; index++) positionLabels[order[index]].Text = $"{index + 1:00}";
        };
        PresetDrawerBody.Children.Add(list);
        PresetDrawerFooter.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u62D6\u52A8\u6E05\u5355\u5361\u7247\u8C03\u6574\u987A\u5E8F\u3002"), FontSize = 12, Opacity = .72 });
        var save = PresetFooterAction(global::Tomatotodo_Windows.Data.UiText.T("\u4FDD\u5B58\u6392\u5E8F"), (_, _) =>
        {
            _state.Presets = order.Select(id => _state.Presets.First(p => p.Id == id)).ToList();
            Save(); PresetDrawerLayer.Visibility = Visibility.Collapsed; Render();
        }, true);
        save.HorizontalAlignment = HorizontalAlignment.Right;
        PresetDrawerFooter.Children.Add(save);
    }

    private void OpenPresetHistory()
    {
        OpenPresetDrawer(global::Tomatotodo_Windows.Data.UiText.T("\u4EFB\u52A1\u6E05\u5355"), global::Tomatotodo_Windows.Data.UiText.T("\u5386\u53F2\u4EFB\u52A1"));
        if (_state.ArchivedTasks.Count == 0)
        {
            PresetDrawerBody.Children.Add(PresetText(global::Tomatotodo_Windows.Data.UiText.T("0 \u9879"), 12));
            PresetDrawerBody.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u8FD8\u6CA1\u6709\u5386\u53F2\u4EFB\u52A1\n\u5B8C\u6210\u4EFB\u52A1\uFF0C\u6216\u8BA9\u8BBE\u7F6E\u4E86\u622A\u6B62\u65E5\u671F\u7684\u4EFB\u52A1\u903E\u671F\u540E\uFF0C\u4F1A\u663E\u793A\u5728\u8FD9\u91CC\u3002"),
                TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 100, 0, 0), TextWrapping = TextWrapping.Wrap,
                Opacity = .72 });
            var disabledActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right };
            var disabledDelete = PresetFooterAction(global::Tomatotodo_Windows.Data.UiText.T("\u5220\u9664"), (_, _) => { });
            disabledDelete.IsEnabled = false;
            var disabledRestore = PresetFooterAction(global::Tomatotodo_Windows.Data.UiText.T("\u21B6  \u6062\u590D"), (_, _) => { }, true);
            disabledRestore.IsEnabled = false;
            disabledActions.Children.Add(disabledDelete);
            disabledActions.Children.Add(disabledRestore);
            FitPresetFooterActions(disabledActions);
            PresetDrawerFooter.Children.Add(disabledActions);
            return;
        }
        Button? deleteAction = null;
        Button? restoreAction = null;
        var rowChecks = new List<CheckBox>();
        var all = new CheckBox { IsThreeState = true, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(all, global::Tomatotodo_Windows.Data.UiText.T("\u5168\u9009\u5386\u53F2\u4EFB\u52A1"));
        ToolTipService.SetToolTip(all, global::Tomatotodo_Windows.Data.UiText.T("\u5168\u9009\u5386\u53F2\u4EFB\u52A1"));
        var table = new StackPanel { Spacing = 0 };
        var header = new Grid
        {
            MinHeight = 48,
            Padding = new Thickness(12, 8, 12, 8),
            ColumnSpacing = 8,
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        header.Children.Add(all);
        var titleHeader = PresetText(global::Tomatotodo_Windows.Data.UiText.T("\u6807\u9898"), 14, true);
        titleHeader.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(titleHeader, 1); header.Children.Add(titleHeader);
        var statusHeader = PresetText(global::Tomatotodo_Windows.Data.UiText.T("\u72B6\u6001"), 14, true);
        statusHeader.VerticalAlignment = VerticalAlignment.Center;
        statusHeader.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(statusHeader, 2); header.Children.Add(statusHeader);
        var timeHeader = PresetText(global::Tomatotodo_Windows.Data.UiText.T("\u65F6\u95F4"), 14, true);
        timeHeader.VerticalAlignment = VerticalAlignment.Center;
        timeHeader.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(timeHeader, 3); header.Children.Add(timeHeader);
        table.Children.Add(header);
        void UpdateSelectionState()
        {
            var selectedCount = _state.ArchivedTasks.Count(t => _selectedHistory.Contains(t.Id));
            all.IsChecked = selectedCount == 0 ? false : selectedCount == _state.ArchivedTasks.Count ? true : null;
            if (deleteAction is not null) deleteAction.IsEnabled = selectedCount > 0;
            if (restoreAction is not null) restoreAction.IsEnabled = selectedCount > 0;
        }
        foreach (var archived in _state.ArchivedTasks.ToArray())
        {
            var row = new Grid { Padding = new Thickness(12, 10, 12, 10), ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            var check = new CheckBox { IsChecked = _selectedHistory.Contains(archived.Id),
                VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(check, global::Tomatotodo_Windows.Data.UiText.F("\u9009\u62E9\u5386\u53F2\u4EFB\u52A1 {0}", archived.Task.Title));
            rowChecks.Add(check);
            check.Checked += (_, _) => { _selectedHistory.Add(archived.Id); UpdateSelectionState(); };
            check.Unchecked += (_, _) => { _selectedHistory.Remove(archived.Id); UpdateSelectionState(); };
            row.Children.Add(check);
            var taskTitle = new TextBlock { Text = archived.Task.Title, FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(taskTitle, 1); row.Children.Add(taskTitle);
            var expired = archived.Status == "expired";
            var statusText = expired ? global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u8FC7\u671F") : global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u5B8C\u6210");
            var statusColor = expired
                ? (IsDarkAppearance ? ColorHelper.FromArgb(255, 255, 138, 128) : ColorHelper.FromArgb(255, 196, 43, 28))
                : (IsDarkAppearance ? ColorHelper.FromArgb(255, 111, 214, 154) : ColorHelper.FromArgb(255, 15, 107, 53));
            var status = new Border
            {
                Background = new SolidColorBrush(ColorHelper.FromArgb(IsDarkAppearance ? (byte)34 : (byte)20,
                    statusColor.R, statusColor.G, statusColor.B)),
                BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(IsDarkAppearance ? (byte)100 : (byte)88,
                    statusColor.R, statusColor.G, statusColor.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 3, 7, 3),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = statusText, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(statusColor),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            AutomationProperties.SetName(status, statusText);
            Grid.SetColumn(status, 2); row.Children.Add(status);
            var time = new TextBlock { Text = archived.ArchivedAt.ToLocalTime().ToString("MM-dd HH:mm"),
                FontSize = 12, Opacity = .72, VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(time, 3); row.Children.Add(time);
            table.Children.Add(new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = row
            });
        }
        all.Click += (_, _) =>
        {
            var selectAll = all.IsChecked == true;
            if (selectAll)
                foreach (var item in _state.ArchivedTasks) _selectedHistory.Add(item.Id);
            else
                _selectedHistory.Clear();
            foreach (var check in rowChecks) check.IsChecked = selectAll;
            UpdateSelectionState();
        };
        PresetDrawerBody.Children.Add(new Grid
        {
            Children =
            {
                new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.F("{0} \u9879\u5386\u53F2\u4EFB\u52A1", _state.ArchivedTasks.Count), FontSize = 12,
                    Opacity = .72, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) }
            }
        });
        PresetDrawerBody.Children.Add(new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = table
        });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right };
        deleteAction = PresetFooterAction(global::Tomatotodo_Windows.Data.UiText.T("\u5220\u9664"), async (_, _) =>
        {
            var dialog = new ContentDialog { Title = global::Tomatotodo_Windows.Data.UiText.T("\u6C38\u4E45\u5220\u9664\u6240\u9009\u5386\u53F2\u4EFB\u52A1\uFF1F"), PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u5220\u9664"),
                CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), XamlRoot = XamlRoot };
            StyleDestructiveDialog(dialog);
            if (await ShowThemedDialogAsync(dialog) != ContentDialogResult.Primary) return;
            _state.ArchivedTasks.RemoveAll(t => _selectedHistory.Contains(t.Id));
            _selectedHistory.Clear(); Save(); OpenPresetHistory();
        });
        restoreAction = PresetFooterAction(global::Tomatotodo_Windows.Data.UiText.T("\u21B6  \u6062\u590D"), async (_, _) =>
        {
            var selected = _state.ArchivedTasks.Where(t => _selectedHistory.Contains(t.Id)).ToArray();
            if (selected.Length == 0) return;
            var targetChoice = new ComboBox { Header = global::Tomatotodo_Windows.Data.UiText.T("\u6062\u590D\u5230\u6E05\u5355"), Width = 280 };
            foreach (var preset in _state.Presets) targetChoice.Items.Add(preset.Name);
            targetChoice.SelectedIndex = Math.Max(0, _state.Presets.FindIndex(p => p.Id == selected[0].PresetId));
            var targetDialog = new ContentDialog { Title = global::Tomatotodo_Windows.Data.UiText.T("\u6062\u590D\u5386\u53F2\u4EFB\u52A1"), Content = targetChoice,
                PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u4E0B\u4E00\u6B65"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), XamlRoot = XamlRoot };
            if (await ShowThemedDialogAsync(targetDialog) != ContentDialogResult.Primary) return;
            var keepProgress = true;
            if (selected.Any(t => t.Task.CompletedPomodoros > 0))
            {
                var progressDialog = new ContentDialog { Title = global::Tomatotodo_Windows.Data.UiText.T("\u4FDD\u7559\u756A\u8304\u8FDB\u5EA6\uFF1F"),
                    Content = global::Tomatotodo_Windows.Data.UiText.T("\u53EF\u4EE5\u4FDD\u7559\u5DF2\u5B8C\u6210\u7684\u756A\u8304\u6570\uFF0C\u6216\u4ECE\u96F6\u91CD\u65B0\u5F00\u59CB\u3002"),
                    PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u4FDD\u7559\u8FDB\u5EA6"), SecondaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u4ECE\u96F6\u5F00\u59CB"),
                    CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), XamlRoot = XamlRoot };
                var result = await ShowThemedDialogAsync(progressDialog);
                if (result == ContentDialogResult.None) return;
                keepProgress = result == ContentDialogResult.Primary;
            }
            var target = _state.Presets[targetChoice.SelectedIndex];
            foreach (var archived in selected)
            {
                var existingPreset = _state.Presets.FirstOrDefault(p => p.Tasks.Any(t => t.Id == archived.Task.Id));
                var task = existingPreset?.Tasks.First(t => t.Id == archived.Task.Id);
                if (task is null)
                {
                    task = JsonSerializer.Deserialize<TodoTask>(JsonSerializer.Serialize(archived.Task))!;
                    task.Id = Guid.NewGuid();
                }
                else if (existingPreset != target)
                    existingPreset!.Tasks.Remove(task);
                task.IsComplete = false;
                if (!keepProgress) task.CompletedPomodoros = 0;
                if (!target.Tasks.Contains(task)) target.Tasks.Add(task);
                _state.ArchivedTasks.Remove(archived);
            }
            _selectedHistory.Clear(); Save(); OpenPresetHistory(); Render();
        }, true);
        actions.Children.Add(deleteAction); actions.Children.Add(restoreAction);
        FitPresetFooterActions(actions);
        UpdateSelectionState();
        PresetDrawerFooter.Children.Add(actions);
    }

    private async Task ShowScheduleDialog(TaskPreset preset, string kind)
    {
        var now = DateTimeOffset.Now;
        var choices = new ComboBox { Header = kind == "due" ? global::Tomatotodo_Windows.Data.UiText.T("\u622A\u6B62\u65E5\u671F") : global::Tomatotodo_Windows.Data.UiText.T("\u63D0\u9192\u6211"), Width = 300 };
        foreach (var label in kind == "due"
            ? new[] { global::Tomatotodo_Windows.Data.UiText.T("\u4E0D\u8BBE\u7F6E"), global::Tomatotodo_Windows.Data.UiText.T("\u4ECA\u5929"), global::Tomatotodo_Windows.Data.UiText.T("\u660E\u5929"), global::Tomatotodo_Windows.Data.UiText.T("\u4E0B\u5468"), global::Tomatotodo_Windows.Data.UiText.T("\u9009\u62E9\u65E5\u671F") }
            : new[] { global::Tomatotodo_Windows.Data.UiText.T("\u4E0D\u63D0\u9192"), global::Tomatotodo_Windows.Data.UiText.T("\u4ECA\u65E5\u665A\u4E9B\u65F6\u5019"), global::Tomatotodo_Windows.Data.UiText.T("\u660E\u5929"), global::Tomatotodo_Windows.Data.UiText.T("\u4E0B\u5468"), global::Tomatotodo_Windows.Data.UiText.T("\u9009\u62E9\u65E5\u671F\u548C\u65F6\u95F4") })
            choices.Items.Add(label);
        choices.SelectedIndex = 0;
        var date = new CalendarDatePicker { Header = global::Tomatotodo_Windows.Data.UiText.T("\u65E5\u671F"), Date = now, Visibility = Visibility.Collapsed };
        var time = new TimePicker { Header = global::Tomatotodo_Windows.Data.UiText.T("\u65F6\u95F4"), Time = new TimeSpan(9, 0, 0), Visibility = Visibility.Collapsed };
        choices.SelectionChanged += (_, _) =>
        {
            date.Visibility = choices.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
            time.Visibility = kind == "remind" && choices.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
        };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(choices); content.Children.Add(date); content.Children.Add(time);
        var dialog = new ContentDialog { Title = kind == "due" ? global::Tomatotodo_Windows.Data.UiText.T("\u622A\u6B62\u65E5\u671F") : global::Tomatotodo_Windows.Data.UiText.T("\u63D0\u9192\u6211"), Content = content,
            PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u786E\u5B9A"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), XamlRoot = XamlRoot };
        MakePresetPopupOpaque(dialog);
        if (await ShowThemedDialogAsync(dialog) != ContentDialogResult.Primary) return;
        DateTimeOffset? value = choices.SelectedIndex switch
        {
            1 when kind == "due" => now.Date.AddHours(23).AddMinutes(59),
            1 => now.Date.AddHours(18),
            2 when kind == "due" => now.Date.AddDays(1).AddHours(23).AddMinutes(59),
            2 => now.Date.AddDays(1).AddHours(9),
            3 => now.Date.AddDays(7).AddHours(kind == "due" ? 23 : 9),
            4 when date.Date.HasValue => date.Date.Value.Date.Add(kind == "due" ? new TimeSpan(23, 59, 0) : time.Time),
            _ => null
        };
        if (kind == "due") preset.DueAt = value;
        else { preset.RemindAt = value; preset.LastRemindedAt = null; }
        Save(); Render();
    }

    private async Task ShowRepeatDialog(TaskPreset preset)
    {
        var choices = new ComboBox { Header = global::Tomatotodo_Windows.Data.UiText.T("\u91CD\u590D"), Width = 300 };
        foreach (var label in new[] { global::Tomatotodo_Windows.Data.UiText.T("\u4E0D\u91CD\u590D"), global::Tomatotodo_Windows.Data.UiText.T("\u6BCF\u4E00\u5929"), global::Tomatotodo_Windows.Data.UiText.T("\u5DE5\u4F5C\u65E5"), global::Tomatotodo_Windows.Data.UiText.T("\u6BCF\u4E00\u5468"), global::Tomatotodo_Windows.Data.UiText.T("\u6BCF\u4E00\u4E2A\u6708"), global::Tomatotodo_Windows.Data.UiText.T("\u6BCF\u4E00\u5E74"), global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u5B9A\u4E49") })
            choices.Items.Add(label);
        choices.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "none", "daily", "workday", "weekly", "monthly", "yearly", "custom" }, preset.Repeat));
        var interval = new NumberBox { Header = global::Tomatotodo_Windows.Data.UiText.T("\u91CD\u590D\u5468\u671F"), Minimum = 1, Maximum = 365,
            Value = preset.RepeatInterval };
        var unit = new ComboBox { Header = global::Tomatotodo_Windows.Data.UiText.T("\u5355\u4F4D") };
        foreach (var label in new[] { global::Tomatotodo_Windows.Data.UiText.T("\u5929"), global::Tomatotodo_Windows.Data.UiText.T("\u5468"), global::Tomatotodo_Windows.Data.UiText.T("\u6708"), global::Tomatotodo_Windows.Data.UiText.T("\u5E74") }) unit.Items.Add(label);
        unit.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "day", "week", "month", "year" }, preset.RepeatUnit));
        var days = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
        for (var column = 0; column < 4; column++)
            days.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        days.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        days.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var boxes = new List<CheckBox>();
        foreach (var label in new[] { global::Tomatotodo_Windows.Data.UiText.T("\u5468\u65E5"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u4E00"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u4E8C"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u4E09"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u56DB"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u4E94"), global::Tomatotodo_Windows.Data.UiText.T("\u5468\u516D") })
        {
            var index = boxes.Count;
            var box = new CheckBox
            {
                Content = label,
                IsChecked = preset.RepeatDays.Contains((DayOfWeek)index),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(box, index % 4);
            Grid.SetRow(box, index / 4);
            boxes.Add(box);
            days.Children.Add(box);
        }
        var custom = new StackPanel { Spacing = 12 };
        var customFields = new Grid { ColumnSpacing = 12 };
        customFields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        customFields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        customFields.Children.Add(interval);
        Grid.SetColumn(unit, 1);
        customFields.Children.Add(unit);
        var daySection = new StackPanel { Spacing = 4 };
        daySection.Children.Add(PresetText(global::Tomatotodo_Windows.Data.UiText.T("\u91CD\u590D\u65E5\u671F"), 12, true));
        daySection.Children.Add(days);
        custom.Children.Add(customFields);
        custom.Children.Add(daySection);
        var customBody = new StackPanel { Spacing = 12 };
        customBody.Children.Add(PresetText(global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u5B9A\u4E49\u5468\u671F"), 14, true));
        customBody.Children.Add(custom);
        var customCard = new Border
        {
            Width = 300, Padding = new Thickness(12), CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1), Child = customBody,
            Visibility = choices.SelectedIndex == 6 ? Visibility.Visible : Visibility.Collapsed };
        choices.SelectionChanged += (_, _) => customCard.Visibility = choices.SelectedIndex == 6 ? Visibility.Visible : Visibility.Collapsed;
        void UpdateWeekdayVisibility() =>
            daySection.Visibility = choices.SelectedIndex == 6 && unit.SelectedIndex == 1
                ? Visibility.Visible : Visibility.Collapsed;
        choices.SelectionChanged += (_, _) => UpdateWeekdayVisibility();
        unit.SelectionChanged += (_, _) => UpdateWeekdayVisibility();
        UpdateWeekdayVisibility();
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(choices);
        content.Children.Add(customCard);
        var dialog = new ContentDialog { Title = global::Tomatotodo_Windows.Data.UiText.T("\u91CD\u590D"), Content = content, PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u786E\u5B9A"),
            CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), XamlRoot = XamlRoot };
        MakePresetPopupOpaque(dialog);
        if (await ShowThemedDialogAsync(dialog) != ContentDialogResult.Primary) return;
        preset.Repeat = new[] { "none", "daily", "workday", "weekly", "monthly", "yearly", "custom" }[choices.SelectedIndex];
        preset.RepeatInterval = (int)interval.Value;
        preset.RepeatUnit = new[] { "day", "week", "month", "year" }[unit.SelectedIndex];
        preset.RepeatDays = preset.Repeat == "custom" && preset.RepeatUnit == "week"
            ? boxes.Select((box, index) => (box, index))
                .Where(item => item.box.IsChecked == true).Select(item => (DayOfWeek)item.index).ToList()
            : [];
        Save(); Render();
    }
}
