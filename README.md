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

## The question types

| Question | Ask when | Answer | Answer fields |
| --- | --- | --- | --- |
| `Choice` | one of a defined set | `ChoiceAnswer` | `Choice`, `Probabilities` (per option), `Confidence` |
| `Score` | a position on an ordered scale | `ScoreAnswer` | `Score`, `Probabilities` (per level), `Legend`, `Confidence` |
| `Noul` | a yes/no condition | `NoulAnswer` | `Probability` (0..1); no confidence |
| `Box` | where something is in an image | `BoxAnswer` | `Top`, `Bottom`, `Left`, `Right` (0..1), `Confidence` |

Each answer mirrors the question asked under its id. Fetch it with
`response.GetChoice(id)` / `GetScore(id)` / `GetNoul(id)` / `GetBox(id)`, or pattern-match the
`Answer` returned by the indexer:

```csharp
// needs `using System.Diagnostics;` for UnreachableException
var text = response[id] switch
{
    ChoiceAnswer c => c.Choice,
    ScoreAnswer s => $"{s.Score:0.0}",
    NoulAnswer n => n.IsTrue(0.5) ? "yes" : "no",
    BoxAnswer b => $"{b.Left:0.00},{b.Top:0.00} to {b.Right:0.00},{b.Bottom:0.00}",
    _ => throw new UnreachableException(),
};
```

`Choice` criteria is a map of option to description (1 to 255 options). `Score` criteria
is an ordered list of 1 to 10 level descriptions, low to high. The model cannot pick an
option you did not give it, so include a no-match option when nothing may fit.

Instructions, option descriptions, levels and Noul glosses can also be structured JSON
when definitions, contrasts or examples make the question clearer. Pass anything that
serializes to a JSON object or array; it is validated and copied when the question is
built, and exposed as a `JsonElement`:

```csharp
new Choice(
    new { task = "Route this ticket to a team", ignore = new List<string> { "greetings", "signatures" } },
    new Dictionary<string, object>
    {
        ["billing"] = new { covers = "charges and refunds", examples = new List<string> { "charged twice" } },
        ["tech"] = "bugs and outages",
    });
```

A structured Score level comes back in `Legend` as compact JSON text.

### Box

A `Box` finds one target in an image and answers with the four edges of the smallest box
around it. Each edge is a fraction of the image's size from its top-left corner: `Top` and
`Bottom` of the height, `Left` and `Right` of the width, so multiply by the image's pixel
size to draw or crop. A Box takes only instructions, naming the target and the image:

```csharp
var questions = new Dictionary<string, Question>
{
    ["mug_visible"] = new Noul("Is there a coffee mug in `photo`?"),
    ["mug"] = new Box("the coffee mug in `photo`"),
};
```

