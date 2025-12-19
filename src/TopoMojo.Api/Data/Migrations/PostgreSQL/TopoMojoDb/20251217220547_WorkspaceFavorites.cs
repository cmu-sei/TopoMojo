using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TopoMojo.Api.Data.Migrations.PostgreSQL.TopoMojoDb
{
    /// <inheritdoc />
    public partial class WorkspaceFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceFavorites",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    WorkspaceId = table.Column<string>(type: "text", nullable: true),
                    WhenCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceFavorites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceFavorites_UserId_WorkspaceId",
                table: "WorkspaceFavorites",
                columns: new[] { "UserId", "WorkspaceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceFavorites");
        }
    }
}
