using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TopoMojo.Api.Data.Migrations.SqlServer.TopoMojoDb
{
    /// <inheritdoc />
    public partial class AddUserLastIdpAssignedRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastIdpAssignedRole",
                table: "Users",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastIdpAssignedRole",
                table: "Users");
        }
    }
}
