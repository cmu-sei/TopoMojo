// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Services
{
    public interface IFileUploadMonitor
    {
        void Update(string key, int progress);
        FileProgress Check(string key);
    }


    public class FileUploadMonitor : IFileUploadMonitor
    {
        public FileUploadMonitor(ILogger<FileUploadMonitor> logger)
        {
            _logger = logger;
            _monitor = [];
            _ = CleanupLoop();
        }
        private readonly ILogger<FileUploadMonitor> _logger;
        private readonly Dictionary<string, FileProgress> _monitor;

        public void Update(string key, int progress)
        {
            if (_monitor.TryGetValue(key, out FileProgress value))
            {
                value.Progress = progress;
                value.Stop = DateTimeOffset.UtcNow;
            }
            else
            {
                _monitor.Add(key, new FileProgress
                {
                    Key = key,
                    Progress = 0,
                    Start = DateTimeOffset.UtcNow
                });
            }
        }

        public FileProgress Check(string key)
        {
            if (_monitor.TryGetValue(key, out FileProgress value))
                return value;

            return new FileProgress { Key = key, Progress = -1 };
        }

        private async Task CleanupLoop()
        {
            while (true)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                foreach(FileProgress item in _monitor.Values.ToArray())
                {
                    if (now.CompareTo(item.Stop.AddMinutes(2)) > 0)
                    {
                        _logger.LogDebug("removed monitor {key}", item.Key);
                        _monitor.Remove(item.Key);
                    }
                }
                await Task.Delay(60000);
            }
        }
    }

    public class FileProgress
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public int Progress { get; set; }
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset Stop { get; set; }
    }
}
