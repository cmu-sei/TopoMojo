// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using VimClient;

namespace TopoMojo.Hypervisor.vSphere
{
    public static class FilterFactory
    {
        private static readonly char[] separator = [' ', ','];

        public static PropertyFilterSpec[] VmFilter(ManagedObjectReference mor, string props = "summary layout")
        {
            props += " resourcePool";
            PropertySpec prop = new()
            {
                type = "VirtualMachine",
                pathSet = props.Split(separator, StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray()
            };

            ObjectSpec objectspec = new()
            {
                obj = mor, //_vms or vm-mor
                selectSet = [
                    new TraversalSpec
                    {
                        type = "Folder",
                        path = "childEntity"
                    },
                    new TraversalSpec
                    {
                        type = "ResourcePool",
                        path = "vm"
                    }
                ]
            };

            return [
                new()
                {
                    propSet = [prop],
                    objectSet = [objectspec]
                }
            ];
        }

        public static PropertyFilterSpec[] TaskFilter(ManagedObjectReference mor)
        {
            PropertySpec prop = new()
            {
                type = "Task",
                pathSet = ["info"]
            };

            ObjectSpec objectspec = new()
            {
                obj = mor, //task-mor
            };

            return [
                new()
                {
                    propSet = [prop],
                    objectSet = [objectspec]
                }
            ];
        }

        public static PropertyFilterSpec[] NetworkFilter(ManagedObjectReference mor)
        {
            return NetworkFilter(mor, "networkInfo.portgroup networkInfo.vswitch");
        }
        public static PropertyFilterSpec[] NetworkFilter(ManagedObjectReference mor, string props)
        {
            PropertySpec prop = new()
            {
                type = "HostNetworkSystem",
                pathSet = props.Split(separator, StringSplitOptions.RemoveEmptyEntries)
            };

            ObjectSpec objectspec = new()
            {
                obj = mor, //_net
            };

            return [
                new()
                {
                    propSet = [prop],
                    objectSet = [objectspec]
                }
            ];
        }

        public static PropertyFilterSpec[] DatastoreFilter(ManagedObjectReference mor)
        {
            PropertySpec prop = new()
            {
                type = "Datastore",
                pathSet = ["browser", "capability", "summary"]
            };

            ObjectSpec objectspec = new()
            {
                obj = mor, //_res
                selectSet = [
                    new TraversalSpec
                    {
                        type = "ComputeResource",
                        path = "datastore"
                    }
                ]
            };

            return [
                new()
                {
                    propSet = [prop],
                    objectSet = [objectspec]
                }
            ];
        }

        public static PropertyFilterSpec[] ResourceFilter(ManagedObjectReference mor)
        {
            PropertySpec prop = new()
            {
                type = "ResourcePool",
                pathSet = ["runtime"]
            };

            ObjectSpec objectspec = new()
            {
                obj = mor, //_pool
            };

            return [
                new()
                {
                    propSet = [prop],
                    objectSet = [objectspec]
                }
            ];
        }

        public static PropertyFilterSpec[] DistributedPortgroupFilter(ManagedObjectReference mor)
        {
            PropertySpec prop = new()
            {
                type = "DistributedVirtualPortgroup",
                pathSet = ["name", "parent", "config"]
            };

            ObjectSpec objectspec = new()
            {
                obj = mor, //_pool
                selectSet =
                [
                    new TraversalSpec
                    {
                        type = "ComputeResource",
                        path = "network",
                    }
                ]
            };

            return [
                new()
                {
                    propSet = [prop],
                    objectSet = [objectspec]
                }
            ];
        }

        public static PropertyFilterSpec[] OpaqueNetworkFilter(ManagedObjectReference mor)
        {
            PropertySpec prop = new()
            {
                type = "OpaqueNetwork",
                pathSet = ["summary"]
            };

            ObjectSpec objectspec = new()
            {
                obj = mor, //_pool
                selectSet =
                [
                    new TraversalSpec
                    {
                        type = "ComputeResource",
                        path = "network",
                    }
                ]
            };

            return [
                new()
                {
                    propSet = [prop],
                    objectSet = [objectspec]
                }
            ];
        }

        public static PropertyFilterSpec[] InitFilter(ManagedObjectReference rootMOR)
        {
            var plan = new TraversalSpec
            {
                name = "FolderTraverseSpec",
                type = "Folder",
                path = "childEntity",
                selectSet = [

                    new TraversalSpec()
                    {
                        type = "Datacenter",
                        path = "hostFolder",
                        selectSet = [
                            new SelectionSpec
                            {
                                name = "FolderTraverseSpec"
                            }
                        ]
                    },

                    new TraversalSpec()
                    {
                        type = "Datacenter",
                        path = "networkFolder",
                        selectSet = [
                            new()
                            {
                                name = "FolderTraverseSpec"
                            }
                        ]
                    },

                    new TraversalSpec()
                    {
                        type = "ComputeResource",
                        path = "resourcePool",
                        selectSet =
                        [
                            new TraversalSpec
                            {
                                type = "ResourcePool",
                                path = "resourcePool"
                            }
                        ]
                    },

                    new TraversalSpec()
                    {
                        type = "ComputeResource",
                        path = "host"
                    }
                ]
            };

            var props = new PropertySpec[]
            {
                new() {
                    type = "Datacenter",
                    pathSet = ["name", "parent", "vmFolder"]
                },

                new() {
                    type = "ComputeResource",
                    pathSet = ["name", "parent", "resourcePool", "host"]
                },

                new() {
                    type = "HostSystem",
                    pathSet = ["configManager"]
                },

                new() {
                    type = "ResourcePool",
                    pathSet = ["name", "parent", "resourcePool"]
                },

                new() {
                    type = "DistributedVirtualSwitch",
                    pathSet = ["name", "parent", "uuid"]
                },

                new() {
                    type = "DistributedVirtualPortgroup",
                    pathSet = ["name", "parent", "config"]
                }

            };

            ObjectSpec objectspec = new()
            {
                obj = rootMOR,
                selectSet = [plan]
            };

            PropertyFilterSpec filter = new()
            {
                propSet = props,
                objectSet = [objectspec]
            };

            return [filter];
        }
    }
}
