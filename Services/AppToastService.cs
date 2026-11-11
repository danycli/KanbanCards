using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace KanbanCards.Services;

/// <summary>Shows brief, non-blocking feedback using the application's card styling.</summary>
internal static class AppToastService
{
    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(31, 41, 55));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(100, 116, 139));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(99, 102, 241));

    public static void Show(string title, string message, bool isSuccess = true)
    {
        var window = new Window
        {
            Width = 360,
            SizeToContent = SizeToContent.Height,
            MinHeight = 88,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowActivated = false,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Title = "Kanban Cards"
        };

        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(15, 13, 16, 13),
            Margin = new Thickness(8)
        };
        card.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black, BlurRadius = 18, ShadowDepth = 3, Opacity = .16
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var badge = new Border
        {
            Width = 27,
            Height = 27,
            CornerRadius = new CornerRadius(9),
            Background = isSuccess ? new SolidColorBrush(Color.FromRgb(238, 242, 255)) : new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = isSuccess ? "✓" : "i",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = Accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };
        var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Ink,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        content.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 11,
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetColumn(content, 1);
        row.Children.Add(badge);
        row.Children.Add(content);
        card.Child = row;
        window.Content = card;

        window.Loaded += (_, _) =>
        {
            var workArea = SystemParameters.WorkArea;
            window.Left = Math.Max(workArea.Left, workArea.Right - window.ActualWidth - 18);
            window.Top = Math.Max(workArea.Top, workArea.Bottom - window.ActualHeight - 18);
        };
        window.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            window.Close();
        };
        timer.Start();
    }
}
