// See https://aka.ms/new-console-template for more information
using System.Diagnostics.Tracing;
using System.Security.Authentication;

Console.WriteLine("Hello, World!");

var listener = new MyListener();

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

    public MyListener()
    {
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "System.Net.Http")
        {
            EnableEvents(eventSource, EventLevel.Informational);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName == "ConnectionEstablished")
        {

            for (int i = 0; i < eventData?.Payload?.Count; i++)
            {
                var value = eventData.Payload[i];
                var name = eventData?.PayloadNames?[i];
                Console.WriteLine($"{name} - {value}");
            }
        }
    }
}