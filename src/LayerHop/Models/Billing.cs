#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LayerHop
{
    /// <summary>Invoice statuses.</summary>
    public static class InvoiceStatuses
    {
        /// <summary>Editable and not visible to the customer.</summary>
        public const string Draft = "draft";
        /// <summary>Sent and payable.</summary>
        public const string Open = "open";
        /// <summary>Paid through checkout.</summary>
        public const string Paid = "paid";
        /// <summary>Cancelled.</summary>
        public const string Void = "void";
    }

    /// <summary>One invoice line. The line total is UnitAmount x Quantity (settlement-token base units).</summary>
    public class InvoiceLineItem
    {
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("quantity")] public int Quantity { get; set; } = 1;
        [JsonProperty("unitAmount")] public string UnitAmount { get; set; } = "";

        internal JObject ToJson() => new JObject
        {
            ["description"] = Description,
            ["quantity"] = Quantity,
            ["unitAmount"] = UnitAmount,
        };
    }

    /// <summary>Body of POST /v1/invoices.</summary>
    public class CreateInvoiceParams
    {
        /// <summary>Receives the invoice email (and the receipt once paid).</summary>
        [JsonProperty("customerEmail")] public string? CustomerEmail { get; set; }
        [JsonProperty("customerName")] public string? CustomerName { get; set; }
        [JsonProperty("merchantName")] public string? MerchantName { get; set; }
        [JsonProperty("memo")] public string? Memo { get; set; }
        /// <summary>1 to 50 lines; the invoice amount is their sum.</summary>
        [JsonProperty("lineItems")] public List<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
        [JsonProperty("settleChain")] public string SettleChain { get; set; } = "";
        [JsonProperty("settleToken")] public string SettleToken { get; set; } = "";
        [JsonProperty("recipient")] public string Recipient { get; set; } = "";
        /// <summary>ISO 8601 date ("2026-11-01") or date-time.</summary>
        [JsonProperty("dueAt")] public string? DueAt { get; set; }
        /// <summary>Finalize and email right away (status "open").</summary>
        [JsonProperty("send")] public bool? Send { get; set; }
    }

    /// <summary>
    /// Body of PATCH /v1/invoices/{id} (drafts only): only non-null fields are sent. An empty string
    /// clears a nullable field (CustomerEmail, CustomerName, MerchantName, Memo, DueAt); a non-null
    /// LineItems replaces every line.
    /// </summary>
    public class UpdateInvoiceParams
    {
        public string? CustomerEmail { get; set; }
        public string? CustomerName { get; set; }
        public string? MerchantName { get; set; }
        public string? Memo { get; set; }
        public List<InvoiceLineItem>? LineItems { get; set; }
        public string? SettleChain { get; set; }
        public string? SettleToken { get; set; }
        public string? Recipient { get; set; }
        public string? DueAt { get; set; }

        internal JObject ToJson()
        {
            JArray? lines = null;
            if (LineItems != null)
            {
                lines = new JArray();
                foreach (var li in LineItems) lines.Add(li.ToJson());
            }
            return new Patch()
                .Nullable("customerEmail", CustomerEmail)
                .Nullable("customerName", CustomerName)
                .Nullable("merchantName", MerchantName)
                .Nullable("memo", Memo)
                .Set("lineItems", lines)
                .Set("settleChain", SettleChain)
                .Set("settleToken", SettleToken)
                .Set("recipient", Recipient)
                .Nullable("dueAt", DueAt)
                .Body;
        }
    }

    /// <summary>A bill sent by email and paid through checkout ("invoice"). Owner-only fields are null from GetPublicInvoice.</summary>
    public class Invoice
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("object")] public string Object { get; set; } = "";
        [JsonProperty("livemode")] public bool Livemode { get; set; }
        [JsonProperty("number")] public string Number { get; set; } = "";
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("merchantName")] public string? MerchantName { get; set; }
        [JsonProperty("customerName")] public string? CustomerName { get; set; }
        [JsonProperty("memo")] public string? Memo { get; set; }
        [JsonProperty("lineItems")] public List<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        [JsonProperty("settlement")] public CheckoutSettlement Settlement { get; set; } = new CheckoutSettlement();
        [JsonProperty("dueAt")] public string? DueAt { get; set; }
        [JsonProperty("paidAt")] public string? PaidAt { get; set; }
        /// <summary>Hosted invoice page (layerhop.com/invoice/{id}).</summary>
        [JsonProperty("url")] public string Url { get; set; } = "";
        /// <summary>PDF receipt, once paid.</summary>
        [JsonProperty("receiptUrl")] public string? ReceiptUrl { get; set; }
        [JsonProperty("createdAt")] public string CreatedAt { get; set; } = "";
        [JsonProperty("customerEmail")] public string? CustomerEmail { get; set; }
        [JsonProperty("recipient")] public string? Recipient { get; set; }
        [JsonProperty("sessionId")] public string? SessionId { get; set; }
        [JsonProperty("subscriptionId")] public string? SubscriptionId { get; set; }
        [JsonProperty("sentAt")] public string? SentAt { get; set; }
        [JsonProperty("receiptSentAt")] public string? ReceiptSentAt { get; set; }
        [JsonProperty("voidedAt")] public string? VoidedAt { get; set; }
        [JsonProperty("updatedAt")] public string? UpdatedAt { get; set; }
    }

    public class ListInvoicesParams
    {
        public string? Status { get; set; }
        public string? Cursor { get; set; }
        public int? Limit { get; set; }
    }

    public class ListInvoicesResponse
    {
        [JsonProperty("invoices")] public List<Invoice> Invoices { get; set; } = new List<Invoice>();
        [JsonProperty("nextCursor")] public string? NextCursor { get; set; }
    }

    /// <summary>The checkout session that pays an invoice: send the payer to Url.</summary>
    public class PayInvoiceResponse
    {
        [JsonProperty("sessionId")] public string SessionId { get; set; } = "";
        [JsonProperty("url")] public string Url { get; set; } = "";
    }

    /// <summary>Deposit address statuses.</summary>
    public static class DepositAddressStatuses
    {
        public const string Active = "active";
        public const string Disabled = "disabled";
    }

    /// <summary>Body of POST /v1/deposit-addresses.</summary>
    public class CreateDepositAddressParams
    {
        [JsonProperty("srcChain")] public string SrcChain { get; set; } = "";
        [JsonProperty("srcToken")] public string SrcToken { get; set; } = "";
        [JsonProperty("dstChain")] public string DstChain { get; set; } = "";
        [JsonProperty("dstToken")] public string DstToken { get; set; } = "";
        /// <summary>Receives DstToken on DstChain.</summary>
        [JsonProperty("recipient")] public string Recipient { get; set; } = "";
        /// <summary>The user's address on SrcChain for refunds. Never a CEX deposit address.</summary>
        [JsonProperty("refundTo")] public string RefundTo { get; set; } = "";
        /// <summary>Default true (any amount, forever). False makes a one-time address for exactly Amount.</summary>
        [JsonProperty("reusable")] public bool? Reusable { get; set; }
        /// <summary>Base units of SrcToken: required when Reusable is false; otherwise sizes the estimate.</summary>
        [JsonProperty("amount")] public string? Amount { get; set; }
        [JsonProperty("label")] public string? Label { get; set; }
        [JsonProperty("metadata")] public Dictionary<string, string>? Metadata { get; set; }
        /// <summary>Defaults to the client's options.</summary>
        [JsonProperty("integrator")] public string? Integrator { get; set; }
        /// <summary>Defaults to the client's options.</summary>
        [JsonProperty("integratorFeeBps")] public int? IntegratorFeeBps { get; set; }
    }

    public class DepositAddressFees
    {
        [JsonProperty("ourFeeBps")] public int OurFeeBps { get; set; }
        [JsonProperty("integratorFeeBps")] public int IntegratorFeeBps { get; set; }
        [JsonProperty("integrator")] public string? Integrator { get; set; }
    }

    public class DepositAddressEstimate
    {
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        [JsonProperty("dstAmount")] public string DstAmount { get; set; } = "";
        [JsonProperty("dstAmountMin")] public string DstAmountMin { get; set; } = "";
        [JsonProperty("estimatedSeconds")] public int EstimatedSeconds { get; set; }
    }

    /// <summary>An address the user sends Token to on ChainId; the provider delivers DstToken to Recipient ("deposit_address"). LayerHop never holds the funds.</summary>
    public class DepositAddress
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("object")] public string Object { get; set; } = "";
        [JsonProperty("livemode")] public bool Livemode { get; set; }
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("provider")] public string Provider { get; set; } = "";
        /// <summary>What your user sends Token to on ChainId.</summary>
        [JsonProperty("address")] public string Address { get; set; } = "";
        [JsonProperty("chainId")] public long ChainId { get; set; }
        [JsonProperty("token")] public string Token { get; set; } = "";
        [JsonProperty("dstChainId")] public long DstChainId { get; set; }
        [JsonProperty("dstToken")] public string DstToken { get; set; } = "";
        [JsonProperty("recipient")] public string Recipient { get; set; } = "";
        [JsonProperty("refundTo")] public string RefundTo { get; set; } = "";
        [JsonProperty("reusable")] public bool Reusable { get; set; }
        [JsonProperty("amount")] public string? Amount { get; set; }
        [JsonProperty("fees")] public DepositAddressFees Fees { get; set; } = new DepositAddressFees();
        [JsonProperty("label")] public string? Label { get; set; }
        [JsonProperty("metadata")] public Dictionary<string, string>? Metadata { get; set; }
        [JsonProperty("depositCount")] public int DepositCount { get; set; }
        [JsonProperty("lastDepositAt")] public string? LastDepositAt { get; set; }
        [JsonProperty("createdAt")] public string CreatedAt { get; set; } = "";
        /// <summary>Set by CreateDepositAddressAsync only.</summary>
        [JsonProperty("estimate")] public DepositAddressEstimate? Estimate { get; set; }
        /// <summary>The 20 most recent deposit orders (GetDepositAddressAsync only).</summary>
        [JsonProperty("deposits")] public List<Order>? Deposits { get; set; }
    }

    public class ListDepositAddressesParams
    {
        public string? Status { get; set; }
        public string? Label { get; set; }
        public string? Cursor { get; set; }
        public int? Limit { get; set; }
    }

    public class ListDepositAddressesResponse
    {
        [JsonProperty("depositAddresses")] public List<DepositAddress> DepositAddresses { get; set; } = new List<DepositAddress>();
        [JsonProperty("nextCursor")] public string? NextCursor { get; set; }
    }

    public class SimulateDepositResponse
    {
        [JsonProperty("order")] public Order Order { get; set; } = new Order();
    }

    /// <summary>Subscription statuses.</summary>
    public static class SubscriptionStatuses
    {
        public const string Active = "active";
        public const string PastDue = "past_due";
        public const string Paused = "paused";
        public const string Canceled = "canceled";
    }

    /// <summary>Billing period units.</summary>
    public static class SubscriptionIntervals
    {
        public const string Day = "day";
        public const string Week = "week";
        public const string Month = "month";
        public const string Year = "year";
    }

    /// <summary>
    /// Body of POST /v1/subscriptions. Set ProductId to bill a product, or Amount, SettleChain,
    /// SettleToken, Recipient and Description instead.
    /// </summary>
    public class CreateSubscriptionParams
    {
        [JsonProperty("customerEmail")] public string CustomerEmail { get; set; } = "";
        /// <summary><see cref="SubscriptionIntervals"/>.</summary>
        [JsonProperty("interval")] public string Interval { get; set; } = "";
        /// <summary>1-365 (API default 1). 3 + month is quarterly.</summary>
        [JsonProperty("intervalCount")] public int? IntervalCount { get; set; }
        [JsonProperty("productId")] public string? ProductId { get; set; }
        [JsonProperty("amount")] public string? Amount { get; set; }
        [JsonProperty("settleChain")] public string? SettleChain { get; set; }
        [JsonProperty("settleToken")] public string? SettleToken { get; set; }
        [JsonProperty("recipient")] public string? Recipient { get; set; }
        [JsonProperty("description")] public string? Description { get; set; }
        [JsonProperty("customerName")] public string? CustomerName { get; set; }
        [JsonProperty("merchantName")] public string? MerchantName { get; set; }
        [JsonProperty("memo")] public string? Memo { get; set; }
        /// <summary>0-60 (API default 3).</summary>
        [JsonProperty("daysUntilDue")] public int? DaysUntilDue { get; set; }
        /// <summary>ISO 8601 date-time of the first period (default now).</summary>
        [JsonProperty("startAt")] public string? StartAt { get; set; }
        [JsonProperty("metadata")] public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Body of PATCH /v1/subscriptions/{id}: only non-null fields are sent; changes apply from the next
    /// invoice. An empty string clears a nullable field (CustomerName, MerchantName, Memo); a non-null
    /// empty Metadata sends null. CancelAtPeriodEnd true schedules a cancel; false undoes it.
    /// </summary>
    public class UpdateSubscriptionParams
    {
        public string? CustomerEmail { get; set; }
        public string? CustomerName { get; set; }
        public string? MerchantName { get; set; }
        public string? Description { get; set; }
        public string? Memo { get; set; }
        public string? Amount { get; set; }
        public int? DaysUntilDue { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public bool? CancelAtPeriodEnd { get; set; }

        internal JObject ToJson()
        {
            JToken? meta = null;
            if (Metadata != null)
            {
                if (Metadata.Count == 0) meta = JValue.CreateNull();
                else
                {
                    var o = new JObject();
                    foreach (var kv in Metadata) o[kv.Key] = kv.Value;
                    meta = o;
                }
            }
            return new Patch()
                .Set("customerEmail", CustomerEmail)
                .Nullable("customerName", CustomerName)
                .Nullable("merchantName", MerchantName)
                .Set("description", Description)
                .Nullable("memo", Memo)
                .Set("amount", Amount)
                .Set("daysUntilDue", DaysUntilDue)
                .Set("metadata", meta)
                .Set("cancelAtPeriodEnd", CancelAtPeriodEnd)
                .Body;
        }
    }

    /// <summary>Bills a customer every period with an emailed invoice, paid with any token ("subscription").</summary>
    public class Subscription
    {
        [JsonProperty("id")] public string Id { get; set; } = "";
        [JsonProperty("object")] public string Object { get; set; } = "";
        [JsonProperty("livemode")] public bool Livemode { get; set; }
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("customerEmail")] public string CustomerEmail { get; set; } = "";
        [JsonProperty("customerName")] public string? CustomerName { get; set; }
        [JsonProperty("merchantName")] public string? MerchantName { get; set; }
        [JsonProperty("description")] public string Description { get; set; } = "";
        [JsonProperty("memo")] public string? Memo { get; set; }
        [JsonProperty("productId")] public string? ProductId { get; set; }
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        [JsonProperty("settlement")] public CheckoutSettlement Settlement { get; set; } = new CheckoutSettlement();
        [JsonProperty("recipient")] public string Recipient { get; set; } = "";
        [JsonProperty("interval")] public string Interval { get; set; } = "";
        [JsonProperty("intervalCount")] public int IntervalCount { get; set; }
        [JsonProperty("daysUntilDue")] public int DaysUntilDue { get; set; }
        [JsonProperty("currentPeriodStart")] public string? CurrentPeriodStart { get; set; }
        [JsonProperty("currentPeriodEnd")] public string? CurrentPeriodEnd { get; set; }
        [JsonProperty("nextInvoiceAt")] public string? NextInvoiceAt { get; set; }
        [JsonProperty("cancelAt")] public string? CancelAt { get; set; }
        [JsonProperty("cancelAtPeriodEnd")] public bool CancelAtPeriodEnd { get; set; }
        [JsonProperty("canceledAt")] public string? CanceledAt { get; set; }
        /// <summary>"requested" or "unpaid".</summary>
        [JsonProperty("cancellationReason")] public string? CancellationReason { get; set; }
        [JsonProperty("pausedAt")] public string? PausedAt { get; set; }
        [JsonProperty("latestInvoiceId")] public string? LatestInvoiceId { get; set; }
        [JsonProperty("metadata")] public Dictionary<string, string>? Metadata { get; set; }
        [JsonProperty("createdAt")] public string CreatedAt { get; set; } = "";
        [JsonProperty("updatedAt")] public string UpdatedAt { get; set; } = "";
    }

    public class ListSubscriptionsParams
    {
        public string? Status { get; set; }
        public string? CustomerEmail { get; set; }
        public string? Cursor { get; set; }
        public int? Limit { get; set; }
    }

    public class ListSubscriptionsResponse
    {
        [JsonProperty("subscriptions")] public List<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        [JsonProperty("nextCursor")] public string? NextCursor { get; set; }
    }
}
