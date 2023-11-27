using System.Diagnostics.Tracing;
using System.Security.Authentication;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Services.GetRequiredService<MyListener>();
// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public sealed class MyListener : EventListener
{
    private readonly ILogger<MyListener> logger;

    public MyListener(ILogger<MyListener> logger)
    {
        this.logger = logger;
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "System.Net.Http")
        {
            EnableEvents(eventSource, EventLevel.Informational);
        }

        if (eventSource.Name == "System.Net.Security")
        {
            EnableEvents(eventSource, EventLevel.Informational);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        for (int i = 0; i < eventData?.Payload?.Count; i++)
        {
            if (eventData?.PayloadNames?[i] == "protocol")
            {
                this.logger.LogInformation("{Name} - {Value}",eventData?.PayloadNames?[i], (SslProtocols)(eventData?.Payload[i] ?? 0));
            }
            else
            {
                this.logger.LogInformation("{Name} - {Value}",eventData?.PayloadNames?[i], eventData?.Payload[i]);
            }
        }
    }
}