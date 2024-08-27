using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using Corsinvest.ProxmoxVE.Api;

namespace TopoMojo.Hypervisor.Proxmox.Models
{
    internal class PveNodeTask
    {
        public string Id { get; set; }
        public string Action { get; set; }
        public int Progress { get; private set; }

        // The Line Number that was last processed to get the current Progress
        public int? LastLine { get; private set; } = null;

        public DateTimeOffset WhenCreated { get; set; }

        public void SetProgress(PveNodeTaskLog log)
        {
            Progress = log.Progress;

            // Hack to ensure topo ui keeps polling until task is removed
            if (Progress > 99)
            {
                Progress = 99;
            }

            var lastEntry = log.Entries.LastOrDefault();

            if (lastEntry != null)
            {
                LastLine = Convert.ToInt32(lastEntry.LineNumber);
            }
        }

        public void SetProgress(Result result)
        {
            var exitStatus = result.Response.data.exitstatus;

            if (exitStatus == "OK")
            {
                Progress = 99;
            }
            else
            {
                Progress = -1;
            }
        }
    }

    internal class PveNodeTaskLog
    {
        public PveNodeTaskLog(Result result)
        {
            if (!result.RequestResource.Contains("/tasks/"))
                throw new ArgumentException();

            List<object> data = result.ToData();
            Entries = data.Select(x => new PveNodeTaskLogEntry(x as ExpandoObject)).ToArray();
            SetProgress();
        }

        public PveNodeTaskLogEntry[] Entries { get; private set; }

        public int Progress { get; private set; }

        private void SetProgress()
        {
            // Tasks do not have a progress property so we have to parse the text output
            // of the task to find progress. The output varies for different types of tasks,
            // so we look for any text in the format of a number ending with a % character.
            // e.g. "drive-scsi0: transferred 25.8 GiB of 32.0 GiB (80.40%) in 2m 12s"
            // would return 80%
            double preciseProgress = 0;
            foreach (var entry in Entries.OrderByDescending(x => x.LineNumber))
            {
                var matches = Regex.Matches(entry.Text, @"(\d+(\.\d+)?%)");
                foreach (Match match in matches)
                {
                    if (double.TryParse(match.Value.TrimEnd('%'), out preciseProgress))
                    {
                        Progress = Convert.ToInt32(preciseProgress);
                        return;
                    }
                }
            }
        }
    }

    internal class PveNodeTaskLogEntry
    {
        public PveNodeTaskLogEntry(ExpandoObject obj)
        {
            LineNumber = ((dynamic)obj).n;
            Text = ((dynamic)obj).t;
        }

        [JsonPropertyName("n")]
        public long LineNumber { get; set; }
        [JsonPropertyName("t")]
        public string Text { get; set; }
    }
}
