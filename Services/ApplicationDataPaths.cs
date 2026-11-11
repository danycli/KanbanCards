using System;
using System.IO;

namespace KanbanCards.Services;

public static class ApplicationDataPaths
{
    public static string DataDirectory
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable("KANBANCARDS_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(overridePath))
                return Path.GetFullPath(overridePath);

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KanbanCards");
        }
    }
}
