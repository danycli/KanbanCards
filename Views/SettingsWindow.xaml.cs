using System.Windows;
using KanbanCards.ViewModels;

namespace KanbanCards.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
