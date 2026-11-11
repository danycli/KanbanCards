using KanbanCards.Data;
using KanbanCards.Models;
using KanbanCards.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace KanbanCards.Tests;

public sealed class DataIntegrityTests
{
    [Fact]
    public Task ImportNotesAsync_RollsBackTheWholeBatchWhenOneNoteIsInvalid() =>
        WithDatabaseAsync(async factory =>
        {
            using (var context = factory.CreateDbContext())
                context.Database.EnsureCreated();

            var service = new NoteService(factory);
            var batch = new[]
            {
                new Note { Title = "Valid card" },
                new Note { Title = null! }
            };

            await Assert.ThrowsAsync<DbUpdateException>(() => service.ImportNotesAsync(batch));

            using var verify = factory.CreateDbContext();
            Assert.Empty(await verify.Notes.ToListAsync());
        });

    [Fact]
    public void Migrate_UpgradesLegacyNotesAndAddsReminderSnoozeColumn()
    {
        WithDatabase(factory =>
        {
            using (var connection = new SqliteConnection(factory.ConnectionString))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL);
                    INSERT INTO "__EFMigrationsHistory" VALUES ('20260919144741_InitialCreate', '8.0.8');
                    CREATE TABLE "Notes" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK_Notes" PRIMARY KEY AUTOINCREMENT,
                        "Title" TEXT NOT NULL, "Description" TEXT NOT NULL, "Color" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL, "UpdatedAt" TEXT NOT NULL);
                    INSERT INTO "Notes" ("Title", "Description", "Color", "CreatedAt", "UpdatedAt")
                    VALUES ('Legacy card', 'Keep me', '#FFF2CC', '2026-11-01 12:00:00', '2026-11-01 12:00:00');
                    """;
                command.ExecuteNonQuery();
            }

            using var context = factory.CreateDbContext();
            context.Database.Migrate();
            var note = Assert.Single(context.Notes.AsNoTracking().ToList());
            Assert.Equal("Legacy card", note.Title);
            Assert.Equal(NoteStatus.Inbox, note.Status);
            Assert.Equal("[]", note.TagsJson);
            Assert.Null(note.ReminderSnoozedUntil);
        });
    }

    private static void WithDatabase(Action<TestFactory> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "KanbanCardsIntegrityTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var factory = new TestFactory(Path.Combine(directory, "kanbancards.db"));
        try { action(factory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static async Task WithDatabaseAsync(Func<TestFactory, Task> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "KanbanCardsIntegrityTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var factory = new TestFactory(Path.Combine(directory, "kanbancards.db"));
        try { await action(factory); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class TestFactory : IDbContextFactory<KanbanCardsDbContext>
    {
        private readonly DbContextOptions<KanbanCardsDbContext> _options;
        public string ConnectionString { get; }

        public TestFactory(string path)
        {
            ConnectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
            _options = new DbContextOptionsBuilder<KanbanCardsDbContext>().UseSqlite(ConnectionString).Options;
        }

        public KanbanCardsDbContext CreateDbContext() => new(_options);
    }
}
