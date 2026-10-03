using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Tomatotodo_Windows;

public sealed partial class MainPage
{
    private static void NormalizeProgressBarLayers(ProgressBar progress)
    {
        progress.BorderThickness = new Thickness(0);
        progress.UseLayoutRounding = true;
        // Keep the native ProgressBar's value handling and accessibility, but
        // remove template strokes that can read as a second overlapping line.
        progress.Loaded += (_, _) => RemoveProgressStrokes(progress);
    }

    private static void RemoveProgressStrokes(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is Border border) border.BorderThickness = new Thickness(0);
            if (child is Rectangle rectangle) rectangle.StrokeThickness = 0;
            RemoveProgressStrokes(child);
        }
    }
}
