// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
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

            switch (question.Grader)
            {

                case AnswerGrader.MatchAll:
                question.IsCorrect = a.Intersect(
                    b.Split(new char[] { ' ', ',', ';', ':', '|'}, StringSplitOptions.RemoveEmptyEntries)
                ).ToArray().Length == a.Length;
                break;

                case AnswerGrader.MatchAny:
                question.IsCorrect = a.Contains(c);
                break;

                case AnswerGrader.MatchAlpha:
                question.IsCorrect = a.First().WithoutSymbols().Equals(c.WithoutSymbols());
                break;

                case AnswerGrader.Match:
                default:
                question.IsCorrect = a.First().Equals(c);
                break;
            }

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
                    q.Weight = q.Weight / total;

                max = questions.Sum(q => q.Weight);
            }

            if (unweighted.Any())
            {
                float val = (1 - max) / unweighted.Length;
                foreach(var q in unweighted.Take(unweighted.Length - 1))
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
    }
}
