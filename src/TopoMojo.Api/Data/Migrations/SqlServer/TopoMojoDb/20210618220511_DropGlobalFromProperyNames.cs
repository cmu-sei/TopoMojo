using Microsoft.EntityFrameworkCore.Migrations;

namespace TopoMojo.Api.Data.Migrations.SqlServer.TopoMojoDb
{
    public partial class DropGlobalFromProperyNames : Migration
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

            migrationBuilder.RenameColumn(
                name: "GlobalId",
                table: "Workspaces",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "WorkspaceGlobalId",
                table: "Workers",
                newName: "WorkspaceId");

            migrationBuilder.RenameIndex(
                name: "IX_Workers_WorkspaceGlobalId",
                table: "Workers",
                newName: "IX_Workers_WorkspaceId");

            migrationBuilder.RenameColumn(
                name: "GlobalId",
                table: "Users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "WorkspaceGlobalId",
                table: "Templates",
                newName: "WorkspaceId");

            migrationBuilder.RenameColumn(
                name: "ParentGlobalId",
                table: "Templates",
                newName: "ParentId");

            migrationBuilder.RenameColumn(
                name: "GlobalId",
                table: "Templates",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Templates_WorkspaceGlobalId",
                table: "Templates",
                newName: "IX_Templates_WorkspaceId");

            migrationBuilder.RenameIndex(
                name: "IX_Templates_ParentGlobalId",
                table: "Templates",
                newName: "IX_Templates_ParentId");

            migrationBuilder.RenameColumn(
                name: "GamespaceGlobalId",
                table: "Players",
                newName: "GamespaceId");

            migrationBuilder.RenameIndex(
                name: "IX_Players_GamespaceGlobalId",
                table: "Players",
                newName: "IX_Players_GamespaceId");

            migrationBuilder.RenameColumn(
                name: "WorkspaceGlobalId",
                table: "Gamespaces",
                newName: "WorkspaceId");

            migrationBuilder.RenameColumn(
                name: "GlobalId",
                table: "Gamespaces",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Gamespaces_WorkspaceGlobalId",
                table: "Gamespaces",
                newName: "IX_Gamespaces_WorkspaceId");

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
                table: "Players",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Gamespaces_Workspaces_WorkspaceId",
                table: "Gamespaces",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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

        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Workspaces",
                newName: "GlobalId");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "Workers",
                newName: "WorkspaceGlobalId");

            migrationBuilder.RenameIndex(
                name: "IX_Workers_WorkspaceId",
                table: "Workers",
                newName: "IX_Workers_WorkspaceGlobalId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Users",
                newName: "GlobalId");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "Templates",
                newName: "WorkspaceGlobalId");

            migrationBuilder.RenameColumn(
                name: "ParentId",
                table: "Templates",
                newName: "ParentGlobalId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Templates",
                newName: "GlobalId");

            migrationBuilder.RenameIndex(
                name: "IX_Templates_WorkspaceId",
                table: "Templates",
                newName: "IX_Templates_WorkspaceGlobalId");

            migrationBuilder.RenameIndex(
                name: "IX_Templates_ParentId",
                table: "Templates",
                newName: "IX_Templates_ParentGlobalId");

            migrationBuilder.RenameColumn(
                name: "GamespaceId",
                table: "Players",
                newName: "GamespaceGlobalId");

            migrationBuilder.RenameIndex(
                name: "IX_Players_GamespaceId",
                table: "Players",
                newName: "IX_Players_GamespaceGlobalId");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "Gamespaces",
                newName: "WorkspaceGlobalId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Gamespaces",
                newName: "GlobalId");

            migrationBuilder.RenameIndex(
                name: "IX_Gamespaces_WorkspaceId",
                table: "Gamespaces",
                newName: "IX_Gamespaces_WorkspaceGlobalId");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Workers",
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
    }
}
