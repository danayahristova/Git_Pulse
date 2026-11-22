using System.Runtime.CompilerServices;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;

namespace GitPulse.Tests.Rendering;

/// <summary>
/// Minimal file-based snapshot testing (no extra packages).
/// The expected render lives in <c>Snapshots/&lt;name&gt;.verified.txt</c> next to the test file.
/// On a mismatch, the actual render is written to <c>&lt;name&gt;.received.txt</c> (git-ignored) and the test fails.
/// To accept new output, review it and run the tests with <c>GITPULSE_UPDATE_SNAPSHOTS=1</c>.
/// </summary>
internal static class Snapshot
{
    public static string Render(IRenderable renderable, int width, int height = 40)
    {
        var console = new TestConsole().Width(width);
        console.Profile.Height = height;
        console.Profile.Capabilities.Unicode = true;
        console.Write(renderable);
        return console.Output;
    }

    public static void Match(string actual, string name, [CallerFilePath] string testFile = "")
    {
        var dir = Path.Combine(Path.GetDirectoryName(testFile)!, "Snapshots");
        var verifiedPath = Path.Combine(dir, name + ".verified.txt");
        var receivedPath = Path.Combine(dir, name + ".received.txt");
        actual = Normalize(actual);

        if (File.Exists(verifiedPath) && Normalize(File.ReadAllText(verifiedPath)) == actual)
        {
            File.Delete(receivedPath);
            return;
        }

        Directory.CreateDirectory(dir);
        if (Environment.GetEnvironmentVariable("GITPULSE_UPDATE_SNAPSHOTS") == "1")
        {
            File.WriteAllText(verifiedPath, actual);
            File.Delete(receivedPath);
            return;
        }

        File.WriteAllText(receivedPath, actual);
        Assert.Fail(
            File.Exists(verifiedPath)
                ? $"Snapshot '{name}' changed. Compare {receivedPath} with {verifiedPath}."
                : $"Snapshot '{name}' is new. Review {receivedPath}, then rerun with GITPULSE_UPDATE_SNAPSHOTS=1.");
    }

    /// <summary>LF line endings, no trailing spaces, exactly one final newline.</summary>
    private static string Normalize(string text) =>
        string.Join('\n', text.ReplaceLineEndings("\n").Split('\n').Select(l => l.TrimEnd())).TrimEnd('\n') + "\n";
}
