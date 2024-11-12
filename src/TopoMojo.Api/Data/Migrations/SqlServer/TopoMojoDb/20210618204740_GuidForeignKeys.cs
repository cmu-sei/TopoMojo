// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.EntityFrameworkCore.Migrations;

namespace TopoMojo.Api.Data.Migrations.SqlServer.TopoMojoDb
{
    public partial class GuidForeignKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gamespaces_Workspaces_WorkspaceId",
                table: "Gamespaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Gamespaces_GamespaceId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Templates_ParentId",
                table: "Templates");

            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Workspaces_WorkspaceId",
                table: "Templates");

            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Workspaces_WorkspaceId",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Workers_WorkspaceId",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Templates_ParentId",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Templates_WorkspaceId",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Players_GamespaceId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Gamespaces_WorkspaceId",
                table: "Gamespaces");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "GamespaceId",
                table: "Players");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Workspaces",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Workers",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Templates",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Players",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<string>(
                name: "WorkspaceGlobalId",
                table: "Gamespaces",
                type: "nchar(36)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Gamespaces",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Workspaces",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Workers",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "WorkspaceId",
                table: "Workers",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Templates",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Players",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "GamespaceId",
                table: "Players",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkspaceGlobalId",
                table: "Gamespaces",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nchar(36)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Gamespaces",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.CreateIndex(
                name: "IX_Workers_WorkspaceId",
                table: "Workers",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_ParentId",
                table: "Templates",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_WorkspaceId",
                table: "Templates",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_GamespaceId",
                table: "Players",
                column: "GamespaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Gamespaces_WorkspaceId",
                table: "Gamespaces",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gamespaces_Workspaces_WorkspaceId",
                table: "Gamespaces",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Gamespaces_GamespaceId",
                table: "Players",
                column: "GamespaceId",
                principalTable: "Gamespaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Templates_ParentId",
                table: "Templates",
                column: "ParentId",
                principalTable: "Templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Workspaces_WorkspaceId",
                table: "Templates",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Workers_Workspaces_WorkspaceId",
                table: "Workers",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
