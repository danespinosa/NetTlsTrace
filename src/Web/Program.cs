using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Text;
using Microsoft.Azure.Cosmos;
using Container = Microsoft.Azure.Cosmos.Container;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7032, listenOptions =>
    {
        listenOptions.UseHttps();
    });
});

builder.Logging.AddEventSourceLogger();

CosmosClient client = new(
    accountEndpoint: "https://localhost:8081/",
    authKeyOrResourceToken: "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    new CosmosClientOptions
    {
        CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions
        {
            DisableDistributedTracing = false,
            CosmosThresholdOptions = new CosmosThresholdOptions
            {
                PayloadSizeThresholdInBytes = 0,
            },
        }
    });
builder.Services.AddSingleton(client);

builder.Services.AddSingleton<MyListener>();

var app = builder.Build();

var listener = app.Services.GetRequiredService<MyListener>();

using MeterListener meterListener = new();
meterListener.InstrumentPublished = (instrument, listener) =>
{
    if (instrument.Meter.Name is "Microsoft.AspNetCore.Hosting")
    {
        listener.EnableMeasurementEvents(instrument);
    }
};

meterListener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);

// Start the meterListener, enabling InstrumentPublished callbacks.
meterListener.Start();


// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", async (CosmosClient client, ILogger<Program> logger) =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
    .Select(o => new { o.TemperatureF, Date = o.Date.ToShortDateString(), o.TemperatureC })
        .ToArray();

    var item = new
    {
        id = $"{DateTimeOffset.UtcNow.Ticks}",
        name = "Test item"
    };
    try
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(
            id: "cosmicworks",
            throughput: 400
        );

        Container container = await database.CreateContainerIfNotExistsAsync(
            id: "products",
            partitionKeyPath: "/id"
        );

        await container.UpsertItemAsync(item);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error inserting item");
    }
    return forecast;
});

app.Run();

static void OnMeasurementRecorded<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
{
    Console.WriteLine($"{instrument.Name} recorded measurement {measurement}");
}

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public sealed class MyListener : EventListener
{
    private readonly ILogger<MyListener>? log;
    const string SystemHttp = "System.Net.Http";
    const string SystemRuntime = "System.Runtime";
    const string SystemSecurity = "System.Net.Security";
    const string SystemSockets = "System.Net.Sockets";
    const string SystemNameResolution = "System.Net.NameResolution";
    const string AspNetConnections = "Microsoft.AspNetCore.Http.Connections";
    const string AspNetKestrel = "Microsoft-AspNetCore-Server-Kestrel";
    const string AspnetHosting = "Microsoft.AspNetCore.Hosting";

    /// <summary>
    /// https://github.com/Azure/azure-cosmos-dotnet-v3/blob/504c2dfd8d6dacb77789a5b48c09897899363b55/Microsoft.Azure.Cosmos/src/DocumentClientEventSource.cs#L14
    /// </summary>
    const string DocumentDBClient = "DocumentDBClient";

    /// <summary>
    /// https://github.com/Azure/azure-cosmos-dotnet-v3/blob/504c2dfd8d6dacb77789a5b48c09897899363b55/Microsoft.Azure.Cosmos/src/Telemetry/OpenTelemetry/CosmosDbEventSource.cs#L15
    /// </summary>
    const string CosmosRequestDiagnostics = "Azure-Cosmos-Operation-Request-Diagnostics";

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
            DocumentDBClient,
            CosmosRequestDiagnostics

        };

    public MyListener(ILogger<MyListener> logger) : base()
    {
        this.log = logger;
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
        if (eventData.EventName == "EventCounters" && eventData?.Payload?[0] is IDictionary<string, object> payloadFields && payloadFields != null)
        {
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
            var message = $"EventSource: {eventData?.EventSource.Name}, EventName: {eventData?.EventName} Payload: {PayloadToString(eventData?.PayloadNames, eventData?.Payload)}";
            log?.LogInformation(message);
        }
    }

    private string PayloadToString(ReadOnlyCollection<string>? payloadNames, ReadOnlyCollection<object?>? payload)
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
    /// Convert a dictionary to a string.
    /// </summary>
    internal static string DictionaryToString(IDictionary<string, object> payloadFields)
    {
        return string.Join(", ", payloadFields.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
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