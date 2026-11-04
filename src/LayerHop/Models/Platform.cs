#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LayerHop
{
    // ---------------------------------------------------------------------------------------------
    // Address screening (D-069)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Address screening on quotes, orders and checkout quotes: which of the request's addresses are on the
    /// OFAC SDN list. Present once the API has a list loaded. With enforcement on, listed addresses are refused
    /// with ADDRESS_SANCTIONED instead.
    /// </summary>
    public class ScreeningAnnotation
    {
        /// <summary>"ofac_sdn".</summary>
        [JsonProperty("source")] public string Source { get; set; } = "";
        /// <summary>Publish date of the list version checked.</summary>
        [JsonProperty("listUpdatedAt")] public string? ListUpdatedAt { get; set; }
        /// <summary>Listed addresses of this request (empty when none).</summary>
        [JsonProperty("sanctioned")] public List<SanctionedAddress> Sanctioned { get; set; } = new List<SanctionedAddress>();
    }

    /// <summary>One listed address of a request.</summary>
    public class SanctionedAddress
    {
        [JsonProperty("address")] public string Address { get; set; } = "";
        /// <summary>"sender", "recipient", "refundTo", "payer" or "payout".</summary>
        [JsonProperty("role")] public string Role { get; set; } = "";
        /// <summary>E.g. "OFAC SDN".</summary>
        [JsonProperty("lists")] public List<string> Lists { get; set; } = new List<string>();
    }

    /// <summary>One address's result of POST /v1/screen.</summary>
    public class ScreeningResult
    {
        /// <summary>As sent.</summary>
        [JsonProperty("address")] public string Address { get; set; } = "";
        [JsonProperty("sanctioned")] public bool Sanctioned { get; set; }
        /// <summary>Lists the address is on ("OFAC SDN"); empty when not listed.</summary>
        [JsonProperty("lists")] public List<string> Lists { get; set; } = new List<string>();
        /// <summary>"ofac_sdn".</summary>
        [JsonProperty("source")] public string Source { get; set; } = "";
        /// <summary>Publish date of the list checked; null while the API has no list loaded (then nothing is reported as sanctioned).</summary>
        [JsonProperty("listUpdatedAt")] public string? ListUpdatedAt { get; set; }
    }

    /// <summary>The sanctions list version the API has loaded.</summary>
    public class ScreeningList
    {
        [JsonProperty("source")] public string Source { get; set; } = "";
        /// <summary>E.g. "OFAC SDN".</summary>
        [JsonProperty("name")] public string Name { get; set; } = "";
        /// <summary>The list's own publish date.</summary>
        [JsonProperty("publishedAt")] public string? PublishedAt { get; set; }
        /// <summary>When LayerHop last downloaded and checked it (daily).</summary>
        [JsonProperty("fetchedAt")] public string FetchedAt { get; set; } = "";
        /// <summary>Digital currency addresses on the list.</summary>
        [JsonProperty("addressCount")] public int AddressCount { get; set; }
    }

    /// <summary>Body of POST /v1/screen.</summary>
    public class ScreenResponse
    {
        /// <summary>One result per address, in order.</summary>
        [JsonProperty("results")] public List<ScreeningResult> Results { get; set; } = new List<ScreeningResult>();
        /// <summary>Null until the API has downloaded a list.</summary>
        [JsonProperty("list")] public ScreeningList? List { get; set; }
        /// <summary>The API refuses listed addresses for everyone (ADDRESS_SANCTIONED).</summary>
        [JsonProperty("enforced")] public bool Enforced { get; set; }
    }

    /// <summary>Body of POST /v1/orders: the unsigned Tx (or the gasless sign request) and the address screening.</summary>
    public class BuildOrderResponse
    {
        /// <summary>Set for normal quotes.</summary>
        [JsonProperty("tx")] public BuiltTx? Tx { get; set; }
        /// <summary>Set for gasless quotes.</summary>
        [JsonProperty("gasless")] public GaslessSignRequest? Gasless { get; set; }
        /// <summary>Address screening (D-069); present once the API has a list loaded.</summary>
        [JsonProperty("screening")] public ScreeningAnnotation? Screening { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // Usage and webhook resend
    // ---------------------------------------------------------------------------------------------

    /// <summary>Requests per HTTP status class.</summary>
    public class StatusClassCounts
    {
        [JsonProperty("2xx")] public long Status2xx { get; set; }
        [JsonProperty("3xx")] public long Status3xx { get; set; }
        [JsonProperty("4xx")] public long Status4xx { get; set; }
        [JsonProperty("5xx")] public long Status5xx { get; set; }
    }

    /// <summary>Filters for GET /v1/usage. Times are ISO 8601 or unix seconds.</summary>
    public class UsageParams
    {
        /// <summary>"hour" or "day" (API default "day").</summary>
        public string? Bucket { get; set; }
        /// <summary>Inclusive start.</summary>
        public string? From { get; set; }
        /// <summary>Exclusive end (default now).</summary>
        public string? To { get; set; }
        /// <summary>Only requests made with this key of your account.</summary>
        public string? KeyId { get; set; }
    }

    /// <summary>Totals of a usage report. Latency percentiles are estimates (null without traffic).</summary>
    public class UsageTotals
    {
        [JsonProperty("requests")] public long Requests { get; set; }
        [JsonProperty("errors")] public long Errors { get; set; }
        [JsonProperty("byStatusClass")] public StatusClassCounts ByStatusClass { get; set; } = new StatusClassCounts();
        [JsonProperty("avgMs")] public double? AvgMs { get; set; }
        [JsonProperty("p50Ms")] public double? P50Ms { get; set; }
        [JsonProperty("p95Ms")] public double? P95Ms { get; set; }
    }

    /// <summary>One hour or day of a usage report.</summary>
    public class UsagePoint
    {
        /// <summary>Bucket start (ISO 8601, UTC).</summary>
        [JsonProperty("ts")] public string Ts { get; set; } = "";
        [JsonProperty("requests")] public long Requests { get; set; }
        [JsonProperty("errors")] public long Errors { get; set; }
        [JsonProperty("byStatusClass")] public StatusClassCounts ByStatusClass { get; set; } = new StatusClassCounts();
        [JsonProperty("p50Ms")] public double? P50Ms { get; set; }
        [JsonProperty("p95Ms")] public double? P95Ms { get; set; }
    }

    /// <summary>One endpoint group of a usage report ("quote", "orders", "checkout", ...; unknown paths count as "other").</summary>
    public class UsageEndpoint
    {
        [JsonProperty("group")] public string Group { get; set; } = "";
        [JsonProperty("requests")] public long Requests { get; set; }
        [JsonProperty("errors")] public long Errors { get; set; }
        [JsonProperty("byStatusClass")] public StatusClassCounts ByStatusClass { get; set; } = new StatusClassCounts();
        [JsonProperty("avgMs")] public double? AvgMs { get; set; }
        [JsonProperty("p50Ms")] public double? P50Ms { get; set; }
        [JsonProperty("p95Ms")] public double? P95Ms { get; set; }
    }

    /// <summary>A stable error code returned in the report's window.</summary>
    public class UsageErrorCode
    {
        [JsonProperty("code")] public string Code { get; set; } = "";
        [JsonProperty("count")] public long Count { get; set; }
        /// <summary>Count per endpoint group.</summary>
        [JsonProperty("byEndpoint")] public Dictionary<string, long> ByEndpoint { get; set; } = new Dictionary<string, long>();
    }

    /// <summary>Body of GET /v1/usage: the key's account and mode.</summary>
    public class UsageReport
    {
        /// <summary>"hour" or "day".</summary>
        [JsonProperty("bucket")] public string Bucket { get; set; } = "";
        [JsonProperty("from")] public string From { get; set; } = "";
        [JsonProperty("to")] public string To { get; set; } = "";
        [JsonProperty("livemode")] public bool Livemode { get; set; }
        [JsonProperty("keyId")] public string? KeyId { get; set; }
        [JsonProperty("totals")] public UsageTotals Totals { get; set; } = new UsageTotals();
        /// <summary>Zero-filled buckets, oldest first.</summary>
        [JsonProperty("series")] public List<UsagePoint> Series { get; set; } = new List<UsagePoint>();
        [JsonProperty("byEndpoint")] public List<UsageEndpoint> ByEndpoint { get; set; } = new List<UsageEndpoint>();
        [JsonProperty("errorCodes")] public List<UsageErrorCode> ErrorCodes { get; set; } = new List<UsageErrorCode>();
    }

    /// <summary>One failed request (GET /v1/usage/errors). Quote RequestId to support.</summary>
    public class UsageError
    {
        [JsonProperty("ts")] public string Ts { get; set; } = "";
        [JsonProperty("keyId")] public string? KeyId { get; set; }
        [JsonProperty("requestId")] public string RequestId { get; set; } = "";
        [JsonProperty("method")] public string Method { get; set; } = "";
        [JsonProperty("path")] public string Path { get; set; } = "";
        [JsonProperty("endpointGroup")] public string EndpointGroup { get; set; } = "";
        [JsonProperty("status")] public int Status { get; set; }
        [JsonProperty("code")] public string Code { get; set; } = "";
        [JsonProperty("latencyMs")] public long LatencyMs { get; set; }
    }

    /// <summary>Filters for GET /v1/usage/errors.</summary>
    public class UsageErrorsParams
    {
        /// <summary>Only this error code, e.g. "NO_ROUTE".</summary>
        public string? Code { get; set; }
        public string? KeyId { get; set; }
        /// <summary>1-200 (API default 50).</summary>
        public int? Limit { get; set; }
    }

    /// <summary>A delivery queued by POST /v1/webhooks/events/{id}/resend.</summary>
    public class WebhookResendDelivery
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("endpointId")] public string EndpointId { get; set; } = "";
    }

    /// <summary>Body of POST /v1/webhooks/events/{id}/resend: the deliveries queued (empty when only open streams get it).</summary>
    public class WebhookResendResult
    {
        [JsonProperty("eventId")] public string EventId { get; set; } = "";
        [JsonProperty("type")] public string Type { get; set; } = "";
        [JsonProperty("livemode")] public bool Livemode { get; set; }
        [JsonProperty("deliveries")] public List<WebhookResendDelivery> Deliveries { get; set; } = new List<WebhookResendDelivery>();
    }

    // ---------------------------------------------------------------------------------------------
    // x402 pay-per-request (D-066)
    // ---------------------------------------------------------------------------------------------

    /// <summary>A scheme and network the facilitator verifies and settles.</summary>
    public class X402Kind
    {
        /// <summary>1 or 2.</summary>
        [JsonProperty("x402Version")] public int X402Version { get; set; }
        /// <summary>"exact".</summary>
        [JsonProperty("scheme")] public string Scheme { get; set; } = "";
        /// <summary>A CAIP-2 id (v2, "eip155:8453") or a v1 network name ("base").</summary>
        [JsonProperty("network")] public string Network { get; set; } = "";
    }

    /// <summary>Body of GET /v1/x402/supported.</summary>
    public class X402Supported
    {
        [JsonProperty("kinds")] public List<X402Kind> Kinds { get; set; } = new List<X402Kind>();
        [JsonProperty("extensions")] public List<string> Extensions { get; set; } = new List<string>();
        [JsonProperty("signers")] public Dictionary<string, List<string>> Signers { get; set; } = new Dictionary<string, List<string>>();
        /// <summary>Whether POST /v1/x402/settle is enabled.</summary>
        [JsonProperty("settle")] public bool Settle { get; set; }
    }

    /// <summary>Body of POST /v1/x402/requirements. Null optional fields are omitted.</summary>
    public class X402RequirementsParams
    {
        /// <summary>"base" (API default), "optimism", "arbitrum", "polygon", or a CAIP-2 id.</summary>
        [JsonProperty("network")] public string? Network { get; set; }
        /// <summary>The price you net per request, USDC base units ("10000" = 0.01 USDC).</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        /// <summary>Your payout address on that network.</summary>
        [JsonProperty("payTo")] public string PayTo { get; set; } = "";
        /// <summary>The resource URL (query and fragment ignored for the deposit address) or any stable id.</summary>
        [JsonProperty("resource")] public string Resource { get; set; } = "";
        [JsonProperty("description")] public string? Description { get; set; }
        [JsonProperty("mimeType")] public string? MimeType { get; set; }
        /// <summary>10-3600 (API default 60).</summary>
        [JsonProperty("maxTimeoutSeconds")] public int? MaxTimeoutSeconds { get; set; }
    }

    /// <summary>Your fee-splitting deposit address for one resource and payout address (the requirements' payTo).</summary>
    public class X402Payee
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("network")] public string Network { get; set; } = "";
        [JsonProperty("chainId")] public long ChainId { get; set; }
        [JsonProperty("asset")] public string Asset { get; set; } = "";
        [JsonProperty("depositAddress")] public string DepositAddress { get; set; } = "";
        /// <summary>Your payout address.</summary>
        [JsonProperty("merchant")] public string Merchant { get; set; } = "";
        [JsonProperty("resource")] public string Resource { get; set; } = "";
        [JsonProperty("feeBps")] public int FeeBps { get; set; }
        [JsonProperty("factory")] public string Factory { get; set; } = "";
        [JsonProperty("lastSweepTx")] public string? LastSweepTx { get; set; }
        [JsonProperty("lastSweptAt")] public string? LastSweptAt { get; set; }
    }

    /// <summary>x402 v2 PaymentRequirements (scheme "exact").</summary>
    public class X402RequirementsV2
    {
        [JsonProperty("scheme")] public string Scheme { get; set; } = "";
        /// <summary>CAIP-2 network id, e.g. "eip155:8453".</summary>
        [JsonProperty("network")] public string Network { get; set; } = "";
        /// <summary>USDC base units the payer signs (price + LayerHop fee).</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        [JsonProperty("asset")] public string Asset { get; set; } = "";
        /// <summary>Your fee-splitting deposit address.</summary>
        [JsonProperty("payTo")] public string PayTo { get; set; } = "";
        [JsonProperty("maxTimeoutSeconds")] public int MaxTimeoutSeconds { get; set; }
        /// <summary>EIP-712 domain of the asset (name, version).</summary>
        [JsonProperty("extra")] public JObject? Extra { get; set; }
    }

    /// <summary>The fee on top of your price.</summary>
    public class X402Fee
    {
        [JsonProperty("bps")] public int Bps { get; set; }
        [JsonProperty("amount")] public string Amount { get; set; } = "";
    }

    /// <summary>Body of POST /v1/x402/requirements: what to answer 402 Payment Required with.</summary>
    public class X402RequirementsResponse
    {
        [JsonProperty("payee")] public X402Payee Payee { get; set; } = new X402Payee();
        /// <summary>Your price (USDC base units).</summary>
        [JsonProperty("price")] public string Price { get; set; } = "";
        /// <summary>What the payer signs (price + fee).</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        [JsonProperty("fee")] public X402Fee Fee { get; set; } = new X402Fee();
        /// <summary>x402 v2 form.</summary>
        [JsonProperty("accepts")] public List<X402RequirementsV2> Accepts { get; set; } = new List<X402RequirementsV2>();
        /// <summary>The same requirements in x402 v1 form (network name, maxAmountRequired).</summary>
        [JsonProperty("acceptsV1")] public List<JObject> AcceptsV1 { get; set; } = new List<JObject>();
        /// <summary>The whole x402 v2 PaymentRequired object (base64 it into the PAYMENT-REQUIRED header).</summary>
        [JsonProperty("paymentRequired")] public JObject PaymentRequired { get; set; } = new JObject();
    }

    /// <summary>Body of POST /v1/x402/verify and /v1/x402/settle.</summary>
    public class X402FacilitatorParams
    {
        /// <summary>1 or 2; omitted when null.</summary>
        [JsonProperty("x402Version")] public int? X402Version { get; set; }
        /// <summary>The decoded X-PAYMENT (v1) or PAYMENT-SIGNATURE (v2) header: payload.signature + payload.authorization (EIP-3009).</summary>
        [JsonProperty("paymentPayload")] public JObject PaymentPayload { get; set; } = new JObject();
        /// <summary>The requirements you answered the 402 with (v1 or v2 form).</summary>
        [JsonProperty("paymentRequirements")] public JObject PaymentRequirements { get; set; } = new JObject();
    }

    /// <summary>Body of POST /v1/x402/verify.</summary>
    public class X402VerifyResponse
    {
        [JsonProperty("isValid")] public bool IsValid { get; set; }
        /// <summary>x402 reason, e.g. "insufficient_funds".</summary>
        [JsonProperty("invalidReason")] public string? InvalidReason { get; set; }
        [JsonProperty("payer")] public string? Payer { get; set; }
        /// <summary>Human-readable detail.</summary>
        [JsonProperty("message")] public string? Message { get; set; }
    }

    /// <summary>Body of POST /v1/x402/settle.</summary>
    public class X402SettleResponse
    {
        [JsonProperty("success")] public bool Success { get; set; }
        /// <summary>x402 reason; "settlement_pending" = sent, receipt not in yet.</summary>
        [JsonProperty("errorReason")] public string? ErrorReason { get; set; }
        [JsonProperty("payer")] public string? Payer { get; set; }
        /// <summary>Transaction hash (empty when nothing was sent).</summary>
        [JsonProperty("transaction")] public string Transaction { get; set; } = "";
        [JsonProperty("network")] public string Network { get; set; } = "";
        [JsonProperty("message")] public string? Message { get; set; }
    }

    /// <summary>x402 payment statuses.</summary>
    public static class X402PaymentStatuses
    {
        public const string Submitted = "submitted";
        public const string Settled = "settled";
        public const string Failed = "failed";
    }

    /// <summary>A payment settled through LayerHop ("x402.payment").</summary>
    public class X402Payment
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("object")] public string Object { get; set; } = "";
        /// <summary>See <see cref="X402PaymentStatuses"/>.</summary>
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("statusDetail")] public string? StatusDetail { get; set; }
        [JsonProperty("network")] public string Network { get; set; } = "";
        [JsonProperty("chainId")] public long ChainId { get; set; }
        [JsonProperty("asset")] public string Asset { get; set; } = "";
        [JsonProperty("payer")] public string Payer { get; set; } = "";
        /// <summary>The deposit address paid.</summary>
        [JsonProperty("payTo")] public string PayTo { get; set; } = "";
        /// <summary>What the payer paid (USDC base units).</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        /// <summary>What you net after the deposit contract's fee.</summary>
        [JsonProperty("merchantAmount")] public string MerchantAmount { get; set; } = "";
        [JsonProperty("feeBps")] public int FeeBps { get; set; }
        [JsonProperty("resource")] public string? Resource { get; set; }
        [JsonProperty("transaction")] public string? Transaction { get; set; }
        /// <summary>The sweep that paid it out to you.</summary>
        [JsonProperty("sweepTx")] public string? SweepTx { get; set; }
        [JsonProperty("settledAt")] public string? SettledAt { get; set; }
        [JsonProperty("createdAt")] public string CreatedAt { get; set; } = "";
        [JsonProperty("updatedAt")] public string? UpdatedAt { get; set; }
    }

    /// <summary>Filters for GET /v1/x402/payments.</summary>
    public class ListX402PaymentsParams
    {
        /// <summary>See <see cref="X402PaymentStatuses"/>.</summary>
        public string? Status { get; set; }
        public string? Cursor { get; set; }
        /// <summary>1-200 (API default 50).</summary>
        public int? Limit { get; set; }
    }

    /// <summary>A page of GET /v1/x402/payments, newest first.</summary>
    public class ListX402PaymentsResponse
    {
        [JsonProperty("payments")] public List<X402Payment> Payments { get; set; } = new List<X402Payment>();
        [JsonProperty("nextCursor")] public string? NextCursor { get; set; }
    }
}
