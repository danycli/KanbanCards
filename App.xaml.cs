using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using KanbanCards.Data;
using KanbanCards.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System;
using System.Linq;

namespace KanbanCards;

public partial class App : Application
{
    public static bool IsShuttingDown { get; set; } = false;
    private IHost _host;
    private ISingleInstanceService? _singleInstanceService;
    private QuickCaptureHotkeyService? _quickCaptureHotkey;

    public App()
    {
        // Global exception safety for long-running stability
        this.DispatcherUnhandledException += (sender, args) =>
        {
            FullScreenMonitorService.LogAudit($"[UNHANDLED DISPATCHER EXCEPTION] {args.Exception}");
            args.Handled = true; // Prevent app crash
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            FullScreenMonitorService.LogAudit($"[UNHANDLED DOMAIN EXCEPTION] {args.ExceptionObject}");
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            FullScreenMonitorService.LogAudit($"[UNOBSERVED TASK EXCEPTION] {args.Exception}");
            args.SetObserved(); // Prevent app crash on background task failure
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContextFactory<KanbanCardsDbContext>();
                services.AddSingleton<INoteService, NoteService>();
                services.AddSingleton<IReminderService, ReminderService>();
                services.AddSingleton<QuickCaptureHotkeyService>();
                services.AddSingleton<IEditorService, EditorService>();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IStartupService, StartupService>();
                services.AddSingleton<ITrayService, TrayService>();
                services.AddSingleton<IFullScreenMonitorService, FullScreenMonitorService>();
                services.AddSingleton<ISingleInstanceService, SingleInstanceService>();
                services.AddSingleton<IUpdateService, UpdateService>();
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
        _singleInstanceService = new SingleInstanceService();
        if (!_singleInstanceService.TryAcquire())
        {
            FullScreenMonitorService.LogAudit("[SINGLE INSTANCE] Another instance of Kanban Cards is already running. Signaling window activation and exiting.");
            _singleInstanceService.SignalExistingInstance();
            _singleInstanceService.Dispose();
            _singleInstanceService = null;
            Shutdown();
            return;
        }

        try
        {
            await _host.StartAsync();

            _singleInstanceService.RegisterWakeupHandler(() =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        var trayService = _host.Services.GetService<ITrayService>();
                        trayService?.OpenMainWindow();
                    }
                    catch (Exception ex)
                    {
                        FullScreenMonitorService.LogAudit($"[SINGLE INSTANCE ERROR] Failed to activate window on wakeup: {ex.Message}");
                    }
                });
            });

            var contextFactory = _host.Services.GetRequiredService<IDbContextFactory<KanbanCardsDbContext>>();
            using (var dbContext = contextFactory.CreateDbContext())
            {
                var databaseAlreadyExisted = File.Exists(dbContext.DbPath);
                var hasPendingMigrations = dbContext.Database.GetPendingMigrations().Any();
                if (databaseAlreadyExisted && hasPendingMigrations)
                {
                    var backupPath = DatabaseBackupService.CreatePreMigrationBackup(dbContext.DbPath);
                    FullScreenMonitorService.LogAudit($"[DATABASE BACKUP] Pre-migration snapshot created at '{backupPath}'.");
                }
                dbContext.Database.Migrate();
                try
                {
                    dbContext.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
                    dbContext.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Notes_CreatedAt ON Notes(CreatedAt DESC);");
                }
                catch { }
            }

            var fullScreenMonitor = _host.Services.GetRequiredService<IFullScreenMonitorService>();
            fullScreenMonitor.Start();

            var trayService = _host.Services.GetRequiredService<ITrayService>();
            trayService.Initialize();
            _quickCaptureHotkey = _host.Services.GetRequiredService<QuickCaptureHotkeyService>();
            _quickCaptureHotkey.Start();

            var settingsService = _host.Services.GetRequiredService<ISettingsService>();
            var settings = settingsService.LoadSettings();
            _host.Services.GetRequiredService<IReminderService>().Start();

            var edgeStack = _host.Services.GetRequiredService<Views.EdgeStackWindow>();
            if (settings.ShowNotesOnDesktop && !fullScreenMonitor.IsFullScreenActive)
            {
                edgeStack.Show();
            }

            if (!settings.LaunchMinimizedToTray)
            {
                trayService.OpenMainWindow();
            }

            // GitHub Update System initialization
            var updateService = _host.Services.GetRequiredService<IUpdateService>();
            updateService.UpdateAvailable += (s, updateInfo) => ShowUpdateNotice(updateService, updateInfo);
            updateService.StartBackgroundChecks();
            
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[STARTUP EXCEPTION] {ex}");
            MessageBox.Show($"Startup failed: {ex.Message}\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        IsShuttingDown = true;
        _singleInstanceService?.Dispose();
        _singleInstanceService = null;
        _quickCaptureHotkey?.Dispose();
        _quickCaptureHotkey = null;

        var fullScreenMonitor = _host.Services.GetService<IFullScreenMonitorService>();
        fullScreenMonitor?.Stop();
        fullScreenMonitor?.Dispose();

        var updateService = _host.Services.GetService<IUpdateService>();
        updateService?.StopBackgroundChecks();
        if (updateService is IDisposable disposableUpdate)
        {
            disposableUpdate.Dispose();
        }
        _host.Services.GetService<IReminderService>()?.Stop();

        var trayService = _host.Services.GetRequiredService<ITrayService>();
        trayService.Shutdown();

        await _host.StopAsync();
        _host.Dispose();
        
        base.OnExit(e);
    }

    public static void ShowUpdateNotice(IUpdateService updateService, Models.UpdateInfo updateInfo)
    {
        var app = Current;
        if (app == null) return;

        FullScreenMonitorService.LogAudit($"[UPDATE NOTICE] ShowUpdateNotice called for v{updateInfo.Version}");

        app.Dispatcher.InvokeAsync(() =>
        {
            foreach (Window w in app.Windows)
            {
                if (w is Views.UpdateNoticeWindow notice)
                {
                    FullScreenMonitorService.LogAudit("[UPDATE NOTICE] UpdateNoticeWindow already open. Activating.");
                    notice.Activate();
                    return;
                }
            }

            FullScreenMonitorService.LogAudit($"[UPDATE NOTICE] Creating and showing UpdateNoticeWindow for v{updateInfo.Version}");
            var vm = new ViewModels.UpdateNoticeViewModel(updateService, updateInfo);
            var noticeWindow = new Views.UpdateNoticeWindow(vm);
            noticeWindow.Show();
        });
    }

    public static void ShowUpToDateNotice(string currentVersion, string? message = null)
    {
        var app = Current;
        if (app == null) return;

        FullScreenMonitorService.LogAudit($"[UPDATE NOTICE] ShowUpToDateNotice called: Current={currentVersion}, Msg={message}");

        app.Dispatcher.InvokeAsync(() =>
        {
            foreach (Window w in app.Windows)
            {
                if (w is Views.UpToDateNoticeWindow existing)
                {
                    existing.Activate();
                    return;
                }
            }

            var window = new Views.UpToDateNoticeWindow(currentVersion, message);
            window.Show();
        });
    }
}
