using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Tomatotodo_Windows.Accounts;
using Windows.Storage.Pickers;

namespace Tomatotodo_Windows.Views;

public sealed partial class AccountWindow : Window, IAccountInteractionService
{
    public Visibility Show(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public AccountViewModel ViewModel { get; }
    private bool _canClose, _closing, _formatting;

    public AccountWindow(Func<IAccountInteractionService, AccountViewModel> createViewModel, ElementTheme theme)
    {
        ViewModel = createViewModel(this);
        InitializeComponent();
        ApplyXamlLanguage();
        Root.Language = Services.LanguagePreference.Resolved;
        Root.FlowDirection = Data.UiLanguage.IsRightToLeft(Root.Language) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        if (((App)Application.Current).MainWindowInstance is { } owner)
            FloatingWindowChrome.SetOwner(this, owner);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        { presenter.IsMaximizable = false; presenter.IsMinimizable = false; }
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(620, area.Width - 40);
        var height = Math.Min(860, area.Height - 60);
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(area.X + (area.Width - width) / 2,
            area.Y + (area.Height - height) / 2, width, height));
        ApplyTheme(theme);
        Activated += (_, _) => UpdateCapsLockWarning();
        Root.Loaded += async (_, _) => { await AnimateAsync(true); await ViewModel.InitializeAsync(); await RefreshAvatarAsync(); };
        AppWindow.Closing += async (_, args) =>
        {
            if (_canClose) return;
            args.Cancel = true;
            if (_closing || ViewModel.IsBusy) return;
            _closing = true;
            await AnimateAsync(false);
            _canClose = true; Close();
        };
        ViewModel.PropertyChanged += ViewModelChanged;
        Closed += (_, _) => { ViewModel.PropertyChanged -= ViewModelChanged; ViewModel.Dispose(); };
    }

    public void ApplyTheme(ElementTheme theme)
    {
        Root.RequestedTheme = theme;
        var dark = theme == ElementTheme.Dark;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
    }

    public void CloseWithOwner() { _canClose = true; Close(); }

    private async Task AnimateAsync(bool opening)
    {
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) return;
        var transform = new TranslateTransform(); Surface.RenderTransform = transform;
        var story = new Storyboard();
        var fade = new DoubleAnimation { From = opening ? 0 : 1, To = opening ? 1 : 0, Duration = new Duration(TimeSpan.FromMilliseconds(opening ? 200 : 140)) };
        Storyboard.SetTarget(fade, Surface); Storyboard.SetTargetProperty(fade, "Opacity"); story.Children.Add(fade);
        var move = new DoubleAnimation { From = opening ? 12 : 0, To = opening ? 0 : 8,
            Duration = fade.Duration, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(move, transform); Storyboard.SetTargetProperty(move, "Y"); story.Children.Add(move);
        var completion = new TaskCompletionSource();
        story.Completed += (_, _) => completion.TrySetResult(); story.Begin(); await completion.Task;
    }

