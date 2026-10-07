using Microsoft.Win32;
using System.Reflection;
using System.Diagnostics;

namespace KanbanCards.Services;

public interface IStartupService
{
    void SetStartup(bool startWithWindows);
}

public class StartupService : IStartupService
{
    public void SetStartup(bool startWithWindows)
    {
        RegistryKey? rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        if (rk == null) return;
        
        string appName = "KanbanCards";
        
        if (startWithWindows)
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            rk.SetValue(appName, $"\"{exePath}\"");
        }
        else
        {
            rk.DeleteValue(appName, false);
        }
    }
}
