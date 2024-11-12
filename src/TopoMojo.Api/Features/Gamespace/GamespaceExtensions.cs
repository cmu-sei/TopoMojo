// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Api.Extensions;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Models
{
    public static class GamespaceExtension
    {
        public static void MergeVms(this GameState state, Vm[] vms)
        {
            foreach (Vm vm in vms)
            {
                string name = vm.Name.Untagged();
                VmState vs = state.Vms
                    .Where(t => t.Name == name && !t.Id.NotEmpty())
                    .FirstOrDefault();

                if (vs != null)
                {
                    vs.Id = vm.Id;
                    vs.IsRunning = vm.State == VmPowerState.Running;
                }
            }
        }

        public static void Grade(this QuestionSpec question, string submission)
        {
            if (string.IsNullOrWhiteSpace(submission))
                return;

            if (question.IsCorrect)
                return;

            string[] a = question.Answer.ToLower().Replace(" ", "").Split('|');
            string b = submission.ToLower();
            string c = b.Replace(" ", "");

            question.IsCorrect = question.Grader switch
            {
                AnswerGrader.MatchAll => a.Intersect(b.Split(AppConstants.StringTokenSeparators, StringSplitOptions.RemoveEmptyEntries))
                    .ToArray().Length == a.Length,
                AnswerGrader.MatchAny => a.Contains(c),
                AnswerGrader.MatchAlpha => a.First().WithoutSymbols().Equals(c.WithoutSymbols()),
                _ => a.First().Equals(c),
            };
            question.IsGraded = true;
        }

        public static void SetQuestionWeights(this VariantSpec spec)
        {
            if (spec is null)
                return;

            var questions = spec.Sections.SelectMany(s => s.Questions).ToArray();
            var unweighted = questions.Where(q => q.Weight == 0).ToArray();
            float max = questions.Sum(q => q.Weight);

            // normalize integer weights to percentage
            if (max > 1)
            {
                float total = Math.Max(max, 100);

                foreach (var q in questions)
                    q.Weight /= total;

                max = questions.Sum(q => q.Weight);
            }

            if (unweighted.Length != 0)
            {
                float val = (1 - max) / unweighted.Length;
                foreach (var q in unweighted.Take(unweighted.Length - 1))
                {
                    q.Weight = val;
                    max += val;
                }

                unweighted.Last().Weight = 1 - max;
            }
        }

        public static DateTimeOffset ResolveExpiration(this GamespaceRegistration request, DateTimeOffset ts, int max)
        {
            if (max > 0)
                request.MaxMinutes = request.MaxMinutes > 0
                    ? Math.Min(request.MaxMinutes, max)
                    : max;

            if (request.ExpirationTime == DateTimeOffset.MinValue)
                request.ExpirationTime = ts.AddMinutes(request.MaxMinutes);

            if (max > 0 && request.ExpirationTime.Subtract(ts).TotalMinutes > max)
                request.ExpirationTime = ts.AddMinutes(max);

            return request.ExpirationTime;
        }

        public static VariantView FilterSections(this VariantView variantView)
        {
            var totalWeightScored = variantView.Sections
                .SelectMany(s => s.Questions)
                .Where(q => q.IsCorrect && q.IsGraded)
                .Sum(q => q.Weight);

            // hide sections for which the player is ineligible based on score
            var availableSections = new List<SectionView>();
            var lastSectionTotal = 0f;

            foreach (var section in variantView.Sections)
            {
                var failsTotalPreReq = section.PreReqTotal > 0 && totalWeightScored < section.PreReqTotal;
                var failsPrevSectionPreReq = section.PreReqPrevSection > 0 && lastSectionTotal < section.PreReqPrevSection;

                if (!failsTotalPreReq && !failsPrevSectionPreReq)
                {
                    availableSections.Add(section);
                }

                lastSectionTotal = section
                    .Questions
                    .Where(q => q.IsCorrect && q.IsGraded)
                    .Sum(q => q.Weight);
            }

            variantView.Sections = availableSections;

            return variantView;
        }
    }
}
