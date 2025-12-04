using Play.Common.MongoDb;
using Play.Inventory.Service.Clients;
using Play.Inventory.Service.Entities;
using Polly;
using Polly.Timeout;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

builder.Services.AddMongo()
    .AddMongoRepository<InventoryItem>("inventoryitems");

builder.Services.AddHttpClient<CatalogClient>((sp, client) =>
{
    client.BaseAddress = new Uri("https://localhost:5001");
})
// Retry Policy com Exponential Backoff e Jitter
.AddPolicyHandler((sp, request) =>
{
    var logger = sp.GetRequiredService<ILogger<CatalogClient>>();
    Random jitter = new Random();

    return Policy<HttpResponseMessage>
        .Handle<TimeoutRejectedException>()
        .OrResult(r => !r.IsSuccessStatusCode)
        .WaitAndRetryAsync(
            5,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                + TimeSpan.FromMilliseconds(jitter.Next(0, 1000)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                logger.LogWarning(
                    $"Delaying for {timespan.TotalSeconds}s before retry {retryAttempt}"
                );
            }
        );
})
// Circuit Breaker Policy
.AddPolicyHandler((sp, request) =>
{
    var logger = sp.GetRequiredService<ILogger<CatalogClient>>();

    return Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .Or<TimeoutRejectedException>()
        .OrResult(r => !r.IsSuccessStatusCode)
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromSeconds(15),
            onBreak: (outcome, timespan) =>
            {
                logger.LogWarning($"Opening the circuit for {timespan.TotalSeconds} seconds...");
            },
            onReset: () =>
            {
                logger.LogWarning("Closing the circuit...");
            }
        );
})
// Timeout Policy
.AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(1));

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "MicroGame API");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();