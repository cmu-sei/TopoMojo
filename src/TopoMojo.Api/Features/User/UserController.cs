// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using TopoMojo.Api.Validators;

namespace TopoMojo.Api.Controllers
{
    [Authorize]
    [ApiController]
    public class UserController : _Controller
    {
        public UserController(
            ILogger<AdminController> logger,
            IHubContext<AppHub, IHubEvent> hub,
            UserValidator validator,
            UserService userService,
            IDistributedCache distributedCache
        ) : base(logger, hub, validator)
        {
            _svc = userService;
            _distCache = distributedCache;
            _random = new Random();
            _cacheOpts = new DistributedCacheEntryOptions {
                AbsoluteExpirationRelativeToNow = new TimeSpan(0, 0, 30)
            };
        }

        private readonly UserService _svc;
        private readonly IDistributedCache _distCache;
        private readonly Random _random;
        private DistributedCacheEntryOptions _cacheOpts;

        /// <summary>
        /// List users. (admin only)
        /// </summary>
        /// <param name="model"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("api/users")]
        [SwaggerOperation(OperationId = "ListUsers")]
        [Authorize(AppConstants.AdminOnlyPolicy)]
        public async Task<ActionResult<User[]>> ListUsers([FromQuery]UserSearch model, CancellationToken ct)
        {
            await Validate(model);

            AuthorizeAny(
                () => Actor.IsAdmin
            );

            return Ok(
                await _svc.List(model, ct)
            );
        }

        [HttpGet("api/user/scopes")]
        [SwaggerOperation(OperationId = "ListAllScopes")]
        [Authorize(AppConstants.AdminOnlyPolicy)]
        public async Task<ActionResult<string[]>> ListAllScopes(CancellationToken ct)
        {
            AuthorizeAny(
                () => Actor.IsAdmin
            );

            return Ok(
                await _svc.ListScopes()
            );
        }

