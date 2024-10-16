// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Models
{
    public class ChallengeView
    {
        public string Text { get; set; }
        public int MaxPoints { get; set; }
        public int MaxAttempts { get; set; }
        public int Attempts { get; set; }
        public double Score { get; set; }
        public int SectionCount { get; set; }
        public int SectionIndex { get; set; }
        public double SectionScore { get; set; }
        public string SectionText { get; set; }
        public DateTimeOffset LastScoreTime { get; set; }
        public ICollection<QuestionView> Questions { get; set; } = [];
    }

    public class ChallengeProgressView
    {
        public required int Attempts { get; set; }
        public required int MaxAttempts { get; set; }
        public required int MaxPoints { get; set; }
        public required DateTimeOffset? LastScoreTime { get; set; }
        public required double Score { get; set; }
        public required VariantView Variant { get; set; }
        public required string Text { get; set; }
    }

    public class VariantView
    {
        public required string Text { get; set; }
        public required ICollection<SectionView> Sections { get; set; } = [];
        public required int TotalSectionCount { get; set; }
    }

    public class SectionView
    {
        public string Name { get; set; }
        public double PreReqPrevSection { get; set; }
        public double PreReqTotal { get; set; }
        public float Score { get; set; }
        public string Text { get; set; }
        public ICollection<QuestionView> Questions { get; set; } = [];
    }

    public class QuestionView
    {
        public string Text { get; set; }
        public string Hint { get; set; }
        public string Answer { get; set; }
        public string Example { get; set; }
        public float Weight { get; set; }
        public float Penalty { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsGraded { get; set; }
    }

    public class QuestionSetEligibility
    {
        public required int SetIndex { get; set; }
        public required bool IsComplete { get; set; }
        public required bool IsEligible { get; set; }
        public required double PreReqPrevSection { get; set; }
        public required double PreReqTotal { get; set; }
        public required double WeightScoredPreviousSection { get; set; }
        public required double WeightScoredTotal { get; set; }
    }

    public class SectionSubmission
    {
        public string Id { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public int SectionIndex { get; set; }
        public ICollection<AnswerSubmission> Questions { get; set; } = [];
    }

    public class AnswerSubmission
    {
        public string Answer { get; set; }
    }
}
