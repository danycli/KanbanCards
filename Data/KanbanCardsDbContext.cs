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
            options.UseSqlite($"Data Source={DbPath}");
        }
    }
}
