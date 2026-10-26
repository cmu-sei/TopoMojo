using System;

namespace TopoMojo.Hypervisor.Exceptions;

/// <summary>
/// A hypervisor failure whose message is shown to the user. Detail, if any, is for admins only
/// and goes to the error log.
/// </summary>
public class HypervisorException(string message, string detail = null) : Exception(message)
{
    public string Detail { get; } = detail;
}
