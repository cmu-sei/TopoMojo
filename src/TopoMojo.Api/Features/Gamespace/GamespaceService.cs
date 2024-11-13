// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Data.Extensions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Extensions;
using TopoMojo.Hypervisor;
using TopoMojo.Api.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text;

namespace TopoMojo.Api.Services
{
    public class GamespaceService(
        ILogger<GamespaceService> logger,
        IMapper mapper,
        CoreOptions options,
        IHypervisorService podService,
        IGamespaceStore gamespaceStore,
        IWorkspaceStore workspaceStore,
        ILockService lockService,
        IDistributedCache distributedCache
        ) : BaseService(logger, mapper, options)
    {
        private readonly IHypervisorService _pod = podService;
        private readonly IGamespaceStore _store = gamespaceStore;
        private readonly IWorkspaceStore _workspaceStore = workspaceStore;
        private readonly ILockService _locker = lockService;
        private readonly Random _random = new();
        private readonly IDistributedCache _distCache = distributedCache;

        public async Task<Gamespace[]> List(GamespaceSearch search, string subjectId, bool sudo, bool observer, string scope, CancellationToken ct = default)
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

            var data = await query.ToArrayAsync(cancellationToken: ct);

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

                Markdown = (await LoadMarkdown(ctx.Workspace.Id, true))
                    ?? $"# {ctx.Workspace.Name}"
            };
        }

        public async Task<GameState> Register(GamespaceRegistration request, User actor)
        {
            // var gamespace = await _Register(request, actor);
            string playerId = request.Players.FirstOrDefault()?.SubjectId ?? actor.Id;

            var gamespace = await _store.LoadActiveByContext(
                request.ResourceId,
                playerId
            );

            if (gamespace is null)
            {
                if (!await _store.IsBelowGamespaceLimit(actor.Id, actor.GamespaceLimit))
                    throw new ClientGamespaceLimitReached();

                string lockKey = $"{playerId}{request.ResourceId}";

                var ctx = await LoadContext(request);

                if (!await _locker.Lock(lockKey))
                    throw new ResourceIsLocked();

                try
                {
                    await Create(ctx, actor);
                    gamespace = ctx.Gamespace;
                }
                finally
                {
                    await _locker.Unlock(lockKey);
                }
            }

            if (request.StartGamespace)
                await Deploy(gamespace, actor.IsBuilder);

            return await LoadState(gamespace, request.AllowPreview);
        }

        public async Task<GameState> Load(string id, string subjectId)
        {
            var ctx = await LoadContext(id, subjectId);

            return ctx.Gamespace is not null
                ? await LoadState(ctx.Gamespace)
                : await LoadState(ctx.Workspace)
            ;

        }

        public async Task<ChallengeSpec> LoadChallenge(string id)
        {
            var entity = await _store.Retrieve(id);

            var spec = JsonSerializer.Deserialize<ChallengeSpec>(entity.Challenge, JsonOptions);

            return spec;
        }

        public async Task<ChallengeProgressView> LoadChallengeProgress(string gamespaceId)
        {
            var gamespaceEntity = await _store.Retrieve(gamespaceId);
            var spec = JsonSerializer.Deserialize<ChallengeSpec>(gamespaceEntity.Challenge, JsonOptions);
            if (spec.Challenge is null)
                return new();

            var mappedVariant = Mapper.Map<VariantView>(spec.Challenge).FilterSections();

            // only include available question sets in the output viewmodel
            var eligibility = GetQuestionSetEligibility(spec.Challenge);
            var eligibleForSetIndices = eligibility.Where(e => e.IsEligible).Select(e => e.SetIndex).ToArray();
            mappedVariant.Sections = mappedVariant.Sections.Where((s, index) => eligibleForSetIndices.Contains(index)).ToArray();

            // if any sections remain locked, note their prereqs in the model
            var nextSectionPreReqTotal = default(double?);
            var nextSectionPreReqThisSection = default(double?);
            var ineligibleSections = eligibility.Where(s => !s.IsEligible && !s.IsComplete).ToArray();

            if (ineligibleSections.Length != 0)
            {
                nextSectionPreReqThisSection = ineligibleSections[0].PreReqPrevSection;
                nextSectionPreReqTotal = ineligibleSections[0].PreReqTotal;
            }

            return new ChallengeProgressView
            {
                Id = gamespaceId,
                Attempts = spec.Submissions.Count,
                ExpiresAtTimestamp = gamespaceEntity.ExpirationTime.ToUnixTimeMilliseconds(),
                LastScoreTime = spec.LastScoreTime == DateTimeOffset.MinValue ? null : spec.LastScoreTime,
                MaxAttempts = spec.MaxAttempts,
                MaxPoints = spec.MaxPoints,
                NextSectionPreReqThisSection = nextSectionPreReqThisSection,
                NextSectionPreReqTotal = nextSectionPreReqTotal,
                Score = WeightToPoints(spec.Score, spec.MaxPoints),
                Text = string.Join("\n\n", spec.Text, spec.Challenge.Text),
                Variant = mappedVariant
            };
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
                    : CoreOptions.DefaultGamespaceMinutes
            ;

            if (string.IsNullOrEmpty(ctx.Request.GraderKey))
                ctx.Request.GraderKey = Guid.NewGuid().ToString("n");

            ctx.Gamespace = new Data.Gamespace
            {
                Id = string.Concat(
                    CoreOptions.Tenant,
                    Guid.NewGuid().ToString("n").AsSpan(CoreOptions.Tenant.Length)
                ),
                Name = ctx.Workspace.Name,
                Workspace = ctx.Workspace,
                ManagerId = actor.Id,
                ManagerName = actor.Name,
                AllowReset = ctx.Request.AllowReset,
                CleanupGraceMinutes = actor.GamespaceCleanupGraceMinutes,
                WhenCreated = ts,
                ExpirationTime = ctx.Request.ResolveExpiration(ts, duration),
                PlayerCount = ctx.Request.PlayerCount > 0 ? ctx.Request.PlayerCount : ctx.Request.Players.Length,
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

            if (gamespace.Players.Count != 0)
                gamespace.Players.First().Permission = Permission.Manager;

            // clone challenge
            var spec = JsonSerializer.Deserialize<ChallengeSpec>(ctx.Workspace.Challenge ?? "{}", JsonOptions);

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

            StringBuilder sb = new(
                JsonSerializer.Serialize(spec, JsonOptions)
            );

            // apply transforms
            foreach (var kvp in spec.Transforms)
                sb.Replace($"##{kvp.Key}##", kvp.Value);

            gamespace.Challenge = sb.ToString();

            await _store.Create(gamespace);
        }

        private void ResolveTransforms(ChallengeSpec spec, RegistrationContext ctx)
        {
            int index = 0;

            foreach (var kvp in spec.Transforms.ToArray())
            {
                kvp.Value = ResolveRandom(kvp.Value, ctx, index);

                if (kvp.Key.Equals("index", StringComparison.CurrentCultureIgnoreCase) && !int.TryParse(kvp.Value, out index))
                    index = 0;

                // insert `key_index: value` for any multi-token values (i.e. list-resolver)
                var tokens = kvp.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);
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

            List<string> options;

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

                    options = [.. seg.Last().Split(' ', StringSplitOptions.RemoveEmptyEntries)];

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

                    options = [.. seg.Last().Split(' ', StringSplitOptions.RemoveEmptyEntries)];

                    result = options[Math.Min(count, options.Count - 1)];
                    break;

                case "ipv4":
                    result = seg.Last().ToRandomIPv4();
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
                            if (!int.TryParse(range[0], out min)) min = 0;
                            if (!int.TryParse(range[1], out max)) max = int.MaxValue;
                        }
                        else
                        {
                            if (!int.TryParse(range[0], out max)) max = int.MaxValue;
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

            var spec = JsonSerializer.Deserialize<ChallengeSpec>(gamespace.Challenge ?? "{}", JsonOptions);

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
                    template.Name = template.Name[..template.Name.LastIndexOf('_')];

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
                    : Math.Min(template.Replicas, CoreOptions.ReplicaLimit)
                ;

                if (replicas > 1)
                {
                    for (int i = 1; i < replicas; i++)
                    {
                        var tt = template.Clone<ConvergedTemplate>();

                        tt.Name += $"_{i + 1}";

                        templates.Add(tt);
                    }

                    template.Name += "_1";
                }
            }

            await _pod.Deploy(new DeploymentContext(
                gamespace.Id,
                gamespace.Workspace.HostAffinity,
                sudo,
                templates.Select(t => t
                        .ToVirtualTemplate(gamespace.Id)
                        .SetHostAffinity(gamespace.Workspace.HostAffinity)
                ).ToArray()
            ), true);
            // TODO: allow clients to specify `WaitForDeployment` bool #27

            for (int i = 0; i < 18; i++)
            {
                await Task.Delay(5000);
                var existing = await _pod.Find(gamespace.Id);
                if (existing.Length == templates.Count)
                {
                    if (gamespace.StartTime.Year <= 1)
                    {
                        gamespace.StartTime = DateTimeOffset.UtcNow;
                        await _store.Update(gamespace);
                    }
                    break;
                }
            }
        }

        private async Task<GameState> LoadState(Data.Workspace workspace)
        {
            return new GameState
            {
                Name = workspace.Name,
                Markdown = (await LoadMarkdown(workspace.Id, true))
                    ?? $"# {workspace.Name}"
            };
        }

        private async Task<GameState> LoadState(TopoMojo.Api.Data.Gamespace gamespace, bool preview = false)
        {
            var state = Mapper.Map<GameState>(gamespace);

            state.Markdown = await LoadMarkdown(gamespace.Workspace?.Id, false)
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
                        IsRunning = vm.State == VmPowerState.Running,
                        IsVisible = gamespace.IsTemplateVisible(vm.Name)
                    })
                    .Where(s => s.IsVisible)
                    .OrderBy(s => s.Name)
                    .ToArray();
            }

            if (preview || gamespace.HasStarted)
            {
                var spec = JsonSerializer.Deserialize<ChallengeSpec>(gamespace.Challenge, JsonOptions);

                if (spec.Challenge == null || spec.Challenge.Sections.Count == 0)
                    return state;

                var questionSetEligibility = GetQuestionSetEligibility(spec.Challenge);
                // this model only returns info about the "active" question set", so select the lowest indexed question set we haven't solved
                var activeSectionIndex = questionSetEligibility
                    .Where(e => e.IsEligible && !e.IsComplete)
                    .OrderBy(e => e.SetIndex)
                    .FirstOrDefault()?.SetIndex ?? 0;

                // map challenge to safe model
                state.Challenge = MapChallengeView(spec, gamespace.Variant, activeSectionIndex);
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
                tasks = [
                    _distCache.RemoveAsync(code),
                    _distCache.RemoveAsync(codekey)
                ];

                await Task.WhenAll(tasks);
            }

            // store new code/key
            code = Guid.NewGuid().ToString("n");

            tasks = [
                _distCache.SetStringAsync(code, id, opts),
                _distCache.SetStringAsync(codekey, code, opts)
            ];

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

            foreach (var g in q)
                results.Add(
                    [.. (await AuditSubmission(g.Id))]
                );

            return results.SelectMany(x => x);
        }

        public async Task<ICollection<SectionSubmission>> AuditSubmission(string id)
        {
            var ctx = await LoadContext(id);

            var spec = JsonSerializer.Deserialize<ChallengeSpec>(ctx.Gamespace.Challenge, JsonOptions);

            return spec.Submissions;
        }

        public async Task RegradeAll(string workspaceId)
        {
            var q = _store.List().Where(g => g.WorkspaceId == workspaceId);

            foreach (var g in q)
                await Regrade(g.Id);
        }

        public async Task<GameState> Regrade(string id)
        {
            if (!await _locker.Lock(id))
                throw new ResourceIsLocked();

            var ctx = await LoadContext(id);

            var spec = JsonSerializer.Deserialize<ChallengeSpec>(ctx.Gamespace.Challenge, JsonOptions);

            // updating working copy from challenge spec
            var workspaceChallenge = JsonSerializer.Deserialize<ChallengeSpec>(ctx.Workspace.Challenge ?? "{}", JsonOptions);

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

            foreach (var submission in spec.Submissions)
            {
                Grade(spec, submission);

                if (spec.Score == 1)
                {
                    ctx.Gamespace.EndTime = submission.Timestamp;
                    break;
                }
            }

            ctx.Gamespace.Challenge = JsonSerializer.Serialize(spec, JsonOptions);

            await _store.Update(ctx.Gamespace);

            await _locker.Unlock(id);

            return await LoadState(ctx.Gamespace);
        }

        public async Task<GameState> Grade(SectionSubmission submission)
        {
            DateTimeOffset ts = DateTimeOffset.UtcNow;

            string id = submission.Id;

            if (!await _locker.Lock(id))
                throw new ResourceIsLocked();

            var ctx = await LoadContext(id);

            var spec = JsonSerializer.Deserialize<ChallengeSpec>(ctx.Gamespace.Challenge, JsonOptions);

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

            var challengeEligibility = GetQuestionSetEligibility(spec.Challenge);
            var setEligibility = challengeEligibility.Single(e => e.SetIndex == submission.SectionIndex);

            if (!setEligibility.IsEligible)
            {
                await _locker.Unlock(id, new QuestionSetLockedByPreReq($"Can't grade gamespace {submission.Id} / section {submission.SectionIndex} due to set eligibility ({setEligibility.PreReqPrevSection} / {setEligibility.PreReqTotal})"));
            }

            submission.Timestamp = ts;
            spec.Submissions.Add(submission);

            Grade(spec, submission);

            ctx.Gamespace.Challenge = JsonSerializer.Serialize(spec, JsonOptions);

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

        private static bool IsDuplicateSubmission(ICollection<SectionSubmission> history, SectionSubmission submission)
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

        private static void Grade(ChallengeSpec spec, SectionSubmission submission)
        {
            var section = spec.Challenge.Sections.ElementAtOrDefault(submission.SectionIndex);
            var lastScore = spec.Score;

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

        private static QuestionSetEligibility[] GetQuestionSetEligibility(VariantSpec variant)
        {
            var previousSectionTotal = 0f;
            var retVal = new List<QuestionSetEligibility>();
            var totalWeightScored = variant
                .Sections
                .SelectMany(s => s.Questions)
                .Where(q => q.IsCorrect && q.IsGraded)
                .Sum(q => q.Weight);

            for (var i = 0; i < variant.Sections.Count; i++)
            {
                var currentSection = variant.Sections.ElementAt(i);
                var passesTotalPreReq = currentSection.PreReqTotal == 0 || totalWeightScored >= currentSection.PreReqTotal;
                var passesPrevSectionPreReq = currentSection.PreReqPrevSection == 0 || previousSectionTotal >= currentSection.PreReqPrevSection;

                retVal.Add(new QuestionSetEligibility
                {
                    SetIndex = i,
                    IsComplete = currentSection.Questions.All(q => q.IsCorrect),
                    IsEligible = passesPrevSectionPreReq && passesTotalPreReq,
                    PreReqPrevSection = currentSection.PreReqPrevSection,
                    PreReqTotal = currentSection.PreReqTotal,
                    WeightScoredPreviousSection = previousSectionTotal,
                    WeightScoredTotal = totalWeightScored
                });

                previousSectionTotal = currentSection
                    .Questions
                    .Where(q => q.IsCorrect && q.IsGraded)
                    .Sum(q => q.Weight);
            }

            return [.. retVal];

        }

        private ChallengeView MapChallengeView(ChallengeSpec spec, int variantIndex, int sectionIndex)
        {
            if (variantIndex > spec.Variants.Count)
            {
                variantIndex = 0;
            }

            var variant = spec.Variants.ElementAt(variantIndex);
            var section = spec.Challenge.Sections?.ElementAtOrDefault(sectionIndex) ?? new SectionSpec();

            var challenge = new ChallengeView
            {
                Text = string.Join("\n\n", spec.Text, spec.Challenge.Text),
                LastScoreTime = spec.LastScoreTime,
                MaxPoints = spec.MaxPoints,
                MaxAttempts = spec.MaxAttempts,
                Attempts = spec.Submissions.Count,
                Score = WeightToPoints(spec.Score, spec.MaxPoints),
                SectionIndex = sectionIndex,
                SectionCount = spec.Challenge.Sections?.Count ?? 0,
                SectionScore = WeightToPoints(section.Score, spec.MaxPoints),
                SectionText = section.Text,
                Questions = Mapper.Map<QuestionView[]>(section.Questions.Where(q => !q.Hidden))
            };

            foreach (var q in challenge.Questions)
            {
                q.Penalty = WeightToPoints(q.Penalty, spec.MaxPoints);
                q.Weight = WeightToPoints(q.Weight, spec.MaxPoints);
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
            RegistrationContext ctx = new()
            {
                Gamespace = await _store.Load(id)
            };

            ctx.Workspace = ctx.Gamespace is not null
                ? ctx.Gamespace.Workspace
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

        private async Task<string> LoadMarkdown(string id, bool aboveCut)
        {
            string path = System.IO.Path.Combine(
                CoreOptions.DocPath,
                id
            ) + ".md";

            string text = id.NotEmpty() && System.IO.File.Exists(path)
                ? await System.IO.File.ReadAllTextAsync(path)
                : null
            ;

            return aboveCut
                ? text?.Split(AppConstants.MarkdownCutLine).First()
                : text
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
            scope += " " + CoreOptions.DefaultUserScope;

            return await _store.HasValidUserScope(id, scope, subjectId);
        }

        public async Task<bool> HasValidUserScopeGamespace(string id, string scope)
        {
            scope += " " + CoreOptions.DefaultUserScope;

            return await _store.HasValidUserScopeGamespace(id, scope);
        }

        private static int WeightToPoints(double weight, double maxPoints)
            => (int)Math.Round(weight * maxPoints, 0, MidpointRounding.AwayFromZero);
    }
}
