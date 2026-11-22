# Phase 0 spike findings

## LibGit2Sharp 0.32.0

- Native binaries load on Windows x64 (Linux is covered by CI).
- `Diff.Compare<PatchStats>` entries no longer expose the file path in 0.32. Use `Diff.Compare<Patch>`, which gives `Path`, `LinesAdded`, `LinesDeleted` per file. `Patch` is heavier (it builds patch text), so revisit it in the performance pass (4.4) if scans are slow.
- `Repository.Discover` walks up from subdirectories, so `--path` can point anywhere inside a repo.
- Test name clash: `GitPulse.Commands` hides LibGit2Sharp's `Commands` class. Qualify it as `LibGit2Sharp.Commands.Stage(...)`.

Test: `tests/GitPulse.Tests/Git/RepositoryReaderTests.cs`.

## Spectre.Console 0.57.2: Layout, BarChart, BreakdownChart

Rendered at 120/100/80/60/40 columns in `TestConsole`. To dump the renders, set `GITPULSE_SPIKE_OUT=<dir>` and run `dotnet test`.

| Finding | Consequence for the renderers |
|---------|-------------------------------|
| `BarChart` item labels are parsed as **markup**: `bot[dependabot]` throws "malformed markup tag" | Always `Markup.Escape` author names, file paths and any repo-provided text |
| `Layout` expands to the **full console height** | Give rows explicit `.Size(n)`/`.Ratio(n)` for the overview, or render without `Layout` when not full-screen |
| Long `BarChart` labels **wrap** | Truncate author names to ~20 chars (with `…`) |
| Small values get **no visible bar** (rounded to 0) | Acceptable since the value is still printed; consider a minimum or a log scale later |
| `BreakdownChart` legend wraps cleanly; values print as-is (`62.5`, `7`) | Pass rounded percentages, or use `.ShowPercentage()` |
| At **40 columns** side-by-side panels are unreadable; **80** is usable | Matches the MVP rule: below 80 columns, show a compact layout or a "too narrow" message |

Test: `tests/GitPulse.Tests/Spikes/LayoutSpikeTests.cs` (asserts no throw and no line wider than the console).

## Environment

- .NET 10 SDK requires opting into Microsoft.Testing.Platform for xunit.v3 (see `global.json`). VSTest packages (`Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.collector`) are not used.
- Verify 33.x fails the build unless a SponsorCheck license property is set. It is not used: snapshot tests use a small built-in helper instead (`tests/GitPulse.Tests/Rendering/Snapshot.cs`). Whether to switch to Verify is still open.
- On the dev machine, Windows Smart App Control blocks freshly built unsigned binaries (`An Application Control policy has blocked this file`, Code Integrity events 3033/3077). Development runs in **WSL Ubuntu** instead (see CLAUDE.md). CI covers Windows.

## Milestone 4 findings

- **Spectre's `Recorder.ExportHtml()` does not HTML-escape text** (0.57.2). A repository named `<script>alert(1)</script>` came out as a live `<script>` element. Repo names, authors, messages and paths are untrusted text, so `report --html` uses its own `IAnsiConsoleEncoder` (`HtmlReport.SafeHtmlEncoder`) that escapes every segment. A test checks this. This may be worth reporting upstream.
- Recording a color-only widget (the heatmap `Canvas`) needs `Profile.Capabilities.Ansi = true` on the off-screen console, or `HeatmapGrid` falls back to shade glyphs.
- In WSL, a `FileSystemWatcher` on a repository under `/mnt/<drive>` gets no events for changes made from Windows. `watch` therefore also polls a cheap HEAD/branch/tag fingerprint.
- `dotnet run` prints "Using launch settings from …" to stdout unless `--no-launch-profile` is passed. It broke the CI check that `--raw` produces valid JSON.
- Scan cache, xUnit (3,260 commits): 7.3 s without the cache, 0.67 s with it. The cache file is 2.6 MB.
