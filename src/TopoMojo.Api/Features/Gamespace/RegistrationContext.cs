// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Api.Models;

namespace TopoMojo.Api.Services
{
    public class RegistrationContext
    {
        public Data.Gamespace Gamespace { get; set; }
        public Data.Workspace Workspace { get; set; }
        public GamespaceRegistration Request { get; set; }
        public bool WorkspaceExists { get { return Workspace is Data.Workspace; } }
        public bool GamespaceExists { get { return Gamespace is Data.Gamespace; } }
    }
}
