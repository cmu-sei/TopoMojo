// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Models
{
    public class ChallengeSpec
    {
        public string Text { get; set; }
        public double Score { get; set; }
        public DateTimeOffset LastScoreTime { get; set; }
        public int MaxPoints { get; set; }
        public int MaxAttempts { get; set; }
        public CustomSpec CustomizeScript { get; set; }
        public CustomSpec GradingScript { get; set; }
        public ICollection<StringKeyValue> Transforms { get; set; } = new List<StringKeyValue>();
        public ICollection<VariantSpec> Variants { get; set; } = new List<VariantSpec>();
        public ICollection<SectionSubmission> Submissions { get; set; } = new List<SectionSubmission>();
        public VariantSpec Challenge { get; set; }
    }

    public class CustomSpec
    {
        public string Image { get; set; }
        public string Script { get; set; }
    }

    public class VariantSpec
    {
        public string Text { get; set; }
        public ICollection<SectionSpec> Sections { get; set; } = new List<SectionSpec>();
        public IsoSpec Iso { get; set; }
    }

    public class IsoSpec
    {
        public string File { get; set; }
        public string Targets { get; set; }
        public ICollection<string> Manifest { get; set; } = new List<string>();
    }

    public class SectionSpec
    {
        public string Name { get; set; }
        public double PreReqPrevSection { get; set; }
        public double PreReqTotal { get; set; }
        public double Score { get; set; }
        public string Text { get; set; }
        public ICollection<QuestionSpec> Questions { get; set; } = new List<QuestionSpec>();
    }

    public class QuestionSpec
    {
        public string Text { get; set; }
        public string Hint { get; set; }
        public string Answer { get; set; }
        public string Example { get; set; }
        public float Weight { get; set; }
        public float Penalty { get; set; }
        public AnswerGrader Grader { get; set; }
        public bool Hidden { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsGraded { get; set; }
    }

    public enum AnswerGrader
    {
        Match,
        MatchAny,
        MatchAll,
        MatchAlpha
    }

    public class StringKeyValue
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }
}
