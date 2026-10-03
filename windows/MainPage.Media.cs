using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Control;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".m4a", ".aac", ".wma", ".wav", ".flac", ".ogg", ".opus" };
    private readonly List<StorageFile> _musicFiles = [];
    private MediaPlayer? _localPlayer;
    private int _musicIndex = -1;
    private string _musicTitle = global::Tomatotodo_Windows.Data.UiText.T("\u5C1A\u672A\u9009\u62E9\u97F3\u4E50\u6587\u4EF6\u5939");
    private string _musicDetail = global::Tomatotodo_Windows.Data.UiText.T("\u5728\u5E38\u89C4 \u2192 \u4EEA\u8868\u76D8 \u2192 \u97F3\u4E50\u8BBE\u7F6E\u4E2D\u9009\u62E9\u6587\u4EF6\u5939");
    private ImageSource? _musicArtwork;
    private string _systemTitle = global::Tomatotodo_Windows.Data.UiText.T("\u6CA1\u6709\u6B63\u5728\u64AD\u653E\u7684\u5A92\u4F53");
    private string _systemDetail = global::Tomatotodo_Windows.Data.UiText.T("\u64AD\u653E\u97F3\u4E50\u6216\u89C6\u9891\u540E\u4F1A\u5728\u8FD9\u91CC\u663E\u793A");
    private ImageSource? _systemArtwork;
    private TextBlock? _mediaTitleText;
    private TextBlock? _mediaDetailText;
    private Border? _mediaArtworkHost;
    private Button? _mediaPlayButton;
    private Slider? _mediaSeek;
    private bool _updatingMediaSeek;
    private bool _mediaSeeking;
    private bool _musicShuffle;
    private bool _musicRepeatOne;
    private bool _musicMuted;
    private double _musicVolume = 0.7;
    private DateTimeOffset _lastMediaRefresh = DateTimeOffset.MinValue;
    private bool _refreshingMedia;
    private int _musicLoadVersion;
    private int _musicPlaybackVersion;
    private GlobalSystemMediaTransportControlsSessionManager? _systemMediaManager;
    private string? _systemMediaKey;

    private string LocalMusicFolderSummary() => _musicFiles.Count > 0
        ? global::Tomatotodo_Windows.Data.UiText.F("\u5DF2\u8F7D\u5165 {0} \u9996 \u00B7 {1}", _musicFiles.Count, System.IO.Path.GetDirectoryName(_musicFiles[0].Path))
        : global::Tomatotodo_Windows.Data.UiText.T("\u5C1A\u672A\u9009\u62E9\u6587\u4EF6\u5939");

    private async Task ChooseMusicFolderAsync()
    {
        var owner = _state;
        try
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.MusicLibrary };
            picker.FileTypeFilter.Add("*");
            InitializePicker(picker);
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null || !ReferenceEquals(owner, _state)) return;
            var token = _accountSession?.Current is { } account
                ? $"TomatotodoMusicFolder-{account.Id:N}" : "TomatotodoMusicFolder";
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, folder);
            _state.LocalMusicFolderToken = token;
            await LoadMusicFolderAsync(folder);
            if (!ReferenceEquals(owner, _state)) return;
            _state.MusicMode = "local";
            Save(); Render();
        }
        catch (Exception exception) { if (ReferenceEquals(owner, _state)) await ShowCourseError(global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u6253\u5F00\u97F3\u4E50\u6587\u4EF6\u5939"), exception.Message); }
    }

    private async Task RestoreMusicFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(_state.LocalMusicFolderToken)) return;
        var owner = _state;
        try
        {
            var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(_state.LocalMusicFolderToken);
            if (!ReferenceEquals(owner, _state)) return;
            await LoadMusicFolderAsync(folder);
        }
        catch
        {
            if (!ReferenceEquals(owner, _state)) return;
            _musicTitle = global::Tomatotodo_Windows.Data.UiText.T("\u97F3\u4E50\u6587\u4EF6\u5939\u4E0D\u53EF\u7528");
            _musicDetail = global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u5728\u5E38\u89C4\u4E2D\u91CD\u65B0\u9009\u62E9\u6587\u4EF6\u5939");
            UpdateMediaVisuals();
        }
    }

    private async Task LoadMusicFolderAsync(StorageFolder folder)
    {
        var owner = _state;
        var version = ++_musicLoadVersion;
        _localPlayer?.Pause();
        _musicFiles.Clear(); _musicIndex = -1; _musicArtwork = null;
        var files = new List<StorageFile>();
        await CollectAudioFilesAsync(folder, files);
        if (!ReferenceEquals(owner, _state) || version != _musicLoadVersion) return;
        _musicFiles.AddRange(files);
        _musicFiles.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
        _musicTitle = _musicFiles.Count == 0 ? global::Tomatotodo_Windows.Data.UiText.T("\u6587\u4EF6\u5939\u4E2D\u6CA1\u6709\u53EF\u64AD\u653E\u7684\u97F3\u9891") : folder.Name;
        _musicDetail = _musicFiles.Count == 0 ? global::Tomatotodo_Windows.Data.UiText.T("\u652F\u6301 MP3\u3001M4A\u3001FLAC\u3001WAV \u7B49\u683C\u5F0F") : global::Tomatotodo_Windows.Data.UiText.F("{0} \u9996\u6B4C\u66F2 \u00B7 \u70B9\u51FB\u64AD\u653E\u5F00\u59CB", _musicFiles.Count);
        UpdateMediaVisuals();
    }

    private async Task CollectAudioFilesAsync(StorageFolder folder, List<StorageFile> files)
    {
        // Keep large libraries bounded so choosing a parent folder cannot freeze the UI.
        foreach (var file in await folder.GetFilesAsync())
        {
            if (AudioExtensions.Contains(file.FileType)) files.Add(file);
            if (files.Count >= 5000) return;
        }
        foreach (var child in await folder.GetFoldersAsync())
        {
            try { await CollectAudioFilesAsync(child, files); }
            catch (UnauthorizedAccessException) { /* Skip inaccessible subfolders. */ }
            if (files.Count >= 5000) return;
        }
    }

    private async Task PlayMusicAtAsync(int index)
    {
        if (_musicFiles.Count == 0) { await ChooseMusicFolderAsync(); return; }
        _musicIndex = (index + _musicFiles.Count) % _musicFiles.Count;
        var file = _musicFiles[_musicIndex];
        var owner = _state;
        var version = ++_musicPlaybackVersion;
        _localPlayer ??= CreateMusicPlayer();
        _localPlayer.Source = MediaSource.CreateFromStorageFile(file);
        _musicTitle = System.IO.Path.GetFileNameWithoutExtension(file.Name);
        _musicDetail = global::Tomatotodo_Windows.Data.UiText.F("{0}/{1} \u00B7 \u672C\u5730\u97F3\u4E50", _musicIndex + 1, _musicFiles.Count);
        _musicArtwork = null;
        UpdateMediaVisuals();
        try
        {
            var properties = await file.Properties.GetMusicPropertiesAsync();
            if (!ReferenceEquals(owner, _state) || version != _musicPlaybackVersion) return;
            if (!string.IsNullOrWhiteSpace(properties.Title)) _musicTitle = properties.Title;
            if (!string.IsNullOrWhiteSpace(properties.Artist)) _musicDetail = properties.Artist;
            using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.MusicView, 240, ThumbnailOptions.UseCurrentScale);
            if (thumbnail is { Size: > 0 })
            {
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(thumbnail);
                if (!ReferenceEquals(owner, _state) || version != _musicPlaybackVersion) return;
                _musicArtwork = bitmap;
            }
        }
        catch { /* A malformed tag/cover must not prevent playback. */ }
        if (!ReferenceEquals(owner, _state) || version != _musicPlaybackVersion) return;
        _localPlayer?.Play();
        UpdateMediaVisuals();
    }

    private MediaPlayer CreateMusicPlayer()
    {
        var player = new MediaPlayer { AutoPlay = false, Volume = _musicVolume, IsMuted = _musicMuted };
        player.MediaEnded += (_, _) => DispatcherQueue.TryEnqueue(async () =>
        { if (ReferenceEquals(_localPlayer, player)) await PlayMusicAtAsync(_musicRepeatOne ? _musicIndex : NextMusicIndex()); });
        player.PlaybackSession.PlaybackStateChanged += (_, _) => DispatcherQueue.TryEnqueue(UpdateMediaVisuals);
        player.MediaFailed += (_, _) => DispatcherQueue.TryEnqueue(() => ShowAppMessage(global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u64AD\u653E\u97F3\u4E50"), global::Tomatotodo_Windows.Data.UiText.T("\u6587\u4EF6\u53EF\u80FD\u5DF2\u79FB\u52A8\u6216\u683C\u5F0F\u4E0D\u53D7\u652F\u6301\u3002"), InfoBarSeverity.Warning));
        return player;
    }

    private int NextMusicIndex() => _musicShuffle && _musicFiles.Count > 1
        ? (_musicIndex + Random.Shared.Next(1, _musicFiles.Count)) % _musicFiles.Count : _musicIndex + 1;

    private async Task RefreshMediaAsync()
    {
        if (_refreshingMedia || _state.MusicMode != "system") return;
        _refreshingMedia = true;
        _lastMediaRefresh = DateTimeOffset.Now;
        try
        {
            _systemMediaManager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = _systemMediaManager.GetCurrentSession();
            if (session is null)
            {
                _systemTitle = global::Tomatotodo_Windows.Data.UiText.T("\u6CA1\u6709\u6B63\u5728\u64AD\u653E\u7684\u5A92\u4F53");
                _systemDetail = global::Tomatotodo_Windows.Data.UiText.T("\u64AD\u653E\u97F3\u4E50\u6216\u89C6\u9891\u540E\u4F1A\u5728\u8FD9\u91CC\u663E\u793A");
                _systemArtwork = null; _systemMediaKey = null;
                UpdateMediaVisuals();
                return;
            }
            var properties = await session.TryGetMediaPropertiesAsync();
            var title = string.IsNullOrWhiteSpace(properties?.Title) ? global::Tomatotodo_Windows.Data.UiText.T("\u672A\u63D0\u4F9B\u6807\u9898") : properties.Title;
            var key = $"{session.SourceAppUserModelId}|{title}|{properties?.Artist}|{properties?.Subtitle}";
            _systemTitle = title;
            _systemDetail = !string.IsNullOrWhiteSpace(properties?.Artist) ? properties.Artist
                : !string.IsNullOrWhiteSpace(properties?.Subtitle) ? properties.Subtitle
                : session.SourceAppUserModelId.Split('!')[0];
            if (_systemMediaKey != key)
            {
                _systemMediaKey = key;
                _systemArtwork = null;
                if (properties?.Thumbnail is { } thumbnail)
                {
                    try
                    {
                        using var stream = await thumbnail.OpenReadAsync();
                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(stream);
                        _systemArtwork = bitmap;
                    }
                    catch { /* Some players do not expose cover art. */ }
                }
            }
            UpdateMediaVisuals();
        }
        catch
        {
            _systemTitle = global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u8BFB\u53D6\u5F53\u524D\u5A92\u4F53");
            _systemDetail = global::Tomatotodo_Windows.Data.UiText.T("\u8BF7\u786E\u8BA4\u64AD\u653E\u5668\u652F\u6301 Windows \u5A92\u4F53\u4F1A\u8BDD");
            _systemArtwork = null;
            UpdateMediaVisuals();
        }
        finally { _refreshingMedia = false; }
    }

    private UIElement BuildMediaBody()
    {
        var body = new Grid { ColumnSpacing = 12, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _mediaArtworkHost = new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(4),
            Background = DashboardBrush("SubtleFillColorSecondaryBrush"),
            BorderBrush = DashboardBrush("CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1) };
        body.Children.Add(_mediaArtworkHost);
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 5 };
        _mediaTitleText = new TextBlock { FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 };
        _mediaDetailText = Secondary("");
        _mediaDetailText.TextTrimming = TextTrimming.CharacterEllipsis;
        _mediaDetailText.MaxLines = 1;
        stack.Children.Add(_mediaTitleText); stack.Children.Add(_mediaDetailText);
        var content = new Grid { VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(stack);
        _mediaPlayButton = null;
        _mediaSeek = null;
        if (_state.MusicMode == "local" && !_dashboardEditing)
        {
            var controls = new Grid { Visibility = Visibility.Collapsed, RowSpacing = 4 };
            controls.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            controls.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _mediaSeek = new Slider { Minimum = 0, Maximum = 1, Height = 28, MinWidth = 0 };
            _mediaSeeking = false;
            _mediaSeek.AddHandler(UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => _mediaSeeking = true), true);
            _mediaSeek.AddHandler(UIElement.PointerReleasedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, _) => _mediaSeeking = false), true);
            _mediaSeek.PointerCaptureLost += (_, _) => _mediaSeeking = false;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_mediaSeek, global::Tomatotodo_Windows.Data.UiText.T("\u64AD\u653E\u8FDB\u5EA6\uFF08\u79D2\uFF09"));
            _mediaSeek.ValueChanged += (_, args) =>
            {
                if (!_updatingMediaSeek && _localPlayer?.PlaybackSession.NaturalDuration.TotalSeconds > 0)
                    _localPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(args.NewValue);
            };
            controls.Children.Add(_mediaSeek);
            var row = new Grid { ColumnSpacing = 4 };
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var transport = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            Button Command(string label, Symbol icon)
            {
                var button = new Button { Content = new Viewbox { Width = 16, Height = 16, Child = new SymbolIcon(icon) },
                    Width = 32, Height = 32, Padding = new Thickness(0), Style = (Style)Application.Current.Resources["ShellIconButtonStyle"] };
                ToolTipService.SetToolTip(button, label);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label);
                return button;
            }
            var previous = Command(global::Tomatotodo_Windows.Data.UiText.T("\u4E0A\u4E00\u9996"), Symbol.Previous);
            _mediaPlayButton = Command(global::Tomatotodo_Windows.Data.UiText.T("\u64AD\u653E"), Symbol.Play);
            _mediaPlayButton.Style = (Style)Application.Current.Resources["TomatotodoAccentButtonStyle"];
            var next = Command(global::Tomatotodo_Windows.Data.UiText.T("\u4E0B\u4E00\u9996"), Symbol.Next);
            var playOrder = Command(global::Tomatotodo_Windows.Data.UiText.T("\u987A\u5E8F\u64AD\u653E"), Symbol.List);
            void UpdatePlayOrder()
            {
                var label = _musicRepeatOne ? global::Tomatotodo_Windows.Data.UiText.T("\u5355\u66F2\u5FAA\u73AF") : _musicShuffle ? global::Tomatotodo_Windows.Data.UiText.T("\u968F\u673A\u64AD\u653E") : global::Tomatotodo_Windows.Data.UiText.T("\u987A\u5E8F\u64AD\u653E");
                var icon = _musicRepeatOne ? Symbol.RepeatOne : _musicShuffle ? Symbol.Shuffle : Symbol.List;
                playOrder.Content = new Viewbox { Width = 16, Height = 16, Child = new SymbolIcon(icon) };
                ToolTipService.SetToolTip(playOrder, label);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(playOrder, global::Tomatotodo_Windows.Data.UiText.F("{0}\uFF0C\u70B9\u51FB\u5207\u6362\u64AD\u653E\u6A21\u5F0F", label));
            }
            UpdatePlayOrder();
            playOrder.Click += (_, _) =>
            {
                if (_musicRepeatOne) { _musicRepeatOne = false; _musicShuffle = false; }
                else if (_musicShuffle) { _musicRepeatOne = true; _musicShuffle = false; }
                else _musicShuffle = true;
                UpdatePlayOrder();
            };
            previous.Click += async (_, _) => await PlayMusicAtAsync(_musicIndex < 0 ? 0 : _musicIndex - 1);
            _mediaPlayButton.Click += async (_, _) =>
            {
                if (_localPlayer is null || _musicIndex < 0) await PlayMusicAtAsync(0);
                else if (_localPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing) _localPlayer.Pause();
                else _localPlayer.Play();
                UpdateMediaVisuals();
            };
            next.Click += async (_, _) => await PlayMusicAtAsync(NextMusicIndex());
            transport.Children.Add(previous); transport.Children.Add(_mediaPlayButton); transport.Children.Add(next);
            transport.Children.Add(playOrder);
            row.Children.Add(transport);
            var volume = new Slider { Minimum = 0, Maximum = 100, StepFrequency = 1, SmallChange = 1, LargeChange = 10, Value = _musicVolume * 100, MinWidth = 0,
                MaxWidth = 112, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(volume, global::Tomatotodo_Windows.Data.UiText.T("\u97F3\u91CF"));
            volume.ValueChanged += (_, args) => { _musicVolume = args.NewValue / 100; if (_localPlayer is not null) _localPlayer.Volume = _musicVolume; };
            ToolTipService.SetToolTip(volume, global::Tomatotodo_Windows.Data.UiText.T("\u97F3\u91CF"));
            var volumeGroup = new Grid { ColumnSpacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
            volumeGroup.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            volumeGroup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var mute = Command(global::Tomatotodo_Windows.Data.UiText.T("\u9759\u97F3"), Symbol.Volume);
            mute.Content = new SymbolIcon(_musicMuted ? Symbol.Mute : Symbol.Volume);
            mute.Click += (_, _) =>
            {
                _musicMuted = !_musicMuted;
                if (_localPlayer is not null) _localPlayer.IsMuted = _musicMuted;
                mute.Content = new SymbolIcon(_musicMuted ? Symbol.Mute : Symbol.Volume);
                ToolTipService.SetToolTip(mute, _musicMuted ? global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88\u9759\u97F3") : global::Tomatotodo_Windows.Data.UiText.T("\u9759\u97F3"));
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(mute, _musicMuted ? global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88\u9759\u97F3") : global::Tomatotodo_Windows.Data.UiText.T("\u9759\u97F3"));
            };
            volume.Width = 112;
            volumeGroup.Children.Add(mute); Grid.SetColumn(volume, 1); volumeGroup.Children.Add(volume);
            Grid.SetColumn(volumeGroup, 1); row.Children.Add(volumeGroup);
            Grid.SetRow(row, 1); controls.Children.Add(row);
            content.Children.Add(controls);
            var hovered = false;
            var focused = false;
            var touched = false;
            void Reveal()
            {
                var show = hovered || focused || touched;
                stack.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
                controls.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
            body.PointerEntered += (_, _) => { hovered = true; Reveal(); };
            body.PointerExited += (_, _) => { hovered = false; Reveal(); };
            body.PointerPressed += (_, args) => { if (args.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Touch) { touched = true; Reveal(); } };
            var focusEntry = new Button { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Opacity = 0 };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(focusEntry, global::Tomatotodo_Windows.Data.UiText.T("\u663E\u793A\u97F3\u4E50\u64AD\u653E\u63A7\u5236"));
            focusEntry.GotFocus += (_, _) => { focused = true; Reveal(); };
            content.Children.Insert(0, focusEntry);
            body.GotFocus += (_, _) => { focused = true; Reveal(); };
            body.LostFocus += (_, _) => DispatcherQueue.TryEnqueue(() =>
            {
                var element = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
                while (element is not null && !ReferenceEquals(element, body)) element = VisualTreeHelper.GetParent(element);
                focused = element is not null; Reveal();
            });
            row.SizeChanged += (_, _) =>
            {
                var compact = row.ActualWidth < 286;
                Grid.SetRow(volumeGroup, compact ? 1 : 0);
                Grid.SetColumn(volumeGroup, compact ? 0 : 1);
                Grid.SetColumnSpan(volumeGroup, compact ? 2 : 1);
            };
        }
        Grid.SetColumn(content, 1); body.Children.Add(content);
        UpdateMediaVisuals();
        if (_state.MusicMode == "system") _ = RefreshMediaAsync();
        return body;
    }

    private void UpdateMediaVisuals()
    {
        if (_mediaTitleText is null || _mediaDetailText is null || _mediaArtworkHost is null) return;
        var local = _state.MusicMode == "local";
        _mediaTitleText.Text = local ? _musicTitle : _systemTitle;
        _mediaDetailText.Text = local ? _musicDetail : _systemDetail;
        ToolTipService.SetToolTip(_mediaTitleText, _mediaTitleText.Text);
        ToolTipService.SetToolTip(_mediaDetailText, _mediaDetailText.Text);
        var art = local ? _musicArtwork : _systemArtwork;
        _mediaArtworkHost.Child = art is null
            ? DashboardIcon(WidgetGlyph("media"), 28)
            : new Image { Source = art, Stretch = Stretch.Uniform };
        if (_mediaPlayButton is not null)
        {
            var playing = _localPlayer?.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
            _mediaPlayButton.Content = new Viewbox { Width = 16, Height = 16, Child = new SymbolIcon(playing ? Symbol.Pause : Symbol.Play) };
            ToolTipService.SetToolTip(_mediaPlayButton, playing ? global::Tomatotodo_Windows.Data.UiText.T("\u6682\u505C") : global::Tomatotodo_Windows.Data.UiText.T("\u64AD\u653E"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_mediaPlayButton, playing ? global::Tomatotodo_Windows.Data.UiText.T("\u6682\u505C") : global::Tomatotodo_Windows.Data.UiText.T("\u64AD\u653E"));
        }
    }

    private void UpdateMusicProgress()
    {
        if (_mediaSeek is null) return;
        _updatingMediaSeek = true;
        try
        {
            var duration = _localPlayer?.PlaybackSession.NaturalDuration.TotalSeconds ?? 0;
            _mediaSeek.IsEnabled = duration > 0;
            _mediaSeek.Maximum = Math.Max(1, duration);
            if (!_mediaSeeking)
                _mediaSeek.Value = Math.Min(duration, _localPlayer?.PlaybackSession.Position.TotalSeconds ?? 0);
        }
        finally { _updatingMediaSeek = false; }
    }
}