A Box always returns a box, even when the target is absent, so ask a `Noul` alongside it
and ignore the box when that answer is low. `BoxAnswer.IsConfident(threshold)` is false
when the service gives no confidence. A request with a Box sends
`TypeSafe-Danger-Zone-Images: true` for you. Box needs Jev 1.14 or later (`jev-latest` and
`jev-preview` point there today) and an [image](#images) in the request. TypeSafe calls images and Box
["dreamer-ready"](https://docs.typesafe.ai/primitives/box.md): experimental, with a
request format and accuracy that may change between releases.

## Images

An `Image` can go anywhere text can: as the whole state, as any value inside it, as a
question's instructions, or as a criteria value (a Choice option, a Score level, a Noul
gloss). `TextWithImages.Of(...)` puts images inside a sentence:

```csharp
var door = Image.FromFile("door-camera.jpg");   // PNG or JPEG, told apart by content
var badge = Image.Jpeg(badgeBytes);

var response = await client.SystemOneAsync(
    new { door },
    new Dictionary<string, Question>
    {
        ["badge_holder"] = new Noul(TextWithImages.Of("Is the person in `door` the person in this badge photo: ", badge, "?")),
        ["gesture"] = new Choice("Which gesture is the hand in `door` making?", new Dictionary<string, object>
        {
            ["thumbs_up"] = Image.Png(thumbsUpBytes),
            ["none"] = "No hand, or none of the examples.",
        }),
        ["where"] = new Box("the person in `door`"),
    });
```

Each image gets a unique `Reference`, written wherever you placed it, and is uploaded once
per request however often you use it. Pass the `Image` object itself: serializing an
`Image` or `TextWithImages` on your own (to a `JsonElement` or `JsonNode`, say) throws
`InvalidOperationException`, since the result would hold only the reference text and the
file would never be uploaded. A request with images goes out as
`multipart/form-data` (the JSON in a `request` part, then one file part per image) with
`TypeSafe-Danger-Zone-Images: true`; one without stays plain JSON. Questions keep the
images in their instructions and criteria, so a question can be reused across requests.
When several questions use one image, put it in the state once and refer to it by path:
an image in instructions is sent to the model once per question that uses it.

The SDK sends images as they are and does not decode, resize or rotate them. TypeSafe
accepts still PNG and JPEG images of 280 to 1568 pixels per side, at most 16 MiB per file
(`Image.MaxBytes`, checked when you make one) and 32 MiB per request, and answers 400 or 413
otherwise. See TypeSafe's [Images](https://docs.typesafe.ai/images.md) guide for the limits
and for preparing images. Images are "dreamer-ready" (experimental) per TypeSafe.

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

## Thresholds from jev-eval

[jev-eval](https://github.com/CMaintz/jev-eval) measures Jev on your labeled data and
writes a `thresholds.json` with the confidence gate for each question and a row-level
gate for several together. Load it and pick the gate for the questions you send:

```csharp
var thresholds = JevThresholds.Load("thresholds.json");
ThresholdGate gate = thresholds.Pick(questions, model: "jev-latest");

logger.LogInformation("{Gate}", gate.Describe());
// gate 0.82 from composite (jev-eval: 93.0% accuracy at 55.0% coverage, n=300, model jev-latest)
foreach (var warning in gate.Warnings)
{
    logger.LogWarning("{Warning}", warning);
}

var response = await client.SystemOneAsync(state, questions);
if (gate.ShouldEscalate(response))
{
    EscalateToHuman();
}
```

`Pick` gates on the choice and score questions only (a noul has no confidence, and
jev-eval does not measure a box). With one
of them it uses that question's gate; with several it uses the composite gate, which must
have been measured on exactly those questions. When the file has no gate that fits, it
throws a `JevThresholdsException` saying why. A different model, or a question reworded
since jev-eval measured it, does not throw; it shows up in `Warnings`.

`ShouldEscalate` compares the row confidence, the lowest confidence across the gated
answers (`RowConfidence`), with the threshold. A missing answer or confidence escalates.

## Errors

All service failures derive from `JevException`, which carries `StatusCode` and
`ResponseBody`. A network error, a timeout, a malformed response body, or a missing API
key also surfaces as a `JevException` (with no status code). Invalid arguments, such as
an empty question map, state that cannot be written as JSON, or an invalid option, throw
`ArgumentException` before any request is sent. Cancelling through your
`CancellationToken` throws `OperationCanceledException` as usual.

| Exception | HTTP | Meaning |
| --- | --- | --- |
| `JevBadRequestException` | 400 | the body broke a request rule; `max_tokens_exceeded` in the body means it is over the model's context length |
| `JevAuthException` | 401 | missing or invalid API key |
| `JevPayloadTooLargeException` | 413 | the body is over 32 MiB |
| `JevValidationException` | 422 | the request was rejected as malformed |
| `JevRateLimitException` | 429 | rate limited; retries exhausted |
| `JevOverloadedException` | 529 | service overloaded; retries exhausted |
| `JevServerException` | other 5xx | the service failed; retries exhausted |

Any other status is a plain `JevException` carrying it. `408`, `429` and every `5xx` are
retried automatically with exponential backoff and jitter, as TypeSafe's own SDKs do,
honoring `retry-after-ms` or `Retry-After` (seconds or a date); each wait is capped at 30
seconds. `MaxRetries` is configurable.

## Configuration

```csharp
using var client = new JevClient(new JevClientOptions
{
    ApiKey = "sk-...",                            // or leave null to read TYPESAFE_API_KEY
    Model = "jev-latest",                         // tracks the recommended model
    Timeout = TimeSpan.FromSeconds(30),
    MaxRetries = 3,
    CompressRequests = true,                      // gzip bodies of 1 KiB or more
    BaseUrl = new Uri("https://api.typesafe.ai"),   // e.g. a proxy; its path prefix is kept
    Handler = customHandler,                      // inject an HttpMessageHandler to test offline
    TimeProvider = fakeTime,                      // drive retry waits from a test clock
});
```

Request bodies of 1024 bytes or more are sent gzipped with `Content-Encoding: gzip`, which
cuts upload time for large states (TypeSafe's 32 MiB limit applies to the body both before
and after compression). Gzip is built into .NET, so this adds no dependency. Set
`CompressRequests = false` if a proxy or gateway at `BaseUrl` does not accept compressed
request bodies.

Options are validated when the client is created. A `Handler` you pass in stays yours:
the client does not dispose it.

To bring your own `HttpClient` (for example a typed client from `IHttpClientFactory` with
Polly handlers), pass it in. It is never disposed or modified, its own timeout applies, and
`Handler` must stay null. Requests go to `BaseUrl`, never the client's `BaseAddress`. Failures
the pipeline throws (an open circuit breaker, say) still surface as `JevException`. With a resilience pipeline that already retries, set
`MaxRetries = 0` so the two layers don't multiply, and give it timeouts long enough for a
model call (the standard handler's defaults are 10 seconds per attempt, 30 in total):

```csharp
// AddStandardResilienceHandler comes from Microsoft.Extensions.Http.Resilience.
services.AddSingleton(new JevClientOptions { ApiKey = "sk-...", MaxRetries = 0 }); // optional; else defaults + TYPESAFE_API_KEY
services.AddHttpClient<JevClient>()
    .AddStandardResilienceHandler(o =>
    {
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(2);
    });

// or by hand:
using var client = new JevClient(httpClient, new JevClientOptions { ApiKey = "sk-..." });
```

### Per-call options

`SystemOneOptions` changes one request without a second client. `Model` overrides the
client's model for that call, for example to try `jev-preview` or a pinned version.
`LongContext` sends `TypeSafe-Danger-Zone-Long-Context: true`, which raises the context
limits; TypeSafe advises turning it on only for the requests that need it.

```csharp
var response = await client.SystemOneAsync(
    longTranscript,
    questions,
    new SystemOneOptions { Model = "jev-preview", LongContext = true });
```

A blank `Model` throws `ArgumentException`; leave it null to keep the client's model. If
you gate with jev-eval thresholds, pass the model you actually called to
`JevThresholds.Pick` so a mismatch shows up in `Warnings`.

To see which models and aliases your key can use, list them:

```csharp
foreach (var model in await client.ModelsAsync())
{
    Console.WriteLine($"{model.Name} ({model.ReleaseDate}): {model.Description}");
}
```

Keep the API key server-side. `JevClient` is thread-safe: create one and reuse it. A typed
client from `AddHttpClient<JevClient>()` is the exception: inject it where needed and don't
hold it in a singleton.

## Roadmap

- Publish `CMaintz.Jev.Client` to NuGet.
- Multi-target `net8.0` plus `netstandard2.0` for broader reach.
- Optional streaming of large batches; response caching for repeated states.
- A live end-to-end sample against a real key.

## License

MIT. See [LICENSE](LICENSE).
