using System.Globalization;
using GitPulse.Analysis;
using GitPulse.Commands;
using GitPulse.Git;
using GitPulse.Models;
using GitPulse.Rendering;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace GitPulse.Interactive;

/// <summary>
/// <c>gitpulse</c> with no command: banner, one scan, then a menu loop until Exit.
/// When the console is not interactive (redirected output, CI) it prints the overview instead.
/// </summary>
internal sealed class InteractiveCommand(IAnsiConsole console) : Command<RepoSettings>
{
    public override int Execute(CommandContext context, RepoSettings settings, CancellationToken cancellationToken)
    {
        var interactive = console.Profile.Capabilities.Interactive;
        if (interactive)
        {
            console.Write(Banner.Render(console.Profile.Width));
        }

        // The first scan's errors (not a repo, no commits for the options) end the program as usual.
        var session = new Session(console, settings, cancellationToken);

        if (!interactive)
        {
            console.WriteLine();
            console.Write(OverviewRenderer.Render(session.Stats, console.Profile.Width));
            console.WriteLine();
            return 0;
        }

        session.Run();
        return 0;
    }

    private enum MenuItem
    {
        Overview,
        Authors,
        Hotspots,
        Activity,
        CommitDetails,
        FilterAuthors,
        FilterBranches,
        DateRange,
        ClearFilters,
        RowCount,
        Exit,
    }

    /// <summary>
    /// The state of one interactive run: the current filter, the scanned snapshot and the view.
    /// Author filters re-run the analysis on the same snapshot; date and branch filters scan again.
    /// </summary>
    private sealed class Session
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private readonly IAnsiConsole _console;
        private readonly string _path;
        private readonly bool _useCache;
        private readonly CancellationToken _cancellationToken;

        private ScanFilter _filter;
        private RepoSnapshot _snapshot;
        private int _top;
        private MenuItem _view = MenuItem.Overview;
        private CommitDetail? _commit;
        private IRenderable? _notice;

        public Session(IAnsiConsole console, RepoSettings settings, CancellationToken cancellationToken)
        {
            _console = console;
            _path = settings.Path;
            _useCache = !settings.NoCache;
            _cancellationToken = cancellationToken;
            _top = settings.Top;
            _filter = settings.ToFilter();
            _snapshot = ScanDisplay.Scan(console, _path, _filter, _useCache, cancellationToken).Snapshot;
            Stats = RepoAnalyzer.Analyze(_snapshot);
        }

        public RepoStats Stats { get; private set; }

        public void Run()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                _console.Clear();
                _console.Write(RenderView(_console.Profile.Width));
                _console.WriteLine();

                if (_notice is not null)
                {
                    _console.Write(_notice);
                    _console.WriteLine();
                    _notice = null;
                }

