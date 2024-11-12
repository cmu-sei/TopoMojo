// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace TopoMojo.Api.Data.Migrations.SqlServer.TopoMojoDb
{
    public partial class ChallengeField : Migration
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
