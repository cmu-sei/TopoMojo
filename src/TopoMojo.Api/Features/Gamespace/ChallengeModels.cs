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
        public string Id { get; set; }
        public int Attempts { get; set; }
        public long ExpiresAtTimestamp { get; set; }
        public int MaxAttempts { get; set; }
        public int MaxPoints { get; set; }
        public DateTimeOffset? LastScoreTime { get; set; }
        public double? NextSectionPreReqThisSection { get; set; }
        public double? NextSectionPreReqTotal { get; set; }
        public double Score { get; set; }
        public VariantView Variant { get; set; } = new();
        public string Text { get; set; }
    }

    public class VariantView
    {
        public string Text { get; set; }
        public ICollection<SectionView> Sections { get; set; } = [];
        public int TotalSectionCount { get; set; }
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
        public int SetIndex { get; set; }
        public bool IsComplete { get; set; }
        public bool IsEligible { get; set; }
        public double PreReqPrevSection { get; set; }
        public double PreReqTotal { get; set; }
        public double WeightScoredPreviousSection { get; set; }
        public double WeightScoredTotal { get; set; }
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
