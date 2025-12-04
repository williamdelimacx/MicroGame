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

Random jitterer = new Random();

builder.Services.AddHttpClient<CatalogClient>((sp, client) =>
{
    client.BaseAddress = new Uri("https://localhost:5001");
})
// POLICY COM SUPORTE A DEPENDENCY INJECTION
.AddPolicyHandler((sp, request) =>
{
    var logger = sp.GetRequiredService<ILogger<CatalogClient>>();
    Random jitter = new Random();

    return Policy<HttpResponseMessage>
        .Handle<TimeoutRejectedException>()
        .OrResult(r => !r.IsSuccessStatusCode)
        .WaitAndRetryAsync(
            5,
            retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                TimeSpan.FromMilliseconds(jitter.Next(0, 1000)),
            (outcome, timespan, retryAttempt, context) =>
            {
                logger.LogWarning(
                    "Delaying for {delay}s before retry {retryAttempt}",
                    timespan.TotalSeconds,
                    retryAttempt
                );
            }
        );
})
.AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(1));

builder.Services.AddControllers();

builder.Services.AddOpenApi();

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
