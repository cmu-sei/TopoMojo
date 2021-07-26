// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;

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
        public ICollection<QuestionView> Questions { get; set; } = new List<QuestionView>();
    }

    public class SectionView
    {
        public string Text { get; set; }
        public float Score { get; set; }
        public ICollection<QuestionView> Questions { get; set; } = new List<QuestionView>();
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

    public class SectionSubmission
    {
        public string Id { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public int SectionIndex { get; set; }
        public ICollection<AnswerSubmission> Questions { get; set; } = new List<AnswerSubmission>();

    }

    public class AnswerSubmission
    {
        public string Answer { get; set; }
    }
}
