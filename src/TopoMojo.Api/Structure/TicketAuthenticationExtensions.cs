// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using Microsoft.AspNetCore.Authentication;
using TopoMojo.Api;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TicketAuthenticationExtensions
    {
        public static AuthenticationBuilder AddTicketAuthentication(
            this AuthenticationBuilder builder,
            string scheme,
            Action<TicketAuthenticationOptions> options
        ) {

            builder.AddScheme<TicketAuthenticationOptions, TicketAuthenticationHandler>(
                scheme ?? TicketAuthentication.AuthenticationScheme,
                options
            );

            return builder;
        }
    }
}
