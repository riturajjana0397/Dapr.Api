using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using Dapr.Api.Infrastructure; // added for IdempotencyHandler
using Dapr.Api.Models;          // added for DaprConfigurations
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;     // for optional initial state marker

var builder = WebApplication.CreateBuilder(args);

// Enable Dapr
builder.Services.AddDaprClient();

// Bind optional Dapr configuration section (if present in appsettings.json)
builder.Services.Configure<DaprConfigurations>(builder.Configuration.GetSection("Dapr"));

// Register IdempotencyHandler
builder.Services.AddSingleton<IdempotencyHandler>();

// In-memory store to simulate data persistence
var orders = new ConcurrentDictionary<string, OrderCreatedDto>();

var app = builder.Build();

app.UseCloudEvents();
app.MapSubscribeHandler();

#region STATE MANAGEMENT (DAPR STATE STORE DEMO)

app.MapPost("/state/save", async ([FromBody] OrderCreatedDto order, DaprClient daprClient) =>
{
    await daprClient.SaveStateAsync("statestore", order.OrderId, order);
    return Results.Ok($"Order {order.OrderId} saved in state store.");
});

app.MapGet("/state/get/{orderId}", async (string orderId, DaprClient daprClient) =>
{
    var order = await daprClient.GetStateAsync<OrderCreatedDto>("statestore", orderId);
    return order is not null ? Results.Ok(order) : Results.NotFound($"Order {orderId} not found.");
});

#endregion

#region PUB/SUB (MESSAGE BROKER DEMO)

app.MapPost("/publish/order", async (DaprClient daprClient) =>
{
    var order = new OrderCreatedDto(Guid.NewGuid().ToString(), "Laptop", 1200.99m);

    // Optional: mark initial state for idempotency tracking (StatusCode 102 = in-progress/published)
    var idKey = $"order-{order.OrderId}";
    var initial = new JObject
    {
        ["StatusCode"] = 102,
        ["Message"] = "Published",
        ["Timestamp"] = DateTime.UtcNow
    };
    await daprClient.SaveStateAsync("statestore", idKey, initial);

    await daprClient.PublishEventAsync("pubsub", "order-created", order);
    return Results.Ok($"Published new order {order.OrderId}");
});

// Subscriber endpoint for "order-created" topic with idempotency protection
app.MapPost("/Subscribe/OrderCreated", async (
    [FromBody] OrderCreatedDto order,
    IdempotencyHandler idempotency,
    IOptions<DaprConfigurations> cfgOptions,
    DaprClient daprClient,
    ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("OrderSubscriber");
    var key = $"order-{order.OrderId}"; // consistent idempotency key

    // Use string result to avoid nullable value type issues
    var result = await idempotency.ExecuteAsync<string>(key, async () =>
    {
        // Business logic executed only once
        orders[order.OrderId] = order;
        logger.LogInformation("Processed order event: {OrderId} - {Product} - ${Amount}", order.OrderId, order.ProductName, order.Amount);

        // Optionally publish a processed event or perform additional work here
        // await daprClient.PublishEventAsync("pubsub", "order-processed", new { order.OrderId, status = "processed" });

        return "processed"; // non-null marker of success
    });

    // If null => duplicate or in-progress; always return 200 to stop retries
    return Results.Ok(result != null ? "Order processed idempotently." : "Duplicate/in-progress skipped.");
});

#endregion

#region CONFIGURATION (DAPR CONFIGURATION STORE DEMO)

app.MapGet("/config", async (DaprClient daprClient) =>
{
    var items = await daprClient.GetConfiguration("appconfig", new[] { "FeatureFlag", "MaxRetries" });
    return Results.Ok(items);
});

#endregion

#region SECRET STORE (DAPR SECRETS DEMO)

app.MapGet("/secrets", async (DaprClient daprClient) =>
{
    var secret = await daprClient.GetSecretAsync("secretstore", "DbConnectionString");
    return Results.Ok(secret);
});

#endregion

#region  OUTPUT BINDING (DAPR BINDING DEMO)

app.MapPost("/binding/send", async (DaprClient daprClient) =>
{
    var payload = new { message = "Hello from Dapr Binding!" };
    await daprClient.InvokeBindingAsync("storagebinding", "create", payload);
    return Results.Ok("Message sent via binding.");
});

#endregion

#region IN-MEMORY DATA (SIMULATED DATABASE FOR DEMO PURPOSES)

app.MapGet("/orders", () => Results.Ok(orders.Values));

#endregion

app.Run();

#region Supporting DTO

public record OrderCreatedDto(string OrderId, string ProductName, decimal Amount);

#endregion
