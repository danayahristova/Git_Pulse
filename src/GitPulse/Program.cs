using GitPulse;
using GitPulse.Interactive;
using Spectre.Console.Cli;

var console = GitPulseApp.CreateConsole(args);

var app = new CommandApp<InteractiveCommand>();
app.Configure(config =>
{
    config.ConfigureConsole(console);
    GitPulseApp.Configure(config);
});

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // First Ctrl+C asks the scan to stop cleanly; a second one kills the process.
    if (!cancellation.IsCancellationRequested)
    {
        e.Cancel = true;
        cancellation.Cancel();
    }
};

return app.Run(args, cancellation.Token);
