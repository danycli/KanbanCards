using System;
using System.Windows;
using KanbanCards.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KanbanCards;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        DataContext = viewModel;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            this.DragMove();
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        ModalDimmer.Visibility = Visibility.Visible;
        UpdateLayout();
        try
        {
            var settingsWindow = _serviceProvider.GetRequiredService<Views.SettingsWindow>();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
        finally
        {
            ModalDimmer.Visibility = Visibility.Collapsed;
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}