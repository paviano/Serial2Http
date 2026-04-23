var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:1234");
builder.Services.AddSingleton<SerialConnector>();
var app = builder.Build();
app.MapPost("/send", async (SerialRequest req, SerialConnector serial) => Results.Ok(await serial.SendAsync(req.Port, req.BaudRate, req.Data, req.TimeoutMs)));
app.Run();
record SerialRequest(string Port, int BaudRate, string Data, int TimeoutMs = 2000);
record SerialResponse(bool Success, string? Data, string? Error);