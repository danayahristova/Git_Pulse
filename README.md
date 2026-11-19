# GitPulse

> `cd` into a repo, type `gitpulse`, and understand its history, people and hotspots in 30 seconds.

GitPulse is a cross-platform command-line tool that turns any local Git repository into a rich, interactive terminal dashboard. It is built with [.NET 10](https://dotnet.microsoft.com/), [Spectre.Console](https://spectreconsole.net/) and [LibGit2Sharp](https://github.com/libgit2/libgit2sharp). It is also meant as a showcase of Spectre.Console, so every major feature of the library has a real place in the app.

> **Status:** early development. Nothing is runnable yet. See the planning docs below.

## Planned features

| Command | What it does |
|---------|--------------|
| `gitpulse` | Interactive mode: banner and menu |
| `gitpulse scan` | Analyze the repo with a live progress display |
| `gitpulse overview` | Dashboard with languages, top authors and a summary |
| `gitpulse authors` | Contributor table and bar chart |
| `gitpulse hotspots` | Most-changed files and a file tree |
| `gitpulse heatmap` | GitHub-style activity heatmap |
| `gitpulse commit <sha>` | Single commit detail with diff |
| `gitpulse watch` | Live-updating dashboard |
| `gitpulse report --html <file>` | Export a report to HTML |

Common options: `--path`, `--since`, `--until`, `--branch`, `--author`, `--top N`, `--no-color`.

GitPulse is **read-only**. It never writes to your repository and never touches the network.

## Building (once code lands)

Requires the .NET 10 SDK.

```sh
dotnet build
dotnet test
dotnet run --project src/GitPulse -- overview --path /path/to/repo
```

## Project docs

- [SCOPE.md](SCOPE.md): vision, goals, Spectre.Console feature coverage matrix
- [MVP.md](MVP.md): first milestone and its acceptance criteria
- [EXECUTION_PLAN.md](EXECUTION_PLAN.md): architecture, milestones and timeline

## License

[MIT](LICENSE)
