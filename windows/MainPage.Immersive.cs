using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Tomatotodo_Windows.Views;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private Button? _miniPlayButton, _immersivePlay;
    private TextBlock? _immersiveStatus;
    private FlipClockDigit[]? _flipDigits;

    private void UpdateImmersiveSurface()
    {
        ImmersiveHost.Visibility = _state.ImmersiveMode ? Visibility.Visible : Visibility.Collapsed;
        if (!_state.ImmersiveMode) return;
        if (_flipDigits is null)
        {
            ImmersiveHost.RequestedTheme = ElementTheme.Dark;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
            _flipDigits = Enumerable.Range(0, 3).Select(_ => new FlipClockDigit()).ToArray();
            foreach (var digit in _flipDigits)
                row.Children.Add(new Border { Child = digit, CornerRadius = new CornerRadius(28),
                    BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 40, 41, 40)) });
            ImmersiveHost.Children.Add(new Viewbox { Child = row, Stretch = Stretch.Uniform, Margin = new Thickness(40, 120, 40, 100), MaxWidth = 1200 });
            var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            _immersiveStatus = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };
            controls.Children.Add(_immersiveStatus);
            controls.Children.Add(new TextBlock { Text = "Tomatotodo", Opacity = .6, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            _immersivePlay = Action(global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u59CB"), (_, _) => StartPause());
            _immersivePlay.Width = _immersivePlay.Height = 40;
            _immersivePlay.Padding = new Thickness(0);
            _immersivePlay.Margin = new Thickness(0);
            _immersivePlay.CornerRadius = new CornerRadius(20);
            _immersivePlay.HorizontalContentAlignment = HorizontalAlignment.Center;
            _immersivePlay.VerticalContentAlignment = VerticalAlignment.Center;
            controls.Children.Add(_immersivePlay);
            ImmersiveHost.Children.Add(new Border { Child = controls, CornerRadius = new CornerRadius(28), Padding = new Thickness(16, 8, 10, 8),
                Background = new SolidColorBrush(ColorHelper.FromArgb(255, 20, 21, 20)), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(12, 20, 12, 0) });
            var exit = Action(global::Tomatotodo_Windows.Data.UiText.T("\u9000\u51FA\u6C89\u6D78"), (_, _) => ExitImmersive());
            exit.Content = new FontIcon { Glyph = "\uE73F", FontSize = 20,
                FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"] };
            exit.Width = exit.Height = 40;
            exit.Padding = new Thickness(0);
            exit.HorizontalContentAlignment = HorizontalAlignment.Center;
            exit.VerticalContentAlignment = VerticalAlignment.Center;
            ToolTipService.SetToolTip(exit, global::Tomatotodo_Windows.Data.UiText.T("\u9000\u51FA\u6C89\u6D78\u6A21\u5F0F (Esc)"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(exit, global::Tomatotodo_Windows.Data.UiText.T("\u9000\u51FA\u6C89\u6D78\u6A21\u5F0F"));
            exit.HorizontalAlignment = HorizontalAlignment.Left; exit.VerticalAlignment = VerticalAlignment.Bottom; exit.Margin = new Thickness(24);
            ImmersiveHost.Children.Add(exit);
            var escape = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
            escape.Invoked += (_, e) => { ExitImmersive(); e.Handled = true; };
            ImmersiveHost.KeyboardAccelerators.Add(escape);
        }
        UpdateImmersiveDigits();
    }

    private void ExitImmersive()
    {
        _state.ImmersiveMode = false; ApplyImmersiveMode(); Save(); Render();
    }

    private void UpdateImmersiveDigits()
    {
        if (!_state.ImmersiveMode || _flipDigits is null) return;
        _flipDigits[0].SetValue((_remainingSeconds / 3600).ToString("00"));
        _flipDigits[1].SetValue((_remainingSeconds / 60 % 60).ToString("00"));
        _flipDigits[2].SetValue((_remainingSeconds % 60).ToString("00"));
        _immersiveStatus!.Text = _running ? (_breakPhase ? global::Tomatotodo_Windows.Data.UiText.T("\u77ED\u4F11\u4E2D") : global::Tomatotodo_Windows.Data.UiText.T("\u4E13\u6CE8\u4E2D")) : global::Tomatotodo_Windows.Data.UiText.T("\u5DF2\u6682\u505C");
        if (_immersivePlay!.Tag is not bool previous || previous != _running)
        {
            _immersivePlay.Content = MiniControlIcon(_running ? Symbol.Pause : Symbol.Play);
            _immersivePlay.Tag = _running;
            ToolTipService.SetToolTip(_immersivePlay, _running ? global::Tomatotodo_Windows.Data.UiText.T("\u6682\u505C") : global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u59CB"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_immersivePlay, _running ? global::Tomatotodo_Windows.Data.UiText.T("\u6682\u505C") : global::Tomatotodo_Windows.Data.UiText.T("\u5F00\u59CB"));
        }
    }
}
