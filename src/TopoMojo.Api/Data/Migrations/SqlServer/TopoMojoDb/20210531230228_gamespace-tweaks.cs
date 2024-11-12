// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TopoMojo.Api.Data.Migrations.SqlServer.TopoMojoDb
{
    public partial class GamespaceTweaks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Users_PersonId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_PersonId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PersonId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "Gamespaces");

            migrationBuilder.DropColumn(
                name: "LastActivity",
                table: "Gamespaces");

            migrationBuilder.AddColumn<string>(
                name: "SubjectId",
                table: "Players",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectName",
                table: "Players",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Players",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceId",
                table: "Players",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowReset",
                table: "Gamespaces",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "Gamespaces",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationTime",
                table: "Gamespaces",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "StartTime",
                table: "Gamespaces",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "StopTime",
                table: "Gamespaces",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Players_UserId",
                table: "Players",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_SubjectId_WorkspaceId",
                table: "Players",
                columns: new[] { "SubjectId", "WorkspaceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Users_UserId",
                table: "Players",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Users_UserId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_UserId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_SubjectId_WorkspaceId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "SubjectName",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "AllowReset",
                table: "Gamespaces");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Gamespaces");

            migrationBuilder.DropColumn(
                name: "ExpirationTime",
                table: "Gamespaces");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Gamespaces");

            migrationBuilder.DropColumn(
                name: "StopTime",
                table: "Gamespaces");

            migrationBuilder.AddColumn<int>(
                name: "PersonId",
                table: "Players",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Audience",
                table: "Gamespaces",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivity",
                table: "Gamespaces",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_Players_PersonId",
                table: "Players",
                column: "PersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Users_PersonId",
                table: "Players",
                column: "PersonId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
