# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

Phase 0 and Milestones 1–2 are merged into `main`. Milestone 3 (detail and filtering) is in progress on `feature/milestone-3`. The work follows the planning docs, which are the source of truth for scope and sequencing:

- `SCOPE.md`: vision, full command list, Spectre.Console feature coverage matrix, constraints, risks
- `MVP.md`: the Milestone 1 feature set and acceptance criteria
- `EXECUTION_PLAN.md`: architecture, milestones with task IDs (e.g. `1.5`), risk checkpoints
- `docs/spikes.md`: Phase 0 findings about LibGit2Sharp and Spectre widget behavior. **Read it before writing renderers.**

Never commit to `main`. The user merges feature branches into `main` through pull requests. Work on the current feature branch, or make a new one from `main` after a merge.

## What it is

GitPulse is a cross-platform .NET 10 console tool. It reads a local Git repository (read-only, through LibGit2Sharp) and renders an interactive terminal dashboard with Spectre.Console. A second goal is to **showcase Spectre.Console**: each feature in the SCOPE.md coverage matrix should appear on a real screen. When you add a screen, check which matrix features it is supposed to cover.

## Commands

```
dotnet build
dotnet test                                                       # Microsoft.Testing.Platform (see global.json)
dotnet test --project tests/GitPulse.Tests -- --filter-method "*RepositoryReader*"   # single test/class
dotnet test --project tests/GitPulse.Tests -- --filter-class "GitPulse.Tests.Git.RepositoryReaderTests"
dotnet run --project src/GitPulse -- --path <repo>
dotnet src/GitPulse/bin/Debug/net10.0/gitpulse.dll                # run the DLL directly (bypasses the apphost .exe)
```

- The tests use **xunit.v3 on Microsoft.Testing.Platform**. VSTest-style `--filter "FullyQualifiedName~..."` does not work; pass xUnit filters after `--`.
- `ITestOutputHelper` output is not shown by `dotnet test`. `LayoutSpikeTests` dumps its renders when `GITPULSE_SPIKE_OUT=<dir>` is set.
- **Snapshot tests** (`tests/.../Rendering/Snapshot.cs`) are a small file-based helper. They do not use Verify, whose license check is an open decision for the user. A mismatch writes `Snapshots/<name>.received.txt` and fails. After reviewing the output, accept it with `GITPULSE_UPDATE_SNAPSHOTS=1 dotnet test`. Read the new `.verified.txt` files before committing; never accept a snapshot blindly.
- **Interactive tests:** push arrow keys as `new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false)`. `PushKey(ConsoleKey.DownArrow)` carries `'('` as its character, and the menu's search box treats it as typed text. Pick menu items by typing into the search box (`Choose("Top authors")`), not by position, so adding menu items does not break tests.
- Exit codes are defined in `GitPulseApp`: 0 ok, 1 expected error (`GitPulseException`), 2 usage error, 70 unexpected error, 130 cancelled.
- The build treats warnings as errors and enforces code style (`Directory.Build.props`). Package versions are pinned exactly in `Directory.Packages.props`, so never put a `Version` on a `PackageReference`.
- **On the dev machine, build and run in WSL, not Windows.** Windows Smart App Control is on, and it blocks freshly built unsigned binaries ("An Application Control policy has blocked this file", 0x800711C7). Do not try to bypass or disable it. The WSL Ubuntu distro has the .NET 10 SDK (apt `dotnet-sdk-10.0`). Run commands as `wsl.exe -d Ubuntu -- bash -lc 'cd "/mnt/d/Projects/Console app projects/GitPulse" && dotnet test'`, or put them in a script file, because PowerShell mangles nested quotes. WSL builds of the `/mnt/...` checkout go to `artifacts-wsl/` (see `Directory.Build.props`), so they never clash with Windows `bin/obj`. In Visual Studio, the **WSL** launch profile runs and debugs the app inside WSL.
- CI (`.github/workflows/ci.yml`) runs build, test and a smoke run on Ubuntu and Windows for pushes and PRs to `main`/`development`.

## Architecture (from EXECUTION_PLAN.md)

