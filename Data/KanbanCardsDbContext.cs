using KanbanCards.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace KanbanCards.Data;

public class KanbanCardsDbContext : DbContext
{
    public DbSet<Note> Notes { get; set; } = null!;

    public string DbPath { get; }

    public KanbanCardsDbContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = Path.Join(path, "KanbanCards", "kanbancards.db");
    }
    
    public KanbanCardsDbContext(DbContextOptions<KanbanCardsDbContext> options) : base(options)
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        DbPath = Path.Join(path, "KanbanCards", "kanbancards.db");
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
            entity.HasIndex(n => n.CreatedAt)
                  .HasDatabaseName("IX_Notes_CreatedAt");
        });
    }
}
