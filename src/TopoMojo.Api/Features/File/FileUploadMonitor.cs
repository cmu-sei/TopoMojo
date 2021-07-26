// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
            _monitor = new Dictionary<string, FileProgress>();
            Task task = CleanupLoop();
        }
        private readonly ILogger<FileUploadMonitor> _logger;
        private Dictionary<string, FileProgress> _monitor;

        public void Update(string key, int progress)
        {
            if (_monitor.ContainsKey(key))
            {
                _monitor[key].Progress = progress;
                _monitor[key].Stop = DateTimeOffset.UtcNow;
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
            if (_monitor.ContainsKey(key))
                return _monitor[key];

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
                        _logger.LogDebug("removed monitor " + item.Key);
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
