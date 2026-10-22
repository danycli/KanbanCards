using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using KanbanCards.Data;
using KanbanCards.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System;

namespace KanbanCards;

public partial class App : Application
{
    private IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<KanbanCardsDbContext>();
                services.AddScoped<INoteService, NoteService>();
                services.AddSingleton<IEditorService, EditorService>();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IStartupService, StartupService>();
                services.AddSingleton<ITrayService, TrayService>();
                services.AddTransient<ViewModels.MainViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddTransient<ViewModels.EdgeStackViewModel>();
                services.AddSingleton<Views.EdgeStackWindow>();
                services.AddTransient<ViewModels.SettingsViewModel>();
                services.AddTransient<Views.SettingsWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            await _host.StartAsync();

            using (var scope = _host.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<KanbanCardsDbContext>();
                dbContext.Database.Migrate();
            }

            var trayService = _host.Services.GetRequiredService<ITrayService>();
            trayService.Initialize();

            var settingsService = _host.Services.GetRequiredService<ISettingsService>();
            var settings = settingsService.LoadSettings();

            if (settings.ShowNotesOnDesktop)
            {
                var edgeStack = _host.Services.GetRequiredService<Views.EdgeStackWindow>();
                edgeStack.Show();
            }
            
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup failed: {ex.Message}\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var trayService = _host.Services.GetRequiredService<ITrayService>();
        trayService.Shutdown();

        await _host.StopAsync();
        _host.Dispose();
        
        base.OnExit(e);
    }
}
