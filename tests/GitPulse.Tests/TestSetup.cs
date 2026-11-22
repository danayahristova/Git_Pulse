using System.Runtime.CompilerServices;
using GitPulse.Caching;

namespace GitPulse.Tests;

internal static class TestSetup
{
    /// <summary>The scan cache folder for this test run (deleted when the process exits).</summary>
    public static string CacheDirectory { get; private set; } = string.Empty;

    /// <summary>
    /// Runs before any test: points the scan cache at a temp folder, so tests never read or
    /// write the developer's real cache (~/.cache/gitpulse, %LOCALAPPDATA%\GitPulse\Cache).
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        CacheDirectory = Directory.CreateTempSubdirectory("gitpulse-cache-").FullName;
        Environment.SetEnvironmentVariable(CacheLocation.EnvironmentVariable, CacheDirectory);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(CacheDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort.
            }
        };
    }
}