    private void PasswordChanged(object sender, RoutedEventArgs e) => ViewModel.Password = PasswordInput.Password;
    private void PasswordFocusChanged(object sender, RoutedEventArgs e) => UpdateCapsLockWarning();
    private void PasswordFocusLost(object sender, RoutedEventArgs e) => CapsLockWarning.Visibility = Visibility.Collapsed;
    private void PasswordKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) => UpdateCapsLockWarning();
    private void UpdateCapsLockWarning()
    {
        var passwordFocused = PasswordInput.FocusState != FocusState.Unfocused || ConfirmInput.FocusState != FocusState.Unfocused || TargetPasswordInput.FocusState != FocusState.Unfocused;
        var locked = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.CapitalLock)
            & Windows.UI.Core.CoreVirtualKeyStates.Locked) != 0;
        CapsLockWarning.Visibility = passwordFocused && locked ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ConfirmChanged(object sender, RoutedEventArgs e) => ViewModel.ConfirmPassword = ConfirmInput.Password;
    private void TargetPasswordChanged(object sender, RoutedEventArgs e) => ViewModel.TargetPassword = TargetPasswordInput.Password;
    private void ActivationChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        if (_formatting) return;
        _formatting = true;
        ViewModel.ActivationCode = sender.Text;
        if (sender.Text != ViewModel.ActivationCode)
        { var position = sender.SelectionStart; sender.Text = ViewModel.ActivationCode; sender.SelectionStart = Math.Min(sender.Text.Length, position + 1); }
        _formatting = false;
    }

    private async void ViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        // Account switching may change the main presenter or open the mini timer.
        // Restore this user-initiated login window after that synchronous switch.
        if (args.PropertyName == nameof(ViewModel.IsSignedIn) && ViewModel.IsSignedIn)
            DispatcherQueue.TryEnqueue(() => { if (!_canClose) Activate(); });
        if (args.PropertyName == nameof(ViewModel.Avatar)) await RefreshAvatarAsync();
        if (args.PropertyName == nameof(ViewModel.Password) && PasswordInput.Password != ViewModel.Password) PasswordInput.Password = ViewModel.Password;
        if (args.PropertyName == nameof(ViewModel.ConfirmPassword) && ConfirmInput.Password != ViewModel.ConfirmPassword) ConfirmInput.Password = ViewModel.ConfirmPassword;
        if (args.PropertyName == nameof(ViewModel.TargetPassword) && TargetPasswordInput.Password != ViewModel.TargetPassword) TargetPasswordInput.Password = ViewModel.TargetPassword;
    }

    private async Task RefreshAvatarAsync()
    {
        if (ViewModel.Avatar is not { } bytes) { ProfilePicture.ProfilePicture = null; return; }
        try
        {
            using var memory = new MemoryStream(bytes);
            using var stream = memory.AsRandomAccessStream();
            var image = new BitmapImage();
            await image.SetSourceAsync(stream);
            if (!_canClose && ReferenceEquals(ViewModel.Avatar, bytes)) ProfilePicture.ProfilePicture = image;
        }
        catch { if (!_canClose) ProfilePicture.ProfilePicture = null; }
    }

    public async Task<AvatarSelection?> PickAvatarAsync()
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
        picker.FileTypeFilter.Add(".jpg"); picker.FileTypeFilter.Add(".jpeg"); picker.FileTypeFilter.Add(".png");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;
        var properties = await file.GetBasicPropertiesAsync();
        if (properties.Size > 20 * 1024 * 1024) throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u56FE\u7247\u4E0D\u80FD\u8D85\u8FC7 20 MB\u3002"));
        using var stream = await file.OpenStreamForReadAsync();
        using var memory = new MemoryStream(); await stream.CopyToAsync(memory);
        return await CropAvatarAsync(memory.ToArray());
    }

    public async Task<bool> ConfirmMigrationAsync(UserProfile source, UserProfile target)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme,
            Title = global::Tomatotodo_Windows.Data.UiText.T("\u8986\u76D6\u76EE\u6807\u8D26\u6237\u7684\u6570\u636E\uFF1F"),
            Content = global::Tomatotodo_Windows.Data.UiText.F("\u6765\u6E90\uFF1A{0}\n\u76EE\u6807\uFF1A{1}\n\n\u6B64\u64CD\u4F5C\u5C06\u8986\u76D6\u76EE\u6807\u8D26\u6237\u7684\u8BBE\u7F6E\u3001\u4E13\u6CE8\u6863\u6848\u3001\u4EFB\u52A1\u3001\u8BFE\u7A0B\u8868\u3001\u6635\u79F0\u548C\u7B80\u4ECB\uFF0C\u4E14\u4E0D\u53EF\u9006\u3002\u76EE\u6807\u90AE\u7BB1\u3001\u8D26\u6237\u7C7B\u578B\u3001\u6FC0\u6D3B\u7801\u548C\u5BC6\u7801\u4FDD\u6301\u4E0D\u53D8\u3002\u4E91\u7AEF\u5934\u50CF\u5B58\u50A8\u5C1A\u672A\u914D\u7F6E\uFF0C\u5934\u50CF\u4EC5\u4FDD\u5B58\u5728\u672C\u673A\u3002", source.DisplayLabel, target.DisplayLabel),
            PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u786E\u8BA4\u8986\u76D6"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), DefaultButton = ContentDialogButton.Close
        };
        MainPage.StyleDestructiveDialog(dialog);
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<bool> ConfirmCloudPullAsync(UserProfile account)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme,
            Title = global::Tomatotodo_Windows.Data.UiText.T("\u7528\u4E91\u7AEF\u6570\u636E\u8986\u76D6\u672C\u673A\u7F13\u5B58\uFF1F"),
            Content = global::Tomatotodo_Windows.Data.UiText.F("\u8D26\u6237\uFF1A{0}\n\n\u8FD9\u4F1A\u8986\u76D6\u8BE5\u8D26\u6237\u5C1A\u672A\u4E0A\u4F20\u7684\u672C\u673A\u8BBE\u7F6E\u3001\u4EFB\u52A1\u3001\u4E13\u6CE8\u6863\u6848\u3001\u8BFE\u7A0B\u8868\u548C\u8D44\u6599\u3002\u8BF7\u5148\u4E0A\u4F20\u9700\u8981\u4FDD\u7559\u7684\u4FEE\u6539\u3002", account.Email),
            PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u786E\u8BA4\u8986\u76D6"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), DefaultButton = ContentDialogButton.Close
        };
        MainPage.StyleDestructiveDialog(dialog);
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
