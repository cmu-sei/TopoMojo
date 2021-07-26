// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Models;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Validators;

namespace TopoMojo.Api.Controllers
{
    public class _Controller : Controller
    {
        public _Controller(
            ILogger logger,
            IHubContext<AppHub, IHubEvent> hub,
            params IModelValidator[] validators
        )
        {
            Logger = logger;
            Hub = hub;
            _validators = validators;
        }

        protected User Actor { get; set; }
        protected ILogger Logger { get; }
        protected IHubContext<AppHub, IHubEvent> Hub { get; }
        private readonly IModelValidator[] _validators;


        /// <summary>
        /// Resolve the ClaimsPrincipal User model
        /// </summary>
        /// <param name="context"></param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            Actor = User.ToModel();
        }

        /// <summary>
        /// Validate a model against all validators registered
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        protected async Task Validate(object model)
        {
            foreach (var v in _validators)
                await v.Validate(model);

        }

        /// <summary>
        /// Authorize if all requirements are met
        /// </summary>
        /// <param name="requirements"></param>
        protected void AuthorizeAll(params Func<Boolean>[] requirements)
        {
            bool valid = true;

            foreach(var requirement in requirements)
                valid &= requirement.Invoke();

            if (valid.Equals(false))
                throw new ActionForbidden();
        }

        /// <summary>
        /// Authorized if any requirement is met
        /// </summary>
        /// <param name="requirements"></param>
        protected void AuthorizeAny(params Func<Boolean>[] requirements)
        {
            if (Actor.IsAdmin)
                return;

            bool valid = false;

            foreach(var requirement in requirements)
            {
                valid |= requirement.Invoke();
                if (valid) break;
            }

            if (valid.Equals(false))
                throw new ActionForbidden();
        }

        internal void Log(string action, dynamic item, string msg = "")
        {
            string entry = String.Format("{0} [{1}] {2} {3} {4} [{5}] {6}",
                Actor?.Name, Actor?.Id, action, item?.GetType().Name, item?.Name, item?.Id, msg);

            Logger.LogInformation(entry);
        }

        // / <summary>
        // / Apply PathBase to urls generated in service
        // / </summary>
        // / <param name="target"></param>
        // internal string ApplyPathBase(string target)
        // {
        //     if (
        //         !string.IsNullOrEmpty(Request.PathBase)
        //         && !string.IsNullOrEmpty(target)
        //         && !target.Contains("://")
        //     )
        //     {
        //         return Request.PathBase + target;
        //     }

        //     return target;
        // }

    }

    public enum AuthRequirement
    {
        None,
        Self
    }
}