        /// <summary>
        /// Get user profile.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("api/user/{id?}")]
        [SwaggerOperation(OperationId = "LoadUser")]
        [Authorize]
        public async Task<ActionResult<User>> LoadUser(string id)
        {
            id = id ?? Actor.Id;

            if (id == Actor.Id)
                return Ok(Actor);

            await Validate(new Entity{ Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => id == Actor.Id
            );

            return Ok(
                await _svc.Load(id)
            );
        }

        /// <summary>
        /// Get user's workspaces.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("api/user/{id}/workspaces")]
        [SwaggerOperation(OperationId = "LoadUserWorkspaces")]
        [Authorize]
        public async Task<ActionResult<WorkspaceSummary[]>> LoadUserWorkspaces(string id)
        {
            await Validate(new Entity{ Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => id == Actor.Id
            );

            return Ok(
                await _svc.LoadWorkspaces(id)
            );
        }

        /// <summary>
        /// Get user's gamespaces.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("api/user/{id}/gamespaces")]
        [SwaggerOperation(OperationId = "LoadUserGamespaces")]
        [Authorize]
        public async Task<ActionResult<WorkspaceSummary[]>> LoadUserGamespaces(string id)
        {
            await Validate(new Entity{ Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => id == Actor.Id
            );

            return Ok(
                await _svc.LoadGamespaces(id)
            );
        }

        /// <summary>
        /// Update user profile
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost("api/user")]
        [SwaggerOperation(OperationId = "AddOrUpdateUser")]
        [Authorize(AppConstants.AdminOnlyPolicy)]
        public async Task<ActionResult<User>> AddOrUpdateUser([FromBody]ChangedUser model)
        {
            AuthorizeAny(
                () => Actor.IsAdmin
            );

            return Ok(
                await _svc.AddOrUpdate(model)
            );
        }

        /// <summary>
        /// Delete user.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("api/user/{id}")]
        [SwaggerOperation(OperationId = "DeleteUser")]
        [Authorize]
        public async Task<IActionResult> DeleteUser(string id)
        {
            await Validate(new Entity{ Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => id == Actor.Id
            );

            await _svc.Delete(id);

            return Ok();
        }

        /// <summary>
        /// Get a user's api key records (no values)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("api/user/{id}/keys")]
        [SwaggerOperation(OperationId = "GetUserKeys")]
        [Authorize(AppConstants.AdminOnlyPolicy)]
        public async Task<ActionResult<ApiKey[]>> GetUserKeys(string id)
        {
            await Validate(new Entity { Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin
            );

            return Ok(
                await _svc.LoadUserKeys(id)
            );
        }

        /// <summary>
        /// Generate an ApiKey
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost("api/apikey/{id}")]
        [SwaggerOperation(OperationId = "CreateUserKey")]
        [Authorize(AppConstants.AdminOnlyPolicy)]
        public async Task<ActionResult<ApiKeyResult>> CreateUserKey(string id)
        {
            await Validate(new Entity { Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin
            );

            return Ok(
                await _svc.CreateApiKey(id, Actor.Name)
            );
        }

        /// <summary>
        /// Delete an ApiKey
        /// </summary>
        /// <param name="keyId"></param>
        /// <returns></returns>
        [HttpDelete("api/apikey/{keyId}")]
        [SwaggerOperation(OperationId = "DeleteUserKey")]
        [Authorize(AppConstants.AdminOnlyPolicy)]
        public async Task<ActionResult> DeleteUserKey(string keyId)
        {

            AuthorizeAny(
                () => Actor.IsAdmin
            );

            await _svc.DeleteApiKey(keyId);

            return Ok();
        }

        /// <summary>
        /// Add or Update the actors user record
        /// </summary>
        /// <returns></returns>
        [HttpPost("api/user/register")]
        [Authorize]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<User> RegisterUser()
        {
            var user = await _svc.AddOrUpdate(new UserRegistration
            {
                Id = Actor.Id,
                Name = Actor.Name
            });

            await HttpContext.SignInAsync(
                AppConstants.CookieScheme,
                new ClaimsPrincipal(
                    new ClaimsIdentity(User.Claims, AppConstants.CookieScheme)
                )
            );

            return user;
        }

        /// <summary>
        /// Get one-time auth ticket.
        /// </summary>
        /// <remarks>
        /// Client websocket connections can be authenticated with this ticket
        /// in an `Authorization: Ticket [ticket]` or `Authorization: Bearer [ticket]` header.
        /// </remarks>
        /// <returns></returns>
        [HttpGet("/api/user/ticket")]
        [Authorize]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> GetOneTimeTicket()
        {
            string token = Guid.NewGuid().ToString("n");

            string key = $"{TicketAuthentication.TicketCachePrefix}{token}";

            string value = $"{Actor.Id}#{Actor.Name}";

            await _distCache.SetStringAsync(key, value, _cacheOpts);

            return Ok(new { Ticket = token });
        }

        /// <summary>
        /// Get auth cookie
        /// </summary>
        /// <remarks>
        /// Used to exhange one-time-ticket for an auth cookie.
        /// Also gives jwt users cookie for vm console auth.
        /// </remarks>
        /// <returns></returns>
        [HttpPost("/api/user/login")]
        [Authorize(AppConstants.AnyUserPolicy)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> GetAuthCookie()
        {
            // if (User.Identity.AuthenticationType == AppConstants.CookieScheme)
            //     return Ok();

            await HttpContext.SignInAsync(
                AppConstants.CookieScheme,
                new ClaimsPrincipal(
                    new ClaimsIdentity(User.Claims, AppConstants.CookieScheme)
                ),
                new AuthenticationProperties()
            );

            return Ok();
        }

        /// <summary>
        /// End a cookie auth session
        /// </summary>
        /// <returns></returns>
        [HttpPost("/api/user/logout")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize(AppConstants.CookiePolicy)]
        public async Task Logout()
        {
            if (User.Identity.AuthenticationType == AppConstants.CookieScheme)
                await HttpContext.SignOutAsync(AppConstants.CookieScheme);
        }

        /// <summary>
        /// Auth check fails if cookie expired
        /// </summary>
        /// <returns></returns>
        [HttpGet("/api/user/ping")]
        [Authorize(AppConstants.CookiePolicy)]
        public IActionResult Heartbeat()
        {
            return Ok();
        }

    }

}
