# Changelog

All notable changes to this project are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **v0.1:** `JevClient` for TypeSafe AI's Jev System One model. `SystemOneAsync(state, questions)` sends a state plus typed questions and returns typed answers with calibrated confidence.
- Three question primitives: `Choice` (map criteria), `Score` (2 to 10 ordered levels), `Noul` (yes/no). Hand-written JSON so the `criteria` map/array and `probabilities` map/array shapes are handled exactly.
- Confidence helper `Answer.IsConfident(threshold)`; type-filtered views `SystemOneResponse.Choices` / `Scores` / `Nouls`.
- Zero runtime dependencies (System.Net.Http + System.Text.Json). API key from `TYPESAFE_API_KEY` or `JevClientOptions`; injectable `HttpMessageHandler` transport seam for offline tests.
- Automatic retry with exponential backoff on `429` / `529`; typed exceptions for `401` / `422` / `429` / `529`.
- Foundry .NET gate: `dotnet format`, `dotnet build -warnaserror` (Roslyn analyzers), `dotnet test`, vulnerable-package audit.

### Fixed

- A `BaseUrl` with a path (e.g. a proxy at `https://host/typesafe`) lost its path: `new Uri(base, "/v1/systemone")` resolves from the host root. The endpoint is now `BaseUrl` path + `/v1/systemone`.

### Roadmap

- `netstandard2.0` multi-target; response caching; a live-key end-to-end sample.