```
src/GitPulse/
  Program.cs     CommandApp setup (Spectre.Console.Cli)
  Commands/      Commands + Settings classes (thin)
  Git/           LibGit2Sharp access -> raw data (no Spectre references)
  Analysis/      Pure functions: raw data -> stats models
  Models/        RepoSnapshot, AuthorStats, FileChurn, ...
  Rendering/     IRenderable builders + Theme.cs (no Git references)
  Interactive/   Menu loop, prompts
tests/GitPulse.Tests/
  Analysis/      Unit tests
  Rendering/     Snapshot tests (Spectre.Console.Testing TestConsole + Snapshot.cs)
  Cli/           End-to-end tests (Spectre.Console.Cli.Testing CommandAppTester, simulated keys)
  Git/           Tests on throwaway repos (TempRepo)
```

How a run flows: `Program` → `GitPulseApp.Configure` (commands and exception handler) → a command → `Commands/ScanDisplay` (Status spinner while opening, then Progress while `Git/RepositoryScanner` reads the repo in three stages and reports through `IScanProgress`) → `Analysis/RepoAnalyzer` produces `RepoStats` → a renderer returns an `IRenderable` for the current width. Commands get `IAnsiConsole` injected by Spectre.Console.Cli. Do not use the static `AnsiConsole` in commands, or tests can't capture the output.

Responsive rendering: renderers take the console width and switch on `Breakpoints.Of(width)` → `WidthClass` (Wide 120+, Medium 100+, Narrow 80+, Compact 40+, TooNarrow). Wide adds the overview calendar; Compact stacks panels. The overview `Layout` is wrapped in `FixedHeight`, because a `Layout` otherwise fills the whole console height.

Filters: `Models/ScanFilter` (since, until, branches, authors). Dates and branches limit the scan (`RepositoryReader.ListCommits`); authors are applied in `RepoAnalyzer.Analyze`, so the interactive menu can change them without rescanning. Machine-readable output (`commit --raw`) must bypass Spectre wrapping and spinners: write to `console.Profile.Out.Writer` when not a terminal, and open the repo without `Status`.

Color-only widgets need a text fallback: a `Canvas` paints background colors, so `HeatmapGrid` checks `options.Ansi` and draws shade glyphs when ANSI is off (redirected output, snapshot tests). Snapshot tests render with no ANSI and no colors; test the colored path separately with `EmitAnsiSequences()`. In Spectre 0.57 `Canvas` pixels are half-blocks (1 column × half a line); `PixelWidth` is obsolete.

Rules:
1. Data flows one way: `Git` → `Analysis` → `Models` → `Rendering`.
2. Renderers take models and **return `IRenderable`**. They never write to the console themselves. This keeps them snapshot-testable and reusable in `Layout`, `Live` and HTML export.
3. Commands stay thin: parse settings → call services → write renderables.
4. Every color comes from the central `Theme` class. Never hardcode colors in markup.
5. Pass any repo-provided text (author names, paths, messages) through `Markup.Escape`, even in `BarChart` labels, which are parsed as markup too.

Types in `src/GitPulse` are `internal`. The test project gets access through `InternalsVisibleTo`.

## Constraints

- **Never write to the repository** (no commit, checkout, etc.) and **no network access**. That includes remote APIs and avatars. Commit links are built from the configured remote URL only.
- Spectre.Console prompts cannot run inside `Live`, `Progress` or `Status`, and they are not thread-safe. Watch mode reads keys manually (`Console.KeyAvailable`/`ReadKey`) and leaves the live display before showing any prompt.
- Pin exact package versions in `Directory.Packages.props` (central package management). Spectre.Console is pre-1.0, and Spectre.Console.Cli is versioned separately, so pin both.
- Layouts must work at 120 and 100 columns. Below 80 columns, show a compact layout or a clear "terminal too narrow" message.
- Expected errors (not a Git repo, empty repo, invalid options) get a friendly error panel and a non-zero exit code. Unexpected errors go to `AnsiConsole.WriteException`.
- Validate options through Spectre.Console.Cli: `--top` must be 1–100, and `--since` must be a valid date.
- Fallback if LibGit2Sharp's native binaries cause trouble: parse `git log --numstat` instead.

## Working practices

- Commit to `development` with conventional commits (`feat:`, `fix:`, `test:`, `chore:`, `ci:`).
- Write `Analysis` tests first. Add a snapshot test for each renderer right after writing it, using a fixed fake dataset.
- Git tests create throwaway repos with `Repository.Init` in a temp dir. Qualify `LibGit2Sharp.Commands`, because it clashes with the `GitPulse.Commands` namespace.
