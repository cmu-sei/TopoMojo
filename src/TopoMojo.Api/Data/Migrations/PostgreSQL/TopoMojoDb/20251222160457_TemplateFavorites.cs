using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TopoMojo.Api.Data.Migrations.PostgreSQL.TopoMojoDb
{
    /// <inheritdoc />
    public partial class TemplateFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TemplateFavorites",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    TemplateId = table.Column<string>(type: "text", nullable: true),
                    WhenCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateFavorites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFavorites_UserId_TemplateId",
                table: "TemplateFavorites",
                columns: new[] { "UserId", "TemplateId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateFavorites");
        }
    }
}
