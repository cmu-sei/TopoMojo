using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TopoMojo.Api.Data.Migrations.PostgreSQL.TopoMojoDb
{
    /// <inheritdoc />
    public partial class AddUserServiceAccountClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServiceAccountClientId",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ServiceAccountClientId",
                table: "Users",
                column: "ServiceAccountClientId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_ServiceAccountClientId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ServiceAccountClientId",
                table: "Users");
        }
    }
}
