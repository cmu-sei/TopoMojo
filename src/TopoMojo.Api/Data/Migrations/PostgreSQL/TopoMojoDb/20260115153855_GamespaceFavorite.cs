using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TopoMojo.Api.Data.Migrations.PostgreSQL.TopoMojoDb
{
    /// <inheritdoc />
    public partial class GamespaceFavorite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "GamespaceFavorites",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WhenCreated",
                table: "GamespaceFavorites",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WhenCreated",
                table: "GamespaceFavorites");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "GamespaceFavorites",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
