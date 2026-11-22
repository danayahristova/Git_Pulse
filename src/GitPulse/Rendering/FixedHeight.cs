using Spectre.Console.Rendering;

namespace GitPulse.Rendering;

/// <summary>
/// Renders <paramref name="inner"/> with a fixed height. A <c>Layout</c> otherwise
/// expands to the full console height, leaving a screen of empty rows.
/// </summary>
internal sealed class FixedHeight(IRenderable inner, int height) : Renderable
{
    public int Height { get; } = height;

    protected override Measurement Measure(RenderOptions options, int maxWidth) =>
        inner.Measure(options with { Height = Height }, maxWidth);

    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth) =>
        inner.Render(options with { Height = Height }, maxWidth);
}
