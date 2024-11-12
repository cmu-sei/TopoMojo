// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace TopoMojo.Api.Data.Migrations.PostgreSQL.TopoMojoDb
{
    public partial class AddHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeen",
                table: "Workers",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEvergreen",
                table: "Topologies",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLaunch",
                table: "Topologies",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "LaunchCount",
                table: "Topologies",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhenPublished",
                table: "Topologies",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "Profiles",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeen",
                table: "Players",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "History",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    ActorId = table.Column<int>(nullable: false),
                    AssetId = table.Column<int>(nullable: false),
                    Action = table.Column<int>(nullable: false),
                    At = table.Column<DateTime>(nullable: false),
                    Actor = table.Column<string>(nullable: true),
                    Asset = table.Column<string>(nullable: true),
                    Annotation = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_History", x => x.Id);
                });

            migrationBuilder.Sql("UPDATE \"Profiles\" set \"Role\"=3 WHERE \"IsAdmin\"=true");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "History");

            migrationBuilder.DropColumn(
                name: "LastSeen",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "IsEvergreen",
                table: "Topologies");

            migrationBuilder.DropColumn(
                name: "LastLaunch",
                table: "Topologies");

            migrationBuilder.DropColumn(
                name: "LaunchCount",
                table: "Topologies");

            migrationBuilder.DropColumn(
                name: "WhenPublished",
                table: "Topologies");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "LastSeen",
                table: "Players");
        }
    }
}
