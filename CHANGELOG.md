# Changelog

All notable changes to this project are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **v0.1:** `JevClient` for TypeSafe AI's Jev System One model. `SystemOneAsync(state, questions)` sends a state plus typed questions and returns typed answers with calibrated confidence.
- Three question primitives: `Choice` (map criteria, 1 to 255 options), `Score` (2 to 10 ordered levels), `Noul` (yes/no), validated on construction. Hand-written JSON so the `criteria` map/array and `probabilities` map/array shapes are handled exactly.
- Typed answers mirroring the questions: `ChoiceAnswer`, `ScoreAnswer` (both `CalibratedAnswer`s with `IsConfident(threshold)`), and `NoulAnswer` (`IsTrue(threshold)`), shaped by the question asked under each id. Typed accessors `SystemOneResponse.GetChoice/GetScore/GetNoul(id)` and typed views `Choices()` / `Scores()` / `Nouls()`. Answers and questions compare by value; an omitted confidence is null.
- Zero runtime dependencies (System.Net.Http + System.Text.Json). API key from `TYPESAFE_API_KEY` or `JevClientOptions`; injectable `HttpMessageHandler` transport seam for offline tests.
- Automatic retry on `429` / `529` with exponential backoff, jitter, and `Retry-After` (seconds or a date), capped at 30 seconds per wait. Typed exceptions for `401` / `422` / `429` / `529`; network errors, timeouts, and malformed responses also surface as `JevException`.
- `JevClientOptions` are validated when the client is created; a `Handler` passed in is not disposed by the client; `TimeProvider` drives retry waits. A `BaseUrl` path (e.g. a proxy prefix) is kept when building the endpoint.
- `JevThresholds.Load(path)` / `Parse(json)` read a jev-eval `thresholds.json` (contract version 1). `Pick(questions, model)` returns the `PickedGate` for your questions (per-question or composite, matching jev-sort 1.2.0) with its measured accuracy, coverage and n, plus `Warnings` for a model mismatch or a reworded question. `ShouldEscalate(answers)` gates on the minimum choice/score confidence.
- Foundry .NET gate: `dotnet format`, `dotnet build -warnaserror` (Roslyn analyzers), `dotnet test`, vulnerable-package audit.

### Roadmap

- `netstandard2.0` multi-target; response caching; a live-key end-to-end sample.
