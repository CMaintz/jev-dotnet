# CMaintz.Jev.Client (.NET)

A small, dependency-free .NET client for [TypeSafe AI's](https://typesafe.ai) Jev,
a "System One" model that returns typed judgments instead of free text. You send a
piece of *state* and a set of typed questions; you get back typed answers with
calibrated confidence, which your code can act on directly.

> This is my own unofficial SDK, not published or endorsed by TypeSafe. It's built
> against the public API at `https://api.typesafe.ai`.

## Why

A chat LLM hands you a paragraph you have to parse and second-guess. Jev hands you a
value with a shape: an enum choice, a number on a scale, or a yes/no probability, each
with a confidence you can threshold on. The idea is to run Jev on everything and only
escalate the low-confidence cases to a person or a bigger model.

## Install

Not yet published to NuGet (planned), so `dotnet add package CMaintz.Jev.Client` will
not find it. Reference the project from source instead:

```bash
git clone https://github.com/CMaintz/jev-dotnet.git
dotnet add <YourProject>.csproj reference jev-dotnet/src/Jev.Client/Jev.Client.csproj
```

or build a local package with `dotnet pack src/Jev.Client -c Release -o ./nupkg` and add
`./nupkg` as a package source.

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

ChoiceAnswer team = response.GetChoice("team");
if (team.IsConfident(0.7))
{
    Route(team.Choice);                // "billing"
}
else
{
    EscalateToHuman();                 // distribution was spread out
}

double anger = response.GetScore("anger").Score;          // e.g. 1.8
bool wantsRefund = response.GetNoul("refund").IsTrue(0.5);
```

The three questions above are answered in a single request. Independent questions are
evaluated in parallel, so batching them is close to free.

## The three primitives

| Question | Ask when | Answer | Answer fields |
| --- | --- | --- | --- |
| `Choice` | one of a defined set | `ChoiceAnswer` | `Choice`, `Probabilities` (per option), `Confidence` |
| `Score` | a position on an ordered scale | `ScoreAnswer` | `Score`, `Probabilities` (per level), `Legend`, `Confidence` |
| `Noul` | a yes/no condition | `NoulAnswer` | `Probability` (0..1); no confidence |

Each answer mirrors the question asked under its id. Fetch it with
`response.GetChoice(id)` / `GetScore(id)` / `GetNoul(id)`, or pattern-match the
`Answer` returned by the indexer:

```csharp
// needs `using System.Diagnostics;` for UnreachableException
var text = response[id] switch
{
    ChoiceAnswer c => c.Choice,
    ScoreAnswer s => $"{s.Score:0.0}",
    NoulAnswer n => n.IsTrue(0.5) ? "yes" : "no",
    _ => throw new UnreachableException(),
};
```

`Choice` criteria is a map of option to description (1 to 255 options). `Score` criteria
is an ordered list of 2 to 10 level descriptions, low to high. The model cannot pick an
option you did not give it, so include a no-match option when nothing may fit.

## Confidence

`ChoiceAnswer` and `ScoreAnswer` are both `CalibratedAnswer`s. They carry a
`Confidence` in `[0, 1]` derived from how peaked the probability distribution is, and
`IsConfident(threshold)` gates either kind:

```csharp
if (answer is CalibratedAnswer c && c.IsConfident(0.7)) { ... }
```

`IsConfident` is false when the service omitted the confidence (`Confidence` is null). A `Noul` has no
confidence, so gate it on the probability itself with `NoulAnswer.IsTrue(threshold)`;
near 0.5 means genuinely uncertain, not "medium yes". Both helpers pass at or above the
threshold. A confidence threshold is not one number: use a
stricter bar for consequential actions than for harmless ones, and tune it on your data.

## Errors

All service failures derive from `JevException`, which carries `StatusCode` and
`ResponseBody`. A network error, a timeout, a malformed response body, or a missing API
key also surfaces as a `JevException` (with no status code). Invalid arguments, such as
an empty question map, state that cannot be written as JSON, or an invalid option, throw
`ArgumentException` before any request is sent. Cancelling through your
`CancellationToken` throws `OperationCanceledException` as usual.

| Exception | HTTP | Meaning |
| --- | --- | --- |
| `JevAuthException` | 401 | missing or invalid API key |
| `JevValidationException` | 422 | the request was rejected as malformed |
| `JevRateLimitException` | 429 | rate limited; retries exhausted |
| `JevOverloadedException` | 529 | service overloaded; retries exhausted |

`429` and `529` are retried automatically with exponential backoff and jitter, honoring
`Retry-After` (seconds or a date); each wait is capped at 30 seconds. `MaxRetries` is
configurable.

## Configuration

```csharp
using var client = new JevClient(new JevClientOptions
{
    ApiKey = "sk-...",                            // or leave null to read TYPESAFE_API_KEY
    Model = "jev-latest",                         // tracks the recommended model
    Timeout = TimeSpan.FromSeconds(30),
    MaxRetries = 3,
    BaseUrl = new Uri("https://api.typesafe.ai"),   // e.g. a proxy; its path prefix is kept
    Handler = customHandler,                      // inject an HttpMessageHandler to test offline
    TimeProvider = fakeTime,                      // drive retry waits from a test clock
});
```

Options are validated when the client is created. A `Handler` you pass in stays yours:
the client does not dispose it.

Keep the API key server-side. `JevClient` is thread-safe: create one and reuse it.

## Roadmap

- Publish `CMaintz.Jev.Client` to NuGet.
- Multi-target `net8.0` plus `netstandard2.0` for broader reach.
- Optional streaming of large batches; response caching for repeated states.
- A live end-to-end sample against a real key.

## License

MIT. See [LICENSE](LICENSE).
