using Microsoft.EntityFrameworkCore.Migrations;

namespace TopoMojo.Api.Data.Migrations.SqlServer.TopoMojoDb
{
    public partial class challenge : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Challenge",
                table: "Workspaces",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Replicas",
                table: "Templates",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Challenge",
                table: "Gamespaces",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Challenge",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "Replicas",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "Challenge",
                table: "Gamespaces");
        }
    }
}
