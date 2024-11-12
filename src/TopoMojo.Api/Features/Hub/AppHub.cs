// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Hubs
{
    public interface IHubEvent
    {
        Task GlobalEvent(BroadcastEvent<string> broadcastEvent);
        Task TopoEvent(BroadcastEvent<Workspace> broadcastEvent);
        Task TemplateEvent(BroadcastEvent<Template> broadcastEvent);
        Task DocumentEvent(BroadcastEvent<object> broadcastEvent);
        Task DocumentEvent(BroadcastEvent<Document> broadcastEvent);
        Task VmEvent(BroadcastEvent<VmState> broadcastEvent);
        Task PresenceEvent(BroadcastEvent broadcastEvent);
        Task GameEvent(BroadcastEvent<GameState> broadcastEvent);
        Task DispatchEvent(BroadcastEvent<Dispatch> broadcastEvent);
    }

    public interface IHubAction
    {
        Task Listen(string id);
        Task Leave(string id);
        Task Greet(string id);
        // Task Typing(string id, bool value);
        Task TemplateMessage(string action, Template model);
    }

    [Authorize(AppConstants.TicketOnlyPolicy)]
    public class AppHub(
        ILogger<AppHub> logger,
        IUserStore userStore,
        HubCache cache
        ) : Hub<IHubEvent>, IHubAction
    {
        private readonly ILogger<AppHub> _logger = logger;
        private readonly HubCache _cache = cache;
        private readonly IUserStore _store = userStore;

        public async override Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();

            _logger.LogDebug("connected {name} {uid} {cid}",
                Context.User.FindFirstValue("name"),
                Context.UserIdentifier,
                Context.ConnectionId
            );
        }

        public override async Task OnDisconnectedAsync(Exception ex)
        {
            await base.OnDisconnectedAsync(ex);

            string channelId = Context.Items["channelId"]?.ToString();

            if (!string.IsNullOrEmpty(channelId))
                await Leave(channelId);
            _cache.Connections.TryRemove(Context.ConnectionId, out _);
        }

        public Task Listen(string channelId)
        {
            var actor = Context.User.ToModel();

            // a gamespace apikey yields a user.id == gamespace.id
            if (
                !actor.IsAdmin &&
                Context.UserIdentifier != channelId &&
                !_store.CanInteract(Context.UserIdentifier, channelId).Result
            ) {
                throw new ActionForbidden();
            }

            Groups.AddToGroupAsync(Context.ConnectionId, channelId);

            if (!Context.Items.ContainsKey("channelId"))
                Context.Items.Add("channelId", channelId);

            _cache.Connections.TryAdd(Context.ConnectionId,
                new CachedConnection
                {
                    Id = Context.ConnectionId,
                    ProfileId = actor.Id,
                    ProfileName = actor.Name,
                    Room = channelId
                }
            );

            return Clients.OthersInGroup(channelId).PresenceEvent(new BroadcastEvent(Context.User, "PRESENCE.ARRIVED"));
        }

        public Task Leave(string channelId)
        {
            _logger.LogDebug("leave {channelId} {name} {cid}",
                channelId,
                Context.User?.Identity.Name,
                Context.ConnectionId
            );

            Groups.RemoveFromGroupAsync(Context.ConnectionId, channelId);

            Context.Items.Remove("channelId");

            _cache.Connections.TryRemove(Context.ConnectionId, out CachedConnection _);

            return Clients.OthersInGroup(channelId).PresenceEvent(new BroadcastEvent(Context.User, "PRESENCE.DEPARTED"));
        }

        public Task Greet(string channelId)
        {
            return Clients.OthersInGroup(channelId).PresenceEvent(new BroadcastEvent(Context.User, "PRESENCE.GREETED"));
        }

        // public Task Typing(string channelId, bool val)
        // {
        //     return Clients.OthersInGroup(channelId).ChatEvent(new BroadcastEvent<Message>(Context.User, (val) ? "CHAT.TYPING" : "CHAT.IDLE", null));
        // }

        public Task TemplateMessage(string action, Template model){
            return Clients.OthersInGroup(model.WorkspaceId).TemplateEvent(new BroadcastEvent<Template>(Context.User, action, model));
        }

        public Task Edited(string channelId, object edits)
        {
            return Clients.OthersInGroup(channelId).DocumentEvent(new BroadcastEvent<object>(Context.User, "DOCUMENT.UPDATED", edits));
        }

        public Task CursorChanged(string channelId, object positions)
        {
            return Clients.OthersInGroup(channelId).DocumentEvent(new BroadcastEvent<object>(Context.User, "DOCUMENT.CURSOR", positions));
        }

    }
}
