using KanbanCards.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KanbanCards.Data.Migrations;

[DbContext(typeof(KanbanCardsDbContext))]
[Migration("20261111150000_AddReminderSnooze")]
public sealed class AddReminderSnooze : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<System.DateTime>(
            name: "ReminderSnoozedUntil",
            table: "Notes",
            type: "TEXT",
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "ReminderSnoozedUntil", table: "Notes");
}
