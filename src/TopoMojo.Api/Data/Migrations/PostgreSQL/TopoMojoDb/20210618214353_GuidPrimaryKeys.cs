// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

﻿using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace TopoMojo.Api.Data.Migrations.PostgreSQL.TopoMojoDb
{
    public partial class GuidPrimaryKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gamespaces_Workspaces_WorkspaceGlobalId",
                table: "Gamespaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Gamespaces_GamespaceGlobalId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Templates_ParentGlobalId",
                table: "Templates");

            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Workspaces_WorkspaceGlobalId",
                table: "Templates");

            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Workspaces_WorkspaceGlobalId",
                table: "Workers");

            migrationBuilder.DropForeignKey(
                name: "FK_ApiKey_Users_UserId",
                table: "ApiKey");

            migrationBuilder.DropIndex(
                name: "IX_Workers_WorkspaceGlobalId",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Templates_ParentGlobalId",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Templates_WorkspaceGlobalId",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Players_GamespaceGlobalId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Gamespaces_WorkspaceGlobalId",
                table: "Gamespaces");

            migrationBuilder.DropIndex(
                name: "IX_ApiKey_UserId",
                table: "ApiKey");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Workspaces_GlobalId",
                table: "Workspaces");

            // migrationBuilder.DropPrimaryKey(
            //     name: "PK_Workspaces",
            //     table: "Workspaces");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Users_GlobalId",
                table: "Users");

            // migrationBuilder.DropPrimaryKey(
            //     name: "PK_Users",
            //     table: "Users");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Templates_GlobalId",
                table: "Templates");

            // migrationBuilder.DropPrimaryKey(
            //     name: "PK_Templates",
            //     table: "Templates");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Gamespaces_GlobalId",
                table: "Gamespaces");

            // migrationBuilder.DropPrimaryKey(
            //     name: "PK_Gamespaces",
            //     table: "Gamespaces");
            migrationBuilder.Sql(@"
                ALTER TABLE ""Workspaces"" DROP CONSTRAINT ""PK_Workspaces"" CASCADE;
                ALTER TABLE ""Gamespaces"" DROP CONSTRAINT ""PK_Gamespaces"" CASCADE;
                ALTER TABLE ""Templates"" DROP CONSTRAINT ""PK_Templates"" CASCADE;
                ALTER TABLE ""Users"" DROP CONSTRAINT ""PK_Users"" CASCADE;
            ");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Gamespaces");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Gamespaces");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Workspaces",
                table: "Workspaces",
                column: "GlobalId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "GlobalId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Templates",
                table: "Templates",
                column: "GlobalId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Gamespaces",
                table: "Gamespaces",
                column: "GlobalId");

            migrationBuilder.CreateIndex(
                name: "IX_Workers_WorkspaceGlobalId",
                table: "Workers",
                column: "WorkspaceGlobalId");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_ParentGlobalId",
                table: "Templates",
                column: "ParentGlobalId");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_WorkspaceGlobalId",
                table: "Templates",
                column: "WorkspaceGlobalId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_GamespaceGlobalId",
                table: "Players",
                column: "GamespaceGlobalId");

            migrationBuilder.CreateIndex(
                name: "IX_Gamespaces_WorkspaceGlobalId",
                table: "Gamespaces",
                column: "WorkspaceGlobalId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKey_UserId",
                table: "ApiKey",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gamespaces_Workspaces_WorkspaceGlobalId",
                table: "Gamespaces",
                column: "WorkspaceGlobalId",
                principalTable: "Workspaces",
                principalColumn: "GlobalId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Gamespaces_GamespaceGlobalId",
                table: "Players",
                column: "GamespaceGlobalId",
                principalTable: "Gamespaces",
                principalColumn: "GlobalId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Templates_ParentGlobalId",
                table: "Templates",
                column: "ParentGlobalId",
                principalTable: "Templates",
                principalColumn: "GlobalId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Workspaces_WorkspaceGlobalId",
                table: "Templates",
                column: "WorkspaceGlobalId",
                principalTable: "Workspaces",
                principalColumn: "GlobalId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Workers_Workspaces_WorkspaceGlobalId",
                table: "Workers",
                column: "WorkspaceGlobalId",
                principalTable: "Workspaces",
                principalColumn: "GlobalId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKey_Users_UserId",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "GlobalId",
                table: "ApiKey",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Workspaces",
                table: "Workspaces");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Templates",
                table: "Templates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Gamespaces",
                table: "Gamespaces");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Workspaces",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Templates",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "Templates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkspaceId",
                table: "Templates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Gamespaces",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "WorkspaceId",
                table: "Gamespaces",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Workspaces_GlobalId",
                table: "Workspaces",
                column: "GlobalId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Workspaces",
                table: "Workspaces",
                column: "Id");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Users_GlobalId",
                table: "Users",
                column: "GlobalId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Templates_GlobalId",
                table: "Templates",
                column: "GlobalId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Templates",
                table: "Templates",
                column: "Id");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Gamespaces_GlobalId",
                table: "Gamespaces",
                column: "GlobalId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Gamespaces",
                table: "Gamespaces",
                column: "Id");
        }
    }
}
