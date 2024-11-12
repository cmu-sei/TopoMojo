// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace TopoMojo.Api.Data.Migrations.PostgreSQL.TopoMojoDb
{
    public partial class PopulateGlobalId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Gamespaces_GamespaceId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Users_UserId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Users_PersonId",
                table: "Workers");

            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Workspaces_WorkspaceId",
                table: "Workers");

            migrationBuilder.DropTable(
                name: "History");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Workers_PersonId",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Players_SubjectId_WorkspaceId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_UserId",
                table: "Players");


            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Players");

            migrationBuilder.AlterColumn<int>(
                name: "WorkspaceId",
                table: "Workers",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "SubjectId",
                table: "Workers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectName",
                table: "Workers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceGlobalId",
                table: "Workers",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallbackUrl",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GamespaceLimit",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GamespaceMaxMinutes",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionLimit",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ParentGlobalId",
                table: "Templates",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceGlobalId",
                table: "Templates",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "GamespaceId",
                table: "Players",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "GamespaceGlobalId",
                table: "Players",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceGlobalId",
                table: "Gamespaces",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApiKey",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character(36)", fixedLength: true, maxLength: 36, nullable: false),
                    UserId = table.Column<string>(type: "character(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKey_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "GlobalId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKey_UserId",
                table: "ApiKey",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Gamespaces_GamespaceId",
                table: "Players",
                column: "GamespaceId",
                principalTable: "Gamespaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Workers_Workspaces_WorkspaceId",
                table: "Workers",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // set data for subsequent migration which will change PK
            migrationBuilder.Sql(@"
                UPDATE ""Workers"" w SET ""WorkspaceGlobalId"" = a.""GlobalId"" FROM ""Workspaces"" a WHERE w.""WorkspaceId"" = a.""Id"";
                UPDATE ""Gamespaces"" w SET ""WorkspaceGlobalId"" = a.""GlobalId"" FROM ""Workspaces"" a WHERE w.""WorkspaceId"" = a.""Id"";
                UPDATE ""Players"" w SET ""GamespaceGlobalId"" = a.""GlobalId"" FROM ""Gamespaces"" a WHERE w.""GamespaceId"" = a.""Id"";
                UPDATE ""Templates"" w SET ""ParentGlobalId"" = a.""GlobalId"" FROM ""Templates"" a WHERE w.""ParentId"" = a.""Id"";
                UPDATE ""Templates"" w SET ""WorkspaceGlobalId"" = a.""GlobalId"" FROM ""Workspaces"" a WHERE w.""WorkspaceId"" = a.""Id"";
                UPDATE ""Workers"" w SET ""SubjectId"" = a.""GlobalId"", ""SubjectName"" = a.""Name"" FROM ""Users"" a WHERE w.""PersonId"" = a.""Id"";
                UPDATE ""Players"" w SET ""SubjectId"" = a.""GlobalId"", ""SubjectName"" = a.""Name"" FROM ""Users"" a WHERE w.""UserId"" = a.""Id"";
            ");

            migrationBuilder.DropColumn(
                name: "PersonId",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Players");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Gamespaces_GamespaceId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Workspaces_WorkspaceId",
                table: "Workers");

            migrationBuilder.DropTable(
                name: "ApiKey");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "SubjectName",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "WorkspaceGlobalId",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "CallbackUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GamespaceLimit",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GamespaceMaxMinutes",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SessionLimit",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ParentGlobalId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "WorkspaceGlobalId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "GamespaceGlobalId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "WorkspaceGlobalId",
                table: "Gamespaces");

            migrationBuilder.AlterColumn<int>(
                name: "WorkspaceId",
                table: "Workers",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonId",
                table: "Workers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "GamespaceId",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkspaceId",
                table: "Players",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "History",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: true),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    Annotation = table.Column<string>(type: "text", nullable: true),
                    Asset = table.Column<string>(type: "text", nullable: true),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_History", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuthorId = table.Column<int>(type: "integer", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Edited = table.Column<bool>(type: "boolean", nullable: false),
                    RoomId = table.Column<string>(type: "character(36)", fixedLength: true, maxLength: 36, nullable: true),
                    Text = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    WhenCreated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workers_PersonId",
                table: "Workers",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_SubjectId_WorkspaceId",
                table: "Players",
                columns: new[] { "SubjectId", "WorkspaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_UserId",
                table: "Players",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RoomId",
                table: "Messages",
                column: "RoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Gamespaces_GamespaceId",
                table: "Players",
                column: "GamespaceId",
                principalTable: "Gamespaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Users_UserId",
                table: "Players",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Workers_Users_PersonId",
                table: "Workers",
                column: "PersonId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Workers_Workspaces_WorkspaceId",
                table: "Workers",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
