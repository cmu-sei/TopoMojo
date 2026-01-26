// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace TopoMojo.Api.Features.Health
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        [HttpGet("version")]
        [AllowAnonymous]
        public ActionResult<string> Version()
        {
            var asm = typeof(HealthController).Assembly;

            var ver = asm.GetName().Version;
            var v = ver is null ? "unknown" : ver.ToString(3);
            return Ok(v);

        }

    }
}
