# Changelog

## 1.0.0

First release, packaged as a .NET tool (`dotnet tool install --global GitPulse`).

### Commands
- `gitpulse`: interactive mode with a banner, a scan with progress, and a searchable menu of every view, commit details and filters.
- `scan`: scan with a multi-stage progress display; fills the cache.
- `overview`: dashboard with summary, languages, top authors and top hotspots, plus a calendar of the last active month at 120+ columns.
- `authors`, `hotspots` (with a folder tree colored by churn), `heatmap` (52-week activity grid, commits by weekday and time of day).
- `commit [REVISION]`: header, message, file list with renames, colored diff, link to the commit on GitHub/GitLab/Bitbucket/Codeberg; `--raw` JSON; `--no-diff`; a commit picker when no revision is given.
- `watch`: live overview that refreshes when commits, branches or tags change.
- `report --html FILE`: overview, activity, authors and hotspots in one standalone HTML page.

### Options
- `--path`, `--since`, `--until`, `--branch` (repeatable), `--author` (repeatable), `--top`, `--no-cache`, `--no-color`, `--version`.

### Behaviour
- Read-only, no network access. Credentials in remote URLs are never shown.
- Scan cache in the user cache folder (never in the repository); later scans only diff new commits.
- First scans diff commits in parallel.
- Layouts adapt to the terminal width (wide, medium, narrow, compact, too narrow).
- Friendly errors with stable exit codes: 0, 1 (expected error), 2 (invalid options), 70 (unexpected), 130 (cancelled).
