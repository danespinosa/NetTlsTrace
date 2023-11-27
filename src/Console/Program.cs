// See https://aka.ms/new-console-template for more information
using System.Diagnostics.Tracing;
using System.Security.Authentication;

Console.WriteLine("Hello, World!");

var listener = new MyListener();

while (true)
{
    try
    {
        // Create a new client so that we get a new connection.
        using var client = new HttpClient();
        await client.GetStringAsync("https://localhost:7032/weatherforecast");
        // await client.GetStringAsync("https://google.com");
        await Task.Delay(TimeSpan.FromSeconds(10));

    }
    catch
    {
    }
}

public sealed class MyListener : EventListener
{

    public MyListener()
    {
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        //if (eventSource.Name == "System.Net.Http")
        //{
        //    EnableEvents(eventSource, EventLevel.Informational);
        //}

        if (eventSource.Name == "System.Net.Security")
        {
            // TODO: Learn how to use the args parameter to filter events.
            // this will only enable the HandshakeStart and HandshakeStop events.
            EnableEvents(eventSource, EventLevel.Informational, EventKeywords.None);

        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName != "HandshakeStart" && eventData.EventName != "HandshakeStop")
        {
            return;
        }
        for (int i = 0; i < eventData?.Payload?.Count; i++)
        {
            if (eventData?.PayloadNames?[i] == "protocol")
            {
                Console.WriteLine($"{eventData?.PayloadNames?[i]} - {(SslProtocols)(eventData?.Payload[i] ?? 0)}");
            }
            else
            {
                Console.WriteLine($"{eventData?.PayloadNames?[i]} - {eventData?.Payload[i]}");
            }
        }
    }
}