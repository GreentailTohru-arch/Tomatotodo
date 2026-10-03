using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace Tomatotodo_Windows.Views;

// Fixed logical design, scaled uniformly by its enclosing Viewbox.
internal sealed class FlipClockDigit : Grid
{
    private string _value = "";
    private Storyboard? _animation;
    public FlipClockDigit() { Width = 300; Height = 420; }

    private Grid Half(string text, bool bottom)
    {
        var face = new Grid { Width = 300, Height = 420, Background = new SolidColorBrush(ColorHelper.FromArgb(255, 22, 23, 22)) };
        face.Children.Add(new TextBlock { Text = text, FontSize = 228, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontFamily = new FontFamily("Segoe UI Variable Display"), Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 224, 225, 223)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        var half = new Grid { Width = 300, Height = 210, VerticalAlignment = bottom ? VerticalAlignment.Bottom : VerticalAlignment.Top,
            Clip = new RectangleGeometry { Rect = new Rect(0, 0, 300, 210) } };
        face.VerticalAlignment = VerticalAlignment.Top;
        if (bottom) face.RenderTransform = new TranslateTransform { Y = -210 };
        half.Children.Add(face);
        return half;
    }

    public void SetValue(string value)
    {
        if (_value == value) return;
        _animation?.Stop();
        var old = _value; _value = value; Children.Clear();
        Children.Add(Half(value, false)); Children.Add(Half(value, true));
        if (old.Length > 0 && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            var top = Half(old, false); var bottom = Half(value, true);
            top.Projection = new PlaneProjection { CenterOfRotationY = 1 };
            bottom.Projection = new PlaneProjection { CenterOfRotationY = 0, RotationX = 90 };
            Children.Add(top); Children.Add(bottom);
            var storyboard = new Storyboard();
            void Turn(Grid target, double from, double to, int delay)
            {
                var animation = new DoubleAnimation { From = from, To = to, Duration = TimeSpan.FromMilliseconds(170), BeginTime = TimeSpan.FromMilliseconds(delay) };
                Storyboard.SetTarget(animation, target.Projection); Storyboard.SetTargetProperty(animation, "RotationX");
                storyboard.Children.Add(animation);
            }
            Turn(top, 0, -90, 0); Turn(bottom, 90, 0, 170);
            storyboard.Completed += (_, _) => { Children.Remove(top); Children.Remove(bottom); };
            _animation = storyboard; storyboard.Begin();
        }
        Children.Add(new Border { Height = 5, Background = new SolidColorBrush(Colors.Black), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
    }
}
