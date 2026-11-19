# GitPulse — Execution Plan

Time estimates assume **~8–10 hours per week** of part-time work. Each milestone ends with something runnable and demo-able.

## 1. Architecture (decide once, before coding)

```
GitPulse.sln
├─ src/GitPulse/
│  ├─ Program.cs              # CommandApp setup
│  ├─ Commands/               # Spectre.Console.Cli commands + Settings classes
│  ├─ Git/                    # LibGit2Sharp access → raw data (no Spectre here)
│  ├─ Analysis/               # Pure functions: raw data → stats models
│  ├─ Models/                 # RepoSnapshot, AuthorStats, FileChurn, ...
│  ├─ Rendering/              # IRenderable builders (no Git here)
│  │   ├─ Theme.cs
│  │   ├─ OverviewRenderer.cs
│  │   └─ ...
│  └─ Interactive/            # Menu loop, prompts
└─ tests/GitPulse.Tests/
   ├─ Analysis/               # Unit tests on stats
   └─ Rendering/              # Snapshot tests via TestConsole
```

**Rules**
1. `Git` → `Analysis` → `Models` → `Rendering` is a one-way flow.
2. Renderers take models and **return `IRenderable`** — they never write to the console themselves. This makes them testable and reusable in `Layout`, `Live` and HTML export.
3. Commands are thin: parse settings → call services → write renderables.
4. All colors come from `Theme`.

## 2. Milestones

### Phase 0 — Setup and spikes (week 1)
| Task | Output |
|------|--------|
| Create solution, projects, `Directory.Packages.props`, `.editorconfig` | Builds |
| Spike: LibGit2Sharp reads commits + diff stats on Windows **and** Linux | Risk retired (or switch to `git log --numstat`) |
| Spike: render a `Layout` with a `BarChart` and `BreakdownChart` | Know the widgets' limits |
| Set up CI (GitHub Actions, Windows + Ubuntu, `dotnet test`) | Green pipeline |

**Exit criteria:** an empty `gitpulse` command runs in CI on both OSes.

### Milestone 1 — MVP (weeks 2–4)
| # | Task | Spectre features |
|---|------|------------------|
| 1.1 | `CommandApp` with `scan`, `overview`, `authors`, `hotspots`; shared `RepoSettings` (`--path`, `--since`, `--top`) with validation | Spectre.Console.Cli |
| 1.2 | `Git/RepositoryReader`: commits + per-file line stats | — |
| 1.3 | `Analysis`: author stats, file churn, language share, summary | — |
| 1.4 | Scan with multi-task progress | `Progress`, `Status` |
| 1.5 | `Theme` + overview renderer | `Layout`, `Panel`, `Grid`, `BarChart`, `BreakdownChart`, `Rule` |
| 1.6 | Authors and hotspots renderers | `Table`, `TextPath` |
| 1.7 | Interactive mode: banner + menu loop | `FigletText`, `SelectionPrompt` |
| 1.8 | Error handling: friendly panels + `WriteException` | `Panel`, `WriteException` |
| 1.9 | Snapshot tests with fake dataset | Spectre.Console.Testing |
| 1.10 | README + screenshot | — |

**Exit criteria:** all MVP acceptance criteria in `MVP.md` checked. **Tag `v0.1.0`.**

### Milestone 2 — Visual depth (weeks 5–6)
| # | Task | Spectre features |
|---|------|------------------|
| 2.1 | `heatmap` command: 52×7 grid, 5 intensity levels | `Canvas` |
| 2.2 | Month view of commit days in overview | `Calendar` |
| 2.3 | File tree with churn coloring, collapse deep folders | `Tree` |
| 2.4 | Commits by weekday / hour charts | `BarChart`, `Columns` |
| 2.5 | Responsive breakpoints (≥120, ≥100, ≥80, compact) | `Layout`, `Rows` |

**Exit:** `v0.2.0` — heatmap and tree look great in a screenshot.

### Milestone 3 — Detail and filtering (weeks 7–8)
| # | Task | Spectre features |
|---|------|------------------|
| 3.1 | `commit <sha>`: header panel, message, files table, colored diff | `Panel`, `Table`, markup |
| 3.2 | Clickable links to the commit on the remote (built from remote URL) | `[link]` markup |
| 3.3 | `--raw` JSON output | `JsonText` |
| 3.4 | Pick a commit from recent history with search | `SelectionPrompt` + search |
| 3.5 | Interactive filters: branches and authors | `MultiSelectionPrompt` |
| 3.6 | Date-range prompt with validation and defaults | `TextPrompt<DateOnly>` |

**Exit:** `v0.3.0`.

### Milestone 4 — Live, export, performance (weeks 9–11)
| # | Task | Spectre features |
|---|------|------------------|
| 4.1 | Cache scan results in `.gitpulse/cache.json`, incremental update by last SHA | `Status` |
| 4.2 | `watch`: overview refreshes on new commits (`FileSystemWatcher` on `.git`), `q` to quit, `r` to refresh — keys read manually | `Live`, `Layout` |
| 4.3 | `report --html out.html`: record renders, export, confirm overwrite | Recording, `ExportHtml`, `ConfirmationPrompt` |
| 4.4 | Performance pass on a large repo (e.g. dotnet/runtime clone) | `Progress` |

**Exit:** `v0.4.0` — sample HTML report committed to repo.

### Milestone 5 — Release (week 12)
| # | Task |
|---|------|
| 5.1 | Package as `dotnet tool` (`PackAsTool`, command name `gitpulse`) |
| 5.2 | README: GIF demo (e.g. recorded with VHS or asciinema), feature coverage matrix, install instructions |
| 5.3 | Manual test pass: Windows Terminal, PowerShell, GNOME Terminal, macOS Terminal/iTerm2 |
| 5.4 | Tag **`v1.0.0`**, publish to NuGet (optional) |

### Stretch (any time after v1.0)
- Repo logo via `CanvasImage` (Spectre.Console.ImageSharp).
- `compare <branchA> <branchB>`.
- Theme switching (`--theme dark|light|mono`).

## 3. Timeline at a glance

| Week | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
|------|---|---|---|---|---|---|---|---|---|----|----|----|
| Phase | P0 | M1 | M1 | M1 | M2 | M2 | M3 | M3 | M4 | M4 | M4 | M5 |

## 4. Working practices

- **Branch per task**, small PRs, conventional commit messages (`feat:`, `fix:`, `test:`).
- **GitHub Issues** — one per task ID above (e.g. `1.5 Overview renderer`), grouped by milestone.
- **Test first for `Analysis`**, snapshot test right after for each renderer.
- **Dogfood:** run GitPulse on its own repository after every milestone.
- **Demo checkpoint** at each milestone: take a screenshot, update README.

## 5. Risk checkpoints

| When | Check | If it fails |
|------|-------|-------------|
| End of Phase 0 | LibGit2Sharp works on both OSes | Switch Git layer to parsing `git log --numstat` |
| End of M1 | Scan of 5k commits < 30 s | Pull caching (4.1) forward into M2 |
| End of M2 | Layout readable at 80 columns | Add compact mode before M3 |
| Start of M4 | Live + key handling stable | Ship `watch` as polling refresh every N seconds |

## 6. First three things to do today

1. `dotnet new console -n GitPulse -o src/GitPulse` and `dotnet new xunit -o tests/GitPulse.Tests`.
2. Add Spectre.Console, Spectre.Console.Cli and LibGit2Sharp; print the last 10 commits of the current repo in a `Table`.
3. Push to GitHub and add the CI workflow.
