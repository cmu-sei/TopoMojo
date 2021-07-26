using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace TopoMojo.Api.Data.Migrations.PostgreSQL.TopoMojoDb
{
    public partial class RefactorKeysAndDateTime : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""Players"" WHERE ""SubjectId"" is null;
                DELETE FROM ""Workers"" WHERE ""SubjectId"" is null;
                UPDATE ""Gamespaces"" SET ""ShareCode"" = null;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_ApiKey_Users_UserId",
                table: "ApiKey");

            migrationBuilder.DropForeignKey(
                name: "FK_Gamespaces_Workspaces_WorkspaceId",
                table: "Gamespaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Gamespaces_GamespaceId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Workspaces_WorkspaceId",
                table: "Workers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Workers",
                table: "Workers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Players",
                table: "Players");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApiKey",
                table: "ApiKey");

            migrationBuilder.DropColumn(
                name: "DocumentUrl",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "CallbackUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "StopTime",
                table: "Gamespaces");

            migrationBuilder.RenameTable(
                name: "ApiKey",
                newName: "ApiKeys");

            migrationBuilder.RenameColumn(
                name: "SessionLimit",
                table: "Users",
                newName: "GamespaceCleanupGraceMinutes");

            migrationBuilder.RenameColumn(
                name: "ShareCode",
                table: "Gamespaces",
                newName: "ManagerName");

            migrationBuilder.RenameColumn(
                name: "ClientId",
                table: "Gamespaces",
                newName: "ManagerId");

            migrationBuilder.RenameIndex(
                name: "IX_ApiKey_UserId",
                table: "ApiKeys",
                newName: "IX_ApiKeys_UserId");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "WhenCreated",
                table: "Workspaces",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastActivity",
                table: "Workspaces",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Workspaces",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(36)",
                oldFixedLength: true,
                oldMaxLength: 36);

            migrationBuilder.AddColumn<bool>(
                name: "HostAffinity",
                table: "Workspaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateScope",
                table: "Workspaces",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkspaceId",
                table: "Workers",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character(36)",
                oldFixedLength: true,
                oldMaxLength: 36,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectName",
                table: "Workers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectId",
                table: "Workers",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "WhenCreated",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Users",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(36)",
                oldFixedLength: true,
                oldMaxLength: 36);

            migrationBuilder.AlterColumn<string>(
                name: "WorkspaceId",
                table: "Templates",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(36)",
                oldFixedLength: true,
                oldMaxLength: 36,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "WhenCreated",
                table: "Templates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<string>(
                name: "ParentId",
                table: "Templates",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(36)",
                oldFixedLength: true,
                oldMaxLength: 36,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Templates",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(36)",
                oldFixedLength: true,
                oldMaxLength: 36);

            migrationBuilder.AddColumn<string>(
                name: "Audience",
                table: "Templates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectName",
                table: "Players",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectId",
                table: "Players",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GamespaceId",
                table: "Players",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character(36)",
                oldFixedLength: true,
                oldMaxLength: 36,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkspaceId",
                table: "Gamespaces",
                type: "character varying(36)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(36)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "WhenCreated",
                table: "Gamespaces",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartTime",
                table: "Gamespaces",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpirationTime",
                table: "Gamespaces",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Gamespaces",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(36)",
                oldFixedLength: true,
                oldMaxLength: 36);

            migrationBuilder.AddColumn<int>(
                name: "CleanupGraceMinutes",
                table: "Gamespaces",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndTime",
                table: "Gamespaces",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ApiKeys",
                type: "character varying(36)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(36)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "ApiKeys",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(36)",
                oldFixedLength: true,
                oldMaxLength: 36);

            migrationBuilder.AddColumn<string>(
                name: "Hash",
                table: "ApiKeys",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ApiKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WhenCreated",
                table: "ApiKeys",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Workers",
                table: "Workers",
                columns: new[] { "SubjectId", "WorkspaceId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Players",
                table: "Players",
                columns: new[] { "SubjectId", "GamespaceId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApiKeys",
                table: "ApiKeys",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Hash",
                table: "ApiKeys",
                column: "Hash");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeys_Users_UserId",
                table: "ApiKeys",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Gamespaces_Workspaces_WorkspaceId",
                table: "Gamespaces",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Gamespaces_GamespaceId",
                table: "Players",
                column: "GamespaceId",
                principalTable: "Gamespaces",
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
                name: "FK_ApiKeys_Users_UserId",
                table: "ApiKeys");

            migrationBuilder.DropForeignKey(
                name: "FK_Gamespaces_Workspaces_WorkspaceId",
                table: "Gamespaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Gamespaces_GamespaceId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Workspaces_WorkspaceId",
                table: "Workers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Workers",
                table: "Workers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Players",
                table: "Players");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApiKeys",
                table: "ApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_Hash",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "HostAffinity",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "TemplateScope",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "CleanupGraceMinutes",
                table: "Gamespaces");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Gamespaces");

            migrationBuilder.DropColumn(
                name: "Hash",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "WhenCreated",
                table: "ApiKeys");

            migrationBuilder.RenameTable(
                name: "ApiKeys",
                newName: "ApiKey");

            migrationBuilder.RenameColumn(
                name: "GamespaceCleanupGraceMinutes",
                table: "Users",
                newName: "SessionLimit");

            migrationBuilder.RenameColumn(
                name: "ManagerName",
                table: "Gamespaces",
                newName: "ShareCode");

            migrationBuilder.RenameColumn(
                name: "ManagerId",
                table: "Gamespaces",
                newName: "ClientId");

            migrationBuilder.RenameIndex(
                name: "IX_ApiKeys_UserId",
                table: "ApiKey",
                newName: "IX_ApiKey_UserId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "WhenCreated",
                table: "Workspaces",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastActivity",
                table: "Workspaces",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Workspaces",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AddColumn<string>(
                name: "DocumentUrl",
                table: "Workspaces",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectName",
                table: "Workers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkspaceId",
                table: "Workers",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectId",
                table: "Workers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Workers",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<DateTime>(
                name: "WhenCreated",
                table: "Users",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Users",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AddColumn<string>(
                name: "CallbackUrl",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkspaceId",
                table: "Templates",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "WhenCreated",
                table: "Templates",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "ParentId",
                table: "Templates",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Templates",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectName",
                table: "Players",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GamespaceId",
                table: "Players",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AlterColumn<string>(
                name: "SubjectId",
                table: "Players",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "WorkspaceId",
                table: "Gamespaces",
                type: "character(36)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "WhenCreated",
                table: "Gamespaces",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartTime",
                table: "Gamespaces",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpirationTime",
                table: "Gamespaces",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Gamespaces",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AddColumn<DateTime>(
                name: "StopTime",
                table: "Gamespaces",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ApiKey",
                type: "character(36)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "ApiKey",
                type: "character(36)",
                fixedLength: true,
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Workers",
                table: "Workers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Players",
                table: "Players",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApiKey",
                table: "ApiKey",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKey_Users_UserId",
                table: "ApiKey",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

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
                name: "FK_Workers_Workspaces_WorkspaceId",
                table: "Workers",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
