using System.Windows;
using System.Windows.Input;
using KanbanCards.ViewModels;

namespace KanbanCards.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel) : this(viewModel, true)
    {
    }

    internal SettingsWindow(SettingsViewModel viewModel, bool initializeComponent)
    {
        if (initializeComponent)
        {
            InitializeComponent();
        }
        DataContext = viewModel;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
