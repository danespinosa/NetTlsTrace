// See https://aka.ms/new-console-template for more information
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.Text;
using Microsoft.Extensions.Logging;

Console.WriteLine("Hello, World!");

ILogger log = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Program");
var listener = new MyListener(log);

long count = 0;
DateTimeOffset now = DateTimeOffset.UtcNow;
var oneMinute = TimeSpan.FromSeconds(10);
//using var client = new HttpClient();
var uri = new Uri("https://localhost:7032/weatherforecast");
//var uri = new Uri("https://bing.com");
while (count <=100000 )
{
    try
    {
        
         using var client = new HttpClient(new SocketsHttpHandler() {  });
        // Create a new client so that we get a new connection.
        Console.WriteLine($"making request to {uri}");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Version = new Version(2, 0);
        //var request = new HttpRequestMessage(HttpMethod.Get, "https://bing.com");
        request.Headers.ConnectionClose = true;
        var response = await client.SendAsync(request);
         //var response = await client.GetAsync(uri);
        var content = await response.Content.ReadAsStringAsync();
        // await client.GetStringAsync("https://google.com");
        await Task.Delay(TimeSpan.FromSeconds(1));
        Console.WriteLine($"Request {count++} started");

    }
    catch(Exception ex)
    {

    }
}

Console.WriteLine($"Total requests {count}");
Console.ReadLine();

public sealed class MyListener : EventListener
{
    const string SystemHttp = "System.Net.Http";
    const string SystemRuntime = "System.Runtime";
    const string SystemSecurity = "System.Net.Security";
    const string SystemSockets = "System.Net.Sockets";
    const string SystemNameResolution = "System.Net.NameResolution";
    const string AspNetConnections = "Microsoft.AspNetCore.Http.Connections";
    const string AspNetKestrel = "Microsoft-AspNetCore-Server-Kestrel";
    const string AspnetHosting = "Microsoft.AspNetCore.Hosting";

    // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/available-counters
    private static readonly HashSet<string> _enabledEventCounters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SystemRuntime,
            AspNetKestrel,
            AspnetHosting,
            AspNetConnections,
            SystemNameResolution,
            SystemSockets,
            SystemSecurity,
            SystemHttp,
            DocDBTrace
        };

    /// <summary>
    /// Cosmos Db Trace Event Source
    /// https://github.com/Azure/azure-cosmos-dotnet-v2/blob/49c80a7522990d16ffa34207e6fd3d0b36715400/docs/documentdb-sdk_capture_etl.md?plain=1#L2
    /// </summary>
    const string DocDBTrace = "DocDBTrace ";

    private readonly ILogger log;

    public MyListener(ILogger log)
    {
        this.log = log;
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (_enabledEventCounters.Contains(eventSource.Name))
        {
            EnableEvents(eventSource, EventLevel.Verbose);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName == "EventCounters")
        {
            IDictionary<string, object> payloadFields = (IDictionary<string, object>)eventData.Payload[0];
            string counterType = GetCounterPayloadValue<string>("CounterType", payloadFields);
            double value = 0;
            if (counterType == "Sum")
            {
                value = GetCounterPayloadValue<double>("Increment", payloadFields);
            }
            if (counterType == "Mean")
            {
                value = GetCounterPayloadValue<double>("Mean", payloadFields);
            }

            // log.LogInformation($"EventSource: {eventData.EventSource.Name}, Value: {(long)value}, Payload: {DictionaryToString(payloadFields)}");
        }
        else
        {
            log?.LogInformation($"EventSource: {eventData.EventSource.Name}, EventName: {eventData.EventName} Payload: {PayloadToString(eventData.PayloadNames, eventData.Payload)}");
        }
    }

    /// <summary>
    /// Convert a dictionary to a string.
    /// </summary>
    internal static string DictionaryToString(IDictionary<string, object> payloadFields)
    {
        return string.Join(", ", payloadFields.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
    }

    private string PayloadToString(ReadOnlyCollection<string>? payloadNames, ReadOnlyCollection<object?> payload)
    {
        if (payloadNames == null || payload == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < payload.Count; i++)
        {
            sb.Append($"{payloadNames[i]}: {payload[i]} ");
        }

        return sb.ToString();
    }


    /// <summary>
    /// Get the counter payload value for a given name.
    /// </summary>
    /// <typeparam name="T">Output type of the value.</typeparam>
    /// <param name="name">The name to look up.</param>
    /// <param name="payloadFields">The payload fields.</param>
    /// <returns>The value.</returns>
    internal static T GetCounterPayloadValue<T>(string name, IDictionary<string, object> payloadFields)
    {
        object output;
        try
        {
            if (payloadFields.TryGetValue(name, out output!))
            {
                return (T)output;
            }
        }
        catch
        {
        }

        return default!;
    }
}