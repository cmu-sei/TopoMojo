// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TopoMojo.Api.Models;
using Microsoft.AspNetCore.SignalR;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Validators;

namespace TopoMojo.Api.Controllers
{
    public class BaseController(
        ILogger logger,
        IHubContext<AppHub, IHubEvent> hub
        ) : Controller
    {
        protected User Actor { get; set; }
        protected ILogger Logger { get; } = logger;
        protected IHubContext<AppHub, IHubEvent> Hub { get; } = hub;


        /// <summary>
        /// Resolve the ClaimsPrincipal User model
        /// </summary>
        /// <param name="context"></param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            Actor = User.ToModel();
        }

        /// <summary>
        /// Authorize if all requirements are met
        /// </summary>
        /// <param name="requirements"></param>
        protected bool AuthorizeAll(params Func<Boolean>[] requirements)
        {
            bool valid = true;

            foreach(var requirement in requirements)
                valid &= requirement.Invoke();

            return valid;
        }

        /// <summary>
        /// Authorized if any requirement is met
        /// </summary>
        /// <param name="requirements"></param>
        protected bool AuthorizeAny(params Func<Boolean>[] requirements)
        {
            if (Actor.IsAdmin)
                return true;

            bool valid = false;

            foreach(var requirement in requirements)
            {
                valid |= requirement.Invoke();
                if (valid) break;
            }

            return valid;
        }

        internal void Log(string action, dynamic item, string msg = "")
        {
            string entry = String.Format("{0} [{1}] {2} {3} {4} [{5}] {6}",
                Actor?.Name, Actor?.Id, action, item?.GetType().Name, item?.Name, item?.Id, msg);

            Logger.LogInformation(entry);
        }

    }

}
