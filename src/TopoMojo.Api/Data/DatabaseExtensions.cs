// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Extensions
{
    public static class DatabaseExtensions
    {

        public static IHost InitializeDatabase(
            this IHost host
        )
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            IConfiguration config = services.GetRequiredService<IConfiguration>();
            IWebHostEnvironment env = services.GetService<IWebHostEnvironment>();
            DatabaseOptions options = config.GetSection("Database").Get<DatabaseOptions>()
                ?? new DatabaseOptions();

            var dbContext = services.GetService<TopoMojoDbContext>();

            if (!dbContext.Database.IsInMemory())
                dbContext.Database.Migrate();

            // add admin if specified and doesn't exist
            if (!string.IsNullOrEmpty(options.AdminId))
            {
                var admin = dbContext.Users.Find(options.AdminId);
                if (admin is null)
                {
                    dbContext.Users.Add(new Data.User
                    {
                        Id = options.AdminId,
                        Name = options.AdminName,
                        Role = UserRole.Administrator,
                        WhenCreated = DateTimeOffset.UtcNow
                    });
                    dbContext.SaveChanges();
                }
            }

            string seedFile = Path.Combine(env.ContentRootPath, options.SeedFile);

            if (File.Exists(seedFile))
            {

                DbSeedModel seedData = JsonSerializer.Deserialize<DbSeedModel>(
                    File.ReadAllText(seedFile)
                );

                foreach (var u in seedData.Users)
                {
                    if (!dbContext.Users.Any(p => p.Id == u.GlobalId))
                    {
                        dbContext.Users.Add(new Data.User
                        {
                            Name = u.Name,
                            Id = u.GlobalId,
                            WhenCreated = DateTimeOffset.UtcNow,
                            Role = UserRole.Administrator
                        });
                    }
                }
                dbContext.SaveChanges();
            }

            return host;
        }
    }
}
