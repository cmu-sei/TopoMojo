// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;

namespace TopoMojo.Api.Models
{
    public enum UserRole
    {
        User,
        Builder,
        Creator,
        Administrator,
        Disabled
    }

    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Scope { get; set; }
        public int WorkspaceLimit { get; set; }
        public int GamespaceLimit { get; set; }
        public int GamespaceMaxMinutes { get; set; }
        public int GamespaceCleanupGraceMinutes { get; set; }
        public UserRole Role { get; set; }
        public DateTimeOffset WhenCreated { get; set; }
        public bool IsAdmin =>
            Role == UserRole.Administrator
        ;
        public bool IsCreator =>
            Role == UserRole.Creator ||
            Role == UserRole.Administrator
        ;
        public bool IsBuilder =>
            Role == UserRole.Builder ||
            Role == UserRole.Creator ||
            Role == UserRole.Administrator
        ;
    }

    public class ChangedUser
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Scope { get; set; }
        public int WorkspaceLimit { get; set; }
        public int GamespaceLimit { get; set; }
        public int GamespaceMaxMinutes { get; set; }
        public int GamespaceCleanupGraceMinutes { get; set; }
        public UserRole Role { get; set; }
    }

    public class UserSearch: Search
    {
        public bool WantsAdmins => Filter.Contains(UserRole.Administrator.ToString().ToLower());
        public bool WantsCreators => Filter.Contains(UserRole.Creator.ToString().ToLower());
        public bool WantsBuilders => Filter.Contains(UserRole.Builder.ToString().ToLower());
        public string scope { get; set; }
    }

    public class UserRegistration
    {
        public string Id { get; set; }
        public string Name{ get; set; }
    }

    public class ApiKeyResult
    {
        public string Value { get; set; }
    }

    public class ApiKey
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTimeOffset WhenCreated { get; set; }
    }
}
