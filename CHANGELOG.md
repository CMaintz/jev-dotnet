# Changelog

All notable changes to this project are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Images: `Image.Png(bytes)`, `Image.Jpeg(bytes)` and `Image.FromFile(path)` (format from the file's content) go anywhere text can, in the state, instructions or any criteria value, and `TextWithImages.Of(...)` puts them inside a sentence. Each distinct image is listed once in `assets` and uploaded as a file part of a `multipart/form-data` request, with `TypeSafe-Danger-Zone-Images: true`; requests without images stay plain JSON. Bytes are copied and capped at 16 MiB; nothing is decoded or resized. Serializing an `Image` or `TextWithImages` on its own, outside a request, throws `InvalidOperationException` rather than silently dropping the file. Experimental ("dreamer-ready") per TypeSafe.
- `Box` questions (Jev 1.14+, experimental "dreamer-ready" per TypeSafe) find where something is in an image. `BoxAnswer(Top, Bottom, Left, Right, Confidence)` with `IsConfident(threshold)`, `SystemOneResponse.GetBox(id)` and `Boxes()`. A request with a Box sends `TypeSafe-Danger-Zone-Images: true`. `JevThresholds.Pick` gates only on Choice and Score questions, so a Box is never gated.
- Request bodies of 1024 bytes or more are gzipped (`Content-Encoding: gzip`), once per call so retries resend the same bytes. On by default; `JevClientOptions.CompressRequests = false` turns it off for a proxy or gateway that does not accept compressed bodies.
- Per-call `SystemOneOptions`: a `SystemOneAsync(state, questions, options)` overload that overrides the model for one request (e.g. `jev-preview` or a pinned version) or turns on long context (`TypeSafe-Danger-Zone-Long-Context: true`) for that request only.
- `JevClient.ModelsAsync()` lists the models and aliases the account can use (`GET /v1/models`) as `ModelInfo(Name, Description, ReleaseDate)`, with the same retries and typed errors as `SystemOneAsync`.
- Typed `JevBadRequestException` (400), `JevPayloadTooLargeException` (413) and `JevServerException` (other 5xx). `408` and every `5xx` are now retried alongside `429`, and a `retry-after-ms` header is honored, matching TypeSafe's own SDKs.
- **v0.1:** `JevClient` for TypeSafe AI's Jev System One model. `SystemOneAsync(state, questions)` sends a state plus typed questions and returns typed answers with calibrated confidence.
- Three question primitives: `Choice` (map criteria, 1 to 255 options), `Score` (1 to 10 ordered levels), `Noul` (yes/no), validated on construction. Instructions, criteria and Noul glosses are text or structured JSON (anything that serializes to an object or array), copied on construction and exposed as `JsonElement`; a structured Score level reads back in `Legend` as compact JSON. Hand-written JSON so the `criteria` map/array and `probabilities` map/array shapes are handled exactly.
- Typed answers mirroring the questions: `ChoiceAnswer`, `ScoreAnswer` (both `CalibratedAnswer`s with `IsConfident(threshold)`), and `NoulAnswer` (`IsTrue(threshold)`), shaped by the question asked under each id. Typed accessors `SystemOneResponse.GetChoice/GetScore/GetNoul(id)` and typed views `Choices()` / `Scores()` / `Nouls()`. Answers and questions compare by value; an omitted confidence is null.
- Zero runtime dependencies (System.Net.Http + System.Text.Json). API key from `TYPESAFE_API_KEY` or `JevClientOptions`; injectable `HttpMessageHandler` transport seam for offline tests.
- Automatic retry on `429` / `529` with exponential backoff, jitter, and `Retry-After` (seconds or a date), capped at 30 seconds per wait. Typed exceptions for `401` / `422` / `429` / `529`; network errors, timeouts, and malformed responses also surface as `JevException`.
- `JevClientOptions` are validated when the client is created; a `Handler` passed in is not disposed by the client; `TimeProvider` drives retry waits. A `BaseUrl` path (e.g. a proxy prefix) is kept when building the endpoint.
- `new JevClient(httpClient, options)` runs on an `HttpClient` you own (e.g. from `IHttpClientFactory`), which the client never disposes or modifies; the API key is sent per request.
- `JevThresholds.Load(path)` / `Parse(json)` read a jev-eval `thresholds.json` (contract version 1). `Pick(questions, model)` returns the `ThresholdGate` for your questions (per-question or composite, matching jev-sort 1.2.0) with its measured accuracy, coverage and n, plus `Warnings` for a model mismatch or a reworded question; the same shape as jev-java's. `ShouldEscalate(response)` gates on the minimum choice/score confidence and `Describe()` gives a one-line log summary.
- Foundry .NET gate: `dotnet format`, `dotnet build -warnaserror` (Roslyn analyzers), `dotnet test`, vulnerable-package audit.

### Roadmap

- `netstandard2.0` multi-target; response caching; a live-key end-to-end sample.
