# GitPulse — Project Scope

## 1. Vision

GitPulse is a cross-platform command-line tool that turns any local Git repository into a rich, interactive terminal dashboard. Its second purpose is to be a **showcase of Spectre.Console**: every major feature of the library should have a natural, useful place in the app.

> One sentence: *"`cd` into a repo, type `gitpulse`, and understand its history, people and hotspots in 30 seconds."*

## 2. Goals

| # | Goal | How we know it's met |
|---|------|----------------------|
| G1 | Give useful insight into a repository's history | Overview answers: who works here, how active is it, what languages, which files change most |
| G2 | Showcase Spectre.Console in depth | Every item in the *Feature Coverage Matrix* (section 6) is used in a real screen |
| G3 | Feel polished | Consistent colors/theme, no broken layouts at 100+ columns, friendly errors |
| G4 | Be a maintainable codebase | Data layer separated from rendering; render code covered by snapshot tests |
| G5 | Work on Windows, Linux and macOS | Manual test pass in Windows Terminal, GNOME Terminal / iTerm2 |

## 3. Target users

- Developers exploring an unfamiliar repository (new job, open-source contribution).
- Maintainers who want a quick health check of their project.
- The author (portfolio piece and Spectre.Console learning project).

## 4. In scope

### 4.1 Data (read-only)
- Read a local Git repository via **LibGit2Sharp** (no writes to the repo, ever).
- Commits: author, date, message, files changed, lines added/removed.
- Branches and tags.
- Working-tree file structure and language detection by file extension.
- Derived metrics: commits per author, commits per day, churn per file ("hotspots"), language share.

### 4.2 Commands (Spectre.Console.Cli)
| Command | Purpose |
|---------|---------|
| `gitpulse` (no args) | Interactive mode: banner + menu |
| `gitpulse scan` | Analyze repo with progress display, cache results |
| `gitpulse overview` | Dashboard: languages, top authors, calendar, summary |
| `gitpulse authors` | Contributor table + bar chart |
| `gitpulse hotspots` | Most-changed files table + file tree |
| `gitpulse heatmap` | GitHub-style activity heatmap |
| `gitpulse commit <sha>` | Single commit detail with diff |
| `gitpulse watch` | Live-updating dashboard |
| `gitpulse report --html <file>` | Export a recorded report to HTML |

Common options: `--path`, `--since`, `--until`, `--branch`, `--author`, `--top N`, `--no-color`.

### 4.3 Presentation
- Theming (one central color palette).
- Responsive layouts that degrade gracefully in narrow terminals.
- Pretty exception output for unexpected errors; friendly messages for expected ones (not a repo, empty repo).

## 5. Out of scope (explicitly)

- Writing to the repository (commit, checkout, rebase, etc.).
- Remote operations (fetch, push) and network access of any kind, including GitHub/GitLab APIs and avatars from the web.
- A full TUI with mouse, focus and multiple simultaneous inputs (that's Terminal.Gui territory, not Spectre).
- Huge monorepos performance tuning (> 500k commits) — best effort only.
- Plugins, config UI, localization.
- Packaging for package managers (winget, Homebrew). A `dotnet tool` package is the only distribution target.

## 6. Spectre.Console feature coverage matrix

| Feature | Where it's used |
|---------|-----------------|
| Markup, colors, styles, emoji | Everywhere (via a central `Theme`) |
| `FigletText` | Banner in interactive mode |
| `Rule` | Section separators |
| `Panel`, `Padder`, `Align` | Headers, summary cards, commit details |
| `Table` | Authors, hotspots, branches |
| `Tree` | Directory structure with churn coloring |
| `Grid`, `Columns`, `Rows` | Summary cards, side-by-side stats |
| `Layout` | Full-screen overview and watch dashboard |
| `BarChart` | Top authors, commits by weekday/hour |
| `BreakdownChart` | Language share |
| `Calendar` | Commit days in a month |
| `Canvas` | 52×7 activity heatmap |
| `TextPath` | Long file paths in tables |
| `Status` (spinner) | Opening repo, loading cache |
| `Progress` (multi-task, custom columns) | `scan` command |
| `Live` | `watch` command |
| `SelectionPrompt` (groups, search) | Interactive main menu, pick a commit |
| `MultiSelectionPrompt` | Pick branches / authors to filter |
| `TextPrompt<T>` (validation, defaults) | Date range, top-N |
| `ConfirmationPrompt` | Overwrite report file |
| `[link]` markup | Commit links to remote URL (if a remote is configured; URL only, no network) |
| `JsonText` (Spectre.Console.Json) | `commit <sha> --raw` |
| `WriteException` | Unhandled errors |
| Recording + `ExportHtml` | `report --html` |
| Spectre.Console.Cli | All commands, settings, validation, help |
| Spectre.Console.Testing | Snapshot tests of renders |
| *(stretch)* `CanvasImage` (Spectre.Console.ImageSharp) | Render repo logo found in the working tree |

## 7. Constraints and known limitations

- **Prompts cannot be used inside `Live`, `Progress` or `Status`** and are not thread-safe. Watch mode must read keys manually (`Console.KeyAvailable` / `ReadKey`) and exit the live display before any prompt.
- Spectre.Console is pre-1.0; pin exact package versions.
- Spectre.Console.Cli is versioned separately from Spectre.Console; pin both.
- Target framework: **.NET 10** (LTS).
- Terminal must support ANSI; redirected output falls back to plain text.

## 8. Risks

| Risk | Impact | Mitigation |
|------|--------|-----------|
| LibGit2Sharp native binaries fail on some OS | Can't read repos | Test all 3 OSes early (Phase 0); fallback idea: shell out to `git log --numstat` |
| Diff stats slow on big repos | Long scans | Cache results in `.gitpulse/cache.json`; `--since` filter; progress display |
| Layouts break in small terminals | Looks bad | Minimum width check + compact fallback rendering |
| Scope creep | Never finishes | MVP defined separately; everything else is stretch |

## 9. Definition of done (whole project)

- All commands in 4.2 implemented.
- Every row in the coverage matrix used.
- Snapshot tests for each renderer; CI green on Windows + Linux.
- README with screenshots/GIF and an exported HTML sample report.
- Published as a `dotnet tool` (local or NuGet).
