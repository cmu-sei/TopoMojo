// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TopoMojo.Api.Data;

namespace TopoMojo.Api.Data.Migrations.SqlServer.TopoMojoDb
{
    [DbContext(typeof(TopoMojoDbContextSqlServer))]
    [Migration("20210618214347_GuidPrimaryKeys")]
    partial class GuidPrimaryKeys
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .UseIdentityColumns()
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("ProductVersion", "5.0.0");

            modelBuilder.Entity("TopoMojo.Api.Data.ApiKey", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(36)
                        .HasColumnType("nchar(36)")
                        .IsFixedLength(true);

                    b.Property<string>("UserId")
                        .HasColumnType("nchar(36)");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("ApiKey");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Gamespace", b =>
                {
                    b.Property<string>("GlobalId")
                        .HasMaxLength(36)
                        .HasColumnType("nchar(36)")
                        .IsFixedLength(true);

                    b.Property<bool>("AllowReset")
                        .HasColumnType("bit");

                    b.Property<string>("Challenge")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClientId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("ExpirationTime")
                        .HasColumnType("datetime2");

                    b.Property<string>("Name")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("ShareCode")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("StartTime")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("StopTime")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("WhenCreated")
                        .HasColumnType("datetime2");

                    b.Property<string>("WorkspaceGlobalId")
                        .HasColumnType("nchar(36)");

                    b.HasKey("GlobalId");

                    b.HasIndex("WorkspaceGlobalId");

                    b.ToTable("Gamespaces");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Player", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<string>("GamespaceGlobalId")
                        .HasMaxLength(36)
                        .HasColumnType("nchar(36)")
                        .IsFixedLength(true);

                    b.Property<int>("Permission")
                        .HasColumnType("int");

                    b.Property<string>("SubjectId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SubjectName")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("GamespaceGlobalId");

                    b.ToTable("Players");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Template", b =>
                {
                    b.Property<string>("GlobalId")
                        .HasMaxLength(36)
                        .HasColumnType("nchar(36)")
                        .IsFixedLength(true);

                    b.Property<string>("Description")
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.Property<string>("Detail")
                        .HasMaxLength(4096)
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Guestinfo")
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<bool>("IsHidden")
                        .HasColumnType("bit");

                    b.Property<bool>("IsPublished")
                        .HasColumnType("bit");

                    b.Property<string>("Iso")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("Networks")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("ParentGlobalId")
                        .HasMaxLength(36)
                        .HasColumnType("nchar(36)")
                        .IsFixedLength(true);

                    b.Property<int>("Replicas")
                        .HasColumnType("int");

                    b.Property<DateTime>("WhenCreated")
                        .HasColumnType("datetime2");

                    b.Property<string>("WorkspaceGlobalId")
                        .HasMaxLength(36)
                        .HasColumnType("nchar(36)")
                        .IsFixedLength(true);

                    b.HasKey("GlobalId");

                    b.HasIndex("ParentGlobalId");

                    b.HasIndex("WorkspaceGlobalId");

                    b.ToTable("Templates");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.User", b =>
                {
                    b.Property<string>("GlobalId")
                        .HasMaxLength(36)
                        .HasColumnType("nchar(36)")
                        .IsFixedLength(true);

                    b.Property<string>("CallbackUrl")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("GamespaceLimit")
                        .HasColumnType("int");

                    b.Property<int>("GamespaceMaxMinutes")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<int>("Role")
                        .HasColumnType("int");

                    b.Property<string>("Scope")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("SessionLimit")
                        .HasColumnType("int");

                    b.Property<DateTime>("WhenCreated")
                        .HasColumnType("datetime2");

                    b.Property<int>("WorkspaceLimit")
                        .HasColumnType("int");

                    b.HasKey("GlobalId");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Worker", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int>("Permission")
                        .HasColumnType("int");

                    b.Property<string>("SubjectId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SubjectName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("WorkspaceGlobalId")
                        .HasMaxLength(36)
                        .HasColumnType("nchar(36)")
                        .IsFixedLength(true);

                    b.HasKey("Id");

                    b.HasIndex("WorkspaceGlobalId");

                    b.ToTable("Workers");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Workspace", b =>
                {
                    b.Property<string>("GlobalId")
                        .HasMaxLength(36)
                        .HasColumnType("nchar(36)")
                        .IsFixedLength(true);

                    b.Property<string>("Audience")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("Author")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("Challenge")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Description")
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.Property<string>("DocumentUrl")
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<bool>("IsPublished")
                        .HasColumnType("bit");

                    b.Property<DateTime>("LastActivity")
                        .HasColumnType("datetime2");

                    b.Property<int>("LaunchCount")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<string>("ShareCode")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("TemplateLimit")
                        .HasColumnType("int");

                    b.Property<bool>("UseUplinkSwitch")
                        .HasColumnType("bit");

                    b.Property<DateTime>("WhenCreated")
                        .HasColumnType("datetime2");

                    b.HasKey("GlobalId");

                    b.ToTable("Workspaces");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.ApiKey", b =>
                {
                    b.HasOne("TopoMojo.Api.Data.User", "User")
                        .WithMany("ApiKeys")
                        .HasForeignKey("UserId");

                    b.Navigation("User");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Gamespace", b =>
                {
                    b.HasOne("TopoMojo.Api.Data.Workspace", "Workspace")
                        .WithMany("Gamespaces")
                        .HasForeignKey("WorkspaceGlobalId");

                    b.Navigation("Workspace");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Player", b =>
                {
                    b.HasOne("TopoMojo.Api.Data.Gamespace", "Gamespace")
                        .WithMany("Players")
                        .HasForeignKey("GamespaceGlobalId");

                    b.Navigation("Gamespace");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Template", b =>
                {
                    b.HasOne("TopoMojo.Api.Data.Template", "Parent")
                        .WithMany("Children")
                        .HasForeignKey("ParentGlobalId");

                    b.HasOne("TopoMojo.Api.Data.Workspace", "Workspace")
                        .WithMany("Templates")
                        .HasForeignKey("WorkspaceGlobalId");

                    b.Navigation("Parent");

                    b.Navigation("Workspace");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Worker", b =>
                {
                    b.HasOne("TopoMojo.Api.Data.Workspace", "Workspace")
                        .WithMany("Workers")
                        .HasForeignKey("WorkspaceGlobalId");

                    b.Navigation("Workspace");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Gamespace", b =>
                {
                    b.Navigation("Players");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Template", b =>
                {
                    b.Navigation("Children");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.User", b =>
                {
                    b.Navigation("ApiKeys");
                });

            modelBuilder.Entity("TopoMojo.Api.Data.Workspace", b =>
                {
                    b.Navigation("Gamespaces");

                    b.Navigation("Templates");

                    b.Navigation("Workers");
                });
#pragma warning restore 612, 618
        }
    }
}
