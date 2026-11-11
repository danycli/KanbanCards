using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KanbanCards.Data.Migrations;

public partial class AddManualCardOrder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsPinned",
            table: "Notes",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "SortOrder",
            table: "Notes",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsPinned", table: "Notes");
        migrationBuilder.DropColumn(name: "SortOrder", table: "Notes");
    }
}
