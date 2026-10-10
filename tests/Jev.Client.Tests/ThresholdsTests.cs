using Xunit;

namespace Jev.Client.Tests;

public sealed class ThresholdsTests
{
    // A jev-eval 1.2.0 thresholds.json, plus an unknown key ("tolerance", "guard") the reader must ignore.
    private const string Fixture = """
        {
          "version": 1,
          "model": "jev-latest",
          "generatedAt": "2026-10-10T12:00:00Z",
          "guard": {"method": "bootstrap", "resamples": 1000, "seed": 0},
          "tolerance": 0,
          "questions": {
            "team": {"type": "choice", "threshold": 0.8, "accuracy": 0.94, "coverage": 0.61, "n": 300},
            "sentiment": {"type": "score", "threshold": 0.7, "accuracy": 0.91, "coverage": 0.66, "n": 300},
            "urgent": {"type": "noul", "threshold": 0.6, "accuracy": 0.95, "coverage": 0.5, "n": 300}
          },
          "composite": {"threshold": 0.82, "accuracy": 0.93, "coverage": 0.55, "n": 300, "questions": ["sentiment", "team"]},
          "definitions": {
            "team": {"type": "choice", "instructions": "Which team?", "criteria": {"billing": "Billing", "tech": "Tech"}},
            "sentiment": {"type": "score", "instructions": "sentiment", "criteria": ["negative", "neutral", "positive"]},
            "urgent": {"type": "noul", "instructions": "Is it urgent?"}
          }
        }
        """;

    private static readonly Choice Team = new("Which team?", new Dictionary<string, string> { ["tech"] = "Tech", ["billing"] = "Billing" });
    private static readonly Score Sentiment = new("sentiment", ["negative", "neutral", "positive"]);
    private static readonly Noul Urgent = new("Is it urgent?");

    private static Dictionary<string, Question> Ask(params (string Id, Question Question)[] questions) =>
        questions.ToDictionary(q => q.Id, q => q.Question);

    private static string Without(string key) =>
        Fixture.Replace($"\"{key}\":", $"\"ignored_{key}\":", StringComparison.Ordinal);

    [Fact]
    public void Parses_gates_composite_and_definitions()
    {
        var file = JevThresholds.Parse(Fixture);

        Assert.Equal("jev-latest", file.Model);
        Assert.Equal(new QuestionGate("choice", new ThresholdGate(0.8, 0.94, 0.61, 300)), file.Questions["team"]);
        Assert.Equal(new CompositeGate(new ThresholdGate(0.82, 0.93, 0.55, 300), ["sentiment", "team"]), file.Composite);
        Assert.Equal(Team, file.Definitions!["team"]);
        Assert.Equal(Urgent, file.Definitions["urgent"]);
    }

    [Fact]
    public void One_gated_question_uses_its_own_gate_and_ignores_nouls()
    {
        var picked = JevThresholds.Parse(Fixture).Pick(Ask(("team", Team), ("urgent", Urgent)), "jev-latest");

        Assert.Equal("team", picked.Source);
        Assert.Equal(0.8, picked.Threshold);
        Assert.Equal(["team"], picked.Questions);
        Assert.Empty(picked.Warnings);
    }

    [Fact]
    public void Several_gated_questions_use_the_composite_gate()
    {
        var picked = JevThresholds.Parse(Fixture).Pick(Ask(("team", Team), ("sentiment", Sentiment), ("urgent", Urgent)));

        Assert.Equal("composite", picked.Source);
        Assert.Equal(new ThresholdGate(0.82, 0.93, 0.55, 300), picked.Gate);
        Assert.Equal("jev-latest", picked.Model);
        Assert.Equal(
            "gate 0.82 from composite (jev-eval: 93.0% accuracy at 55.0% coverage, n=300, model jev-latest)",
            picked.Describe());
    }

