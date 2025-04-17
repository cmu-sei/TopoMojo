using System;

namespace TopoMojo.Hypervisor.Exceptions;

public class HypervisorException(string message) : Exception(message);
