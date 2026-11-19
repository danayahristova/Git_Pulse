# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

GitPulse is in the **planning stage**: no source code exists yet, only design docs. Read these before starting work. They are the source of truth for scope and sequencing:

- `SCOPE.md`: vision, full command list, Spectre.Console feature coverage matrix, constraints, risks
- `MVP.md`: the Milestone 1 feature set and acceptance criteria
- `EXECUTION_PLAN.md`: architecture, milestones with task IDs (e.g. `1.5`), risk checkpoints

Update this file once the solution and projects exist. The `.vs/` folder points to a `GitPulse.slnx` solution, so use the `.slnx` format.

## What it is

GitPulse is a cross-platform .NET 10 console tool. It reads a local Git repository (read-only, through LibGit2Sharp) and renders an interactive terminal dashboard with Spectre.Console. A second goal is to **showcase Spectre.Console**: each feature in the SCOPE.md coverage matrix should appear on a real screen. When you add a screen, check which matrix features it is supposed to cover.

## Planned commands

```
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~OverviewRenderer"   # single test/class
dotnet run --project src/GitPulse -- overview --path <repo> --top 10
```

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

## Constraints

- **Never write to the repository** (no commit, checkout, etc.) and **no network access**. That includes remote APIs and avatars. Commit links are built from the configured remote URL only.
- Spectre.Console prompts cannot run inside `Live`, `Progress` or `Status`, and they are not thread-safe. Watch mode reads keys manually (`Console.KeyAvailable`/`ReadKey`) and leaves the live display before showing any prompt.
- Pin exact package versions in `Directory.Packages.props` (central package management). Spectre.Console is pre-1.0, and Spectre.Console.Cli is versioned separately, so pin both.
- Layouts must work at 120 and 100 columns. Below 80 columns, show a compact layout or a clear "terminal too narrow" message.
- Expected errors (not a Git repo, empty repo, invalid options) get a friendly error panel and a non-zero exit code. Unexpected errors go to `AnsiConsole.WriteException`.
- Validate options through Spectre.Console.Cli: `--top` must be 1–100, and `--since` must be a valid date.
- Fallback if LibGit2Sharp's native binaries cause trouble: parse `git log --numstat` instead.

## Working practices

- One branch per task. Use conventional commits (`feat:`, `fix:`, `test:`).
- Write `Analysis` tests first. Add a snapshot test for each renderer right after writing it, using a fixed fake dataset.
- CI target: GitHub Actions on Windows + Ubuntu running `dotnet test`.
