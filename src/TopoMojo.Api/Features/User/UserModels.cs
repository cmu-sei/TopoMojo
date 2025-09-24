// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace TopoMojo.Api.Models
{
    public enum UserRole
    {
        User,
        Builder,
        Creator,
        Administrator,
        Disabled,
        Observer
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
        public UserRole AppRole { get; set; }
        public UserRole? LastIdpAssignedRole { get; set; }
        public UserRole Role { get; set; }
        public string ServiceAccountClientId { get; set; }
        public DateTimeOffset WhenCreated { get; set; }
        public bool IsAdmin => Role == UserRole.Administrator;
        public bool IsObserver =>
            Role == UserRole.Observer ||
            Role == UserRole.Creator ||
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

        public bool HasScope(string scope)
        {
            return Scope.Split(' ', ',', ';').Contains(scope);
        }
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
        public string ServiceAccountClientId { get; set; }
        public UserRole Role { get; set; }
    }

    public class UserSearch : Search
    {
        [SwaggerIgnore][JsonIgnore] public bool WantsAdmins => Filter.Contains(UserRole.Administrator.ToString().ToLower());
        [SwaggerIgnore][JsonIgnore] public bool WantsObservers => Filter.Contains(UserRole.Observer.ToString().ToLower());
        [SwaggerIgnore][JsonIgnore] public bool WantsCreators => Filter.Contains(UserRole.Creator.ToString().ToLower());
        [SwaggerIgnore][JsonIgnore] public bool WantsBuilders => Filter.Contains(UserRole.Builder.ToString().ToLower());
        [BindProperty(Name = "isServiceAccount")] public bool? IsServiceAccount { get; set; }
        [BindProperty(Name = "scope")] public string Scope { get; set; }
    }

    public class UserRegistration
    {
        public string Id { get; set; }
        public string Name { get; set; }
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

    public class OneTimeTicketResult
    {
        public string Ticket { get; set; }
    }
}
