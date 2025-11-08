namespace Dapr.Api.Models
{
    public class DaprConfigurations
    {
        public string PubSubName { get; set; } = "pubsub";
        public string OrderCreatedTopic { get; set; } = "order-created";
        public string OrderProcessedTopic { get; set; } = "order-processed";
        public string StateStoreName { get; set; } = "statestore";
        public string EmailBinding { get; set; } = "";          // e.g. "sendgrid-binding"
        public string SecretStoreName { get; set; } = "vault";
        public string DbSecretName { get; set; } = "DbConnection";
        public string ConfigurationStoreName { get; set; } = "appconfigstore";
        public string FeatureFlagKey { get; set; } = "EnableNewFlow";
    }


}
