using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitPulse.Rendering;

/// <summary>The interactive-mode banner.</summary>
internal static class Banner
{
    public const string Tagline = "Understand a repository's history, people and hotspots.";

    // The default figlet font needs roughly this many columns for "GitPulse".
    private const int FigletWidth = 50;

    public static IRenderable Render(int width)
    {
        IRenderable title = width >= FigletWidth
            ? new FigletText("GitPulse").Color(Theme.Primary)
            : new Markup(Theme.Title("GitPulse"));

        return new Rows(title, new Markup(Theme.Dim(Tagline)), new Text(string.Empty));
    }
}
