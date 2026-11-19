# GitPulse — MVP

## 1. Purpose of the MVP

Prove the full pipeline works end-to-end — **read repo → compute stats → render beautifully → interactive menu** — with the smallest set of features that is already useful and already looks impressive.

The MVP should take **about 2–3 weeks** of part-time work.

## 2. MVP user story

> As a developer, I run `gitpulse` inside a repository, watch a progress display while it scans, then pick from a menu to see an overview dashboard, the top authors, or the most-changed files. I can return to the menu and exit when I'm done.

## 3. Included features

### 3.1 Commands
| Command | MVP behavior |
|---------|--------------|
| `gitpulse` | Figlet banner → scan → main menu loop (`SelectionPrompt`) until **Exit** |
| `gitpulse scan` | Multi-task `Progress`: *Reading commits*, *Computing diff stats*, *Detecting languages* |
| `gitpulse overview` | `Layout` with header `Panel`, `BreakdownChart` (languages), `BarChart` (top 5 authors), summary `Grid` (commits, authors, first/last commit, branches) |
| `gitpulse authors` | `Table`: name, commits, +lines, −lines, first/last commit; `--top N` |
| `gitpulse hotspots` | `Table` of top N files by churn, paths rendered with `TextPath` |

### 3.2 Options
- `--path <dir>` (default: current directory)
- `--top <N>` (default: 10, validated 1–100)
- `--since <date>` (validated)

### 3.3 Quality
- Friendly error panel for: not a Git repo, empty repo, invalid options.
- `AnsiConsole.WriteException` for unexpected exceptions.
- Central `Theme` class (colors used consistently).
- Snapshot tests (Spectre.Console.Testing) for overview, authors and hotspots renderers using a fixed fake dataset.

### 3.4 Spectre.Console features covered by the MVP
Markup · `FigletText` · `Rule` · `Panel` · `Grid` · `Table` · `TextPath` · `Layout` · `BarChart` · `BreakdownChart` · `Status` · `Progress` · `SelectionPrompt` · `TextPrompt<T>` · `WriteException` · Spectre.Console.Cli · Spectre.Console.Testing

That is ~60% of the coverage matrix in the scope document.

## 4. Not in the MVP (deferred)

| Feature | Deferred to |
|---------|-------------|
| `Tree` view of files | Milestone 2 |
| `heatmap` (`Canvas`) and `Calendar` | Milestone 2 |
| `commit <sha>` detail, diff, `JsonText`, links | Milestone 3 |
| Filters with `MultiSelectionPrompt` (branches/authors) | Milestone 3 |
| `watch` (`Live`) | Milestone 4 |
| `report --html` (recording/export), `ConfirmationPrompt` | Milestone 4 |
| Caching scan results | Milestone 4 |
| `CanvasImage` logo | Stretch |
| `dotnet tool` packaging | Milestone 5 |

## 5. Technical decisions for the MVP

- **.NET 10**, single console project + one test project.
- **Packages:** `Spectre.Console`, `Spectre.Console.Cli`, `LibGit2Sharp`, `Spectre.Console.Testing` (tests), `xunit`, `Verify.Xunit` (snapshots). Exact versions pinned in `Directory.Packages.props`.
- **No cache yet**: interactive mode scans once per run and keeps results in memory.
- Data and rendering are separated from day one (see execution plan architecture), so later milestones don't require rewrites.

## 6. MVP acceptance criteria

- [ ] `gitpulse` in a real repo (e.g. a clone of Spectre.Console itself) shows banner, progress, menu; menu loops until Exit.
- [ ] Overview, authors and hotspots render correctly at 120 and 100 columns.
- [ ] Below 80 columns, a compact layout or a clear "terminal too narrow" message is shown — never a garbled screen.
- [ ] Running outside a repo shows a friendly error and exit code ≠ 0.
- [ ] `--top 0` / `--since banana` produce validation errors from Spectre.Console.Cli.
- [ ] Scanning a repo with ~5,000 commits finishes in under 30 seconds with visible progress.
- [ ] Snapshot tests pass; `dotnet test` green on Windows and Linux.
- [ ] README with one screenshot.
