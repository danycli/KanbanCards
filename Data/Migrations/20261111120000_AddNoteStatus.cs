using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KanbanCards.Data.Migrations;

public partial class AddNoteStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Status",
            table: "Notes",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Status", table: "Notes");
    }
}
