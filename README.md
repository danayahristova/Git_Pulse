# GitPulse

> `cd` into a repo, type `gitpulse`, and understand its history, people and hotspots in 30 seconds.

GitPulse is a cross-platform command-line tool that turns any local Git repository into a rich, interactive terminal dashboard. It is built with [.NET 10](https://dotnet.microsoft.com/), [Spectre.Console](https://spectreconsole.net/) and [LibGit2Sharp](https://github.com/libgit2/libgit2sharp). It is also meant as a showcase of Spectre.Console, so every major feature of the library has a real place in the app.

GitPulse is **read-only**. It never writes to your repository and never touches the network.

> **Status:** Milestones 1 (MVP) and 2 (visual depth) are done; Milestone 3 (detail and filtering) is in progress. The commands below work; later milestones add the rest (see [Roadmap](#roadmap)).

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

At 120+ columns the overview adds a third column with a calendar of the last active month. Below 80 columns the panels stack vertically; below 40 columns GitPulse says the terminal is too narrow instead of drawing a garbled screen.

`gitpulse heatmap` shows a year of activity (colored cells in a terminal, shade characters when output is redirected):

```text
┌─Activity · last 36 weeks─────────────────────────────────────────────────────┐
│     Mar   Apr     May     Jun       Jul     Aug       Sep     Oct     Nov    │
│ Mon · ░ · · ▒ ░ ░ · · · · ░ · · · · · · · · · · · ░ ░ · · ░ ░ ▒ · · · · ░ ░  │
│     ░ ░ ░ · ░ ░ ░ ░ ░ ░ ░ ░ · · · · · ░ · · · · ░ ░ · ░ ░ ░ · ░ ▓ ░ ░ ░ ░ ·  │
│ Wed · █ █ ░ ░ ░ ░ · · · · ░ ▒ · · · · ░ · · · · · · · ░ ░ · · ░ ░ · ░ · · ▒  │
│     · ░ ░ ▒ · ░ · ░ · · · · · · · · · ░ · · · · · · · · · · ▒ · · · · · · ░  │
│ Fri ░ ░ · ░ ░ ░ ░ ░ · ░ · · ░ · · · · ░ · ░ ░ ░ ░ · · · · ░ · ░ · · · · ░    │
│     · ░ ░ ░ · · ░ · · ░ ░ · ░ · · · · · · · · · ░ · · ░ ░ ░ · · · · · · ·    │
│     · ░ · ░ · · ░ · ░ · ░ ▓ · · · · · · · · · ░ ░ · · ░ ░ · · · · · · ░ ·    │
│                                                                              │
│     Less · ░ ▒ ▓ █ More                                                      │
│                                                                              │
│ 208 commits on 97 days · busiest day 2026-04-01 (14 commits)                 │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Usage

```sh
gitpulse                      # interactive: banner, scan, then a menu (views, commit details, filters)
gitpulse scan                 # scan with a live progress display
gitpulse overview             # dashboard: summary, languages, top authors, top hotspots (+ calendar at 120+ columns)
gitpulse authors --top 20     # contributor table
gitpulse hotspots --since 2026-06-01   # most-changed files (lines added + deleted) and a folder tree
gitpulse heatmap              # activity heatmap, commits by weekday and by time of day
gitpulse commit HEAD~2        # one commit: header, message, changed files, colored diff, link
gitpulse commit 1a2b3c4 --raw # the same commit as JSON
gitpulse commit               # pick a commit from recent history (interactive terminals)
gitpulse overview --author ada --branch main --since 2026-01-01 --until 2026-06-30
```

GitPulse only reads repositories on your machine. To look at one from GitHub, clone it first (a normal, full clone).

Options for `scan`, `overview`, `authors`, `hotspots`, `heatmap` and the interactive mode:

| Option | Meaning | Default |
|--------|---------|---------|
| `-p, --path <DIR>` | Repository to analyze (any folder inside it works) | current directory |
| `-s, --since <DATE>` | Only commits on or after this date (`yyyy-MM-dd`) | all history |
| `-u, --until <DATE>` | Only commits on or before this date (`yyyy-MM-dd`) | all history |
| `-b, --branch <NAME>` | Only commits reachable from this local branch; repeat for several | `HEAD` |
| `-a, --author <TEXT>` | Only commits whose author name or email contains this text (any case); repeat for several | everyone |
| `-t, --top <N>` | Rows in tables (1–100) | 10 |

Options for `commit [REVISION]`:

| Option | Meaning |
|--------|---------|
| `REVISION` | A SHA or its start, `HEAD~N`, a branch or a tag. Leave out to pick from a list (interactive only). |
| `-p, --path <DIR>` | Repository to read |
| `--raw` | Print JSON (stats per file, no diff text). Colored in a terminal, plain when redirected. |
| `--no-diff` | Header and file list only |

The commit view links to the commit on GitHub, GitLab, Bitbucket or Codeberg when the `origin` remote points there. The link is built from the remote URL only (no network), and any username or token in that URL is never shown.

In the interactive menu, *Filter by author*, *Filter by branch* and *Date range* change what every view shows; *Clear filters* resets them.

When output is redirected (for example in CI), `gitpulse` prints the overview instead of showing the menu.

Exit codes: `0` success, `1` expected error (not a repository, empty repository, no commits for the filters, unknown branch or commit), `2` invalid options, `70` unexpected error, `130` cancelled with Ctrl+C.

How the numbers are counted:
- Authors are grouped by email (case-insensitive) and shown with the name from their latest commit.
- Merge commits count as commits but add no line changes, like `git log --numstat`, so merged work is not counted twice.
- Languages are measured by file size in the current `HEAD`, by file extension. Unrecognized files are ignored.
- Days, weekdays and hours use each commit's own time zone (the author's local time). Weeks start on Monday.
- The heatmap ends on the day of the last commit, so older repositories still show their activity. Its 4 shades are relative to the busiest day shown.

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
| M1 MVP (done) | `scan`, `overview`, `authors`, `hotspots`, interactive menu |
| M2 Visual depth (done) | `heatmap`, calendar view, file tree, commits by weekday/hour |
| M3 Detail and filtering (in progress) | `commit <sha>` with diff, links, JSON output, branch/author filters |
| M4 Live and export | scan cache, `watch` live dashboard, `report --html` |
| M5 Release | `dotnet tool` package |

## Project docs

- [SCOPE.md](SCOPE.md): vision, goals, Spectre.Console feature coverage matrix
- [MVP.md](MVP.md): first milestone and its acceptance criteria
- [EXECUTION_PLAN.md](EXECUTION_PLAN.md): architecture, milestones and timeline
- [docs/spikes.md](docs/spikes.md): findings from the Phase 0 experiments

## License

[MIT](LICENSE)
