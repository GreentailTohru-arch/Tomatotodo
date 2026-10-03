using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private StackPanel? _appearancePage;
    private Grid? _appearancePreview;
    private Grid? _appearanceColorPanel;
    private TextBlock? _appearanceMonogram;
    private TextBlock? _appearancePreviewDescription;
    private ComboBox? _appearanceThemeMode;
    private ComboBox? _appearanceAccentMode;
    private ToggleSwitch? _appearancePureBlack;
    private ToggleSwitch? _appearanceShellAcrylic;
    private Slider? _appearanceScale;
    private TextBlock? _appearanceScaleText;
    private SettingsExpander? _appearanceAccentExpander;
    private StackPanel? _appearanceAccentBody;
    private readonly List<FontIcon> _appearanceAccentGlyphs = [];
    private readonly List<(string Header, string Description, Control Card)> _appearanceSearchCards = [];
    private bool _updatingAppearanceControls;

    // Windows 11 Settings accent-color board, retained in the same visible order as the reference.
    private static readonly string[] WindowsAccentColors =
    [
        "#FFB900", "#FF8C00", "#F7630C", "#CA5010", "#DA3B01", "#EF6950", "#D13438", "#FF4343", "#E74856",
        "#E81123", "#EA005E", "#C30052", "#E3008C", "#BF0077", "#C239B3", "#9A0089", "#0078D7", "#0063B1",
        "#8E8CD8", "#6B69D6", "#8764B8", "#744DA9", "#B146C2", "#881798", "#0099BC", "#2D7D9A", "#00B7C3",
        "#038387", "#00B294", "#018574", "#00CC6A", "#10893E", "#7A7574", "#5D5A58", "#68768A", "#515C6B",
        "#567C73", "#486860", "#498205", "#107C10", "#767676", "#4C4A48", "#647C7F", "#525E61", "#7E977D",
        "#4B5D52", "#847545", "#7E735F"
    ];

    private void RenderAppearance()
    {
        if (_appearancePage is not null)
        {
            RefreshAppearanceVisuals();
            foreach (var (header, description, card) in _appearanceSearchCards)
            {
                card.ClearValue(Control.BackgroundProperty);
                TrackSettingsSearch(header, description, card);
            }
            if (!PageBody.Children.Contains(_appearancePage)) PageBody.Children.Add(_appearancePage);
            return;
        }

        var page = new StackPanel { Spacing = 12, Margin = new Thickness(0, 2, 0, 50) };
        page.Children.Add(BuildAppearancePreview());

        var mode = new ComboBox { Width = 130, HorizontalAlignment = HorizontalAlignment.Right };
        _appearanceThemeMode = mode;
        mode.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u52A8")); mode.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u6D45\u8272")); mode.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u6DF1\u8272"));
        mode.SelectedIndex = _state.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
        mode.SelectionChanged += (_, _) =>
        {
            if (_updatingAppearanceControls) return;
            _state.Theme = mode.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "auto" };
            ApplyTheme(); Save(); Render();
        };
        page.Children.Add(AppearanceSetting(global::Tomatotodo_Windows.Data.UiText.T("\u9009\u62E9\u6A21\u5F0F"), global::Tomatotodo_Windows.Data.UiText.T("\u63A7\u5236\u5E94\u7528\u4F7F\u7528\u6D45\u8272\u3001\u6DF1\u8272\u6216\u8DDF\u968F\u7CFB\u7EDF\u5916\u89C2\u3002"), mode, IconGlyph.Brightness));

        page.Children.Add(BuildAccentPalette());

        var acrylic = new ToggleSwitch { IsOn = _state.ShellAcrylic, OnContent = global::Tomatotodo_Windows.Data.UiText.T("\u5F00"), OffContent = global::Tomatotodo_Windows.Data.UiText.T("\u5173"), MinWidth = 98, HorizontalAlignment = HorizontalAlignment.Right };
        _appearanceShellAcrylic = acrylic;
        acrylic.Toggled += (_, _) => { if (_updatingAppearanceControls) return; _state.ShellAcrylic = acrylic.IsOn; Save(); Render(); };
        page.Children.Add(AppearanceSetting(global::Tomatotodo_Windows.Data.UiText.T("\u4E9A\u514B\u529B\u78E8\u7802\u73BB\u7483"), global::Tomatotodo_Windows.Data.UiText.T("\u6807\u9898\u680F\u4E0E\u5BFC\u822A\u680F\u4F7F\u7528\u4E9A\u514B\u529B\uFF0C\u6DF1\u8272\u6A21\u5F0F\u4E0B\u4E5F\u8986\u76D6\u5DE5\u4F5C\u533A\uFF1B\u7CFB\u7EDF\u5173\u95ED\u900F\u660E\u6548\u679C\u65F6\u81EA\u52A8\u4F7F\u7528\u7EAF\u8272\u3002"), acrylic, IconGlyph.AcrylicLayers));

        var black = new ToggleSwitch { IsOn = _state.PureBlack, OnContent = global::Tomatotodo_Windows.Data.UiText.T("\u5F00"), OffContent = global::Tomatotodo_Windows.Data.UiText.T("\u5173"), MinWidth = 98, HorizontalAlignment = HorizontalAlignment.Right };
        _appearancePureBlack = black;
        black.Toggled += (_, _) => { if (_updatingAppearanceControls) return; _state.PureBlack = black.IsOn; ApplyTheme(); Save(); Render(); };
        page.Children.Add(AppearanceSetting(global::Tomatotodo_Windows.Data.UiText.T("\u7EAF\u9ED1\u6A21\u5F0F"), global::Tomatotodo_Windows.Data.UiText.T("\u4EC5\u5728\u6DF1\u8272\u6A21\u5F0F\u4E0B\u4F7F\u7528\u7EAF\u9ED1\u80CC\u666F\uFF0C\u9002\u5408 OLED \u5C4F\u5E55\u3002"), black, IconGlyph.Contrast));

        var scale = new Slider { Minimum = 85, Maximum = 125, Value = _state.UiScale, StepFrequency = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        _appearanceScale = scale;
        var scaleText = new TextBlock { Text = $"{_state.UiScale}%", Width = 56, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        _appearanceScaleText = scaleText;
        scale.ValueChanged += (_, _) =>
        {
            if (_updatingAppearanceControls) return;
            _state.UiScale = (int)Math.Round(scale.Value / 5) * 5;
            scaleText.Text = $"{_state.UiScale}%";
            ApplyUiScale();
            Save();
        };
        var scalePanel = new Grid { ColumnSpacing = 16, MinWidth = 220 };
        scalePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scalePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        scalePanel.Children.Add(scale); Grid.SetColumn(scaleText, 1); scalePanel.Children.Add(scaleText);
        page.Children.Add(AppearanceSetting(global::Tomatotodo_Windows.Data.UiText.T("\u5B57\u4F53\u7F29\u653E"), global::Tomatotodo_Windows.Data.UiText.T("\u540C\u6B65\u8C03\u6574\u9875\u9762\u6587\u5B57\u4E0E\u64CD\u4F5C\u63A7\u4EF6\u7684\u663E\u793A\u5BC6\u5EA6\u3002"), scalePanel, IconGlyph.FontSize));

        page.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u989C\u8272\u65B9\u6848\u4F1A\u4FDD\u5B58\u5230\u672C\u673A\uFF0C\u4E0E\u4F60\u7684\u8BFE\u7A0B\u3001\u4EFB\u52A1\u548C\u4E13\u6CE8\u8BB0\u5F55\u5206\u5F00\u5B58\u50A8\u3002"),
            FontSize = 12, Opacity = .72, Margin = new Thickness(2, 8, 0, 0) });
        _appearancePage = page;
        PageBody.Children.Add(page);
    }

    private UIElement BuildAppearancePreview()
    {
        var accent = ParseColor(_state.AccentColor);
        var preview = new Grid { Height = 122, Background = new SolidColorBrush(IsDarkAppearance
            ? ColorHelper.FromArgb(255, 45, 45, 45) : Colors.White), CornerRadius = new CornerRadius(12) };
        _appearancePreview = preview;
        preview.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_compactSettingsLayout ? 112 : 190) });
        preview.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var colorPanel = new Grid { Background = new SolidColorBrush(accent), CornerRadius = new CornerRadius(12, 0, 0, 12) };
        _appearanceColorPanel = colorPanel;
        var monogram = new TextBlock { Text = "T", FontFamily = new FontFamily("Georgia"), FontSize = 62,
            Foreground = new SolidColorBrush(ContrastRatio(Colors.Black, accent) >= ContrastRatio(Colors.White, accent)
                ? Colors.Black : Colors.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _appearanceMonogram = monogram;
        colorPanel.Children.Add(monogram);
        preview.Children.Add(colorPanel);
        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 6, Margin = new Thickness(26, 0, 26, 0) };
        copy.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u4E2A\u6027\u5316 \u00B7 \u989C\u8272"), FontSize = 23, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var description = new TextBlock { Text = $"{ThemeLabel()} · {_state.AccentColor.ToUpperInvariant()} · {_state.UiScale}%", FontSize = 12, Opacity = .72 };
        _appearancePreviewDescription = description;
        copy.Children.Add(description);
        Grid.SetColumn(copy, 1); preview.Children.Add(copy);
        return preview;
    }

    private UIElement BuildAccentPalette()
    {
        var mode = new ComboBox
        {
            Width = 130, VerticalAlignment = VerticalAlignment.Center,
            SelectedIndex = _state.AccentMode == "auto" ? 0 : 1
        };
        _appearanceAccentMode = mode;
        mode.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u52A8"));
        mode.Items.Add(global::Tomatotodo_Windows.Data.UiText.T("\u624B\u52A8"));
        mode.SelectionChanged += (_, _) =>
        {
            if (_updatingAppearanceControls) return;
            _state.AccentMode = mode.SelectedIndex == 0 ? "auto" : "manual";
            SyncSystemAccentColor();
            Save();
            ApplyAccentResources();
            RefreshAppearanceVisuals();
        };
        var expander = new SettingsExpander
        {
            Header = global::Tomatotodo_Windows.Data.UiText.T("\u4E3B\u9898\u8272"),
            HeaderIcon = FluentIcon(IconGlyph.Color, 20, new SolidColorBrush(AccentDisplayColor)),
            Content = mode,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsExpanded = _accentPaletteExpanded,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        expander.Expanded += (_, _) => _accentPaletteExpanded = true;
        expander.Collapsed += (_, _) => _accentPaletteExpanded = false;
        _appearanceAccentExpander = expander;

        var bodyInset = _ultraCompactSettingsLayout ? 20 : 56;
        var swatchSize = _ultraCompactSettingsLayout ? 36 : _compactSettingsLayout ? 42 : 50;
        var colorColumns = _ultraCompactSettingsLayout ? 5 : _compactSettingsLayout ? 6 : 9;
        var body = new StackPanel { Spacing = 14, Padding = new Thickness(bodyInset, 12, 18, 20) };
        _appearanceAccentBody = body;
        body.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u6700\u8FD1\u4F7F\u7528\u7684\u989C\u8272"), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        body.Children.Add(BuildColorGrid(_state.RecentAccentColors, 5, swatchSize));
        body.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("Windows \u989C\u8272"), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 0) });
        body.Children.Add(BuildColorGrid(WindowsAccentColors, colorColumns, swatchSize));
        expander.Items.Add(new SettingsCard
        {
            ContentAlignment = ContentAlignment.Vertical,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            // The expander's SettingsCard template already owns the outer surface.  Removing the
            // nested card padding lets the explicit 56 epx content inset share the header-text baseline.
            Padding = new Thickness(0),
            Content = body
        });

        var showPicker = new Button { Content = global::Tomatotodo_Windows.Data.UiText.T("\u67E5\u770B\u989C\u8272"), MinWidth = 130, HorizontalAlignment = HorizontalAlignment.Right };
        showPicker.Click += async (_, _) => await ShowCustomAccentDialog();
        expander.Items.Add(new SettingsCard
        {
            Header = global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u5B9A\u4E49\u989C\u8272"),
            ContentAlignment = ContentAlignment.Right,
            Content = showPicker
        });
        TrackAppearanceSearch(global::Tomatotodo_Windows.Data.UiText.T("\u4E3B\u9898\u8272"), global::Tomatotodo_Windows.Data.UiText.T("Windows \u989C\u8272\u3001\u6700\u8FD1\u4F7F\u7528\u7684\u989C\u8272\u548C\u81EA\u5B9A\u4E49\u989C\u8272"), expander);
        return expander;
    }

    private async Task ShowCustomAccentDialog()
    {
        var picker = new ColorPicker
        {
            Color = ParseColor(_state.AccentColor),
            IsMoreButtonVisible = true,
            IsColorSpectrumVisible = true,
            IsColorChannelTextInputVisible = true,
            IsColorPreviewVisible = true,
            IsAlphaEnabled = false,
            Width = 390
        };
        var dialog = new ContentDialog
        {
            Title = global::Tomatotodo_Windows.Data.UiText.T("\u9009\u62E9\u81EA\u5B9A\u4E49\u4E3B\u9898\u8272"),
            Content = picker,
            PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u5B8C\u6210"),
            CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await ShowThemedDialogAsync(dialog) == ContentDialogResult.Primary)
            SelectAccent(ToHex(picker.Color));
    }

    private UIElement BuildColorGrid(IEnumerable<string> colors, int columns, double size)
    {
        var list = colors.ToList();
        var grid = new Grid { ColumnSpacing = 4, RowSpacing = 4, HorizontalAlignment = HorizontalAlignment.Left };
        for (var index = 0; index < columns; index++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(size) });
        for (var row = 0; row < Math.Ceiling(list.Count / (double)columns); row++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(size) });
        for (var index = 0; index < list.Count; index++)
        {
            var hex = list[index];
            var selected = string.Equals(_state.AccentColor, hex, StringComparison.OrdinalIgnoreCase);
            var colorSurface = new Border
            {
                Background = new SolidColorBrush(ParseColor(hex)),
                BorderBrush = new SolidColorBrush(selected ? Colors.White : Colors.Transparent),
                BorderThickness = new Thickness(selected ? 2 : 0),
                CornerRadius = new CornerRadius(3)
            };
            var selectedMarker = selected ? new Border
            {
                Width = 21, Height = 21, Background = new SolidColorBrush(Colors.White), CornerRadius = new CornerRadius(0, 3, 0, 3),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Child = new SymbolIcon { Symbol = Symbol.Accept, Foreground = new SolidColorBrush(Colors.Black), Width = 14, Height = 14 }
            } : null;
            var content = new Grid();
            content.Children.Add(colorSurface);
            if (selectedMarker is not null) content.Children.Add(selectedMarker);
            var button = new Button
            {
                Width = size, Height = size, Padding = new Thickness(0), CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Colors.Transparent), BorderBrush = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = content
            };
            // The white outline is deliberately separate from the Button template: Windows settings keeps the
            // swatch color unchanged on hover, then adds only a crisp white 2 px keyline.
            button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Colors.Transparent);
            button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Colors.Transparent);
            button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Colors.Transparent);
            button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Colors.Transparent);
            button.PointerEntered += (_, _) =>
            {
                if (!selected)
                {
                    colorSurface.BorderBrush = new SolidColorBrush(Colors.White);
                    colorSurface.BorderThickness = new Thickness(2);
                }
            };
            button.PointerExited += (_, _) =>
            {
                if (!selected)
                {
                    colorSurface.BorderBrush = new SolidColorBrush(Colors.Transparent);
                    colorSurface.BorderThickness = new Thickness(0);
                }
            };
            ToolTipService.SetToolTip(button, hex);
            button.Click += (_, _) => SelectAccent(hex);
            Grid.SetColumn(button, index % columns); Grid.SetRow(button, index / columns); grid.Children.Add(button);
        }
        return grid;
    }

    private void SelectAccent(string hex)
    {
        // Do not rebuild the SettingsExpander: only its chevron may start the expand animation.
        _state.AccentMode = "manual";
        _state.AccentColor = hex.ToUpperInvariant();
        _state.RecentAccentColors.RemoveAll(color => string.Equals(color, _state.AccentColor, StringComparison.OrdinalIgnoreCase));
        _state.RecentAccentColors.Insert(0, _state.AccentColor);
        _state.RecentAccentColors = _state.RecentAccentColors.Take(5).ToList();
        Save();
        ApplyAccentResources();
        RefreshAppearanceVisuals();
    }

    private void RefreshAppearanceVisuals()
    {
        if (_appearancePage is null) return;
        var accent = ParseColor(_state.AccentColor);
        _updatingAppearanceControls = true;
        try
        {
            if (_appearanceThemeMode is not null)
                _appearanceThemeMode.SelectedIndex = _state.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
            if (_appearanceAccentMode is not null)
                _appearanceAccentMode.SelectedIndex = _state.AccentMode == "auto" ? 0 : 1;
            if (_appearancePureBlack is not null) _appearancePureBlack.IsOn = _state.PureBlack;
            if (_appearanceShellAcrylic is not null) _appearanceShellAcrylic.IsOn = _state.ShellAcrylic;
            if (_appearanceScale is not null) _appearanceScale.Value = _state.UiScale;
        }
        finally { _updatingAppearanceControls = false; }

        if (_appearancePreview is not null)
        {
            _appearancePreview.Background = new SolidColorBrush(IsDarkAppearance
                ? ColorHelper.FromArgb(255, 45, 45, 45) : Colors.White);
            _appearancePreview.ColumnDefinitions[0].Width = new GridLength(_compactSettingsLayout ? 112 : 190);
        }
        if (_appearanceColorPanel is not null) _appearanceColorPanel.Background = new SolidColorBrush(accent);
        if (_appearanceMonogram is not null)
            _appearanceMonogram.Foreground = new SolidColorBrush(ContrastRatio(Colors.Black, accent) >= ContrastRatio(Colors.White, accent)
                ? Colors.Black : Colors.White);
        if (_appearancePreviewDescription is not null)
            _appearancePreviewDescription.Text = $"{ThemeLabel()} · {_state.AccentColor.ToUpperInvariant()} · {_state.UiScale}%";
        if (_appearanceScaleText is not null) _appearanceScaleText.Text = $"{_state.UiScale}%";
        foreach (var glyph in _appearanceAccentGlyphs)
            glyph.Foreground = new SolidColorBrush(AccentDisplayColor);
        if (_appearanceAccentExpander?.HeaderIcon is FontIcon icon)
            icon.Foreground = new SolidColorBrush(AccentDisplayColor);

        if (_appearanceAccentBody is not null)
        {
            var size = _ultraCompactSettingsLayout ? 36 : _compactSettingsLayout ? 42 : 50;
            var columns = _ultraCompactSettingsLayout ? 5 : _compactSettingsLayout ? 6 : 9;
            _appearanceAccentBody.Padding = new Thickness(_ultraCompactSettingsLayout ? 20 : 56, 12, 18, 20);
            _appearanceAccentBody.Children.RemoveAt(3);
            _appearanceAccentBody.Children.Insert(3, BuildColorGrid(WindowsAccentColors, columns, size));
            _appearanceAccentBody.Children.RemoveAt(1);
            _appearanceAccentBody.Children.Insert(1, BuildColorGrid(_state.RecentAccentColors, 5, size));
        }
    }

    private SettingsCard AppearanceSetting(string header, string description, UIElement content, string glyph)
    {
        var glyphText = FluentIcon(glyph, 20, new SolidColorBrush(AccentDisplayColor));
        _appearanceAccentGlyphs.Add(glyphText);
        var card = CreateSettingsCard(header, description, content, glyphText);
        TrackAppearanceSearch(header, description, card);
        return card;
    }

    private void TrackAppearanceSearch(string header, string description, Control card)
    {
        _appearanceSearchCards.Add((header, description, card));
        TrackSettingsSearch(header, description, card);
    }

    private string ThemeLabel() => _state.Theme switch { "light" => global::Tomatotodo_Windows.Data.UiText.T("\u6D45\u8272"), "dark" => global::Tomatotodo_Windows.Data.UiText.T("\u6DF1\u8272"), _ => global::Tomatotodo_Windows.Data.UiText.T("\u81EA\u52A8") };
    private static Windows.UI.Color ParseColor(string hex)
    {
        try { return Windows.UI.Color.FromArgb(255, Convert.ToByte(hex[1..3], 16), Convert.ToByte(hex[3..5], 16), Convert.ToByte(hex[5..7], 16)); }
        catch { return ColorHelper.FromArgb(255, 48, 100, 59); }
    }
    private static string ToHex(Windows.UI.Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private bool SyncSystemAccentColor()
    {
        if (_state.AccentMode != "auto") return false;
        var systemAccent = ToHex(_systemUiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent));
        if (string.Equals(_state.AccentColor, systemAccent, StringComparison.OrdinalIgnoreCase)) return false;
        _state.AccentColor = systemAccent;
        return true;
    }

    private void SystemUiSettings_ColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!SyncSystemAccentColor())
            {
                if (_state.Theme == "auto") { ApplyTheme(); Render(); }
                return;
            }
            Save();
            if (IsAppearanceSettings)
            {
                ApplyAccentResources();
                RefreshAppearanceVisuals();
            }
            else Render();
        });
    }
}
