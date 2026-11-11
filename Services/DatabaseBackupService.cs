using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;

namespace KanbanCards.Services;

/// <summary>
/// Creates a consistent SQLite snapshot before the app applies schema migrations.
/// SQLite's backup API includes committed WAL data, unlike copying the .db file.
/// </summary>
public static class DatabaseBackupService
{
    private const int RetainedBackupCount = 5;

    public static string CreatePreMigrationBackup(string databasePath)
    {
        var fullDatabasePath = Path.GetFullPath(databasePath);
        var backupDirectory = Path.Combine(Path.GetDirectoryName(fullDatabasePath)!, "backups");
        Directory.CreateDirectory(backupDirectory);

        var backupPath = Path.Combine(
            backupDirectory,
            $"kanbancards-pre-migration-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.db");

        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = fullDatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };
        var destinationBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };

        using (var source = new SqliteConnection(sourceBuilder.ToString()))
        using (var destination = new SqliteConnection(destinationBuilder.ToString()))
        {
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
        }

        foreach (var oldBackup in Directory.GetFiles(backupDirectory, "kanbancards-pre-migration-*.db")
                     .Select(path => new FileInfo(path))
                     .OrderByDescending(file => file.CreationTimeUtc)
                     .Skip(RetainedBackupCount))
        {
            oldBackup.Delete();
        }

        return backupPath;
    }

    public static string RestoreLatestBackup(string databasePath, Action? afterRestore = null)
    {
        var fullDatabasePath = Path.GetFullPath(databasePath);
        var backupDirectory = Path.Combine(Path.GetDirectoryName(fullDatabasePath)!, "backups");
        var latest = Directory.Exists(backupDirectory)
            ? Directory.GetFiles(backupDirectory, "kanbancards-pre-migration-*.db").OrderByDescending(File.GetCreationTimeUtc).FirstOrDefault()
            : null;
        if (latest is null) throw new FileNotFoundException("No automatic database backup is available yet.");

        var safetyBackup = File.Exists(fullDatabasePath) ? CreatePreMigrationBackup(fullDatabasePath) : null;
        try
        {
            RestoreFrom(latest, fullDatabasePath);
            afterRestore?.Invoke();
        }
        catch
        {
            if (safetyBackup is not null)
                RestoreFrom(safetyBackup, fullDatabasePath);
            throw;
        }
        return latest;
    }

    private static void RestoreFrom(string sourcePath, string destinationPath)
    {
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destinationPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }
}
