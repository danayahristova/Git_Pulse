# Release checklist

Run this before tagging a release. CI already builds, tests and runs the packaged tool on Ubuntu and Windows. This pass covers what CI can't: real terminals, colors, Unicode, resizing and keyboard input.

## 0. Before you start

- [ ] All milestone pull requests are merged into `main`, and CI on `main` is green.
- [ ] `Version` in `src/GitPulse/GitPulse.csproj` is the version being released, and `CHANGELOG.md` has a section for it.
- [ ] Install the package that CI built (the `gitpulse-package` artifact), or build it locally:
      `dotnet pack src/GitPulse -c Release -o artifacts/packages && dotnet tool install --global GitPulse --add-source artifacts/packages`
- [ ] `gitpulse --version` prints the release version.

## 1. In each terminal

Terminals to cover: **Windows Terminal (PowerShell)**, **Windows PowerShell 5.1 / conhost**, **GNOME Terminal** (or another Linux terminal), **macOS Terminal or iTerm2**. Use a repository with real history; a clone of a public project works well.

| Check | Expected |
|-------|----------|
| `gitpulse` | Centered banner, spinner, progress bars with counts, then the overview and the menu. Arrow keys and typing in the search box both work. |
| Menu: every view | Overview, Top authors, Most-changed files, Activity heatmap, Commit details all render; the screen clears between views. |
| Menu: Filter by author / branch | Checklist with Space and Enter; the header shows the filter; *Clear filters* undoes it. |
| Menu: Date range | Rejects `banana`, a future start date, and an end before the start; defaults are filled in. |
| Menu: Rows to show | Rejects `0` and `500`; `3` changes the tables. |
| Box drawing and symbols | Borders, `█`, `■`, `…`, `▀` (heatmap) and `·` look right; no `?` or boxes. |
| Colors | Heatmap greens, red/green +/− lines, purple accents. `--no-color` and `NO_COLOR=1` turn all of it off. |
| Resize | `gitpulse overview` at 130, 110, 90, 60 and 35 columns: 3 columns with calendar → 2 columns → stacked → "too narrow". |
| `gitpulse watch` | Footer shows "● watching"; making a commit in another window updates it within a few seconds; `r` refreshes; `q` quits cleanly and the prompt comes back. |
| `gitpulse commit` | The picker is grouped by day and searchable; the commit view shows a colored diff; the link opens in the browser (when the terminal supports links). |
| `gitpulse report --html r.html` | Writes the file; running it again asks before overwriting; the page looks right in a browser. |
| Ctrl+C during a scan of a big repository | Prints "Cancelled." and exits; the terminal is left in a normal state. |
| `gitpulse overview > out.txt` | No escape codes in the file; the overview is still readable. |

## 2. Release

- [ ] Merge the release branch; CI on `main` is green.
- [ ] Tag: `git tag -a v1.0.0 -m "GitPulse 1.0.0" && git push origin v1.0.0`
- [ ] Optional, publish to NuGet (public and permanent: a version can be unlisted but never deleted):
      `dotnet nuget push artifacts/packages/GitPulse.1.0.0.nupkg --api-key <key> --source https://api.nuget.org/v3/index.json`
- [ ] Optional: create a GitHub release from the tag with the changelog section and the `.nupkg` attached.
- [ ] Record the demo GIF (`vhs docs/demo.tape`), add it to the README, and take the README screenshot.
