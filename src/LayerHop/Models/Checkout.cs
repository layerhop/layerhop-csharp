#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LayerHop
{
    /// <summary>Checkout session statuses. Paid is final; treat Processing as not yet paid.</summary>
    public static class CheckoutStatuses
    {
        public const string Open = "open";
        public const string Processing = "processing";
        public const string Paid = "paid";
        public const string Expired = "expired";
        public const string NeedsReview = "needs_review";
    }

    /// <summary>Body of POST /v1/checkout/sessions. Null optional fields are omitted.</summary>
    public class CreateCheckoutParams
    {
        /// <summary>Amount due, integer string in base units of the settlement token (49 USDC = "49000000").</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        /// <summary>Chain id or slug: ethereum, arbitrum, base, optimism, bsc, polygon or solana.</summary>
        [JsonProperty("settleChain")] public string SettleChain { get; set; } = "";
        /// <summary>"USDC" or "USDT" (or its address) on SettleChain.</summary>
        [JsonProperty("settleToken")] public string SettleToken { get; set; } = "";
        /// <summary>Your payout address on the settlement chain.</summary>
        [JsonProperty("recipient")] public string Recipient { get; set; } = "";
        [JsonProperty("merchantName")] public string? MerchantName { get; set; }
        /// <summary>Shown to the payer.</summary>
        [JsonProperty("description")] public string? Description { get; set; }
        /// <summary>Your own order id; never shown to the payer.</summary>
        [JsonProperty("reference")] public string? Reference { get; set; }
        /// <summary>Up to 20 string values; never shown to the payer.</summary>
        [JsonProperty("metadata")] public Dictionary<string, string>? Metadata { get; set; }
        [JsonProperty("successUrl")] public string? SuccessUrl { get; set; }
        [JsonProperty("cancelUrl")] public string? CancelUrl { get; set; }
        /// <summary>5-10080; API default 60.</summary>
        [JsonProperty("expiresInMinutes")] public int? ExpiresInMinutes { get; set; }
    }

    /// <summary>The token a checkout is paid out in.</summary>
    public class CheckoutSettlement
    {
        [JsonProperty("chainId")] public long ChainId { get; set; }
        [JsonProperty("token")] public string Token { get; set; } = "";
        [JsonProperty("symbol")] public string Symbol { get; set; } = "";
        [JsonProperty("decimals")] public int Decimals { get; set; }
    }

    /// <summary>The current (or final) payment attempt of a session.</summary>
    public class CheckoutPayment
    {
        [JsonProperty("quoteId")] public string QuoteId { get; set; } = "";
        [JsonProperty("payer")] public string? Payer { get; set; }
        [JsonProperty("srcChainId")] public long? SrcChainId { get; set; }
        [JsonProperty("srcToken")] public string? SrcToken { get; set; }
        /// <summary>What the payer sent, base units of SrcToken.</summary>
        [JsonProperty("srcAmount")] public string? SrcAmount { get; set; }
        [JsonProperty("srcTxHash")] public string? SrcTxHash { get; set; }
        /// <summary>The verified delivery transaction once paid.</summary>
        [JsonProperty("dstTxHash")] public string? DstTxHash { get; set; }
        [JsonProperty("paidAt")] public string? PaidAt { get; set; }
    }

    /// <summary>A payment request ("checkout.session").</summary>
    public class CheckoutSession
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("object")] public string Object { get; set; } = "";
        [JsonProperty("livemode")] public bool Livemode { get; set; }
        /// <summary>See <see cref="CheckoutStatuses"/>.</summary>
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("statusDetail")] public string? StatusDetail { get; set; }
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        [JsonProperty("settlement")] public CheckoutSettlement Settlement { get; set; } = new CheckoutSettlement();
        [JsonProperty("recipient")] public string Recipient { get; set; } = "";
        [JsonProperty("merchantName")] public string? MerchantName { get; set; }
        [JsonProperty("description")] public string? Description { get; set; }
        /// <summary>The hosted checkout page: send the payer here.</summary>
        [JsonProperty("url")] public string Url { get; set; } = "";
        [JsonProperty("productId")] public string? ProductId { get; set; }
        [JsonProperty("invoiceId")] public string? InvoiceId { get; set; }
        /// <summary>Total of completed refunds, settlement-token base units.</summary>
        [JsonProperty("refundedAmount")] public string? RefundedAmount { get; set; }
        [JsonProperty("successUrl")] public string? SuccessUrl { get; set; }
        [JsonProperty("cancelUrl")] public string? CancelUrl { get; set; }
        [JsonProperty("expiresAt")] public string ExpiresAt { get; set; } = "";
        /// <summary>Null until a payment is reported.</summary>
        [JsonProperty("payment")] public CheckoutPayment? Payment { get; set; }
        /// <summary>Returned only with the owning account's API key.</summary>
        [JsonProperty("reference")] public string? Reference { get; set; }
        [JsonProperty("metadata")] public Dictionary<string, string>? Metadata { get; set; }
        [JsonProperty("createdAt")] public string CreatedAt { get; set; } = "";
        [JsonProperty("updatedAt")] public string UpdatedAt { get; set; } = "";
    }

    /// <summary>Filters for GET /v1/checkout/sessions.</summary>
    public class ListCheckoutSessionsParams
    {
        public string? Status { get; set; }
        public string? Cursor { get; set; }
        /// <summary>1-100 (API default 50).</summary>
        public int? Limit { get; set; }
    }

    public class ListCheckoutSessionsResponse
    {
        [JsonProperty("sessions")] public List<CheckoutSession> Sessions { get; set; } = new List<CheckoutSession>();
        [JsonProperty("nextCursor")] public string? NextCursor { get; set; }
    }

    /// <summary>Body of POST /v1/checkout/sessions/{id}/quote.</summary>
    public class CheckoutQuoteParams
    {
        /// <summary>Chain the payer pays from (id or slug).</summary>
        [JsonProperty("srcChain")] public string SrcChain { get; set; } = "";
        /// <summary>Token address / mint, curated symbol or native symbol.</summary>
        [JsonProperty("srcToken")] public string SrcToken { get; set; } = "";
        /// <summary>Payer's address on SrcChain.</summary>
        [JsonProperty("sender")] public string Sender { get; set; } = "";
        /// <summary>API default 50.</summary>
        [JsonProperty("slippageBps")] public int? SlippageBps { get; set; }
    }

    /// <summary>A quote for paying a session. DstAmountMin is always at least the amount due.</summary>
    public class CheckoutQuote : Quote
    {
        /// <summary>What the payer sends, base units of the chosen token.</summary>
        [JsonProperty("srcAmount")] public string SrcAmount { get; set; } = "";
    }

    public class CheckoutQuoteResponse
    {
        [JsonProperty("sessionId")] public string SessionId { get; set; } = "";
        [JsonProperty("livemode")] public bool Livemode { get; set; }
        [JsonProperty("amountDue")] public string AmountDue { get; set; } = "";
        [JsonProperty("best")] public CheckoutQuote Best { get; set; } = new CheckoutQuote();
        [JsonProperty("quotes")] public List<CheckoutQuote> Quotes { get; set; } = new List<CheckoutQuote>();
        [JsonProperty("errors")] public List<ProviderFailure> Errors { get; set; } = new List<ProviderFailure>();
    }

    /// <summary>Body of POST /v1/checkout/products.</summary>
    public class CreateCheckoutProductParams
    {
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("description")] public string? Description { get; set; }
        [JsonProperty("merchantName")] public string? MerchantName { get; set; }
        /// <summary>Price, integer string in base units of the settlement token.</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        [JsonProperty("settleChain")] public string SettleChain { get; set; } = "";
        [JsonProperty("settleToken")] public string SettleToken { get; set; } = "";
        [JsonProperty("recipient")] public string Recipient { get; set; } = "";
        [JsonProperty("successUrl")] public string? SuccessUrl { get; set; }
        [JsonProperty("cancelUrl")] public string? CancelUrl { get; set; }
        /// <summary>Lifetime of each session started from the product (5-10080).</summary>
        [JsonProperty("expiresInMinutes")] public int? ExpiresInMinutes { get; set; }
        /// <summary>https origins allowed to start sessions from a browser; empty = any.</summary>
        [JsonProperty("allowedOrigins")] public List<string>? AllowedOrigins { get; set; }
        /// <summary>Defaults to true.</summary>
        [JsonProperty("active")] public bool? Active { get; set; }
    }

    /// <summary>
    /// Body of PATCH /v1/checkout/products/{id}: only non-null fields are sent. An empty string clears
    /// a nullable field (Description, MerchantName, SuccessUrl, CancelUrl) by sending null; a non-null
    /// empty AllowedOrigins sends [] (any origin).
    /// </summary>
    public class UpdateCheckoutProductParams
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? MerchantName { get; set; }
        public string? Amount { get; set; }
        public string? SettleChain { get; set; }
        public string? SettleToken { get; set; }
        public string? Recipient { get; set; }
        public string? SuccessUrl { get; set; }
        public string? CancelUrl { get; set; }
        public int? ExpiresInMinutes { get; set; }
        public List<string>? AllowedOrigins { get; set; }
        public bool? Active { get; set; }

        internal JObject ToJson() => new Patch()
            .Set("name", Name)
            .Nullable("description", Description)
            .Nullable("merchantName", MerchantName)
            .Set("amount", Amount)
            .Set("settleChain", SettleChain)
            .Set("settleToken", SettleToken)
            .Set("recipient", Recipient)
            .Nullable("successUrl", SuccessUrl)
            .Nullable("cancelUrl", CancelUrl)
            .Set("expiresInMinutes", ExpiresInMinutes)
            .Set("allowedOrigins", AllowedOrigins == null ? null : Patch.Strings(AllowedOrigins))
            .Set("active", Active)
            .Body;
    }

    /// <summary>A fixed price for payment buttons, layerhop.com/buy/{id} links and store plugins ("checkout.product").</summary>
    public class CheckoutProduct
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("object")] public string Object { get; set; } = "";
        [JsonProperty("livemode")] public bool Livemode { get; set; }
        [JsonProperty("active")] public bool Active { get; set; }
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("description")] public string? Description { get; set; }
        [JsonProperty("merchantName")] public string? MerchantName { get; set; }
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        [JsonProperty("settlement")] public CheckoutSettlement Settlement { get; set; } = new CheckoutSettlement();
        /// <summary>https://layerhop.com/buy/{id}: starts a session and opens the payment page.</summary>
        [JsonProperty("url")] public string Url { get; set; } = "";
        [JsonProperty("recipient")] public string? Recipient { get; set; }
        [JsonProperty("successUrl")] public string? SuccessUrl { get; set; }
        [JsonProperty("cancelUrl")] public string? CancelUrl { get; set; }
        [JsonProperty("expiresInMinutes")] public int? ExpiresInMinutes { get; set; }
        [JsonProperty("allowedOrigins")] public List<string>? AllowedOrigins { get; set; }
        [JsonProperty("createdAt")] public string? CreatedAt { get; set; }
        [JsonProperty("updatedAt")] public string? UpdatedAt { get; set; }
    }

    public class ListCheckoutProductsParams
    {
        public bool? Active { get; set; }
        public string? Cursor { get; set; }
        public int? Limit { get; set; }
    }

    public class ListCheckoutProductsResponse
    {
        [JsonProperty("products")] public List<CheckoutProduct> Products { get; set; } = new List<CheckoutProduct>();
        [JsonProperty("nextCursor")] public string? NextCursor { get; set; }
    }

    /// <summary>Body of POST /v1/checkout/products/{id}/sessions: all a caller may add to the product's terms.</summary>
    public class StartProductCheckoutParams
    {
        [JsonProperty("reference")] public string? Reference { get; set; }
        /// <summary>Up to 10 string values.</summary>
        [JsonProperty("metadata")] public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>Refund statuses.</summary>
    public static class RefundStatuses
    {
        public const string Pending = "pending";
        public const string Submitted = "submitted";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }

    /// <summary>Refund destinations for <see cref="CreateRefundParams.To"/>.</summary>
    public static class RefundDestinations
    {
        /// <summary>The payer's original chain and token (default).</summary>
        public const string Original = "original";
        /// <summary>The settlement token, to the payer's address on the settlement chain.</summary>
        public const string Settlement = "settlement";
    }

    /// <summary>Body of POST /v1/checkout/sessions/{id}/refunds.</summary>
    public class CreateRefundParams
    {
        /// <summary>Settlement-token base units; default: everything not yet refunded.</summary>
        [JsonProperty("amount")] public string? Amount { get; set; }
        [JsonProperty("reason")] public string? Reason { get; set; }
        /// <summary>Wallet that signs the refund (default: the session's payout address).</summary>
        [JsonProperty("from")] public string? From { get; set; }
        /// <summary><see cref="RefundDestinations"/>.</summary>
        [JsonProperty("to")] public string? To { get; set; }
    }

    public class RefundDestination
    {
        [JsonProperty("chainId")] public long ChainId { get; set; }
        [JsonProperty("token")] public string Token { get; set; } = "";
        [JsonProperty("address")] public string Address { get; set; } = "";
    }

    /// <summary>Returns (part of) a paid checkout session to its payer ("refund").</summary>
    public class Refund
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("object")] public string Object { get; set; } = "";
        [JsonProperty("sessionId")] public string? SessionId { get; set; }
        [JsonProperty("livemode")] public bool Livemode { get; set; }
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("statusDetail")] public string? StatusDetail { get; set; }
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        [JsonProperty("reason")] public string? Reason { get; set; }
        [JsonProperty("quoteId")] public string? QuoteId { get; set; }
        [JsonProperty("to")] public RefundDestination To { get; set; } = new RefundDestination();
        [JsonProperty("txHash")] public string? TxHash { get; set; }
        [JsonProperty("createdAt")] public string CreatedAt { get; set; } = "";
        [JsonProperty("updatedAt")] public string? UpdatedAt { get; set; }
    }

    /// <summary>The route that executes a refund: BuildTx(QuoteId), sign from From, ReportSubmitted.</summary>
    public class RefundQuote
    {
        [JsonProperty("quoteId")] public string QuoteId { get; set; } = "";
        [JsonProperty("provider")] public string Provider { get; set; } = "";
        [JsonProperty("srcAmount")] public string SrcAmount { get; set; } = "";
        [JsonProperty("dstAmount")] public string DstAmount { get; set; } = "";
        [JsonProperty("dstAmountMin")] public string DstAmountMin { get; set; } = "";
        [JsonProperty("estimatedSeconds")] public int EstimatedSeconds { get; set; }
        [JsonProperty("expiresAt")] public long ExpiresAt { get; set; }
        /// <summary>The wallet that must sign the refund transaction.</summary>
        [JsonProperty("from")] public string From { get; set; } = "";
    }

    public class CreateRefundResponse
    {
        [JsonProperty("refund")] public Refund Refund { get; set; } = new Refund();
        [JsonProperty("quote")] public RefundQuote Quote { get; set; } = new RefundQuote();
        /// <summary>The remaining steps (build, sign, report).</summary>
        [JsonProperty("next")] public string Next { get; set; } = "";
    }

    public class ListRefundsResponse
    {
        [JsonProperty("refunds")] public List<Refund> Refunds { get; set; } = new List<Refund>();
    }
}
