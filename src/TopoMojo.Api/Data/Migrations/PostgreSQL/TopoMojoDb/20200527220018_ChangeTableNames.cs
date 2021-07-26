// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace TopoMojo.Api.Data.Migrations.PostgreSQL.TopoMojoDb
{
    public partial class ChangeTableNames : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gamespaces_Topologies_TopologyId",
                table: "Gamespaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Profiles_PersonId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Topologies_TopologyId",
                table: "Templates");

            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Profiles_PersonId",
                table: "Workers");

            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Topologies_TopologyId",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Workers_TopologyId",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Templates_TopologyId",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Gamespaces_TopologyId",
                table: "Gamespaces");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Topologies_GlobalId",
                table: "Topologies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Topologies",
                table: "Topologies");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Profiles_GlobalId",
                table: "Profiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Profiles",
                table: "Profiles");

            migrationBuilder.RenameColumn(
                name: "TopologyId",
                table: "Workers",
                newName: "WorkspaceId");

            migrationBuilder.RenameColumn(
                name: "TopologyId",
                table: "Templates",
                newName: "WorkspaceId");

            migrationBuilder.RenameColumn(
                name: "TopologyId",
                table: "Gamespaces",
                newName: "WorkspaceId");

            migrationBuilder.RenameTable(
                name: "Topologies",
                newName: "Workspaces");

            migrationBuilder.RenameTable(
                name: "Profiles",
                newName: "Users");

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

            migrationBuilder.CreateIndex(
                name: "IX_Workers_WorkspaceId",
                table: "Workers",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_WorkspaceId",
                table: "Templates",
                column: "WorkspaceId");

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
                name: "FK_Players_Users_PersonId",
                table: "Players",
                column: "PersonId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Workspaces_WorkspaceId",
                table: "Templates",
                column: "WorkspaceId",
                principalTable: "Workspaces",
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gamespaces_Workspaces_WorkspaceId",
                table: "Gamespaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Users_PersonId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Workspaces_WorkspaceId",
                table: "Templates");

            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Users_PersonId",
                table: "Workers");

            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Workspaces_WorkspaceId",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Workers_WorkspaceId",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Templates_WorkspaceId",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Gamespaces_WorkspaceId",
                table: "Gamespaces");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Workspaces_GlobalId",
                table: "Workspaces");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Workspaces",
                table: "Workspaces");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Users_GlobalId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "Workers",
                newName: "TopologyId");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "Templates",
                newName: "TopologyId");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "Gamespaces",
                newName: "TopologyId");

            migrationBuilder.RenameTable(
                name: "Workspaces",
                newName: "Topologies");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "Profiles");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Topologies_GlobalId",
                table: "Topologies",
                column: "GlobalId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Topologies",
                table: "Topologies",
                column: "Id");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Profiles_GlobalId",
                table: "Profiles",
                column: "GlobalId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Profiles",
                table: "Profiles",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Workers_TopologyId",
                table: "Workers",
                column: "TopologyId");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_TopologyId",
                table: "Templates",
                column: "TopologyId");

            migrationBuilder.CreateIndex(
                name: "IX_Gamespaces_TopologyId",
                table: "Gamespaces",
                column: "TopologyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gamespaces_Topologies_TopologyId",
                table: "Gamespaces",
                column: "TopologyId",
                principalTable: "Topologies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Profiles_PersonId",
                table: "Players",
                column: "PersonId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Topologies_TopologyId",
                table: "Templates",
                column: "TopologyId",
                principalTable: "Topologies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Workers_Profiles_PersonId",
                table: "Workers",
                column: "PersonId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Workers_Topologies_TopologyId",
                table: "Workers",
                column: "TopologyId",
                principalTable: "Topologies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
