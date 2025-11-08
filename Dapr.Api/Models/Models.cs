namespace Dapr.Api.Models
{
    public record OrderCreateDto
    {
        public string OrderId { get; init; } = Guid.NewGuid().ToString();
        public string CustomerEmail { get; init; } = "";
        public decimal Amount { get; init; }
    }
}
