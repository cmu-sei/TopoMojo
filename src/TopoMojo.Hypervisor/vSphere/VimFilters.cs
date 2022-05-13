// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using VimClient;

namespace TopoMojo.Hypervisor.vSphere
{
    public static class FilterFactory
    {

        public static PropertyFilterSpec[] VmFilter(ManagedObjectReference mor, string props = "summary layout")
        {
            props += " resourcePool";
            PropertySpec prop = new PropertySpec {
                type = "VirtualMachine",
                pathSet = props.Split(new char[] {' ', ','}, StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray()
            };

            ObjectSpec objectspec = new ObjectSpec {
                obj = mor, //_vms or vm-mor
                selectSet = new SelectionSpec[] {
                    new TraversalSpec {
                        type = "Folder",
                        path = "childEntity"
                    },
                    new TraversalSpec {
                        type = "ResourcePool",
                        path = "vm"
                    }
                }
            };

            return new PropertyFilterSpec[] {
                new PropertyFilterSpec {
                    propSet = new PropertySpec[] { prop },
                    objectSet = new ObjectSpec[] { objectspec }
                }
            };
        }

        public static PropertyFilterSpec[] TaskFilter(ManagedObjectReference mor)
        {
            PropertySpec prop = new PropertySpec {
                type = "Task",
                pathSet = new string[] {"info"}
            };

            ObjectSpec objectspec = new ObjectSpec {
                obj = mor, //task-mor
            };

            return new PropertyFilterSpec[] {
                new PropertyFilterSpec {
                    propSet = new PropertySpec[] { prop },
                    objectSet = new ObjectSpec[] { objectspec }
                }
            };
        }

        public static PropertyFilterSpec[] NetworkFilter(ManagedObjectReference mor)
        {
            return NetworkFilter(mor, "networkInfo.portgroup networkInfo.vswitch");
        }
        public static PropertyFilterSpec[] NetworkFilter(ManagedObjectReference mor, string props)
        {
            PropertySpec prop = new PropertySpec {
                type = "HostNetworkSystem",
                pathSet = props.Split(new char[] {' ', ','}, StringSplitOptions.RemoveEmptyEntries)
            };

            ObjectSpec objectspec = new ObjectSpec {
                obj = mor, //_net
            };

            return new PropertyFilterSpec[] {
                new PropertyFilterSpec {
                    propSet = new PropertySpec[] { prop },
                    objectSet = new ObjectSpec[] { objectspec }
                }
            };
        }

        public static PropertyFilterSpec[] DatastoreFilter(ManagedObjectReference mor)
        {
            PropertySpec prop = new PropertySpec {
                type = "Datastore",
                pathSet = new string[] { "browser", "capability", "summary" }
            };

            ObjectSpec objectspec = new ObjectSpec {
                obj = mor, //_res
                selectSet = new SelectionSpec[] {
                    new TraversalSpec {
                        type = "ComputeResource",
                        path = "datastore"
                    }
                }
            };

            return new PropertyFilterSpec[] {
                new PropertyFilterSpec {
                    propSet = new PropertySpec[] { prop },
                    objectSet = new ObjectSpec[] { objectspec }
                }
            };
        }

        public static PropertyFilterSpec[] ResourceFilter(ManagedObjectReference mor)
        {
            PropertySpec prop = new PropertySpec {
                type = "ResourcePool",
                pathSet = new string[] {"runtime"}
            };

            ObjectSpec objectspec = new ObjectSpec {
                obj = mor, //_pool
            };

            return new PropertyFilterSpec[] {
                new PropertyFilterSpec {
                    propSet = new PropertySpec[] { prop },
                    objectSet = new ObjectSpec[] { objectspec }
                }
            };
        }

        public static PropertyFilterSpec[] DistributedPortgroupFilter(ManagedObjectReference mor)
        {
            PropertySpec prop = new PropertySpec {
                type = "DistributedVirtualPortgroup",
                pathSet = new string[] { "name", "parent", "config" }
            };

            ObjectSpec objectspec = new ObjectSpec {
                obj = mor, //_pool
                selectSet = new SelectionSpec[]
                {
                    new TraversalSpec {
                        type = "ComputeResource",
                        path = "network",
                    }
                }
            };

            return new PropertyFilterSpec[] {
                new PropertyFilterSpec {
                    propSet = new PropertySpec[] { prop },
                    objectSet = new ObjectSpec[] { objectspec }
                }
            };
        }

        public static PropertyFilterSpec[] OpaqueNetworkFilter(ManagedObjectReference mor)
        {
            PropertySpec prop = new PropertySpec {
                type = "OpaqueNetwork",
                pathSet = new string[] { "summary" }
            };

            ObjectSpec objectspec = new ObjectSpec {
                obj = mor, //_pool
                selectSet = new SelectionSpec[]
                {
                    new TraversalSpec {
                        type = "ComputeResource",
                        path = "network",
                    }
                }
            };

            return new PropertyFilterSpec[] {
                new PropertyFilterSpec {
                    propSet = new PropertySpec[] { prop },
                    objectSet = new ObjectSpec[] { objectspec }
                }
            };
        }

        public static PropertyFilterSpec[] InitFilter(ManagedObjectReference rootMOR)
        {
            var plan = new TraversalSpec
            {
                name = "FolderTraverseSpec",
                type = "Folder",
                path = "childEntity",
                selectSet = new SelectionSpec[] {

                    new TraversalSpec()
                    {
                        type = "Datacenter",
                        path = "hostFolder",
                        selectSet = new SelectionSpec[] {
                            new SelectionSpec {
                                name = "FolderTraverseSpec"
                            }
                        }
                    },

                    new TraversalSpec()
                    {
                        type = "Datacenter",
                        path = "networkFolder",
                        selectSet = new SelectionSpec[] {
                            new SelectionSpec {
                                name = "FolderTraverseSpec"
                            }
                        }
                    },

                    new TraversalSpec()
                    {
                        type = "ComputeResource",
                        path = "resourcePool",
                        selectSet = new SelectionSpec[]
                        {
                            new TraversalSpec
                            {
                                type="ResourcePool",
                                path="resourcePool"
                            }
                        }
                    },

                    new TraversalSpec()
                    {
                        type = "ComputeResource",
                        path = "host"
                    }
                }
            };

            var props = new PropertySpec[]
            {
                new PropertySpec
                {
                    type = "Datacenter",
                    pathSet = new string[] { "name", "parent", "vmFolder" }
                },

                new PropertySpec
                {
                    type = "ComputeResource",
                    pathSet = new string[] { "name", "parent", "resourcePool", "host" }
                },

                new PropertySpec
                {
                    type = "HostSystem",
                    pathSet = new string[] { "configManager" }
                },

                new PropertySpec
                {
                    type = "ResourcePool",
                    pathSet = new string[] { "name", "parent", "resourcePool" }
                },

                new PropertySpec
                {
                    type = "DistributedVirtualSwitch",
                    pathSet = new string[] { "name", "parent", "uuid" }
                },

                new PropertySpec
                {
                    type = "DistributedVirtualPortgroup",
                    pathSet = new string[] { "name", "parent", "config" }
                }

            };

            ObjectSpec objectspec = new ObjectSpec();
            objectspec.obj = rootMOR;
            objectspec.selectSet = new SelectionSpec[] { plan };

            PropertyFilterSpec filter = new PropertyFilterSpec();
            filter.propSet = props;
            filter.objectSet = new ObjectSpec[] { objectspec };

            return new PropertyFilterSpec[] { filter };
        }
    }
}
