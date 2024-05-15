// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;

Process p = Process.GetProcessesByName("Web")[0];
DiagnosticsClient diagnosticsClient = new(p.Id);
using var session = diagnosticsClient.StartEventPipeSession(
    providers:
    new List<EventPipeProvider>()
    {
        new EventPipeProvider("System.Runtime", EventLevel.Informational, (long)EventKeywords.All, new Dictionary<string, string>() { { "EventCounterIntervalSec", "2" } })
    });

var source = new EventPipeEventSource(session.EventStream);
source.Dynamic.All += (eventData) =>
{
    // Console.WriteLine(eventData.EventName);
};

CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(10));
try
{
    cancellationTokenSource.Token.Register(() => 
    {
        try
        {

            Console.WriteLine("cancellation requested");
            session.Stop();
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Task cancelled exception");
        }
        Console.WriteLine("cancelled");
    });
    await Task.Run(() => source.Process(), cancellationTokenSource.Token);
}
catch (Exception e)
{
    Console.WriteLine("Exception {e}", e);
}
Console.WriteLine("finished");