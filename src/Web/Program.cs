using System.Collections.ObjectModel;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Security.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7032, listenOptions =>
    {
        listenOptions.UseHttps();
    });
});

builder.Services.AddSingleton<MyListener>();

var app = builder.Build();

//var listener = app.Services.GetRequiredService<MyListener>();

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

app.MapGet("/weatherforecast", async () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
    .Select(o => new { o.TemperatureF, Date = o.Date.ToShortDateString(), o.TemperatureC})
        .ToArray();
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
    private readonly ILogger<MyListener>? _logger;

    public MyListener(ILogger<MyListener> logger):base()
    {
        Console.WriteLine($"logger is null ctor {logger is null}");
        this._logger = logger;
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        //if (eventSource.Name == "System.Net.Http")
        //{
        //    EnableEvents(eventSource, EventLevel.Informational);
        //}

        Console.WriteLine($"Enabled {eventSource.Name}");
        if (eventSource.Name == "Microsoft.AspNetCore.Hosting")
        {
            Console.WriteLine("Enabled System.Net.Http");
           EnableEvents(eventSource, EventLevel.Verbose);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        //if (eventData.EventName == null || !(eventData.EventName.Contains("EventCounters")))
        //{
        //    return;
        //}
        //var payloadDict = (IDictionary<string, object>?)eventData?.Payload?[0];
        if (eventData.EventName.Contains("Request") || eventData.EventSource.Name.Contains("Microsoft-AspNetCore-Server-Kestrel"))
        {
            for (int i = 0; i < eventData?.Payload?.Count; i++)
            {
                this._logger?.LogInformation("{Provider}-{EventName}-{Name} - {Value}", eventData.EventSource.Name,eventData.EventName, eventData?.PayloadNames?[i], eventData?.Payload[i]);
            }
        }

        //if (payloadDict is not null && payloadDict.TryGetValue("Name", out object? name) && name is string theName && (theName.Contains("http11-connections-current-total") || theName.Contains("http20-connections-current-total")))
        //{
            
        //}
    }
}