using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace KanbanCards.Converters;

public sealed class NoteColorToCalendarStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var noteColor = ParseColor(value as string);
        // Keep the popup on the same warm paper palette as the sticky note without
        // letting the note's saturated fill overpower the dates and navigation.
        var paper = Blend(noteColor, Colors.White, 0.78);
        var accent = Blend(noteColor, Colors.Black, 0.28);
        var hover = Blend(noteColor, Colors.White, 0.54);
        var outline = Blend(noteColor, Colors.Black, 0.16);
        var foreground = GetLuminance(accent) < 0.48 ? Brushes.White : new SolidColorBrush(Color.FromRgb(50, 45, 40));

        var dayButtonStyle = new Style(typeof(CalendarDayButton));
        dayButtonStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        dayButtonStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        dayButtonStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(54, 48, 43))));
        dayButtonStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3)));
        dayButtonStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(1)));
        dayButtonStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 30d));
        dayButtonStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 28d));
        dayButtonStyle.Setters.Add(new Setter(Control.TemplateProperty, CreateDayTemplate()));

        var todayTrigger = new Trigger { Property = CalendarDayButton.IsTodayProperty, Value = true };
        todayTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(accent)));
        todayTrigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        dayButtonStyle.Triggers.Add(todayTrigger);

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(hover)));
        hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(outline)));
        dayButtonStyle.Triggers.Add(hoverTrigger);

        var selectedTrigger = new Trigger { Property = CalendarDayButton.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(accent)));
        selectedTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(accent)));
        selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
        dayButtonStyle.Triggers.Add(selectedTrigger);

        var inactiveTrigger = new Trigger { Property = CalendarDayButton.IsInactiveProperty, Value = true };
        inactiveTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(145, 137, 128))));
        dayButtonStyle.Triggers.Add(inactiveTrigger);

        var calendarButtonStyle = new Style(typeof(CalendarButton));
        calendarButtonStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        calendarButtonStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        calendarButtonStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(54, 48, 43))));
        calendarButtonStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        calendarButtonStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 30d));
        calendarButtonStyle.Setters.Add(new Setter(Control.TemplateProperty, CreateCalendarButtonTemplate()));
        var navHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        navHover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(hover)));
        calendarButtonStyle.Triggers.Add(navHover);

        var calendarStyle = new Style(typeof(System.Windows.Controls.Calendar));
        calendarStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(paper)));
        calendarStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(54, 48, 43))));
        calendarStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(outline)));
        calendarStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        calendarStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
        calendarStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12d));
        calendarStyle.Resources[typeof(CalendarDayButton)] = dayButtonStyle;
        calendarStyle.Resources[typeof(CalendarButton)] = calendarButtonStyle;
        return calendarStyle;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static Color ParseColor(string? value)
    {
        try
        {
            if (ColorConverter.ConvertFromString(value ?? string.Empty) is Color color)
                return color;
        }
        catch (FormatException) { }

        return Color.FromRgb(144, 202, 249);
    }

    private static Color Blend(Color foreground, Color background, double amount) => Color.FromRgb(
        (byte)Math.Round(foreground.R * (1 - amount) + background.R * amount),
        (byte)Math.Round(foreground.G * (1 - amount) + background.G * amount),
        (byte)Math.Round(foreground.B * (1 - amount) + background.B * amount));

    private static double GetLuminance(Color color) =>
        (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;

    private static ControlTemplate CreateDayTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "DaySurface";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(TextElement.ForegroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
        border.AppendChild(content);
        return new ControlTemplate(typeof(CalendarDayButton)) { VisualTree = border };
    }

    private static ControlTemplate CreateCalendarButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "ButtonSurface";
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(TextElement.ForegroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
        border.AppendChild(content);
        return new ControlTemplate(typeof(CalendarButton)) { VisualTree = border };
    }
}
