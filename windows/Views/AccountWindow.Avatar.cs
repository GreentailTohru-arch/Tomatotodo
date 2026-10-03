using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Tomatotodo_Windows.Accounts;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Tomatotodo_Windows.Views;

public sealed partial class AccountWindow
{
    private async Task<AvatarSelection?> CropAvatarAsync(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var inputStream = input.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(inputStream);
        if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0 ||
            (ulong)decoder.PixelWidth * decoder.PixelHeight > 80_000_000)
            throw new ArgumentException(global::Tomatotodo_Windows.Data.UiText.T("\u56FE\u7247\u5C3A\u5BF8\u8FC7\u5927\uFF0C\u8BF7\u5148\u7F29\u5C0F\u56FE\u7247\u3002"));
        // Normalize EXIF and bound memory before any UI coordinate calculations.
        var decodeScale = Math.Min(1d, 2048d / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var decodedWidth = (uint)Math.Max(1, Math.Round(decoder.PixelWidth * decodeScale));
        var decodedHeight = (uint)Math.Max(1, Math.Round(decoder.PixelHeight * decodeScale));
        var data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform { ScaledWidth = decodedWidth, ScaledHeight = decodedHeight, InterpolationMode = BitmapInterpolationMode.Fant },
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
        var swapsAxes = decoder.OrientedPixelWidth != decoder.PixelWidth;
        var originalPixels = data.DetachPixelData();
        var originalWidth = (int)(swapsAxes ? decodedHeight : decodedWidth);
        var originalHeight = (int)(swapsAxes ? decodedWidth : decodedHeight);
        var crop = new AvatarCrop(originalPixels, originalWidth, originalHeight);
        const double size = 280;
        var viewport = new Canvas { Width = size, Height = size,
            ManipulationMode = Microsoft.UI.Xaml.Input.ManipulationModes.None,
            Background = new SolidColorBrush(Microsoft.UI.Colors.White),
            Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, size, size) } };
        var image = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
        void RefreshBitmap()
        {
            var bitmap = new WriteableBitmap(crop.Width, crop.Height);
            using var buffer = bitmap.PixelBuffer.AsStream();
            buffer.Write(crop.Pixels);
            bitmap.Invalidate();
            image.Source = bitmap;
        }
        RefreshBitmap();
        viewport.Children.Add(image);
        // Keep guides outside the render target: only the chosen image is saved.
        var preview = new Grid { Width = size, Height = size };
        preview.Children.Add(viewport);
        var mask = new GeometryGroup { FillRule = FillRule.EvenOdd };
        mask.Children.Add(new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, size, size) });
        mask.Children.Add(new EllipseGeometry { Center = new Windows.Foundation.Point(size / 2, size / 2),
            RadiusX = size / 2 - 1, RadiusY = size / 2 - 1 });
        preview.Children.Add(new Microsoft.UI.Xaml.Shapes.Path { Data = mask,
            Fill = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(180, 0, 0, 0)), IsHitTestVisible = false });
        preview.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Margin = new Thickness(1),
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.White), StrokeThickness = 2, IsHitTestVisible = false });
        var framing = new ContentControl { Content = preview, IsTabStop = true,
            HorizontalAlignment = HorizontalAlignment.Center, UseSystemFocusVisuals = true };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(framing, global::Tomatotodo_Windows.Data.UiText.T("\u5934\u50CF\u5706\u5F62\u53D6\u666F\u533A\u57DF\uFF0C\u65B9\u5411\u952E\u79FB\u52A8\u56FE\u7247\uFF0C\u6309\u4F4F Shift \u7CBE\u7EC6\u8C03\u6574"));
        var panel = new StackPanel { Spacing = 16, MaxWidth = 360 };
        panel.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u5C06\u5934\u50CF\u79FB\u5165\u5706\u6846\u3002\u62D6\u52A8\u8C03\u6574\u4F4D\u7F6E\uFF0C\u7F29\u653E\u540E\u53EF\u7EE7\u7EED\u5FAE\u8C03\u3002"), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(framing);
        var zoom = new Slider { Minimum = 1, Maximum = 4, Value = 1, Header = global::Tomatotodo_Windows.Data.UiText.T("\u7F29\u653E") };
        panel.Children.Add(zoom);
        var zoomLabel = new TextBlock { Text = "100%", HorizontalAlignment = HorizontalAlignment.Right };
        panel.Children.Add(zoomLabel);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var rotate = new Button { Content = global::Tomatotodo_Windows.Data.UiText.T("\u65CB\u8F6C 90\u00B0"), MinHeight = 32 };
        var reset = new Button { Content = global::Tomatotodo_Windows.Data.UiText.T("\u91CD\u7F6E\u53D6\u666F"), MinHeight = 32 };
        actions.Children.Add(rotate); actions.Children.Add(reset); panel.Children.Add(actions);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        panel.Children.Add(error);
        Windows.Foundation.Point? start = null;
        void Update()
        {
            var scale = size / crop.Side;
            image.Width = crop.Width * scale; image.Height = crop.Height * scale;
            Canvas.SetLeft(image, -crop.Left * scale);
            Canvas.SetTop(image, -crop.Top * scale);
        }
        zoom.ValueChanged += (_, _) => { crop.SetZoom(zoom.Value); Update(); zoomLabel.Text = $"{zoom.Value * 100:0}%"; };
        framing.KeyDown += (_, e) =>
        {
            var fine = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            var step = (fine ? 1 : 8) * crop.Side / size;
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Left: crop.Move(-step, 0); break;
                case Windows.System.VirtualKey.Right: crop.Move(step, 0); break;
                case Windows.System.VirtualKey.Up: crop.Move(0, -step); break;
                case Windows.System.VirtualKey.Down: crop.Move(0, step); break;
                default: return;
            }
            Update(); e.Handled = true;
        };
        viewport.PointerWheelChanged += (_, e) =>
        {
            zoom.Value = Math.Clamp(zoom.Value + e.GetCurrentPoint(viewport).Properties.MouseWheelDelta / 120d * .1, 1, 4);
            e.Handled = true;
        };
        rotate.Click += (_, _) => { crop.Rotate(); RefreshBitmap(); Update(); };
        reset.Click += (_, _) => { crop = new AvatarCrop(originalPixels, originalWidth, originalHeight); zoom.Value = 1; RefreshBitmap(); Update(); };
        viewport.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(viewport).Properties.IsLeftButtonPressed &&
                e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse) return;
            framing.Focus(FocusState.Pointer);
            start = e.GetCurrentPoint(viewport).Position;
            viewport.CapturePointer(e.Pointer); e.Handled = true;
        };
        viewport.PointerMoved += (_, e) =>
        {
            if (start is not { } origin) return;
            var current = e.GetCurrentPoint(viewport).Position;
            crop.Move((current.X - origin.X) * crop.Side / size, (current.Y - origin.Y) * crop.Side / size);
            start = current; Update(); e.Handled = true;
        };
        viewport.PointerReleased += (_, _) => { start = null; viewport.ReleasePointerCaptures(); };
        viewport.PointerCanceled += (_, _) => { start = null; viewport.ReleasePointerCaptures(); };
        viewport.PointerCaptureLost += (_, _) => start = null;
        Update();
        panel.Children.Add(new TextBlock { Text = global::Tomatotodo_Windows.Data.UiText.T("\u4E5F\u53EF\u4F7F\u7528\u6EDA\u8F6E\u7F29\u653E\u3001\u65B9\u5411\u952E\u79FB\u52A8\uFF0C\u6309\u4F4F Shift \u7CBE\u7EC6\u8C03\u6574\u3002"),
            TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var dialog = new ContentDialog { Title = global::Tomatotodo_Windows.Data.UiText.T("\u7F16\u8F91\u5934\u50CF"), Content = new ScrollViewer { Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme,
            PrimaryButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u4F7F\u7528\u6B64\u5934\u50CF"), CloseButtonText = global::Tomatotodo_Windows.Data.UiText.T("\u53D6\u6D88"), DefaultButton = ContentDialogButton.Primary };
        byte[]? result = null;
        dialog.PrimaryButtonClick += async (_, e) =>
        {
            var deferral = e.GetDeferral();
            try
            {
                dialog.IsPrimaryButtonEnabled = false;
                framing.IsEnabled = zoom.IsEnabled = rotate.IsEnabled = reset.IsEnabled = false;
                var pixels = crop.Export();
                using var output = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                    256, 256, 96, 96, pixels);
                await encoder.FlushAsync();
                output.Seek(0); using var reader = new DataReader(output);
                await reader.LoadAsync((uint)output.Size);
                result = new byte[(int)output.Size]; reader.ReadBytes(result);
            }
            catch
            {
                e.Cancel = true;
                error.Text = global::Tomatotodo_Windows.Data.UiText.T("\u65E0\u6CD5\u751F\u6210\u5934\u50CF\uFF0C\u8BF7\u8C03\u6574\u53D6\u666F\u540E\u91CD\u8BD5\u3002\u539F\u5934\u50CF\u672A\u4FEE\u6539\u3002");
                error.Visibility = Visibility.Visible;
            }
            finally
            {
                dialog.IsPrimaryButtonEnabled = true;
                framing.IsEnabled = zoom.IsEnabled = rotate.IsEnabled = reset.IsEnabled = true;
                deferral.Complete();
            }
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary && result is not null
            ? new AvatarSelection(result, "avatar.png") : null;
    }
}