                var choice = _console.Prompt(Menu());
                switch (choice)
                {
                    case MenuItem.Exit:
                        return;
                    case MenuItem.RowCount:
                        _top = AskRowCount(_top);
                        break;
                    case MenuItem.CommitDetails:
                        ShowCommit();
                        break;
                    case MenuItem.FilterAuthors:
                        FilterAuthors();
                        break;
                    case MenuItem.FilterBranches:
                        FilterBranches();
                        break;
                    case MenuItem.DateRange:
                        AskDateRange();
                        break;
                    case MenuItem.ClearFilters:
                        Apply(ScanFilter.None);
                        break;
                    default:
                        _view = choice;
                        break;
                }
            }
        }

        private IRenderable RenderView(int width) => _view switch
        {
            MenuItem.Authors => AuthorsRenderer.Render(Stats, _top, width),
            MenuItem.Hotspots => HotspotsRenderer.Render(Stats, _top, width),
            MenuItem.Activity => ActivityRenderer.Render(Stats, width),
            MenuItem.CommitDetails when _commit is not null => CommitRenderer.Render(_commit, width),
            _ => OverviewRenderer.Render(Stats, width),
        };

        private SelectionPrompt<MenuItem> Menu()
        {
            var title = Theme.Title("What next?");
            if (!_filter.IsEmpty)
            {
                title += " " + Theme.Dim("· " + _filter.Describe());
            }

            var prompt = new SelectionPrompt<MenuItem>()
                .Title(title)
                .PageSize(12)
                .HighlightStyle(new Style(Theme.Primary, decoration: Decoration.Bold))
                .MoreChoicesText(Theme.Dim("(move up and down to see more)"))
                .EnableSearch()
                .SearchPlaceholderText(Theme.Dim("type to search"))
                .UseConverter(item => item switch
                {
                    MenuItem.Overview => "Overview dashboard",
                    MenuItem.Authors => "Top authors",
                    MenuItem.Hotspots => "Most-changed files",
                    MenuItem.Activity => "Activity heatmap",
                    MenuItem.CommitDetails => "Commit details…",
                    MenuItem.FilterAuthors => "Filter by author…" + Count(_filter.AuthorList.Count + _filter.ExactAuthorList.Count),
                    MenuItem.FilterBranches => "Filter by branch…" + Count(_filter.BranchList.Count),
                    MenuItem.DateRange => "Date range…" + (_filter.Since is null && _filter.Until is null ? string.Empty : Theme.Dim(" (set)")),
                    MenuItem.ClearFilters => "Clear filters",
                    MenuItem.RowCount => $"Rows to show ({_top})",
                    MenuItem.Exit => "Exit",
                    _ => item.ToString(),
                });

            prompt.AddChoices(MenuItem.Overview, MenuItem.Authors, MenuItem.Hotspots, MenuItem.Activity, MenuItem.CommitDetails);
            prompt.AddChoices(MenuItem.FilterAuthors, MenuItem.FilterBranches, MenuItem.DateRange);
            if (!_filter.IsEmpty)
            {
                prompt.AddChoice(MenuItem.ClearFilters);
            }

            prompt.AddChoices(MenuItem.RowCount, MenuItem.Exit);
            return prompt;

            static string Count(int n) => n == 0 ? string.Empty : Theme.Dim($" ({n} selected)");
        }

        /// <summary>Switches to <paramref name="filter"/>, scanning again only when dates or branches change.</summary>
        private void Apply(ScanFilter filter)
        {
            var rescan = filter.Since != _filter.Since
                         || filter.Until != _filter.Until
                         || !filter.BranchList.SequenceEqual(_filter.BranchList);
            try
            {
                var snapshot = rescan
                    ? ScanDisplay.Scan(_console, _path, filter, _useCache, _cancellationToken).Snapshot
                    : _snapshot with { Filter = filter };
                var stats = RepoAnalyzer.Analyze(snapshot);

                (_snapshot, _filter, Stats) = (snapshot, filter, stats);
                if (_view == MenuItem.CommitDetails)
                {
                    _view = MenuItem.Overview;
                }
            }
            catch (GitPulseException error)
            {
                // Keep the previous filters and tell the user why.
                _notice = new Rows(ErrorRenderer.Render(error), new Markup(Theme.Dim("The filters were not changed.")));
            }
        }

        private void ShowCommit()
        {
            var commits = _snapshot.Commits
                .Where(c => _filter.MatchesAuthor(c.AuthorName, c.AuthorEmail))
                .Take(CommitPicker.DefaultCount)
                .Select(c => new CommitHeader(c.Sha, c.AuthorName, c.When, c.MessageShort))
                .ToList();

            try
            {
                var sha = CommitPicker.Pick(_console, commits);
                using var reader = ScanDisplay.Open(_console, _path);
                _commit = reader.ReadCommitDetail(sha, DiffLimits.Default);
                _view = MenuItem.CommitDetails;
            }
            catch (GitPulseException error)
            {
                _notice = ErrorRenderer.Render(error);
            }
        }

        private void FilterAuthors()
        {
            // Offer everyone in the scanned history, not just the currently filtered authors.
            var everyone = AuthorAnalyzer.Compute(_snapshot.Commits);
            var prompt = new MultiSelectionPrompt<AuthorStats>()
                .Title(Theme.Title("Show commits by which authors?"))
                .NotRequired()
                .PageSize(15)
                .HighlightStyle(new Style(Theme.Primary, decoration: Decoration.Bold))
                .MoreChoicesText(Theme.Dim("(move up and down to see more)"))
                .InstructionsText(Theme.Dim("(space selects, enter applies; select nobody to show everyone)"))
                .UseConverter(a => $"{Markup.Escape(a.Name)} {Theme.Dim($"<{a.Email}> · {Format.Plural(a.Commits, "commit")}")}")
                .AddChoices(everyone);

            foreach (var author in everyone.Where(a => _filter.ExactAuthorList.Contains(Key(a), StringComparer.OrdinalIgnoreCase)))
            {
                prompt.Select(author);
            }

            var chosen = _console.Prompt(prompt).Select(Key).ToList();
            // Ticked authors match exactly (a typed --author keeps matching by "contains").
            Apply(_filter with { ExactAuthors = chosen.Count == 0 ? null : chosen });

            static string Key(AuthorStats a) => ScanFilter.AuthorKey(a.Name, a.Email);
        }

        private void FilterBranches()
        {
            IReadOnlyList<string> branches;
            using (var reader = RepositoryReader.Open(_path))
            {
                branches = reader.BranchNames;
            }

            var prompt = new MultiSelectionPrompt<string>()
                .Title(Theme.Title("Show history of which branches?"))
                .NotRequired()
                .PageSize(15)
                .HighlightStyle(new Style(Theme.Primary, decoration: Decoration.Bold))
                .MoreChoicesText(Theme.Dim("(move up and down to see more)"))
                .InstructionsText(Theme.Dim("(space selects, enter applies; select none for the current branch)"))
                .UseConverter(b => Markup.Escape(b) + (b == branches[0] ? Theme.Dim(" (current)") : string.Empty))
                .AddChoices(branches);

            foreach (var branch in branches.Where(b => _filter.BranchList.Contains(b)))
            {
                prompt.Select(branch);
            }

            var chosen = _console.Prompt(prompt).ToList();
            Apply(_filter with { Branches = chosen.Count == 0 ? null : chosen });
        }

        private void AskDateRange()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var first = DateOnly.FromDateTime(_snapshot.Commits.Min(c => c.When).DateTime);

            var since = _console.Prompt(DatePrompt("Show commits since", _filter.Since ?? first)
                .Validate(d => d <= today
                    ? ValidationResult.Success()
                    : ValidationResult.Error(Theme.Colored("That date is in the future.", Theme.Error))));

            var until = _console.Prompt(DatePrompt("Show commits until", _filter.Until ?? today)
                .Validate(d => d >= since
                    ? ValidationResult.Success()
                    : ValidationResult.Error(Theme.Colored($"Choose {since.ToString("yyyy-MM-dd", Invariant)} or later.", Theme.Error))));

            Apply(_filter with { Since = since, Until = until });
        }

        private static TextPrompt<DateOnly> DatePrompt(string question, DateOnly defaultValue) =>
            new TextPrompt<DateOnly>($"{question} {Theme.Dim("(yyyy-MM-dd)")}")
                {
                    Culture = Invariant,
                }
                .DefaultValue(defaultValue)
                .WithConverter(d => d.ToString("yyyy-MM-dd", Invariant))
                .ValidationErrorMessage(Theme.Colored("Please enter a date like 2026-01-31.", Theme.Error));

        private int AskRowCount(int current) =>
            _console.Prompt(
                new TextPrompt<int>($"How many rows should tables show? {Theme.Dim($"({RepoSettings.MinTop}-{RepoSettings.MaxTop})")}")
                    .DefaultValue(current)
                    .ValidationErrorMessage(Theme.Colored("Please enter a whole number.", Theme.Error))
                    .Validate(n => n is >= RepoSettings.MinTop and <= RepoSettings.MaxTop
                        ? ValidationResult.Success()
                        : ValidationResult.Error(Theme.Colored(
                            $"Choose a number from {RepoSettings.MinTop} to {RepoSettings.MaxTop}.", Theme.Error))));
    }
}
