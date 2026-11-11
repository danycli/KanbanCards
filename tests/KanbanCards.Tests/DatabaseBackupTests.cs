using KanbanCards.Services;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using Xunit;

namespace KanbanCards.Tests;

public sealed class DatabaseBackupTests
{
    [Fact]
    public void RestoreLatestBackup_RunsUpgradeCallbackAfterRestoring()
    {
        WithDatabase((databasePath, directory) =>
        {
            CreateStateDatabase(databasePath, "backup");
            DatabaseBackupService.CreatePreMigrationBackup(databasePath);
            SetDatabaseState(databasePath, "current");
            bool migrated = false;

            DatabaseBackupService.RestoreLatestBackup(databasePath, () => migrated = true);

            Assert.True(migrated);
            Assert.Equal("backup", ReadDatabaseState(databasePath));
        });
    }

    [Fact]
    public void RestoreLatestBackup_RollsBackToSafetyCopyWhenUpgradeFails()
    {
        WithDatabase((databasePath, directory) =>
        {
            CreateStateDatabase(databasePath, "backup");
            DatabaseBackupService.CreatePreMigrationBackup(databasePath);
            SetDatabaseState(databasePath, "current");

            Assert.Throws<InvalidOperationException>(() =>
                DatabaseBackupService.RestoreLatestBackup(databasePath, () => throw new InvalidOperationException("migration failed")));

            Assert.Equal("current", ReadDatabaseState(databasePath));
        });
    }

    private static void WithDatabase(Action<string, string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "KanbanCardsBackupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(Path.Combine(directory, "kanbancards.db"), directory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void CreateStateDatabase(string path, string value)
    {
        using var connection = Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE State (Value TEXT NOT NULL); INSERT INTO State VALUES ($value);";
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static void SetDatabaseState(string path, string value)
    {
        using var connection = Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE State SET Value = $value;";
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static string ReadDatabaseState(string path)
    {
        using var connection = Open(path);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM State;";
        return (string)command.ExecuteScalar()!;
    }

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }
}
