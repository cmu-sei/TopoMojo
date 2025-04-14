using System;
using System.ServiceModel.Channels;

namespace TopoMojo.Hypervisor.Exceptions;

public class HypervisorException(string message) : Exception(message);