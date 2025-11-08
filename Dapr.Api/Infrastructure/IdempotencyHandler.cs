using Dapr.Api.Models;
using Dapr.Client;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Dapr.Api.Infrastructure
{
    public class IdempotencyHandler
    {
        private readonly DaprClient _daprClient;
        private readonly string _stateStoreName;
        private readonly ILogger<IdempotencyHandler> _logger;

        public IdempotencyHandler(DaprClient daprClient, IOptions<DaprConfigurations> options, ILogger<IdempotencyHandler> logger)
        {
            _daprClient = daprClient;
            _logger = logger;
            _stateStoreName = options.Value.StateStoreName ?? "statestore";
        }

        /// <summary>
        /// Executes <paramref name="operation"/> only if the state indicates "not processed".
        /// Handles in-progress (102) and completed (200). Returns the operation result or null if skipped.
        /// </summary>
        public async Task<T?> ExecuteAsync<T>(string key, Func<Task<T>> operation)
        {
            try
            {
                // Read existing state as JObject so we can check StatusCode property safely
                JObject? existing = null;
                try
                {
                    existing = await _daprClient.GetStateAsync<JObject>(_stateStoreName, key);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read state for key {Key}. Proceeding to attempt operation.", key);
                    // proceed — we will try to mark in-progress (save). This handles transient state store issues.
                }

                var status = existing?["StatusCode"]?.Value<int>() ?? 0;
                if (status == 200)
                {
                    _logger.LogInformation("Key {Key} already completed (200). Skipping.", key);
                    return default;
                }
                if (status == 102)
                {
                    _logger.LogInformation("Key {Key} in-progress (102). Skipping to avoid duplicate work.", key);
                    return default;
                }

                // Mark in-progress
                await MarkAsync(key, 102, "InProgress");

                T result;
                try
                {
                    result = await operation();
                }
                catch (Exception opEx)
                {
                    _logger.LogError(opEx, "Operation failed for key {Key}. Marking as failed.", key);
                    // Mark failed (500)
                    await MarkAsync(key, 500, "Failed");
                    throw;
                }

                // Mark completed
                await MarkAsync(key, 200, "Completed");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in idempotency handler for key {Key}", key);
                return default;
            }
        }

        private async Task MarkAsync(string key, int statusCode, string message)
        {
            try
            {
                var record = new JObject
                {
                    ["StatusCode"] = statusCode,
                    ["Message"] = message,
                    ["Timestamp"] = DateTime.UtcNow
                };
                await _daprClient.SaveStateAsync(_stateStoreName, key, record);
                _logger.LogDebug("Saved idempotency state {Status} for {Key}", statusCode, key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save state for key {Key}.", key);
                // swallow: we don't want state store failure to crash subscriber
            }
        }
    }
}
