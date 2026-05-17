using System.Text.Json.Serialization;

namespace Nimblist.api.DTO
{
    public class SubscriptionStatusDto
    {
        public string Tier { get; set; } = "free"; // "free" | "paid"
        public string? Status { get; set; }         // PayPal status if subscription exists
        public bool IsInTrial { get; set; }
        public DateTime? TrialEndDate { get; set; }
        public DateTime? NextBillingDate { get; set; }
        public string? PayPalSubscriptionId { get; set; }
    }

    public class ActivateSubscriptionRequest
    {
        public string SubscriptionId { get; set; } = null!;
    }

    public class PayPalSubscriptionDetails
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("status")]
        public string Status { get; set; } = null!;

        [JsonPropertyName("start_time")]
        public DateTime? StartTime { get; set; }

        [JsonPropertyName("billing_info")]
        public PayPalBillingInfo? BillingInfo { get; set; }
    }

    public class PayPalBillingInfo
    {
        [JsonPropertyName("next_billing_time")]
        public DateTime? NextBillingTime { get; set; }

        [JsonPropertyName("cycle_executions")]
        public List<PayPalCycleExecution>? CycleExecutions { get; set; }
    }

    public class PayPalCycleExecution
    {
        [JsonPropertyName("tenure_type")]
        public string TenureType { get; set; } = null!;

        [JsonPropertyName("sequence")]
        public int Sequence { get; set; }

        [JsonPropertyName("cycles_completed")]
        public int CyclesCompleted { get; set; }

        [JsonPropertyName("cycles_remaining")]
        public int CyclesRemaining { get; set; }
    }
}
