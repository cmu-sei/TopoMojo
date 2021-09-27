using Microsoft.EntityFrameworkCore.Migrations;

namespace TopoMojo.Api.Data.Migrations.SqlServer.TopoMojoDb
{
    public partial class TemplateVariant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Variant",
                table: "Templates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Variant",
                table: "Gamespaces",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Variant",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "Variant",
                table: "Gamespaces");
        }
    }
}
