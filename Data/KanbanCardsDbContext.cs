using KanbanCards.Models;
using Microsoft.EntityFrameworkCore;
using KanbanCards.Services;
using System;
using System.IO;

namespace KanbanCards.Data;

public class KanbanCardsDbContext : DbContext
{
    public DbSet<Note> Notes { get; set; } = null!;

    public string DbPath { get; }

    public KanbanCardsDbContext()
    {
        DbPath = Path.Join(ApplicationDataPaths.DataDirectory, "kanbancards.db");
    }
    
    public KanbanCardsDbContext(DbContextOptions<KanbanCardsDbContext> options) : base(options)
    {
        DbPath = Path.Join(ApplicationDataPaths.DataDirectory, "kanbancards.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = DbPath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
                DefaultTimeout = 5 // 5 second busy timeout to prevent "database is locked" errors
            }.ToString();

            options.UseSqlite(connectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Note>(entity =>
        {
            entity.Property(n => n.Status).HasDefaultValue(NoteStatus.Inbox);
            entity.Property(n => n.SortOrder).HasDefaultValue(0);
            entity.Property(n => n.IsPinned).HasDefaultValue(false);
            entity.Property(n => n.Priority).HasDefaultValue(0);
            entity.Property(n => n.TagsJson).HasDefaultValue("[]");
            entity.Property(n => n.ChecklistJson).HasDefaultValue("[]");
            entity.Property(n => n.ReminderEnabled).HasDefaultValue(false);
            entity.HasIndex(n => n.CreatedAt)
                  .HasDatabaseName("IX_Notes_CreatedAt");
            entity.HasIndex(n => n.DueAt).HasDatabaseName("IX_Notes_DueAt");
        });
    }
}
