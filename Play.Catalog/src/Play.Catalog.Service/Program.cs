using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Play.Catalog.Service.Entities;
using Play.Catalog.Service.Repositories;
using Play.Catalog.Service.Settings;

var builder = WebApplication.CreateBuilder(args);

BsonSerializer.RegisterSerializer(new GuidSerializer(MongoDB.Bson.BsonType.String));
BsonSerializer.RegisterSerializer(new DecimalSerializer(MongoDB.Bson.BsonType.String));
BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(MongoDB.Bson.BsonType.String));

var configuration = builder.Configuration;

var serviceSettings = configuration
    .GetSection(nameof(ServiceSettings))
    .Get<ServiceSettings>();

var mongoDbSettings = configuration
    .GetSection(nameof(MongoDbSettings))
    .Get<MongoDbSettings>();

builder.Services.AddSingleton(serviceProvider =>
{
  var mongoClient = new MongoClient(mongoDbSettings.ConnectionString);
  return mongoClient.GetDatabase(serviceSettings.ServiceName);
});

builder.Services.AddSingleton<IRepository<Item>>(serviceProvider =>
{
  var database = serviceProvider.GetService<IMongoDatabase>();
  return new MongoRepository<Item>(database, "items");
});

builder.Services.AddControllers(options =>
{
  options.SuppressAsyncSuffixInActionNames = false;
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
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
