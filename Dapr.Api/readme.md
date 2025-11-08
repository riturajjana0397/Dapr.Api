# Dapr .NET 8 Minimal API Demo

This repository demonstrates integrating core Dapr building blocks into a .NET 8 Minimal API. It focuses on simple, composable examples you can extend.

## Included Dapr Building Blocks
- State Management (Redis or any configured state store)
- Pub/Sub Messaging
- Configuration Store
- Secret Store
- Output Binding
- CloudEvents + Subscription Handler

## Project Layout
```
Dapr.Api/
│  Program.cs                # Minimal API endpoints
│  Dapr.Api.csproj
│  appsettings.json
│  secrets.json              # Local dev secret file (used by secret store component)
│
├─Infrastructure/DaprComponents/   # Dapr component definitions
│    pubsub.yaml
│    state.yaml
│    secretstore.yaml
│    configuration.yaml
│    bindings.yaml
│
├─Infrastructure/IdempotencyHandler.cs (utility not used in current Program.cs)
└─Models/                      
     DaprConfigurations.cs     # Default names (not currently wired into Program.cs)
     Models.cs                 # DTOs (e.g., OrderCreatedDto)
```

## How the App Uses Dapr
The API talks to the Dapr sidecar via the injected `DaprClient`. You run the app with `dapr run` supplying the components folder so Dapr loads the declarative component definitions (state store, pubsub, secrets, config, binding).

## Endpoints Overview
| Purpose | HTTP | Endpoint | Dapr Feature | Notes |
|---------|------|----------|--------------|-------|
| Save order state | POST | `/state/save` | State Store | Persists `OrderCreatedDto` keyed by `OrderId` |
| Get order state | GET | `/state/get/{orderId}` | State Store | Returns stored order or 404 |
| Publish new order | POST | `/publish/order` | Pub/Sub | Publishes to topic `order-created` on pubsub component `pubsub` |
| Subscriber (auto-routed by Dapr) | POST | `/Subscribe/OrderCreated` | Pub/Sub | Consumes `order-created` events; uses CloudEvents middleware |
| Fetch config | GET | `/config` | Configuration | Calls `GetConfiguration("appconfig", ["FeatureFlag", "MaxRetries"])` |
| Retrieve secret | GET | `/secrets` | Secret Store | Reads key `DbConnectionString` from component `secretstore` |
| Output binding write | POST | `/binding/send` | Binding | Invokes binding `storagebinding` operation `create` |
| List processed orders | GET | `/orders` | In-memory demo | Returns in-memory dictionary of received events |

### Pub/Sub Subscription
`app.UseCloudEvents()` enables CloudEvents handling. `app.MapSubscribeHandler()` lets Dapr discover subscriptions from route attributes / handlers automatically. The subscriber endpoint stores events in an in-memory `ConcurrentDictionary` and logs receipt.

### Configuration Response Shape
`GetConfiguration` returns a `GetConfigurationResponse`. The endpoint surfaces that object directly; for simple value extraction you can map:
```csharp
var cfg = await daprClient.GetConfiguration("appconfig", new[]{"FeatureFlag","MaxRetries"});
var values = cfg.Items.ToDictionary(kv => kv.Key, kv => kv.Value.Value);
```
(Adjust if you later adopt the `DaprConfigurations` wrapper.)

### Secret Store
The secret component (`secretstore.yaml`) resolves `DbConnectionString`. Dapr returns a dictionary: `{ "DbConnectionString": "<value>" }`.

### Output Binding
`/binding/send` invokes binding `storagebinding` with operation `create`. Ensure `bindings.yaml` matches the binding type (e.g., local file, storage queue). Payload: `{ message = "Hello from Dapr Binding!" }`.

## Running Locally
1. Install prerequisites:
   - .NET 8 SDK
   - Dapr CLI
   - Docker (for default Redis and any containerized components)
2. Initialize Dapr (if first time):
   ```bash
   dapr init
   ```
3. From project directory run:
   ```bash
   dapr run \
     --app-id dapr-api-demo \
     --app-port 5000 \
     --dapr-http-port 3500 \
     --components-path ./Infrastructure/DaprComponents \
     dotnet run
   ```
4. Call endpoints (examples):
   - Publish order: `POST http://localhost:5000/publish/order`
   - List orders: `GET http://localhost:5000/orders`
   - Get config: `GET http://localhost:5000/config`
   - Get secret: `GET http://localhost:5000/secrets`
   - Write via binding: `POST http://localhost:5000/binding/send`

## Logs & Diagnostics
- Sidecar/app list: `dapr list`
- View logs: `dapr logs --app-id dapr-api-demo`
- Stop app: `dapr stop --app-id dapr-api-demo`

## Extending the Demo
- Wire `DaprConfigurations` into DI and replace hardcoded names.
- Add idempotent processing using `IdempotencyHandler` and a state store record.
- Introduce processed event publishing (`order-processed`) with retry semantics.
- Use `BulkPublishEventAsync` or state transactions for batch operations.

## Notes
- Current `Program.cs` uses direct literal component names; keep them consistent with YAML component metadata.
- Replace the default dev secret solution (`secrets.json`) with a secure store (Azure Key Vault) for production.

## Summary
This minimal API shows pragmatic Dapr integration: persisting state, publishing & subscribing events, reading dynamic configuration, retrieving secrets, and invoking an output binding—all without embedding infrastructure logic into business code.

> "Dapr lets developers focus on business logic — not plumbing."
