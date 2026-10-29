// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using Xunit;

namespace TopoMojo.Api.Tests;

/// <summary>
/// Pins the challenge penalty/scoring semantics: penalty is a 0-1 fraction of the
/// question mark (NOT normalized against the challenge total), deducted per prior
/// wrong try and floored at 0, counting only wrong tries made before the question
/// was first answered correctly.
/// </summary>
public sealed class PenaltyScoringTests
{
    // ---- SetQuestionWeights: weight is normalized, penalty is preserved as a fraction ----

    [Fact]
    public void SetQuestionWeights_NormalizesWeight_ButPreservesPenaltyFraction()
    {
        var variant = new VariantSpec
        {
            Sections =
            {
                new SectionSpec
                {
                    Questions =
                    {
                        new QuestionSpec { Weight = 50f, Penalty = 0.1f, Answer = "a" },
                        new QuestionSpec { Weight = 50f, Penalty = 0.2f, Answer = "b" },
                    },
                },
            },
        };

        variant.SetQuestionWeights();

        var qs = variant.Sections.First().Questions.ToArray();

        // 0-100 scale weights normalize to 0-1 (50 / 100 = 0.5).
        Assert.True(Math.Abs(qs[0].Weight - 0.5f) < 0.001f, $"weight was {qs[0].Weight}");
        Assert.True(Math.Abs(qs[1].Weight - 0.5f) < 0.001f, $"weight was {qs[1].Weight}");

        // Penalty is a fraction of the question's own mark and must NOT be divided
        // by the challenge total - it passes through unchanged.
        Assert.True(Math.Abs(qs[0].Penalty - 0.1f) < 0.001f, $"penalty was {qs[0].Penalty}");
        Assert.True(Math.Abs(qs[1].Penalty - 0.2f) < 0.001f, $"penalty was {qs[1].Penalty}");
    }

    // ---- IsMatch: single source of truth for answer matching ----

    [Theory]
    [InlineData("cp", "cp", true)]
    [InlineData("cp", "CP", true)]
    [InlineData("cp", "  cp  ", true)]
    [InlineData("cp", "zz", false)]
    [InlineData("cp", "", false)]
    [InlineData("cp", null, false)]
    public void IsMatch_ExactMatchGrader(string answer, string submission, bool expected)
    {
        var q = new QuestionSpec { Answer = answer, Grader = AnswerGrader.Match };
        Assert.Equal(expected, q.IsMatch(submission));
    }

    // ---- CountIncorrectAttempts: order-aware, stops at first correct ----

    [Fact]
    public void CountIncorrectAttempts_CorrectFirstTry_IsZero()
    {
        var question = new QuestionSpec { Answer = "cp", Grader = AnswerGrader.Match };
        var spec = new ChallengeSpec { Submissions = { Submission(0, 1, "cp") } };

        Assert.Equal(0, GamespaceService.CountIncorrectAttempts(spec, 0, 0, question));
    }

    [Fact]
    public void CountIncorrectAttempts_TwoWrongThenCorrect_IsTwo()
    {
        var question = new QuestionSpec { Answer = "cp", Grader = AnswerGrader.Match };
        var spec = new ChallengeSpec
        {
            Submissions =
            {
                Submission(0, 1, "zz"),
                Submission(0, 2, "yy"),
                Submission(0, 3, "cp"),
            },
        };

        Assert.Equal(2, GamespaceService.CountIncorrectAttempts(spec, 0, 0, question));
    }

    [Fact]
    public void CountIncorrectAttempts_IgnoresWrongSubmissionsAfterCorrect()
    {
        // Regression for the order-agnostic over-penalization bug: once a question is
        // correct it is locked, so a later submission resending a wrong value for it
        // (e.g. while working other questions in the section) must not add penalty.
        var question = new QuestionSpec { Answer = "cp", Grader = AnswerGrader.Match };
        var spec = new ChallengeSpec
        {
            Submissions =
            {
                Submission(0, 1, "zz"),   // wrong  -> counts
                Submission(0, 2, "cp"),   // correct -> stop
                Submission(0, 3, "oops"), // post-correct wrong -> must NOT count
            },
        };

        Assert.Equal(1, GamespaceService.CountIncorrectAttempts(spec, 0, 0, question));
    }

    [Fact]
    public void CountIncorrectAttempts_OrdersByTimestamp_NotInsertionOrder()
    {
        // The correct answer is inserted first but has the latest timestamp; ordering
        // by timestamp must place the two earlier wrong tries before it.
        var question = new QuestionSpec { Answer = "cp", Grader = AnswerGrader.Match };
        var spec = new ChallengeSpec
        {
            Submissions =
            {
                Submission(0, 3, "cp"),
                Submission(0, 1, "zz"),
                Submission(0, 2, "yy"),
            },
        };

        Assert.Equal(2, GamespaceService.CountIncorrectAttempts(spec, 0, 0, question));
    }

    private static SectionSubmission Submission(int sectionIndex, int minute, string answer) => new()
    {
        SectionIndex = sectionIndex,
        Timestamp = new DateTimeOffset(2026, 6, 9, 0, minute, 0, TimeSpan.Zero),
        Questions = { new AnswerSubmission { Answer = answer } },
    };
}
