using KanbanCards.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KanbanCards.Data.Migrations;

[DbContext(typeof(KanbanCardsDbContext))]
[Migration("20261111140000_AddRemainingNoteFields")]
public partial class AddRemainingNoteFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<System.DateTime>("ArchivedAt", "Notes", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<int>("Priority", "Notes", type: "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<System.DateTime>("DueAt", "Notes", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<System.DateTime>("ReminderSentAt", "Notes", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<bool>("ReminderEnabled", "Notes", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("TagsJson", "Notes", type: "TEXT", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>("ChecklistJson", "Notes", type: "TEXT", nullable: false, defaultValue: "[]");
        migrationBuilder.CreateIndex("IX_Notes_DueAt", "Notes", "DueAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Notes_DueAt", "Notes");
        migrationBuilder.DropColumn("ArchivedAt", "Notes");
        migrationBuilder.DropColumn("Priority", "Notes");
        migrationBuilder.DropColumn("DueAt", "Notes");
        migrationBuilder.DropColumn("ReminderSentAt", "Notes");
        migrationBuilder.DropColumn("ReminderEnabled", "Notes");
        migrationBuilder.DropColumn("TagsJson", "Notes");
        migrationBuilder.DropColumn("ChecklistJson", "Notes");
    }
}
