// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;

namespace TopoMojo.Hypervisor
{
    public class Vm
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }
        public string Path { get; set; }
        public string Reference { get; set; }
        public string DiskPath { get; set; }
        public string Stats { get; set; }
        public string Status { get; set; }
        public string GroupName { get; set; }
        public VmPowerState State { get; set; }
        public VmQuestion Question { get; set; }
        public VmTask Task { get; set; }
    }

    public enum VmPowerState { Off, Running, Suspended}

    public class VmQuestion
    {
        public string Id { get; set; }
        public string Prompt { get; set; }
        public string DefaultChoice { get; set; }
        public VmQuestionChoice[] Choices { get; set; }
    }

    public class VmAnswer
    {
        public string QuestionId { get; set; }
        public string ChoiceKey { get; set; }
    }

    public class VmQuestionChoice
    {
        public string Key { get; set; }
        public string Label { get; set; }
    }

    public class VmTask
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Progress { get; set; }
        public DateTimeOffset WhenCreated { get; set; }
    }

    public class VmOptions {
        public string[] Iso { get; set; }
        public string[] Net { get; set; }
    }

     public class VmOperation
    {
        public string Id { get; set; }
        public VmOperationType Type { get; set; }
        // public int WorkspaceId { get; set; }
    }

    public enum VmOperationType
    {
        Start,
        Stop,
        Save,
        Revert,
        Delete,
        Reset
    }

    public class VmConsole
    {
        public string Id { get; set; }
        public string IsolationId { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public bool IsRunning { get; set; }
    }
}
