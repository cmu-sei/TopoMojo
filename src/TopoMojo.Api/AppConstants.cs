// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

namespace TopoMojo.Api
{
    internal static class AppConstants
    {
        public const string Audience = "topomojo-api";
        public const string PrivilegedAudience = "topomojo-api-privileged";
        public const string AdminOnlyPolicy = "AdminOnly";
        public const string TicketOnlyPolicy = "TicketOnly";
        public const string CookiePolicy = "CookieRequired";
        public const string AnyUserPolicy = "AnyUserPolicy";
        public const string DataProtectionPurpose = "_dp:TopoMojo";
        public const string SubjectClaimName = "sub";
        public const string NameClaimName = "name";
        public const string NameIdClaimName = "nameid";
        public const string RoleClaimName = "role";
        public const string ClientIdClaimName = "client_id";
        public const string ClientScopeClaimName = "client_scope";
        public const string ClientUrlClaimName = "client_url";
        public const string UserScopeClaim = "u_scope";
        public const string UserWorkspaceLimitClaim = "u_wsl";
        public const string UserGamespaceLimitClaim = "u_gsl";
        public const string UserGamespaceMaxMinutesClaim = "u_gmm";
        public const string UserGamespaceCleanupGraceMinutesClaim = "u_gcg";
        public const string RegistrationCachePrefix = "lp:";
        public const string CookieScheme = "topomojo.mks";
        public const string MarkdownCutLine = "<!-- cut -->";
        public const string TagDelimiter = "#";
        public static char[] StringTokenSeparators = [' ', ',', ';', ':', '|', '\t'];
        public static char[] StringLineSeparators = [';', '\n', '\r'];

        public const string ErrorListCacheKey = "errbf";
    }

    internal static class AuditId
    {

    }

    public static class Message
    {
        public const string ResourceNotFound = "ResourceNotFound";
        public const string MaximumTakeExceeded = "MaximumTakeExceeded";
        public const string WorkspaceNotIsolated = "WorkspaceNotIsolated";
        public const string InvalidClientAudience = "InvalidClientAudience";
        public const string ResourceAlreadyExists = "ResourceAlreadyExists";
        public const string InvalidPropertyValue = "InvalidPropertyValue";
    }

}
