using Inventory.Api.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));
builder.Services.AddSingleton<StockService>();

var app = builder.Build();

app.MapGet("/stock/{sku}", async (string sku, StockService stock) =>
    Results.Ok(new { sku, available = await stock.GetStockAsync(sku) }));

app.MapPost("/seed/{sku}/{qty:int}", async (string sku, int qty, StockService stock) =>
{
    await stock.SeedAsync(sku, qty);
    return Results.Ok(new { sku, available = qty });
});

app.MapPost("/reserve", async (ReserveRequest req, StockService stock) =>
{
    var result = await stock.ReserveAsync(req.OrderId, req.Sku, req.Quantity);
    return Results.Ok(new { req.OrderId, result = result.ToString() });
});

app.MapPost("/release", async (ReleaseRequest req, StockService stock) =>
    Results.Ok(new { released = await stock.ReleaseAsync(req.OrderId, req.Sku) }));

// Deliberately broken version, for the race condition experiment only
app.MapPost("/reserve-naive/{sku}", async (string sku, StockService stock) =>
    Results.Ok(new { ok = await stock.ReserveNaiveAsync(sku, 1) }));

app.Run();

public record ReserveRequest(Guid OrderId, string Sku, int Quantity);
public record ReleaseRequest(Guid OrderId, string Sku);