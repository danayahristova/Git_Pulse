# GitPulse

> `cd` into a repo, type `gitpulse`, and understand its history, people and hotspots in 30 seconds.

GitPulse is a cross-platform command-line tool that turns any local Git repository into a rich, interactive terminal dashboard. It is built with [.NET 10](https://dotnet.microsoft.com/), [Spectre.Console](https://spectreconsole.net/) and [LibGit2Sharp](https://github.com/libgit2/libgit2sharp). It is also meant as a showcase of Spectre.Console, so every major feature of the library has a real place in the app.

GitPulse is **read-only**. It never writes to your repository and never touches the network.

> **Status:** Milestone 1 (MVP) is in progress. The commands below work; later milestones add the rest (see [Roadmap](#roadmap)).

## What it looks like

`gitpulse overview` at 100 columns (sample data; colors not shown):

```text
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│ sample-repo  1,287 commits · 6 authors · all history                                             │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
┌─Summary────────────────────────────────────────┐┌─Top 5 authors──────────────────────────────────┐
│ Commits       1,287                            ││                                        commits │
│ Authors       6                                ││         Ada Lovelace  ████████████████████ 612 │
│ First commit  2026-01-03                       ││       Linus Torvalds  ███████ 301              │
│ Last commit   2026-11-18                       ││         Grace Hopper  ████ 214                 │
│ Active for    319 days                         ││ Margaret Hamilton W…  98                       │
│ Branches      4                                ││      bot[dependabot]  50                       │
│ Tags          12                               │└────────────────────────────────────────────────┘
│ Files         342                              │┌─Top hotspots───────────────────────────────────┐
└────────────────────────────────────────────────┘│ src/Core/Engine/Scheduler.cs            16,322 │
┌─Languages──────────────────────────────────────┐│ src/Core/Engine/RenderPipeline.cs       10,300 │
│ ██████████████████████████████████████████████ ││ tests/Core.Tests/SchedulerTests.cs       4,600 │
│                                                ││ …/Folder/Structure/OverviewCommand.cs    3,000 │
│ ■ C# 61.2%    ■ Markdown 15.8%  ■ JSON 9.0%    ││ README.md                                1,840 │
│ ■ YAML 6.8%   ■ Shell 3.9%      ■ Other 3.3%   ││ [docs]/guide.md                            320 │
│                                                ││                                                │
└────────────────────────────────────────────────┘└────────────────────────────────────────────────┘
```

Below 80 columns the panels stack vertically; below 40 columns GitPulse says the terminal is too narrow instead of drawing a garbled screen.

## Usage

```sh
gitpulse                      # interactive: banner, scan, then a menu (Overview / Authors / Hotspots)
gitpulse scan                 # scan with a live progress display
gitpulse overview             # dashboard: summary, languages, top authors, top hotspots
gitpulse authors --top 20     # contributor table
gitpulse hotspots --since 2026-06-01   # most-changed files (lines added + deleted)
```

| Option | Meaning | Default |
|--------|---------|---------|
| `-p, --path <DIR>` | Repository to analyze (any folder inside it works) | current directory |
| `-s, --since <DATE>` | Only commits on or after this date (`yyyy-MM-dd`) | all history |
| `-t, --top <N>` | Rows in tables (1–100) | 10 |

When output is redirected (for example in CI), `gitpulse` prints the overview instead of showing the menu.

Exit codes: `0` success, `1` expected error (not a repository, empty repository, no commits in range), `2` invalid options, `70` unexpected error, `130` cancelled with Ctrl+C.

How the numbers are counted:
- Authors are grouped by email (case-insensitive) and shown with the name from their latest commit.
- Merge commits count as commits but add no line changes, like `git log --numstat`, so merged work is not counted twice.
- Languages are measured by file size in the current `HEAD`, by file extension. Unrecognized files are ignored.

## Building

Requires the .NET 10 SDK.

```sh
dotnet build
dotnet test
dotnet run --project src/GitPulse -- overview --path /path/to/repo
```

Rendering is covered by snapshot tests: each renderer's output is compared with a checked-in `*.verified.txt` file under `tests/GitPulse.Tests/Rendering/Snapshots`. When output changes on purpose, review the new `*.received.txt` files and accept them by running the tests with `GITPULSE_UPDATE_SNAPSHOTS=1`.

## Roadmap

| Milestone | Adds |
|-----------|------|
| M1 MVP (in progress) | `scan`, `overview`, `authors`, `hotspots`, interactive menu |
| M2 Visual depth | `heatmap`, calendar view, file tree, commits by weekday/hour |
| M3 Detail and filtering | `commit <sha>` with diff, links, JSON output, branch/author filters |
| M4 Live and export | scan cache, `watch` live dashboard, `report --html` |
| M5 Release | `dotnet tool` package |

## Project docs

- [SCOPE.md](SCOPE.md): vision, goals, Spectre.Console feature coverage matrix
- [MVP.md](MVP.md): first milestone and its acceptance criteria
- [EXECUTION_PLAN.md](EXECUTION_PLAN.md): architecture, milestones and timeline
- [docs/spikes.md](docs/spikes.md): findings from the Phase 0 experiments

## License

[MIT](LICENSE)
