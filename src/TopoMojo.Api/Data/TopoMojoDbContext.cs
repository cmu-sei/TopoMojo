// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace TopoMojo.Api.Data
{
    public class TopoMojoDbContext(DbContextOptions options) : DbContext(options)
    {
        private const int KEYLENGTH = 36;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Workspace>(b =>
            {
                b.Property(w => w.Id).HasMaxLength(KEYLENGTH);
                b.Property(w => w.Name).HasMaxLength(64);
                b.Property(w => w.Author).HasMaxLength(64);
                b.Property(w => w.Audience).HasMaxLength(64);
                b.Property(w => w.TemplateScope).HasMaxLength(64);
                b.Property(w => w.Description).HasMaxLength(255);
            });

            builder.Entity<Gamespace>(b =>
            {
                b.HasOne(g => g.Workspace).WithMany(w => w.Gamespaces).OnDelete(DeleteBehavior.SetNull);
                b.Property(w => w.Id).HasMaxLength(KEYLENGTH);
                b.Property(w => w.Name).HasMaxLength(64);
                b.Property(w => w.GraderKey).HasMaxLength(64);
            });

            builder.Entity<Template>(b =>
            {
                b.HasOne(t => t.Workspace).WithMany(w => w.Templates).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(t => t.Parent).WithMany().OnDelete(DeleteBehavior.SetNull);
                b.Property(w => w.Id).HasMaxLength(KEYLENGTH);
                b.Property(w => w.ParentId).HasMaxLength(KEYLENGTH);
                b.Property(g => g.WorkspaceId).HasMaxLength(KEYLENGTH);
                b.Property(w => w.Name).HasMaxLength(64);
                b.Property(w => w.Description).HasMaxLength(255);
                b.Property(w => w.Audience).HasMaxLength(64);
                b.Property(w => w.Guestinfo).HasMaxLength(1024);
                b.Property(w => w.Networks).HasMaxLength(64);
                b.Property(w => w.Detail).HasMaxLength(4096);
            });

            builder.Entity<User>(b =>
            {
                b.Property(w => w.Id).HasMaxLength(KEYLENGTH);
                b.Property(w => w.Name).HasMaxLength(64);

                // a user may have an associated ServiceAccountClientId (to support OAuth client credentials auth),
                // but most users will not
                b
                    .Property(w => w.ServiceAccountClientId)
                    .HasMaxLength(128);
                b
                    .HasIndex(w => w.ServiceAccountClientId)
                    .IsUnique();
            });

            builder.Entity<Player>(b =>
            {
                b.HasOne(p => p.Gamespace).WithMany(g => g.Players).OnDelete(DeleteBehavior.Cascade);
                b.HasKey(g => new { g.SubjectId, g.GamespaceId });
                b.Property(g => g.GamespaceId).HasMaxLength(KEYLENGTH);
                b.Property(g => g.SubjectId).HasMaxLength(KEYLENGTH);
                b.Property(g => g.SubjectName).HasMaxLength(64);
            });

            builder.Entity<Worker>(b =>
            {
                b.HasOne(w => w.Workspace).WithMany(w => w.Workers).OnDelete(DeleteBehavior.Cascade);
                b.HasKey(w => new { w.SubjectId, w.WorkspaceId });
                b.Property(g => g.WorkspaceId).HasMaxLength(KEYLENGTH);
                b.Property(g => g.SubjectId).HasMaxLength(KEYLENGTH);
                b.Property(w => w.SubjectName).HasMaxLength(64);
            });

            builder.Entity<ApiKey>(b =>
            {
                b.HasOne(a => a.User).WithMany(u => u.ApiKeys).OnDelete(DeleteBehavior.Cascade);
                b.Property(w => w.Id).HasMaxLength(KEYLENGTH);
                b.Property(w => w.Hash).HasMaxLength(64);
                b.HasIndex(w => w.Hash);
            });

            builder.Entity<Dispatch>(b =>
            {
                b.Property(d => d.Id).HasMaxLength(KEYLENGTH);
                b.Property(d => d.ReferenceId).HasMaxLength(KEYLENGTH);
                b.Property(d => d.TargetGroup).HasMaxLength(KEYLENGTH);
                b.HasIndex(d => d.TargetGroup);
            });

            builder.Entity<WorkspaceFavorite>(b =>
            {
                b.HasKey(x => x.Id);
                b.HasIndex(x => new { x.UserId, x.WorkspaceId }).IsUnique();
            });

            builder.Entity<GamespaceFavorite>(b =>
            {
                b.HasKey(x => x.Id);
                b.HasIndex(x => new { x.UserId, x.GamespaceId }).IsUnique();
            });
        }

        public DbSet<Workspace> Workspaces { get; set; }
        public DbSet<Template> Templates { get; set; }
        public DbSet<Gamespace> Gamespaces { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Worker> Workers { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }
        public DbSet<Dispatch> Dispatches { get; set; }
        public DbSet<WorkspaceFavorite> WorkspaceFavorites { get; set; }
        public DbSet<GamespaceFavorite> GamespaceFavorites { get; set; }

    }

    public class TopoMojoDbContextPostgreSQL(DbContextOptions<TopoMojoDbContextPostgreSQL> options) : TopoMojoDbContext(options)
    {
    }

    public class TopoMojoDbContextSqlServer(DbContextOptions<TopoMojoDbContextSqlServer> options) : TopoMojoDbContext(options)
    {
    }

    public class TopoMojoDbContextInMemory(DbContextOptions<TopoMojoDbContextInMemory> options) : TopoMojoDbContext(options)
    {
    }
}
