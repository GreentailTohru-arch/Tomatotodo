using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private void InitializeNavigationMotion()
    {
        AttachNavigationMotion(LocalizedElement4, ShellSettingsGlyph, true);
        AttachNavigationMotion(ShellBackButton, ShellBackGlyph, false);
    }

    private void AttachNavigationMotion(UIElement target, FrameworkElement glyph, bool settings)
    {
        var transform = new CompositeTransform();
        glyph.RenderTransform = transform;
        glyph.RenderTransformOrigin = new Windows.Foundation.Point(.5, .5);
        Storyboard? active = null;
        void Play()
        {
            active?.Stop();
            transform.Rotation = transform.TranslateX = 0;
            transform.ScaleX = transform.ScaleY = 1;
            if (!_systemUiSettings.AnimationsEnabled) return;
            var storyboard = new Storyboard();
            void Track(string property, double rest, double pressed)
            {
                var animation = new DoubleAnimationUsingKeyFrames();
                animation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = rest });
                animation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(100), Value = pressed,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                animation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(360), Value = rest,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                Storyboard.SetTarget(animation, transform);
                Storyboard.SetTargetProperty(animation, property);
                storyboard.Children.Add(animation);
            }
            Track(settings ? "Rotation" : "TranslateX", 0, settings ? 40 : -4);
            if (settings) { Track("ScaleX", 1, .9); Track("ScaleY", 1, .9); }
            active = storyboard;
            storyboard.Begin();
        }
        // handledEventsToo preserves feedback when native control templates
        // consume pointer events; navigation itself remains immediate.
        target.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (e.GetCurrentPoint(target).Properties.IsLeftButtonPressed) Play();
        }), true);
        target.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space) Play();
        }), true);
        glyph.Unloaded += (_, _) => active?.Stop();
    }
}
