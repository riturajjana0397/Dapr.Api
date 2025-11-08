using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

// Enable Dapr
builder.Services.AddDaprClient();

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
    await daprClient.PublishEventAsync("pubsub", "order-created", order);
    return Results.Ok($"Published new order {order.OrderId}");
});

// Subscriber endpoint for "order-created" topic
app.MapPost("/Subscribe/OrderCreated", async ([FromBody] OrderCreatedDto order, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("OrderSubscriber");
    orders[order.OrderId] = order;
    logger.LogInformation($"Received order event: {order.OrderId} - {order.ProductName} - ${order.Amount}");
    return Results.Ok("Order processed successfully.");
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
