using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domus.Infrastructure.Migrations
{
    public partial class RemoveShowInRanking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowInRanking",
                table: "Participants");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowInRanking",
                table: "Participants",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }
    }
}