    [Fact]
    public void Describe_names_the_question_a_single_gate_came_from()
    {
        var picked = JevThresholds.Parse(Fixture).Pick(Ask(("sentiment", Sentiment)));

        Assert.StartsWith("gate 0.7 from \"sentiment\" (jev-eval: 91.0% accuracy", picked.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Only_nouls_is_an_error()
    {
        var e = Assert.Throws<JevThresholdsException>(() => JevThresholds.Parse(Fixture).Pick(Ask(("urgent", Urgent))));
        Assert.Contains("nothing to gate on (every question is a noul)", e.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_refused_question_is_an_error()
    {
        var other = new Choice("Lang?", new Dictionary<string, string> { ["en"] = "English" });
        var e = Assert.Throws<JevThresholdsException>(() => JevThresholds.Parse(Fixture).Pick(Ask(("lang", other))));
        Assert.Contains("no gate for \"lang\" (jev-eval refused it or found no gate meeting the goal", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_composite_over_other_questions_names_both_lists()
    {
        var other = new Choice("Lang?", new Dictionary<string, string> { ["en"] = "English" });
        var e = Assert.Throws<JevThresholdsException>(
            () => JevThresholds.Parse(Fixture).Pick(Ask(("team", Team), ("lang", other))));
        Assert.Contains("gates [sentiment, team] but these questions gate [lang, team]", e.Message, StringComparison.Ordinal);
        Assert.Contains("re-run jev-eval thresholds with these questions", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_composite_is_an_error()
    {
        var e = Assert.Throws<JevThresholdsException>(
            () => JevThresholds.Parse(Without("composite")).Pick(Ask(("team", Team), ("sentiment", Sentiment))));
        Assert.Contains("no composite gate for [sentiment, team]", e.Message, StringComparison.Ordinal);
        Assert.Contains("jev-eval refused or found the row gate unstable", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_different_model_is_a_warning()
    {
        var picked = JevThresholds.Parse(Fixture).Pick(Ask(("team", Team)), "jev-2");

        var warning = Assert.Single(picked.Warnings);
        Assert.Contains("measured on jev-latest but you use jev-2", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reworded_question_is_a_warning()
    {
        var reworded = new Score("sentiment", ["bad", "neutral", "good"]);
        var picked = JevThresholds.Parse(Fixture).Pick(Ask(("team", Team), ("sentiment", reworded)));

        Assert.Equal(["sentiment was reworded since it was measured; re-measure."], picked.Warnings);
    }

    [Fact]
    public void A_retyped_question_is_a_warning()
    {
        var retyped = new Choice("sentiment", new Dictionary<string, string> { ["neg"] = "negative" });
        var picked = JevThresholds.Parse(Fixture).Pick(Ask(("team", Team), ("sentiment", retyped)));

        Assert.Equal(["sentiment was reworded since it was measured; re-measure."], picked.Warnings);
    }

    [Fact]
    public void An_unreadable_definition_counts_as_reworded()
    {
        var json = Fixture.Replace("\"criteria\": [\"negative\", \"neutral\", \"positive\"]", "\"criteria\": [1, 2]", StringComparison.Ordinal);
        var file = JevThresholds.Parse(json);

        Assert.False(file.Definitions!.ContainsKey("sentiment"));
        Assert.Single(file.Pick(Ask(("sentiment", Sentiment))).Warnings);
    }

    [Theory]
    [InlineData("{\"type\": \"choice\", \"instructions\": \"x\", \"criteria\": {\"a\": 1}}")]
    [InlineData("{\"type\": \"choice\", \"instructions\": \"x\", \"criteria\": []}")]
    [InlineData("{\"type\": \"choice\", \"instructions\": \"x\", \"criteria\": {}}")]
    [InlineData("{\"type\": \"other\", \"instructions\": \"x\"}")]
    [InlineData("{\"type\": \"score\"}")]
    [InlineData("\"team\"")]
    public void Malformed_definitions_are_unreadable(string definition)
    {
        var json = Fixture.Replace(
            "\"team\": {\"type\": \"choice\", \"instructions\": \"Which team?\", \"criteria\": {\"billing\": \"Billing\", \"tech\": \"Tech\"}}",
            $"\"team\": {definition}",
            StringComparison.Ordinal);

        Assert.Single(JevThresholds.Parse(json).Pick(Ask(("team", Team))).Warnings);
    }

    [Fact]
    public void A_noul_definition_keeps_its_criteria()
    {
        var json = Fixture.Replace(
            "\"urgent\": {\"type\": \"noul\", \"instructions\": \"Is it urgent?\"}",
            "\"urgent\": {\"type\": \"noul\", \"instructions\": \"Is it urgent?\", \"criteria\": {\"true\": \"yes\"}}",
            StringComparison.Ordinal);

        Assert.Equal(new Noul("Is it urgent?", whenTrue: "yes"), JevThresholds.Parse(json).Definitions!["urgent"]);
    }

    [Fact]
    public void Files_without_definitions_never_warn_about_rewording()
    {
        var file = JevThresholds.Parse(Without("definitions"));

        Assert.Null(file.Definitions);
        Assert.Empty(file.Pick(Ask(("sentiment", new Score("other", ["a", "b"])))).Warnings);
    }

    [Theory]
    [InlineData("{\"version\": 2, \"model\": \"m\", \"questions\": {}}", "expected version 1, got 2")]
    [InlineData("{\"model\": \"m\", \"questions\": {}}", "expected version 1, got none")]
    [InlineData("{\"version\": 1, \"questions\": {}}", "missing string field \"model\"")]
    [InlineData("{\"version\": 1, \"model\": \"m\"}", "\"questions\" is missing or not a JSON object")]
    [InlineData("{\"version\": 1, \"model\": \"m\", \"questions\": {\"q\": 3}}", "questions entry \"q\"")]
    [InlineData("{\"version\": 1, \"model\": \"m\", \"questions\": {\"q\": {\"type\": \"choice\", \"threshold\": 0.5, \"accuracy\": 1, \"coverage\": 1}}}", "missing integer field \"n\"")]
    [InlineData("{\"version\": 1, \"model\": \"m\", \"questions\": {\"q\": {\"type\": \"choice\", \"threshold\": \"x\", \"accuracy\": 1, \"coverage\": 1, \"n\": 1}}}", "missing numeric field \"threshold\"")]
    [InlineData("{\"version\": 1, \"model\": \"m\", \"questions\": {}, \"composite\": []}", "\"composite\" is missing or not a JSON object")]
    [InlineData("{\"version\": 1, \"model\": \"m\", \"questions\": {}, \"composite\": {\"threshold\": 0.5, \"accuracy\": 1, \"coverage\": 1, \"n\": 1}}", "\"composite.questions\" is not a list of question ids")]
    [InlineData("{\"version\": 1, \"model\": \"m\", \"questions\": {}, \"composite\": {\"threshold\": 0.5, \"accuracy\": 1, \"coverage\": 1, \"n\": 1, \"questions\": [1]}}", "\"composite.questions\" is not a list of question ids")]
    [InlineData("[]", "the document is missing or not a JSON object")]
    [InlineData("{not json", "not valid JSON")]
    public void Malformed_files_are_refused(string json, string expected)
    {
        var e = Assert.Throws<JevThresholdsException>(() => JevThresholds.Parse(json));
        Assert.Contains(expected, e.Message, StringComparison.Ordinal);
        Assert.StartsWith("thresholds file: ", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_reads_a_file_and_names_it_in_errors()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Fixture);
            Assert.Equal("jev-latest", JevThresholds.Load(path).Model);

            File.WriteAllText(path, "{\"version\": 0}");
            var e = Assert.Throws<JevThresholdsException>(() => JevThresholds.Load(path));
            Assert.StartsWith(path, e.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Row_confidence_is_the_minimum_over_gated_answers()
    {
        var picked = JevThresholds.Parse(Fixture).Pick(Ask(("team", Team), ("sentiment", Sentiment), ("urgent", Urgent)));
        var answers = new Dictionary<string, Answer>
        {
            ["team"] = new ChoiceAnswer("tech", new Dictionary<string, double> { ["tech"] = 0.95 }, 0.9),
            ["sentiment"] = new ScoreAnswer(1.0, [0.1, 0.8, 0.1], new Dictionary<string, string>(), 0.85),
            ["urgent"] = new NoulAnswer(0.5),
        };

        Assert.Equal(0.85, picked.RowConfidence(answers));
        Assert.False(picked.ShouldEscalate(answers));
        Assert.False(picked.ShouldEscalate(new SystemOneResponse { Answers = answers }));

        answers["sentiment"] = new ScoreAnswer(1.0, [0.3, 0.4, 0.3], new Dictionary<string, string>(), 0.4);
        Assert.True(picked.ShouldEscalate(answers));
    }

    [Fact]
    public void A_missing_answer_or_confidence_escalates()
    {
        var picked = JevThresholds.Parse(Fixture).Pick(Ask(("team", Team)));
        var noConfidence = new Dictionary<string, Answer>
        {
            ["team"] = new ChoiceAnswer("tech", new Dictionary<string, double>(), null),
        };

        Assert.Null(picked.RowConfidence(noConfidence));
        Assert.True(picked.ShouldEscalate(noConfidence));
        Assert.True(picked.ShouldEscalate(new Dictionary<string, Answer>()));
    }
}
