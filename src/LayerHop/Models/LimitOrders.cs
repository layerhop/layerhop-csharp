#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LayerHop
{
    /// <summary>Limit order statuses. Filled, Cancelled, Expired and Failed are final.</summary>
    public static class LimitOrderStatuses
    {
        public const string Built = "built";
        public const string Open = "open";
        public const string Filled = "filled";
        public const string Cancelled = "cancelled";
        public const string Expired = "expired";
        public const string Failed = "failed";
    }

    /// <summary>Outcomes for <see cref="LayerHopClient.SimulateLimitOrderAsync"/> (test mode only).</summary>
    public static class LimitOrderSimulations
    {
        public const string Fill = "fill";
        public const string PartialFill = "partial_fill";
        public const string Expire = "expire";
    }

    /// <summary>Body of POST /v1/limit-orders.</summary>
    public class CreateLimitOrderParams
    {
        /// <summary>Only "solana" for now; API default when null.</summary>
        [JsonProperty("chain")] public string? Chain { get; set; }
        /// <summary>Maker wallet (base58): signs the order and receives the output.</summary>
        [JsonProperty("wallet")] public string Wallet { get; set; } = "";
        [JsonProperty("srcToken")] public string SrcToken { get; set; } = "";
        [JsonProperty("dstToken")] public string DstToken { get; set; } = "";
        /// <summary>Base units of SrcToken to sell (worth at least $5).</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        /// <summary>Minimum base units of DstToken for the whole Amount (limit price = DstAmount / Amount).</summary>
        [JsonProperty("dstAmount")] public string DstAmount { get; set; } = "";
        /// <summary>Unix seconds, at least 60 s ahead. Null = good-till-cancelled.</summary>
        [JsonProperty("expiresAt")] public long? ExpiresAt { get; set; }
        /// <summary>Defaults to the client's Channel.</summary>
        [JsonProperty("channel")] public string? Channel { get; set; }
        [JsonProperty("metadata")] public Dictionary<string, string>? Metadata { get; set; }
        [JsonProperty("integrator")] public string? Integrator { get; set; }
        [JsonProperty("integratorFeeBps")] public int? IntegratorFeeBps { get; set; }
    }

    public class LimitOrderFees
    {
        [JsonProperty("ourFeeBps")] public int OurFeeBps { get; set; }
        [JsonProperty("integratorFeeBps")] public int IntegratorFeeBps { get; set; }
        [JsonProperty("integrator")] public string? Integrator { get; set; }
    }

    /// <summary>A Solana limit order (Jupiter Trigger) ("limit_order"). Each fill is an <see cref="Order"/> with LimitOrderId set.</summary>
    public class LimitOrder
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("object")] public string Object { get; set; } = "";
        [JsonProperty("livemode")] public bool Livemode { get; set; }
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("statusDetail")] public string? StatusDetail { get; set; }
        [JsonProperty("provider")] public string Provider { get; set; } = "";
        [JsonProperty("chainId")] public long ChainId { get; set; }
        [JsonProperty("wallet")] public string Wallet { get; set; } = "";
        [JsonProperty("srcToken")] public string SrcToken { get; set; } = "";
        [JsonProperty("dstToken")] public string DstToken { get; set; } = "";
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        [JsonProperty("dstAmount")] public string DstAmount { get; set; } = "";
        [JsonProperty("expiresAt")] public string? ExpiresAt { get; set; }
        [JsonProperty("fees")] public LimitOrderFees Fees { get; set; } = new LimitOrderFees();
        [JsonProperty("providerOrderId")] public string? ProviderOrderId { get; set; }
        [JsonProperty("filledAmount")] public string FilledAmount { get; set; } = "0";
        [JsonProperty("filledDstAmount")] public string FilledDstAmount { get; set; } = "0";
        [JsonProperty("fillCount")] public int FillCount { get; set; }
        [JsonProperty("openTxHash")] public string? OpenTxHash { get; set; }
        [JsonProperty("cancelTxHash")] public string? CancelTxHash { get; set; }
        [JsonProperty("channel")] public string Channel { get; set; } = "";
        [JsonProperty("metadata")] public Dictionary<string, string>? Metadata { get; set; }
        [JsonProperty("createdAt")] public string CreatedAt { get; set; } = "";
        [JsonProperty("updatedAt")] public string UpdatedAt { get; set; } = "";
        /// <summary>Unsigned tx for Wallet to sign (sign only, then SubmitLimitOrderAsync). CreateLimitOrderAsync only.</summary>
        [JsonProperty("tx")] public BuiltTx? Tx { get; set; }
        /// <summary>Fills, newest first (GetLimitOrderAsync only).</summary>
        [JsonProperty("fills")] public List<Order>? Fills { get; set; }
    }

    public class ListLimitOrdersParams
    {
        /// <summary>That wallet's orders; without it, the API key's account.</summary>
        public string? Wallet { get; set; }
        public string? Status { get; set; }
        public string? Cursor { get; set; }
        public int? Limit { get; set; }
    }

    public class ListLimitOrdersResponse
    {
        [JsonProperty("limitOrders")] public List<LimitOrder> LimitOrders { get; set; } = new List<LimitOrder>();
        [JsonProperty("nextCursor")] public string? NextCursor { get; set; }
    }

    public class SubmitLimitOrderResponse
    {
        [JsonProperty("limitOrder")] public LimitOrder LimitOrder { get; set; } = new LimitOrder();
        /// <summary>The landed transaction's signature.</summary>
        [JsonProperty("txHash")] public string TxHash { get; set; } = "";
    }

    /// <summary>Tx is the unsigned cancel tx, or null when a built order was cancelled at once.</summary>
    public class CancelLimitOrderResponse
    {
        [JsonProperty("limitOrder")] public LimitOrder LimitOrder { get; set; } = new LimitOrder();
        [JsonProperty("tx")] public BuiltTx? Tx { get; set; }
    }

    /// <summary>One day of uptime history.</summary>
    public class StatusDay
    {
        [JsonProperty("date")] public string Date { get; set; } = "";
        [JsonProperty("uptime")] public double? Uptime { get; set; }
    }

    public class StatusComponent
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("group")] public string Group { get; set; } = "";
        /// <summary>"operational", "degraded" or "down".</summary>
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("latencyMs")] public int? LatencyMs { get; set; }
        [JsonProperty("uptime90d")] public double? Uptime90d { get; set; }
        [JsonProperty("days")] public List<StatusDay> Days { get; set; } = new List<StatusDay>();
    }

    public class StatusIncident
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("componentId")] public string ComponentId { get; set; } = "";
        [JsonProperty("component")] public string Component { get; set; } = "";
        [JsonProperty("title")] public string Title { get; set; } = "";
        [JsonProperty("startedAt")] public string StartedAt { get; set; } = "";
        [JsonProperty("resolvedAt")] public string? ResolvedAt { get; set; }
    }

    /// <summary>Body of GET /v1/status (status.layerhop.com).</summary>
    public class ServiceStatus
    {
        /// <summary>"operational", "degraded" or "outage".</summary>
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("updatedAt")] public string UpdatedAt { get; set; } = "";
        [JsonProperty("components")] public List<StatusComponent> Components { get; set; } = new List<StatusComponent>();
        [JsonProperty("incidents")] public List<StatusIncident> Incidents { get; set; } = new List<StatusIncident>();
        [JsonProperty("url")] public string Url { get; set; } = "";
    }

    /// <summary>Webhook event types. Dashboard samples have type "webhook.test".</summary>
    public static class WebhookEventTypes
    {
        public const string OrderSubmitted = "order.submitted";
        public const string OrderFilled = "order.filled";
        public const string OrderRefunded = "order.refunded";
        public const string OrderFailed = "order.failed";
        public const string CheckoutSessionProcessing = "checkout.session.processing";
        public const string CheckoutSessionCompleted = "checkout.session.completed";
        public const string CheckoutSessionPaymentFailed = "checkout.session.payment_failed";
        public const string CheckoutSessionExpired = "checkout.session.expired";
        public const string CheckoutSessionRefunded = "checkout.session.refunded";
        public const string InvoiceSent = "invoice.sent";
        public const string InvoicePaid = "invoice.paid";
        public const string SubscriptionCreated = "subscription.created";
        public const string SubscriptionRenewed = "subscription.renewed";
        public const string SubscriptionPastDue = "subscription.past_due";
        public const string SubscriptionCanceled = "subscription.canceled";
        public const string LimitOrderOpened = "limit_order.opened";
        public const string LimitOrderFilled = "limit_order.filled";
        public const string LimitOrderCancelled = "limit_order.cancelled";
        public const string LimitOrderExpired = "limit_order.expired";
        public const string LimitOrderFailed = "limit_order.failed";
    }

    /// <summary>The JSON body LayerHop POSTs to your webhook endpoints.</summary>
    public class WebhookEvent
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        /// <summary><see cref="WebhookEventTypes"/>.</summary>
        [JsonProperty("type")] public string Type { get; set; } = "";
        [JsonProperty("livemode")] public bool Livemode { get; set; }
        /// <summary>True for samples from the dashboard or TriggerWebhookEventAsync.</summary>
        [JsonProperty("sample")] public bool Sample { get; set; }
        [JsonProperty("createdAt")] public string CreatedAt { get; set; } = "";
        [JsonProperty("data")] public WebhookEventData Data { get; set; } = new WebhookEventData();
    }

    /// <summary>
    /// The objects an event is about: Order for order.*, Session for checkout.session.* (plus Refund for
    /// checkout.session.refunded), Invoice for invoice.* (plus the paying Session for invoice.paid),
    /// Subscription for subscription.* (plus Invoice for renewed / past_due), LimitOrder for
    /// limit_order.* (plus the fill's Order in Fill for limit_order.filled).
    /// </summary>
    public class WebhookEventData
    {
        [JsonProperty("order")] public Order? Order { get; set; }
        [JsonProperty("session")] public CheckoutSession? Session { get; set; }
        [JsonProperty("refund")] public Refund? Refund { get; set; }
        [JsonProperty("invoice")] public Invoice? Invoice { get; set; }
        [JsonProperty("subscription")] public Subscription? Subscription { get; set; }
        [JsonProperty("limitOrder")] public LimitOrder? LimitOrder { get; set; }
        [JsonProperty("fill")] public Order? Fill { get; set; }
    }

    /// <summary>Body of POST /v1/webhooks/trigger.</summary>
    public class TriggerWebhookResponse
    {
        [JsonProperty("eventId")] public string EventId { get; set; } = "";
        /// <summary>Number of test endpoints the event was queued for.</summary>
        [JsonProperty("deliveries")] public int Deliveries { get; set; }
        [JsonProperty("payload")] public WebhookEvent Payload { get; set; } = new WebhookEvent();
    }
}
