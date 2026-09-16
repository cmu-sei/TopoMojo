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
            // Task names are display text, not operation identifiers.
            // Hypervisor adapters identify migration from their operation status.
            Kind = VmActivityKind.Busy,
            Status = task.Progress < 0 ? VmActivityStatus.Failed : VmActivityStatus.Active,
            Message = task.Progress < 0 ? "The VM operation failed." : null
        };
    }
}

public enum VmActivityKind { Starting, Migrating, Busy, Unknown }
public enum VmActivityStatus { Active, Failed }
