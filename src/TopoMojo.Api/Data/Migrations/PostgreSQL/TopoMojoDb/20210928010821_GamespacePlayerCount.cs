using Microsoft.EntityFrameworkCore.Migrations;

namespace TopoMojo.Api.Data.Migrations.PostgreSQL.TopoMojoDb
{
    public partial class GamespacePlayerCount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlayerCount",
                table: "Gamespaces",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayerCount",
                table: "Gamespaces");
        }
    }
}
