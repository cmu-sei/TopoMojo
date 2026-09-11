// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Hypervisor;

/// <summary>Activity is independent of observed VM power and console connectivity.</summary>
public class VmActivity
{
    public VmActivityKind Kind { get; set; }
    public VmActivityStatus Status { get; set; }
    public string Message { get; set; }

    public static VmActivity FromTask(VmTask task)
    {
        if (task == null || task.Progress >= 100)
            return null;

        return new VmActivity
        {
            Kind = task.Name?.Contains("migrat", System.StringComparison.OrdinalIgnoreCase) == true
                || task.Name?.Contains("relocat", System.StringComparison.OrdinalIgnoreCase) == true
                ? VmActivityKind.Migrating : VmActivityKind.Busy,
            Status = task.Progress < 0 ? VmActivityStatus.Failed : VmActivityStatus.Active,
            Message = task.Progress < 0 ? "The VM operation failed." : null
        };
    }
}

public enum VmActivityKind { Starting, Migrating, Busy, Unknown }
public enum VmActivityStatus { Active, Failed }
