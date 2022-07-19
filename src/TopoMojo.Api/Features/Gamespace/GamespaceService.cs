// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Data.Extensions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Extensions;
using TopoMojo.Hypervisor;
using TopoMojo.Api.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace TopoMojo.Api.Services
{
    public class GamespaceService : _Service
    {
        public GamespaceService(
            ILogger<GamespaceService> logger,
            IMapper mapper,
            CoreOptions options,
            IHypervisorService podService,
            IGamespaceStore gamespaceStore,
            IWorkspaceStore workspaceStore,
            ILockService lockService,
            IDistributedCache distributedCache

        ) : base (logger, mapper, options)
        {
            _pod = podService;
            _store = gamespaceStore;
            _workspaceStore = workspaceStore;
            _locker = lockService;
            _random = new Random();
            _distCache = distributedCache;
        }

        private readonly IHypervisorService _pod;
        private readonly IGamespaceStore _store;
        private readonly IWorkspaceStore _workspaceStore;
        private readonly ILockService _locker;
        private readonly Random _random;
        private readonly IDistributedCache _distCache;

        public async Task<Gamespace[]> List(GamespaceSearch search, string subjectId, bool sudo, bool observer, string scope, CancellationToken ct = default(CancellationToken))
        {
            
            var query = (observer && search.WantsAll)
                ? _store.List(search.Term) // dashboard list - admin or observer
                : _store.ListByUser(subjectId) // side panel browser - anyone
            ;

            if (search.WantsActive)
            {
                var ts = DateTimeOffset.UtcNow;
                query = query.Where(g =>
                    g.EndTime < DateTimeOffset.MinValue.AddDays(1) &&
                    g.ExpirationTime > ts
                );
            }

            query = query.OrderByDescending(g => g.WhenCreated);

            if (search.Skip > 0)
                query = query.Skip(search.Skip);

            if (search.Take > 0)
                query = query.Take(search.Take);

            query = query.Include(g => g.Workspace);

            var data = await query.ToArrayAsync();
            
            // filter only when user is observer (but not admin)
            // select gamespaces with matching workspace audience / user scope
            if (search.WantsAll && observer && !sudo)
            { 
                // complex string splitting done after all querying completed
                data = data
                    .Where(g => g.Workspace.Audience.HasAnyToken(scope)) 
                    .ToArray();
            }

            return Mapper.Map<Gamespace[]>(data);
        }

        public async Task<GameState> Preview(string resourceId)
        {
            var ctx = await LoadContext(resourceId);

            return new GameState
            {
                Name = ctx.Workspace.Name,

                Markdown = (await LoadMarkdown(ctx.Workspace.Id)).Split("<!-- cut -->").First()
                    ?? $"# {ctx.Workspace.Name}"
            };
        }

        public async Task<GameState> Register(GamespaceRegistration request, User actor)
        {
            var gamespace = await _Register(request, actor);

            if (request.StartGamespace)
                await Deploy(gamespace, actor.IsBuilder);

            return await LoadState(gamespace, request.AllowPreview);
        }

        private async Task<TopoMojo.Api.Data.Gamespace> _Register(GamespaceRegistration request, User actor)
        {
            string playerId = request.Players.FirstOrDefault()?.SubjectId ?? actor.Id;

            var gamespace = await _store.LoadActiveByContext(
                request.ResourceId,
                playerId
            );

            if (gamespace is Data.Gamespace)
                return gamespace;

            if (! await _store.IsBelowGamespaceLimit(actor.Id, actor.GamespaceLimit))
                throw new ClientGamespaceLimitReached();

            string lockKey = $"{playerId}{request.ResourceId}";

            var ctx = await LoadContext(request);

            if (! await _locker.Lock(lockKey))
                throw new ResourceIsLocked();

            try
            {
                await Create(ctx, actor);
            }
            finally
            {
                await _locker.Unlock(lockKey);
            }

            return ctx.Gamespace;
        }

        public async Task<GameState> Load(string id, string subjectId)
        {
            var ctx = await LoadContext(id, subjectId);

            return ctx.Gamespace is Data.Gamespace
                ? await LoadState(ctx.Gamespace)
                : await LoadState(ctx.Workspace)
            ;

        }

        public async Task<ChallengeSpec> LoadChallenge(string id, bool sudo)
        {
            var entity = await _store.Retrieve(id);

            var spec = JsonSerializer.Deserialize<ChallengeSpec>(entity.Challenge, jsonOptions);

            return spec;
        }

        public async Task Update(ChangedGamespace model)
        {
            var entity = await _store.Retrieve(model.Id);

            Mapper.Map(model, entity);

            await _store.Update(entity);
        }

        public async Task<GameState> Start(string id, bool sudo = false)
        {
            var ctx = await LoadContext(id);

            if (ctx.WorkspaceExists && !ctx.Gamespace.IsExpired)
                await Deploy(ctx.Gamespace, sudo);

            return await LoadState(ctx.Gamespace);
        }

        public async Task<GameState> Stop(string id)
        {
            var ctx = await LoadContext(id);

            await _pod.DeleteAll(id);

            return await LoadState(ctx.Gamespace);
        }

        public async Task<GameState> Complete(string id)
        {
            var ctx = await LoadContext(id);

            if (ctx.Gamespace.IsActive)
            {
                ctx.Gamespace.EndTime = DateTimeOffset.UtcNow;

                await _store.Update(ctx.Gamespace);
            }

            // let janitor clean up
            // await _pod.DeleteAll(id);

            return await LoadState(ctx.Gamespace);
        }

        private async Task Create(RegistrationContext ctx, User actor)
        {

            var ts = DateTimeOffset.UtcNow;

            int duration = actor.GamespaceMaxMinutes > 0
                ? actor.GamespaceMaxMinutes
                : ctx.Workspace.DurationMinutes > 0
                    ? ctx.Workspace.DurationMinutes
                    : _options.DefaultGamespaceMinutes
            ;

            if (string.IsNullOrEmpty(ctx.Request.GraderKey))
                ctx.Request.GraderKey = Guid.NewGuid().ToString("n");

            ctx.Gamespace = new Data.Gamespace
            {
                Id = Guid.NewGuid().ToString("n"),
                Name = ctx.Workspace.Name,
                Workspace = ctx.Workspace,
                ManagerId = actor.Id,
                ManagerName = actor.Name,
                AllowReset = ctx.Request.AllowReset,
                CleanupGraceMinutes = actor.GamespaceCleanupGraceMinutes,
                WhenCreated = ts,
                ExpirationTime = ctx.Request.ResolveExpiration(ts, duration),
                PlayerCount = ctx.Request.PlayerCount > 0 ? ctx.Request.PlayerCount : ctx.Request.Players.Count(),
                GraderKey = ctx.Request.GraderKey.ToSha256()
            };

            var gamespace = ctx.Gamespace;

            foreach (var player in ctx.Request.Players)
            {
                gamespace.Players.Add(
                    new Data.Player
                    {
                        SubjectId = player.SubjectId,
                        SubjectName = player.SubjectName
                    }
                );
            }
            
            if (gamespace.Players.Any())
                gamespace.Players.First().Permission = Permission.Manager;
            
            // clone challenge
            var spec = JsonSerializer.Deserialize<ChallengeSpec>(ctx.Workspace.Challenge ?? "{}", jsonOptions);

            // select variant, adjusting from 1-based to 0-based index
            gamespace.Variant = ctx.Request.Variant > 0
                ? Math.Min(ctx.Request.Variant, spec.Variants.Count) - 1
                : _random.Next(spec.Variants.Count)
            ;

            //resolve transforms
            ResolveTransforms(spec, ctx);

            // TODO: if customize-script, run and update transforms

            spec.Challenge = spec.Variants
                .Skip(gamespace.Variant).Take(1)
                .FirstOrDefault();

            // initialize selected challenge
            spec.Challenge.SetQuestionWeights();

            spec.MaxPoints = ctx.Request.Points;

            spec.MaxAttempts = ctx.Request.MaxAttempts;

            spec.Variants = null;

            gamespace.Challenge = JsonSerializer.Serialize(spec, jsonOptions);

            // apply transforms
            foreach (var kvp in spec.Transforms)
                gamespace.Challenge = gamespace.Challenge.Replace($"##{kvp.Key}##", kvp.Value);

            await _store.Create(gamespace);

        }

        private void ResolveTransforms(ChallengeSpec spec, RegistrationContext ctx)
        {
            int index = 0;

            foreach(var kvp in spec.Transforms.ToArray())
            {
                kvp.Value = ResolveRandom(kvp.Value, ctx, index);

                if (kvp.Key.ToLower() == "index" && !Int32.TryParse(kvp.Value, out index))
                    index = 0;

                // insert `key_index: value` for any multi-token values (i.e. list-resolver)
                var tokens =  kvp.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 1)
                {
                    int i = 0;
                    foreach (string token in tokens)
                    {
                        spec.Transforms.Add(new StringKeyValue
                        {
                            Key = $"{kvp.Key}_{++i}",
                            Value = token
                        });
                    }
                }
            }

        }

        private string ResolveRandom(string key, RegistrationContext ctx, int index = 0)
        {
            byte[] buffer;

            List<string> options = new();

            string result = "";

            string[] seg = key.Split(':');

            int count = 8;

            switch (seg[0])
            {
                case "id":
                result = ctx.Gamespace.Id;
                break;

                case "variant":
                result = ctx.Gamespace.Variant.ToString();
                break;

                case "app_url":
                case "topomojo_url":
                result = ctx.Request.GraderUrl.Split("/api").First();
                break;

                case "grader_key":
                case "api_key":
                case "apikey":
                result = ctx.Request.GraderKey;
                break;

                case "grader_url":
                result = ctx.Request.GraderUrl;
                break;

                case "uid":
                result = Guid.NewGuid().ToString("n");
                break;

                case "hex":
                if (seg.Length < 2 || !int.TryParse(seg[1], out count))
                    count = 8;

                count = Math.Min(count, 256) / 2;

                buffer = new byte[count];

                _random.NextBytes(buffer);

                result = BitConverter.ToString(buffer).Replace("-", "").ToLower();

                break;

                case "b64":
                if (seg.Length < 2 || !int.TryParse(seg[1], out count))
                    count = 16;

                count = Math.Min(count, 256);

                buffer = new byte[count];

                _random.NextBytes(buffer);

                result = Convert.ToBase64String(buffer);

                break;

                case "list":
                if (seg.Length < 3 || !int.TryParse(seg[1], out count))
                    count = 1;

                options = seg.Last()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .ToList();

                while (count > 0 && options.Count > 0)
                {
                    string val = options[_random.Next(options.Count)];
                    result += val + " ";
                    options.Remove(val);
                    count -= 1;
                }

                result = result.Trim();
                break;

                case "index":
                // if value doesn't specify index, use index from prior transform
                if (seg.Length > 1 && !int.TryParse(seg[1], out count))
                    count = index;
                
                options = seg.Last()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
                
                result = options[Math.Min(count, options.Count - 1)];
                break;

                case "int":
                default:
                int min = 0;
                int max = int.MaxValue;
                if (seg.Length > 1)
                {
                    string[] range = seg[1].Split('-');
                    if (range.Length > 1)
                    {
                        int.TryParse(range[0], out min);
                        int.TryParse(range[1], out max);
                    }
                    else
                    {
                        int.TryParse(range[0], out max);
                    }
                }
                result = _random.Next(min, max).ToString();
                break;

            }

            return result;
        }

        private async Task Deploy(TopoMojo.Api.Data.Gamespace gamespace, bool sudo = false)
        {
            var tasks = new List<Task<Vm>>();

            var spec = JsonSerializer.Deserialize<ChallengeSpec>(gamespace.Challenge ?? "{}", jsonOptions);

            var isoTargets = (spec.Challenge?.Iso?.Targets ?? "")
                .ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var templates = Mapper.Map<List<ConvergedTemplate>>(
                gamespace.Workspace.Templates
                    .Where(t => t.Variant == 0 || (t.Variant - 1) == gamespace.Variant)
            );

            foreach (var template in templates.ToList())
            {
                // normalize name for variant-specific templates
                if (template.Variant > 0 && template.Name.EndsWith($"_v{template.Variant}"))
                    template.Name = template.Name.Substring(0, template.Name.LastIndexOf('_'));

                // apply template macro substitutions
                foreach (var kvp in spec.Transforms)
                {
                    template.Guestinfo = template.Guestinfo?.Replace($"##{kvp.Key}##", kvp.Value);
                    template.Detail = template.Detail?.Replace($"##{kvp.Key}##", kvp.Value);
                }

                // apply challenge iso
                if (string.IsNullOrEmpty(template.Iso) && isoTargets.Contains(template.Name.ToLower()))
                    template.Iso = $"{spec.Challenge.Iso.File}";

                // expand replicas
                int replicas = template.Replicas < 0
                    ? gamespace.PlayerCount
                    : Math.Min(template.Replicas, _options.ReplicaLimit)
                ;

                if (replicas > 1)
                {
                    for (int i = 1; i < replicas; i++)
                    {
                        var tt = template.Clone<ConvergedTemplate>();

                        tt.Name += $"_{i+1}";

                        templates.Add(tt);
                    }

                    template.Name += "_1";
                }
            }

            foreach (var template in templates)
            {
                tasks.Add(
                    _pod.Deploy(
                        template
                        .ToVirtualTemplate(gamespace.Id)
                        .SetHostAffinity(gamespace.Workspace.HostAffinity),
                        sudo
                    )
                );
            }

            await Task.WhenAll(tasks.ToArray());

            if (gamespace.Workspace.HostAffinity)
            {
                var vms = tasks.Select(t => t.Result).ToArray();

                await _pod.SetAffinity(gamespace.Id, vms, true);

                foreach (var vm in vms)
                    vm.State = VmPowerState.Running;
            }

            if (gamespace.StartTime.Year <= 1)
            {
                gamespace.StartTime = DateTimeOffset.UtcNow;
                await _store.Update(gamespace);
            }
        }

        private async Task<GameState> LoadState(Data.Workspace workspace)
        {
            return new GameState
            {
                Name = workspace.Name,
                Markdown = (await LoadMarkdown(workspace.Id)).Split("<!-- cut -->").First()
                    ?? $"# {workspace.Name}"
            };
        }
        private async Task<GameState> LoadState(TopoMojo.Api.Data.Gamespace gamespace, bool preview = false)
        {
            var state = Mapper.Map<GameState>(gamespace);

            state.Markdown = await LoadMarkdown(gamespace.Workspace?.Id)
                ?? $"# {gamespace.Name}";

            if (!preview && !gamespace.HasStarted)
            {
                state.Markdown = state.Markdown.Split("<!-- cut -->").FirstOrDefault();
            }

            if (gamespace.IsActive)
            {
                state.Vms = (await _pod.Find(gamespace.Id))
                    .Select(vm => new VmState
                    {
                        Id = vm.Id,
                        Name = vm.Name.Untagged(),
                        IsolationId = vm.Name.Tag(),
                        IsRunning = (vm.State == VmPowerState.Running),
                        IsVisible = gamespace.IsTemplateVisible(vm.Name)
                    })
                    .Where(s => s.IsVisible)
                    .OrderBy(s => s.Name)
                    .ToArray();
            }

            if (preview || gamespace.HasStarted)
            {
                var spec = JsonSerializer.Deserialize<ChallengeSpec>(gamespace.Challenge, jsonOptions);

                if (spec.Challenge == null || spec.Challenge.Sections.Count == 0)
                    return state;

                // TODO: get active question set

                // map challenge to safe model
                state.Challenge = MapChallenge(spec, 0);
            }

            return state;
        }

        public async Task Delete(string id, bool sudo)
        {
            var ctx = await LoadContext(id);

            await _pod.DeleteAll(id);

            if (!sudo && !ctx.Gamespace.AllowReset)
                throw new ActionForbidden();

            await _store.Delete(ctx.Gamespace.Id);
        }

        public async Task<Player[]> Players(string id)
        {
            return Mapper.Map<Player[]>(
                await _store.LoadPlayers(id)
            );
        }

        public async Task<JoinCode> GenerateInvitation(string id)
        {
            Task[] tasks;

            var opts = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = new TimeSpan(0, 30, 0)
            };

            // remove existing code/key
            string codekey = $"in:{id}";

            string code = await _distCache.GetStringAsync(codekey);

            if (code.NotEmpty())
            {
                tasks = new Task[] {
                    _distCache.RemoveAsync(code),
                    _distCache.RemoveAsync(codekey)
                };

                await Task.WhenAll(tasks);
            }

            // store new code/key
            code = Guid.NewGuid().ToString("n");

            tasks = new Task[] {
                _distCache.SetStringAsync(code, id, opts),
                _distCache.SetStringAsync(codekey, code, opts)
            };

            await Task.WhenAll(tasks);

            return new JoinCode
            {
                Id = id,
                Code = code
            };
        }

        public async Task Enlist(string code, User actor)
        {
            string id = await _distCache.GetStringAsync(code);

            if (id.IsEmpty())
                throw new InvalidInvitation();

            var gamespace = await _store.Load(id);

            if (gamespace.Players.Any(m => m.SubjectId == actor.Id))
                return;

            gamespace.Players.Add(new Data.Player
            {
                SubjectId = actor.Id,
                SubjectName = actor.Name,
            });

            await _store.Update(gamespace);

        }

        public async Task<Enlistment> Enlist(Enlistee model)
        {
           string id = await _distCache.GetStringAsync(model.Code);

            if (id.IsEmpty())
                throw new InvalidInvitation();

            var gamespace = await _store.Load(id);

            string token = Guid.NewGuid().ToString("n");
            string name = model.SubjectName ?? "anonymous";

            gamespace.Players.Add(new Data.Player
            {
                SubjectId = token,
                SubjectName = name
            });

            await _store.Update(gamespace);

            return new Enlistment
            {
                Token = token,
                GamespaceId = id
            };
        }

        public async Task Delist(string id, string subjectId)
        {
            await _store.DeletePlayer(id, subjectId);
        }

        public async Task<IEnumerable<SectionSubmission>> AuditSubmissions(string workspaceId)
        {
            var q = _store.List().Where(g => g.WorkspaceId == workspaceId);

            var results = new List<SectionSubmission[]>();

            foreach(var g in q)
                results.Add(
                    (await AuditSubmission(g.Id)).ToArray()
                );

            return results.SelectMany(x => x);
        }

        public async Task<ICollection<SectionSubmission>> AuditSubmission(string id)
        {
            var ctx = await LoadContext(id);

            var spec = JsonSerializer.Deserialize<ChallengeSpec>(ctx.Gamespace.Challenge, jsonOptions);

            return spec.Submissions;
        }

        public async Task RegradeAll(string workspaceId)
        {
            var q = _store.List().Where(g => g.WorkspaceId == workspaceId);

            foreach(var g in q)
                await Regrade(g.Id);
        }

        public async Task<GameState> Regrade(string id)
        {
            if (! await _locker.Lock(id))
                throw new ResourceIsLocked();

            var ctx = await LoadContext(id);

            var spec = JsonSerializer.Deserialize<ChallengeSpec>(ctx.Gamespace.Challenge, jsonOptions);

            // updating working copy from challenge spec
            var workspaceChallenge = JsonSerializer.Deserialize<ChallengeSpec>(ctx.Workspace.Challenge ?? "{}", jsonOptions);

            var variant = workspaceChallenge.Variants.Skip(ctx.Gamespace.Variant).FirstOrDefault();

            int i = 0;
            foreach (var section in spec.Challenge.Sections)
            {
                int j = 0;
                foreach (var question in section.Questions)
                {
                    var updatedSection = variant.Sections.ElementAtOrDefault(i++);
                    if (updatedSection != null)
                    {
                        var q = updatedSection.Questions.ElementAtOrDefault(j++);
                        if (q != null)
                        {
                            question.Grader = q.Grader;
                            question.Answer = q.Answer ?? "";
                        }
                    }

                }
            }


            foreach(var submission in spec.Submissions)
            {
                _Grade(spec, submission);

                if (spec.Score == 1)
                {
                    ctx.Gamespace.EndTime = submission.Timestamp;
                    break;
                }
            }

            ctx.Gamespace.Challenge = JsonSerializer.Serialize(spec, jsonOptions);

            await _store.Update(ctx.Gamespace);

            await _locker.Unlock(id);

            return await LoadState(ctx.Gamespace);
        }

        public async Task<GameState> Grade(SectionSubmission submission)
        {
            DateTimeOffset ts = DateTimeOffset.UtcNow;

            string id = submission.Id;

            if (! await _locker.Lock(id))
                throw new ResourceIsLocked();

            var ctx = await LoadContext(id);

            var spec = JsonSerializer.Deserialize<ChallengeSpec>(ctx.Gamespace.Challenge, jsonOptions);

            var section = spec.Challenge.Sections.ElementAtOrDefault(submission.SectionIndex);

            if (IsDuplicateSubmission(spec.Submissions, submission))
            {
                _locker.Unlock(id).Wait();
                return await LoadState(ctx.Gamespace);
            }

            if (section == null)
                _locker.Unlock(id, new InvalidOperationException()).Wait();

            if (!ctx.Gamespace.IsActive)
                _locker.Unlock(id, new GamespaceIsExpired()).Wait();

            if (spec.MaxAttempts > 0 && spec.Submissions.Where(s => s.SectionIndex == submission.SectionIndex).Count() >= spec.MaxAttempts)
                _locker.Unlock(id, new AttemptLimitReached()).Wait();

            submission.Timestamp = ts;
            spec.Submissions.Add(submission);

            _Grade(spec, submission);

            ctx.Gamespace.Challenge = JsonSerializer.Serialize(spec, jsonOptions);

            // handle completion if max attempts reached or full score
            if (
                spec.Score == 1 ||
                (
                    spec.MaxAttempts > 0 &&
                    spec.Submissions.Count == spec.MaxAttempts
                )
            )
            {
                ctx.Gamespace.EndTime = ts;
            }

            await _store.Update(ctx.Gamespace);

            // map return model
            var result = await LoadState(ctx.Gamespace);

            // merge submission into return model
            int i = 0;
            foreach (var question in result.Challenge.Questions)
                question.Answer = submission.Questions.ElementAtOrDefault(i++)?.Answer ?? "";

            await _locker.Unlock(id);

            return result;
        }

        private bool IsDuplicateSubmission(ICollection<SectionSubmission> history, SectionSubmission submission)
        {
            bool dupe = false;

            string incoming = string.Join('|',
                submission.Questions.Select(q => q.Answer ?? "")
            );

            foreach (var s in history)
            {
                string target = string.Join('|',
                    s.Questions.Select(q => q.Answer ?? "")
                );

                dupe |= target.Equals(incoming);

                if (dupe)
                    break;
            }

            return dupe || incoming.Replace("|", "") == string.Empty;
        }

        private void _Grade(ChallengeSpec spec, SectionSubmission submission)
        {
            var section = spec.Challenge.Sections.ElementAtOrDefault(submission.SectionIndex);

            double lastScore = spec.Score;

            int i = 0;
            foreach (var question in section.Questions)
                question.Grade(submission.Questions.ElementAtOrDefault(i++)?.Answer ?? "");

            section.Score = section.Questions
                .Where(q => q.IsCorrect)
                .Select(q => q.Weight - q.Penalty)
                .Sum()
            ;

            spec.Score = spec.Challenge.Sections
                .SelectMany(s => s.Questions)
                .Where(q => q.IsCorrect)
                .Select(q => q.Weight - q.Penalty)
                .Sum()
            ;

            if (spec.Score > lastScore)
                spec.LastScoreTime = submission.Timestamp;
        }

        private ChallengeView MapChallenge(ChallengeSpec spec, int sectionIndex = 0)
        {
            var section = spec.Challenge.Sections?.ElementAtOrDefault(sectionIndex) ?? new SectionSpec();

            var challenge = new ChallengeView
            {
                Text = string.Join("\n\n", spec.Text, spec.Challenge.Text),
                LastScoreTime = spec.LastScoreTime,
                MaxPoints = spec.MaxPoints,
                MaxAttempts = spec.MaxAttempts,
                Attempts = spec.Submissions.Count,
                Score = Math.Round(spec.Score * spec.MaxPoints, 0, MidpointRounding.AwayFromZero),
                SectionIndex = sectionIndex,
                SectionCount = spec.Challenge.Sections?.Count ?? 0,
                SectionScore = Math.Round(section.Score * spec.MaxPoints, 0, MidpointRounding.AwayFromZero),
                SectionText = section.Text,
                Questions = Mapper.Map<QuestionView[]>(section.Questions.Where(q => !q.Hidden))
            };

            foreach(var q in challenge.Questions)
            {
                q.Weight = (float) Math.Round(q.Weight * spec.MaxPoints, 0, MidpointRounding.AwayFromZero);
                q.Penalty = (float) Math.Round(q.Penalty * spec.MaxPoints, 0, MidpointRounding.AwayFromZero);
            }

            return challenge;
        }

        private async Task<RegistrationContext> LoadContext(GamespaceRegistration reg)
        {
            return new RegistrationContext
            {
                Request = reg,
                Workspace = await _workspaceStore.Load(reg.ResourceId)
            };
        }

        private async Task<RegistrationContext> LoadContext(string id, string subjectId = null)
        {
            var ctx = new RegistrationContext();

            ctx.Gamespace = await _store.Load(id);

            ctx.Workspace = ctx.Gamespace is Data.Gamespace
                ? ctx.Gamespace?.Workspace
                : await _workspaceStore.Load(id)
            ;

            // if just workspace, check for existing gamespace
            if (ctx.Gamespace is null && subjectId.NotEmpty())
            {
                ctx.Gamespace = await _store.LoadActiveByContext(
                    ctx.Workspace.Id,
                    subjectId
                );
            }

            return ctx;
        }

        private async Task<string> LoadMarkdown(string id)
        {
            string path = System.IO.Path.Combine(
                _options.DocPath,
                id
            ) + ".md";

            return id.NotEmpty() && System.IO.File.Exists(path)
                ? await System.IO.File.ReadAllTextAsync(path)
                : String.Empty
            ;
        }

        public async Task<bool> CanManage(string id, string actorId)
        {
            return await _store.CanManage(id, actorId);
        }

        public async Task<bool> CanInteract(string id, string actorId)
        {
            return await _store.CanInteract(id, actorId);
        }

        public async Task<bool> HasValidUserScope(string id, string scope, string subjectId)
        {
            scope += " " + _options.DefaultUserScope;

            return await _store.HasValidUserScope(id, scope, subjectId);
        }

        public async Task<bool> HasValidUserScopeGamespace(string id, string scope)
        {
            scope += " " + _options.DefaultUserScope;

            return await _store.HasValidUserScopeGamespace(id, scope);
        }
    }
}
