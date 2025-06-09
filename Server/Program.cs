var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/delay/{seconds}", async (int seconds, CancellationToken cancellationToken) =>
{
    try
    {
        Console.WriteLine($"Starting delay of {seconds} seconds...");
        await Task.Delay(seconds * 1000, cancellationToken);
        Console.WriteLine($"Completed delay of {seconds} seconds");
        return Results.Ok(new { message = $"Completed after {seconds} seconds", server = "NET Core Server" });
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Request was cancelled by the client.");
        return Results.StatusCode(499); // Client Closed Request
    }
})
.WithName("DelayEndpoint");

app.MapGet("/weatherforecast", async (HttpContext context) =>
{
    var cancellationToken = context.RequestAborted;
    var forecast = new List<WeatherForecast>();
    for (var i = 0; i < 10; i++)
    {
        try
        {
            await Task.Delay(5000, cancellationToken);
            var weatherReport = new WeatherForecast(
                DateOnly.FromDateTime(DateTime.Now.AddDays(i)),
                Random.Shared.Next(-20, 55),
                summaries[Random.Shared.Next(summaries.Length)]
            );
            forecast.Add(weatherReport);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Request was cancelled by the client.");
            throw new OperationCanceledException("Client cancelled the request", cancellationToken);
        }
        if (cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Request was cancelled by the client.");
            throw new OperationCanceledException("Client cancelled the request", cancellationToken);
        }

    }

    if (cancellationToken.IsCancellationRequested)
    {
        Console.WriteLine("Request was cancelled by the client.");
        throw new OperationCanceledException("Client cancelled the request", cancellationToken);
    }

    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
