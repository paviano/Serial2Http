var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:1234");
builder.Services.AddSingleton<SerialConnector>();
var app = builder.Build();
app.MapPost("/send", async (SerialRequest req, SerialConnector serial) =>
{
    Console.WriteLine($"[IN]  {req.Port} @ {req.BaudRate} | enc={req.Encoding} | timeout={req.TimeoutMs} ms | data={req.Data}");
    var result = await serial.SendAsync(req);
    return Results.Ok(result);
});
app.Run();

record SerialRequest(string Port, int BaudRate, string Data, int TimeoutMs = 2000, string Encoding = "ASCII");
record SerialResponse(bool Success, string? Data, string? Error);