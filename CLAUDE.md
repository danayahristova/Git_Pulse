# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

Phase 0 (setup and spikes) is done. The work follows the planning docs, which are the source of truth for scope and sequencing:

- `SCOPE.md`: vision, full command list, Spectre.Console feature coverage matrix, constraints, risks
- `MVP.md`: the Milestone 1 feature set and acceptance criteria
- `EXECUTION_PLAN.md`: architecture, milestones with task IDs (e.g. `1.5`), risk checkpoints
- `docs/spikes.md`: Phase 0 findings about LibGit2Sharp and Spectre widget behavior. **Read it before writing renderers.**

All work happens on the `development` branch. Never commit to `main`.

The current default command (`Commands/RecentCommitsCommand`) is the Phase 0 spike. Interactive mode replaces it in task 1.7.

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
- The build treats warnings as errors and enforces code style (`Directory.Build.props`). Package versions are pinned exactly in `Directory.Packages.props`, so never put a `Version` on a `PackageReference`.
- On the dev machine, Windows Smart App Control sometimes blocks freshly built binaries ("An Application Control policy has blocked this file"). Rerun after `dotnet build --no-incremental`. Do not try to bypass the policy.
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
  Rendering/     Snapshot tests (Spectre.Console.Testing TestConsole + Verify.Xunit)
```

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
