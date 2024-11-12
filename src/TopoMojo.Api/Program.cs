// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Api.Extensions;

namespace TopoMojo.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "TopoMojo";

            LoadSettings();

            var hostBuilder = CreateHostBuilder(args)
                .Build()
                .InitializeDatabase();

            bool dbonly = args.ToList().Contains("--dbonly")
                || Environment.GetEnvironmentVariable("TOPOMOJO_DBONLY")?.ToLower() == "true";

            if (!dbonly)
                hostBuilder.Run();

        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        public static void LoadSettings()
        {
            string envname = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            string path = Environment.GetEnvironmentVariable("APPSETTINGS_PATH") ?? "./conf/appsettings.conf";
            ConfToEnv("appsettings.conf");
            ConfToEnv($"appsettings.{envname}.conf");
            ConfToEnv(path);
        }

        public static void ConfToEnv(string conf)
        {
            if (!File.Exists(conf))
                return;

            try
            {
                foreach (string line in File.ReadAllLines(conf))
                {
                    if (
                        line.Equals(string.Empty)
                        || line.Trim().StartsWith('#')
                        || !line.Contains('=')
                    )
                    {
                        continue;
                    }

                    int x = line.IndexOf('=');

                    Environment.SetEnvironmentVariable(
                        line[..x].Trim(),
                        line[(x + 1)..].Trim()
                    );
                }
            }
            catch {}
        }
    }
}
