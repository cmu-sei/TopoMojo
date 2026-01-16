using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TopoMojo.Api.Data.Migrations.SqlServer.TopoMojoDb
{
    /// <inheritdoc />
    public partial class _ : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GamespaceFavorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    GamespaceId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamespaceFavorites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GamespaceFavorites_UserId_GamespaceId",
                table: "GamespaceFavorites",
                columns: new[] { "UserId", "GamespaceId" },
                unique: true,
                filter: "[UserId] IS NOT NULL AND [GamespaceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GamespaceFavorites");
        }
    }
}
