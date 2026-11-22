# GitPulse

> `cd` into a repo, type `gitpulse`, and understand its history, people and hotspots in 30 seconds.

GitPulse is a cross-platform command-line tool that turns any local Git repository into a rich, interactive terminal dashboard: who works on it, how active it is, which languages it uses, which files change most, and what any single commit did. It is built with [.NET 10](https://dotnet.microsoft.com/), [Spectre.Console](https://spectreconsole.net/) and [LibGit2Sharp](https://github.com/libgit2/libgit2sharp), and doubles as a showcase of Spectre.Console: every major feature of the library has a real place in the app (see the [coverage matrix](#spectreconsole-coverage)).

GitPulse is **read-only**. It never writes to your repository and never touches the network.

## Install

GitPulse is a [.NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools) and needs the [.NET 10 runtime](https://dotnet.microsoft.com/download) or newer.

Once it is published on NuGet:

```sh
dotnet tool install --global GitPulse
gitpulse --version
```

From source, today:

```sh
git clone https://github.com/danayahristova/Git_Pulse.git
cd Git_Pulse
dotnet pack src/GitPulse -c Release -o artifacts/packages
dotnet tool install --global GitPulse --add-source artifacts/packages
```

Every CI run on Ubuntu also uploads the package as the `gitpulse-package` artifact. Update with `dotnet tool update --global GitPulse`, remove with `dotnet tool uninstall --global GitPulse`.

## Quick start

```sh
cd path/to/some/repo
gitpulse              # banner, scan with progress, then a menu: views, commit details, filters
```

GitPulse reads repositories on your machine. To look at one from GitHub, clone it first (a normal, full clone).

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

`gitpulse heatmap` shows a year of activity (colored cells in a terminal, shade characters when output is redirected or `--no-color` is used):

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

A complete HTML report made from made-up data is in [docs/sample-report.html](docs/sample-report.html).

## Commands

```sh
gitpulse                      # interactive: banner, scan, then a menu (views, commit details, filters)
gitpulse scan                 # scan with a live progress display (and fill the cache)
gitpulse overview             # dashboard: summary, languages, top authors, top hotspots (+ calendar at 120+ columns)
gitpulse authors --top 20     # contributor table
gitpulse hotspots             # most-changed files (lines added + deleted) and a folder tree
gitpulse heatmap              # activity heatmap, commits by weekday and by time of day
gitpulse commit HEAD~2        # one commit: header, message, changed files, colored diff, link
gitpulse commit 1a2b3c4 --raw # the same commit as JSON
gitpulse commit               # pick a commit from recent history (interactive terminals)
gitpulse watch                # live overview that refreshes when new commits arrive (q quits, r refreshes)
gitpulse report --html report.html   # overview, activity, authors and hotspots in one HTML file
gitpulse overview --author ada --branch main --since 2026-01-01 --until 2026-06-30
```

`gitpulse --help` lists everything; `gitpulse <command> --help` shows a command's options and examples.

### Options

For `scan`, `overview`, `authors`, `hotspots`, `heatmap`, `watch`, `report` and the interactive mode:

| Option | Meaning | Default |
|--------|---------|---------|
| `-p, --path <DIR>` | Repository to analyze (any folder inside it works) | current directory |
| `-s, --since <DATE>` | Only commits on or after this date (`yyyy-MM-dd`) | all history |
| `-u, --until <DATE>` | Only commits on or before this date (`yyyy-MM-dd`) | all history |
| `-b, --branch <NAME>` | Only commits reachable from this local branch; repeat for several | `HEAD` |
| `-a, --author <TEXT>` | Only commits whose author name or email contains this text (any case); repeat for several | everyone |
| `-t, --top <N>` | Rows in tables (1–100) | 10 |
| `--no-cache` | Don't read or write the [scan cache](#scan-cache) | cache on |
| `--no-color` | Plain output without colors (`NO_COLOR` works too) | colors on |

More options:
- `watch --interval <SECONDS>`: how often to check for changes (1–60, default 2).
- `report --html <FILE>` (required) and `report --force`: overwrite without asking. Without `--force`, GitPulse asks in a terminal and refuses when there is none.
- `commit [REVISION]`: a SHA or its start, `HEAD~N`, a branch or a tag; leave it out to pick from a list. `--raw` prints JSON (stats per file, no diff text; colored in a terminal, plain when redirected). `--no-diff` shows the header and file list only. `-p`/`--path` and `--no-color` work too.
- `--version` prints the version.

The commit view links to the commit on GitHub, GitLab, Bitbucket or Codeberg when the `origin` remote points there. The link is built from the remote URL only (no network), and any username or token in that URL is never shown.

In the interactive menu, *Filter by author*, *Filter by branch* and *Date range* change what every view shows; *Clear filters* resets them. When output is redirected (for example in CI), `gitpulse` prints the overview instead of showing the menu.

Exit codes: `0` success, `1` expected error (not a repository, empty repository, no commits for the filters, unknown branch or commit, existing report file), `2` invalid options, `70` unexpected error, `130` cancelled with Ctrl+C.

### How the numbers are counted

- Authors are grouped by email (case-insensitive) and shown with the name from their latest commit.
- Merge commits count as commits but add no line changes, like `git log --numstat`, so merged work is not counted twice.
- Churn is lines added + lines deleted. Renames inside a commit are detected for the commit view.
- Languages are measured by file size in the current `HEAD`, by file extension. Unrecognized files are ignored.
- Days, weekdays and hours use each commit's own time zone (the author's local time). Weeks start on Monday.
- The heatmap ends on the day of the last commit, so older repositories still show their activity. Its 4 shades are relative to the busiest day shown.

## Scan cache

The first scan of a repository computes line counts for every commit (in parallel); later scans reuse them and only diff new commits. xUnit (3,283 commits) takes about 3.7 s the first time and 0.7 s after that.

The cache is one JSON file per repository in your user cache folder: `~/.cache/gitpulse` on Linux and WSL, `%LOCALAPPDATA%\GitPulse\Cache` on Windows, `~/Library/Caches/GitPulse` on macOS. Set `GITPULSE_CACHE_DIR` to put it somewhere else. It is never written inside the repository. Deleting it is always safe; it is rebuilt on the next scan. `--no-cache` skips it.

## Spectre.Console coverage

Where each feature from the [scope](SCOPE.md#6-spectreconsole-feature-coverage-matrix) is used:

| Feature | Where |
|---------|-------|
| Markup, colors, styles | Everywhere, through one central `Theme` |
| `FigletText` | Interactive banner |
| `Rule` | Commit diff sections, `scan` result |
| `Panel`, `Padder`, `Align` | Every dashboard panel; commit message indent; centered banner |
| `Table` | `authors`, `hotspots`, commit file list |
| `Tree` | `hotspots` folder tree, colored by churn |
| `Grid`, `Columns`, `Rows` | Summary card and hotspot list; weekday/hour charts side by side; stacked compact layouts |
| `Layout` | Overview dashboard (2 or 3 columns), `watch` |
| `BarChart` | Top authors, commits by weekday and by time of day |
| `BreakdownChart` | Language share |
| `Calendar` | Last active month in the overview (120+ columns) |
| `Canvas` | 52 × 7 activity heatmap |
| `TextPath` | Long file paths in tables and lists |
| `Status` | Opening the repository, loading/saving the cache, writing the report |
| `Progress` (multi-task, custom column) | Scans: three stages with a custom count column |
| `Live` | `watch` |
| `SelectionPrompt` (groups, search) | Interactive menu (search); commit picker (grouped by day, search) |
| `MultiSelectionPrompt` | Author and branch filters |
| `TextPrompt<T>` (validation, defaults) | Rows to show (`int`), date range (`DateOnly`) |
| `ConfirmationPrompt` | Overwriting an existing report |
| `[link]` markup | Commit link to the remote host |
| `JsonText` (Spectre.Console.Json) | `commit --raw` in a terminal |
| `WriteException` | Unexpected errors |
| Recording + HTML export | `report --html`, with GitPulse's own escaping HTML encoder (see [docs/spikes.md](docs/spikes.md)) |
| Spectre.Console.Cli | All commands, settings, validation, help, examples |
| Spectre.Console.Testing | Snapshot, CLI and interactive tests |
| *(stretch, not done)* `CanvasImage` | — |

## Building and testing

Requires the .NET 10 SDK.

```sh
dotnet build
dotnet test
dotnet run --project src/GitPulse --no-launch-profile -- overview --path /path/to/repo
```

Rendering is covered by snapshot tests: each renderer's output is compared with a checked-in `*.verified.txt` file under `tests/GitPulse.Tests/Rendering/Snapshots`. When output changes on purpose, review the new `*.received.txt` files and accept them by running the tests with `GITPULSE_UPDATE_SNAPSHOTS=1`. Tests use a temporary scan cache and never touch yours.

To record the demo GIF, install [VHS](https://github.com/charmbracelet/vhs) and run `vhs docs/demo.tape` in a folder with an interesting repository; it writes `docs/demo.gif`.

## Project docs

- [SCOPE.md](SCOPE.md): vision, goals, Spectre.Console feature coverage matrix
- [MVP.md](MVP.md): first milestone and its acceptance criteria
- [EXECUTION_PLAN.md](EXECUTION_PLAN.md): architecture, milestones and timeline
- [docs/spikes.md](docs/spikes.md): findings from experiments and performance measurements
- [docs/release-checklist.md](docs/release-checklist.md): the manual test pass before a release
- [CHANGELOG.md](CHANGELOG.md): what changed in each version

## License

[MIT](LICENSE)
