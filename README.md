# Jev.Client (.NET)

A small, dependency-free .NET client for [TypeSafe AI's](https://typesafe.ai) **Jev**,
a "System One" model that returns typed judgments instead of free text. You send a
piece of *state* and a set of typed questions; you get back typed answers with
calibrated confidence, which your code can act on directly.

> Unofficial community SDK. Not published or endorsed by TypeSafe. Built against the
> public API at `https://api.typesafe.ai`.

## Why

A chat LLM hands you a paragraph you have to parse and second-guess. Jev hands you a
value with a shape: an enum choice, a number on a scale, or a yes/no probability, each
with a confidence you can threshold on. The idiom is to run Jev on everything and
escalate only the low-confidence cases to a person or a larger model.

## Install

```
dotnet add package Jev.Client
```

Targets `net8.0`. No runtime dependencies (built on `System.Net.Http` and
`System.Text.Json`).

## Quick start

```csharp
using Jev;

using var client = JevClient.FromEnvironment(); // reads TYPESAFE_API_KEY

var questions = new Dictionary<string, Question>
{
    ["team"] = new Choice(
        "Which team should handle this ticket",
        new Dictionary<string, string>
        {
            ["billing"] = "Payment or subscription issues",
            ["technical"] = "Bugs or integration problems",
            ["sales"] = "Pricing or account questions",
        }),
    ["anger"] = new Score(
        "How frustrated the customer appears",
        ["Calm, just stating facts", "Frustrated but civil", "Very angry"]),
    ["refund"] = new Noul("Does the customer ask for a refund?"),
};

var response = await client.SystemOneAsync(
    new { subject = "Charged twice!", body = "I want my money back." },
    questions);

var team = response["team"];
if (team.IsConfident(0.7))
{
    Route(team.ChoiceValue!);          // "billing"
}
else
{
    EscalateToHuman();                 // distribution was spread out
}

double anger = response["anger"].ScoreValue ?? 0;   // e.g. 1.8
bool wantsRefund = response["refund"].NoulValue > 0.5;
```

The three questions above are answered in a single request. Independent questions are
evaluated in parallel, so batching them is close to free.

## The three primitives

| Type | Ask when | Answer fields |
| --- | --- | --- |
| `Choice` | one of a defined set | `ChoiceValue`, `Probabilities` (per option), `Confidence` |
| `Score` | a position on an ordered scale | `ScoreValue`, `ScoreProbabilities` (per level), `Legend`, `Confidence` |
| `Noul` | a yes/no condition | `NoulValue` (0..1); no confidence |

`Choice` criteria is a map of option to description (max 255 options). `Score` criteria
is an ordered list of 2 to 10 level descriptions, low to high. The model cannot pick an
option you did not give it, so include a no-match option when nothing may fit.

## Confidence

`Choice` and `Score` answers carry a `Confidence` in `[0, 1]` derived from how peaked
the probability distribution is. `Answer.IsConfident(threshold)` is a convenience for
gating. A `Noul` has no confidence; gate it on the probability itself (near 0.5 means
genuinely uncertain, not "medium yes"). A confidence threshold is not one number: use a
stricter bar for consequential actions than for harmless ones, and tune it on your data.

## Errors

All failures derive from `JevException`, which carries `StatusCode` and `ResponseBody`:

| Exception | HTTP | Meaning |
| --- | --- | --- |
| `JevAuthException` | 401 | missing or invalid API key |
| `JevValidationException` | 422 | the request was rejected as malformed |
| `JevRateLimitException` | 429 | rate limited; retries exhausted |
| `JevOverloadedException` | 529 | service overloaded; retries exhausted |

`429` and `529` are retried automatically with exponential backoff (honoring
`Retry-After` when present); `MaxRetries` is configurable.

## Configuration

```csharp
using var client = new JevClient(new JevClientOptions
{
    ApiKey = "sk-...",                            // or leave null to read TYPESAFE_API_KEY
    Model = "jev-latest",                         // tracks the recommended model
    Timeout = TimeSpan.FromSeconds(30),
    MaxRetries = 3,
    Handler = customHandler,                      // inject an HttpMessageHandler to test offline
});
```

Keep the API key server-side. `JevClient` is thread-safe: create one and reuse it.

## Roadmap

- Multi-target `net8.0` plus `netstandard2.0` for broader reach.
- Optional streaming of large batches; response caching for repeated states.
- A live end-to-end sample against a real key.

## License

MIT. See [LICENSE](LICENSE).
