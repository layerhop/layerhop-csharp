#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LayerHop
{
    /// <summary>Options for <see cref="LayerHopClient"/>. Every property is optional.</summary>
    public class LayerHopOptions
    {
        /// <summary>API origin; defaults to https://api.layerhop.com. A trailing slash is removed.</summary>
        public string? BaseUrl { get; set; }
        /// <summary>Sent as the <c>x-api-key</c> header. Keep partner keys on your server, not in a shipped game build.</summary>
        public string? ApiKey { get; set; }
        /// <summary>Fee channel sent with every quote (default "api").</summary>
        public string? Channel { get; set; }
        /// <summary>Your partner id, sent as <c>integrator</c> on every quote.</summary>
        public string? Integrator { get; set; }
        /// <summary>Overrides the partner fee; honored only with a partner API key.</summary>
        public int? IntegratorFeeBps { get; set; }
        /// <summary>Defaults to "layerhop-csharp/&lt;Version&gt;".</summary>
        public string? UserAgent { get; set; }
    }

    /// <summary>
    /// Client for the LayerHop REST API. It never holds private keys and never signs: it returns
    /// unsigned transactions that your own wallet integration signs and broadcasts. Amounts are integer
    /// strings in base units. Safe for concurrent use.
    /// </summary>
    public sealed class LayerHopClient : IDisposable
    {
        /// <summary>The hosted API origin.</summary>
        public const string DefaultBaseUrl = "https://api.layerhop.com";
        /// <summary>SDK version, sent in the User-Agent header.</summary>
        public const string Version = "0.6.0";

        private static readonly HttpMethod Patch = new HttpMethod("PATCH");

        private readonly HttpClient _http;
        private readonly bool _ownsHttp;
        private readonly string _baseUrl;
        private readonly string? _apiKey;
        private readonly string _channel;
        private readonly string? _integrator;
        private readonly int? _integratorFeeBps;
        private readonly string _userAgent;

        /// <summary>
        /// Creates a client. Pass your own <paramref name="httpClient"/> to share connections or to plug in a
        /// custom <see cref="HttpMessageHandler"/> (tests, Unity WebGL); it is not disposed by this client.
        /// </summary>
        public LayerHopClient(LayerHopOptions? options = null, HttpClient? httpClient = null)
        {
            options ??= new LayerHopOptions();
            var b = (options.BaseUrl ?? "").TrimEnd('/');
            _baseUrl = b.Length == 0 ? DefaultBaseUrl : b;
            _apiKey = string.IsNullOrEmpty(options.ApiKey) ? null : options.ApiKey;
            _channel = string.IsNullOrEmpty(options.Channel) ? "api" : options.Channel!;
            _integrator = string.IsNullOrEmpty(options.Integrator) ? null : options.Integrator;
            _integratorFeeBps = options.IntegratorFeeBps;
            _userAgent = string.IsNullOrEmpty(options.UserAgent) ? "layerhop-csharp/" + Version : options.UserAgent!;
            if (httpClient != null)
            {
                _http = httpClient;
            }
            else
            {
                _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                _ownsHttp = true;
            }
        }

        /// <summary>The resolved API origin.</summary>
        public string BaseUrl => _baseUrl;

        /// <summary>The fee channel sent with quotes.</summary>
        public string Channel => _channel;

        /// <inheritdoc />
        public void Dispose()
        {
            if (_ownsHttp) _http.Dispose();
        }

        // -----------------------------------------------------------------------------------------
        // Transport
        // -----------------------------------------------------------------------------------------

        private static string Seg(string s) => Uri.EscapeDataString(s);

        private sealed class Query
        {
            private readonly StringBuilder _sb = new StringBuilder();

            public Query Add(string key, string? value)
            {
                if (value == null) return this;
                _sb.Append(_sb.Length == 0 ? '?' : '&').Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
                return this;
            }

            public Query Add(string key, int? value) => value.HasValue ? Add(key, value.Value.ToString(CultureInfo.InvariantCulture)) : this;

            public Query Add(string key, bool? value) => value.HasValue ? Add(key, value.Value ? "true" : "false") : this;

            public override string ToString() => _sb.ToString();
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, Query? query, object? body, CancellationToken ct)
        {
            var raw = await SendRawAsync(method, path, query, body, ct).ConfigureAwait(false);
            try
            {
                return Json.Deserialize<T>(raw);
            }
            catch (JsonException e)
            {
                throw new JsonSerializationException("layerhop: decode " + method.Method + " " + path + ": " + e.Message, e);
            }
        }

        private async Task<string> SendRawAsync(HttpMethod method, string path, Query? query, object? body, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(method, _baseUrl + "/v1" + path + (query?.ToString() ?? "")))
            {
                req.Headers.TryAddWithoutValidation("accept", "application/json");
                req.Headers.TryAddWithoutValidation("user-agent", _userAgent);
                if (_apiKey != null) req.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
                if (body != null)
                {
                    var json = body is JToken tok ? tok.ToString(Formatting.None) : Json.Serialize(body);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                using (var res = await _http.SendAsync(req, ct).ConfigureAwait(false))
                {
                    var text = res.Content == null ? "" : await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var status = (int)res.StatusCode;
                    if (status < 200 || status > 299) throw DecodeError(res, status, text);
                    return text;
                }
            }
        }

        private static LayerHopApiException DecodeError(HttpResponseMessage res, int status, string text)
        {
            string? requestId = null;
            if (res.Headers.TryGetValues("x-request-id", out var ids))
                foreach (var id in ids) { requestId = id; break; }
            var code = "INTERNAL";
            var message = string.IsNullOrEmpty(res.ReasonPhrase) ? "HTTP " + status : res.ReasonPhrase!;
            try
            {
                if (JToken.Parse(text) is JObject root && root["error"] is JObject err)
                {
                    if (err["code"] is JValue c && c.Type == JTokenType.String && ((string?)c)?.Length > 0) code = (string)c!;
                    if (err["message"] is JValue m && m.Type == JTokenType.String && ((string?)m)?.Length > 0) message = (string)m!;
                    if (err["requestId"] is JValue r && r.Type == JTokenType.String && ((string?)r)?.Length > 0) requestId = (string)r!;
                }
            }
            catch (JsonException)
            {
                // Not JSON: keep the defaults.
            }
            return new LayerHopApiException(code, message, status, requestId);
        }

        private static readonly JObject EmptyBody = new JObject();

        // -----------------------------------------------------------------------------------------
        // Swaps
        // -----------------------------------------------------------------------------------------

        /// <summary>GET /v1/health.</summary>
        public Task<Health> HealthAsync(CancellationToken ct = default) =>
            SendAsync<Health>(HttpMethod.Get, "/health", null, null, ct);

        private sealed class ChainsBody { [JsonProperty("chains")] public List<Chain> Chains { get; set; } = new List<Chain>(); }
        private sealed class TokensBody { [JsonProperty("tokens")] public List<Token> Tokens { get; set; } = new List<Token>(); }
        private sealed class PricesBody { [JsonProperty("prices")] public Dictionary<string, double> Prices { get; set; } = new Dictionary<string, double>(); }
        private sealed class TxBody { [JsonProperty("tx")] public BuiltTx Tx { get; set; } = new BuiltTx(); }
        private sealed class TxHashBody { [JsonProperty("txHash")] public string TxHash { get; set; } = ""; }
        private sealed class InvoicesBody { [JsonProperty("invoices")] public List<Invoice> Invoices { get; set; } = new List<Invoice>(); }

        /// <summary>Every chain LayerHop can route from or to (GET /v1/chains).</summary>
        public async Task<List<Chain>> GetChainsAsync(CancellationToken ct = default) =>
            (await SendAsync<ChainsBody>(HttpMethod.Get, "/chains", null, null, ct).ConfigureAwait(false)).Chains;

        /// <summary>The built-in tokens of one chain, by id or slug (GET /v1/tokens).</summary>
        public async Task<List<Token>> GetTokensAsync(string chain, CancellationToken ct = default) =>
            (await SendAsync<TokensBody>(HttpMethod.Get, "/tokens", new Query().Add("chain", chain), null, ct).ConfigureAwait(false)).Tokens;

        /// <summary>
        /// USD prices for up to 100 <c>chainId:address</c> ids (the chain may be a slug) (GET /v1/prices).
        /// Keyed by the ids exactly as requested; ids without a known price are left out.
        /// </summary>
        public async Task<Dictionary<string, double>> GetPricesAsync(IEnumerable<string> ids, CancellationToken ct = default) =>
            (await SendAsync<PricesBody>(HttpMethod.Get, "/prices", new Query().Add("ids", string.Join(",", ids)), null, ct).ConfigureAwait(false)).Prices;

        /// <summary>Asks every provider for a price (GET /v1/quote). Best is the highest output for the user.</summary>
        public Task<QuoteResponse> QuoteAsync(QuoteParams p, CancellationToken ct = default)
        {
            var q = new Query()
                .Add("srcChain", p.SrcChain)
                .Add("srcToken", p.SrcToken)
                .Add("amount", p.Amount)
                .Add("dstChain", p.DstChain)
                .Add("dstToken", p.DstToken)
                .Add("sender", p.Sender)
                .Add("recipient", p.Recipient)
                .Add("slippageBps", p.SlippageBps ?? 50)
                .Add("channel", _channel)
                .Add("integrator", _integrator)
                .Add("integratorFeeBps", _integratorFeeBps);
            if (p.GasOnArrivalUsd.HasValue && p.GasOnArrivalUsd.Value > 0)
                q.Add("gasOnArrivalUsd", p.GasOnArrivalUsd.Value.ToString("R", CultureInfo.InvariantCulture));
            else if (p.GasOnArrival)
                q.Add("gasOnArrival", "true");
            return SendAsync<QuoteResponse>(HttpMethod.Get, "/quote", q, null, ct);
        }

        /// <summary>The unsigned transaction for a quote (POST /v1/orders). EVM txs may carry an Approval to send first.</summary>
        public async Task<BuiltTx> BuildTxAsync(string quoteId, CancellationToken ct = default) =>
            (await SendAsync<TxBody>(HttpMethod.Post, "/orders", null, new JObject { ["quoteId"] = quoteId }, ct).ConfigureAwait(false)).Tx;

        /// <summary>
        /// Sends a wallet-signed Solana transaction (base64) for SubmitViaApi quotes (POST /v1/orders/submit).
        /// Returns the transaction signature.
        /// </summary>
        public async Task<string> SubmitAsync(string quoteId, string signedTransaction, CancellationToken ct = default) =>
            (await SendAsync<TxHashBody>(HttpMethod.Post, "/orders/submit", null,
                new JObject { ["quoteId"] = quoteId, ["signedTransaction"] = signedTransaction }, ct).ConfigureAwait(false)).TxHash;

        /// <summary>Tells LayerHop the tx for a quote was broadcast so it is tracked (POST /v1/orders/{quoteId}/submitted). Idempotent.</summary>
        public Task<ReportSubmittedResponse> ReportSubmittedAsync(string quoteId, string txHash, CancellationToken ct = default) =>
            SendAsync<ReportSubmittedResponse>(HttpMethod.Post, "/orders/" + Seg(quoteId) + "/submitted", null, new JObject { ["txHash"] = txHash }, ct);

        /// <summary>Builds the tx for a quote and dry-runs it without recording an order (POST /v1/simulate).</summary>
        public Task<SimulateResponse> SimulateAsync(string quoteId, CancellationToken ct = default) =>
            SendAsync<SimulateResponse>(HttpMethod.Post, "/simulate", null, new JObject { ["quoteId"] = quoteId }, ct);

        /// <summary>Current state of an order (GET /v1/orders/{provider}/{id}); id is the provider order id or the source tx hash.</summary>
        public Task<OrderStatus> GetOrderStatusAsync(string provider, string id, CancellationToken ct = default) =>
            SendAsync<OrderStatus>(HttpMethod.Get, "/orders/" + Seg(provider) + "/" + Seg(id), null, null, ct);

        /// <summary>Orders by sender or recipient (GET /v1/orders).</summary>
        public Task<ListOrdersResponse> ListOrdersAsync(ListOrdersParams? p = null, CancellationToken ct = default)
        {
            p ??= new ListOrdersParams();
            var q = new Query().Add("address", p.Address).Add("status", p.Status).Add("cursor", p.Cursor).Add("limit", p.Limit);
            return SendAsync<ListOrdersResponse>(HttpMethod.Get, "/orders", q, null, ct);
        }

        /// <summary>
        /// Polls <see cref="GetOrderStatusAsync"/> until the order is final (filled / refunded / failed), yielding
        /// each state change. Throws <see cref="LayerHopWatchTimeoutException"/> after <paramref name="timeout"/>
        /// (default 30 min). Polls every <paramref name="interval"/> (default 3 s).
        /// </summary>
        public async IAsyncEnumerable<OrderStatus> WatchOrder(string provider, string id, TimeSpan? interval = null, TimeSpan? timeout = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var every = interval.HasValue && interval.Value > TimeSpan.Zero ? interval.Value : TimeSpan.FromSeconds(3);
            var limit = timeout.HasValue && timeout.Value > TimeSpan.Zero ? timeout.Value : TimeSpan.FromMinutes(30);
            var deadline = DateTime.UtcNow + limit;
            string? last = null;
            OrderStatus? lastStatus = null;
            while (true)
            {
                var st = await GetOrderStatusAsync(provider, id, ct).ConfigureAwait(false);
                lastStatus = st;
                if (st.State != last)
                {
                    last = st.State;
                    yield return st;
                }
                if (st.IsFinal) yield break;
                if (DateTime.UtcNow + every >= deadline) throw new LayerHopWatchTimeoutException(lastStatus);
                await Task.Delay(every, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Polls until the order is final and returns the final status, calling <paramref name="onChange"/> on
        /// every state change. Throws <see cref="LayerHopWatchTimeoutException"/> after the timeout.
        /// </summary>
        public async Task<OrderStatus> WatchOrderAsync(string provider, string id, TimeSpan? interval = null, TimeSpan? timeout = null,
            Action<OrderStatus>? onChange = null, CancellationToken ct = default)
        {
            OrderStatus? final = null;
            await foreach (var st in WatchOrder(provider, id, interval, timeout, ct).ConfigureAwait(false))
            {
                onChange?.Invoke(st);
                final = st;
            }
            return final!;
        }

        // -----------------------------------------------------------------------------------------
        // Checkout sessions, products, refunds
        // -----------------------------------------------------------------------------------------

        /// <summary>Creates a checkout session and returns it with its hosted URL (POST /v1/checkout/sessions). Needs an API key.</summary>
        public Task<CheckoutSession> CreateCheckoutSessionAsync(CreateCheckoutParams p, CancellationToken ct = default) =>
            SendAsync<CheckoutSession>(HttpMethod.Post, "/checkout/sessions", null, p, ct);

        /// <summary>GET /v1/checkout/sessions/{id}. Public: without the owner's key, Reference and Metadata are null.</summary>
        public Task<CheckoutSession> GetCheckoutSessionAsync(string id, CancellationToken ct = default) =>
            SendAsync<CheckoutSession>(HttpMethod.Get, "/checkout/sessions/" + Seg(id), null, null, ct);

        /// <summary>Your sessions of the key's mode, newest first (GET /v1/checkout/sessions).</summary>
        public Task<ListCheckoutSessionsResponse> ListCheckoutSessionsAsync(ListCheckoutSessionsParams? p = null, CancellationToken ct = default)
        {
            p ??= new ListCheckoutSessionsParams();
            var q = new Query().Add("status", p.Status).Add("cursor", p.Cursor).Add("limit", p.Limit);
            return SendAsync<ListCheckoutSessionsResponse>(HttpMethod.Get, "/checkout/sessions", q, null, ct);
        }

        /// <summary>Cancels an open session (POST /v1/checkout/sessions/{id}/expire). Other statuses: CONFLICT.</summary>
        public Task<CheckoutSession> ExpireCheckoutSessionAsync(string id, CancellationToken ct = default) =>
            SendAsync<CheckoutSession>(HttpMethod.Post, "/checkout/sessions/" + Seg(id) + "/expire", null, EmptyBody, ct);

        /// <summary>Prices paying a session with a token (POST /v1/checkout/sessions/{id}/quote). Public. Pay with BuildTxAsync + ReportSubmittedAsync.</summary>
        public Task<CheckoutQuoteResponse> QuoteCheckoutSessionAsync(string id, CheckoutQuoteParams p, CancellationToken ct = default) =>
            SendAsync<CheckoutQuoteResponse>(HttpMethod.Post, "/checkout/sessions/" + Seg(id) + "/quote", null, p, ct);

        /// <summary>Creates a product (POST /v1/checkout/products). Needs an API key.</summary>
        public Task<CheckoutProduct> CreateCheckoutProductAsync(CreateCheckoutProductParams p, CancellationToken ct = default) =>
            SendAsync<CheckoutProduct>(HttpMethod.Post, "/checkout/products", null, p, ct);

        /// <summary>GET /v1/checkout/products/{id}. Public: name, price and settlement; with the owner's key, every field.</summary>
        public Task<CheckoutProduct> GetCheckoutProductAsync(string id, CancellationToken ct = default) =>
            SendAsync<CheckoutProduct>(HttpMethod.Get, "/checkout/products/" + Seg(id), null, null, ct);

        /// <summary>Your products of the key's mode (GET /v1/checkout/products).</summary>
        public Task<ListCheckoutProductsResponse> ListCheckoutProductsAsync(ListCheckoutProductsParams? p = null, CancellationToken ct = default)
        {
            p ??= new ListCheckoutProductsParams();
            var q = new Query().Add("active", p.Active).Add("cursor", p.Cursor).Add("limit", p.Limit);
            return SendAsync<ListCheckoutProductsResponse>(HttpMethod.Get, "/checkout/products", q, null, ct);
        }

        /// <summary>Changes only the fields that are set (PATCH /v1/checkout/products/{id}).</summary>
        public Task<CheckoutProduct> UpdateCheckoutProductAsync(string id, UpdateCheckoutProductParams p, CancellationToken ct = default) =>
            SendAsync<CheckoutProduct>(Patch, "/checkout/products/" + Seg(id), null, p.ToJson(), ct);

        /// <summary>Starts a session on a product's terms (POST /v1/checkout/products/{id}/sessions). Public. Inactive: CONFLICT.</summary>
        public Task<CheckoutSession> StartProductCheckoutAsync(string id, StartProductCheckoutParams? p = null, CancellationToken ct = default) =>
            SendAsync<CheckoutSession>(HttpMethod.Post, "/checkout/products/" + Seg(id) + "/sessions", null, (object?)p ?? EmptyBody, ct);

        /// <summary>
        /// Refunds (part of) a paid session (POST /v1/checkout/sessions/{id}/refunds). Non-custodial: build the
        /// returned quote with BuildTxAsync, sign it from Quote.From, then ReportSubmittedAsync.
        /// </summary>
        public Task<CreateRefundResponse> CreateRefundAsync(string sessionId, CreateRefundParams? p = null, CancellationToken ct = default) =>
            SendAsync<CreateRefundResponse>(HttpMethod.Post, "/checkout/sessions/" + Seg(sessionId) + "/refunds", null, (object?)p ?? EmptyBody, ct);

        /// <summary>A session's refunds, newest first (GET /v1/checkout/sessions/{id}/refunds).</summary>
        public Task<ListRefundsResponse> ListRefundsAsync(string sessionId, CancellationToken ct = default) =>
            SendAsync<ListRefundsResponse>(HttpMethod.Get, "/checkout/sessions/" + Seg(sessionId) + "/refunds", null, null, ct);

        // -----------------------------------------------------------------------------------------
        // Invoices
        // -----------------------------------------------------------------------------------------

        /// <summary>Creates an invoice (POST /v1/invoices); a draft unless Send is true. Needs an API key.</summary>
        public Task<Invoice> CreateInvoiceAsync(CreateInvoiceParams p, CancellationToken ct = default) =>
            SendAsync<Invoice>(HttpMethod.Post, "/invoices", null, p, ct);

        /// <summary>GET /v1/invoices/{id}.</summary>
        public Task<Invoice> GetInvoiceAsync(string id, CancellationToken ct = default) =>
            SendAsync<Invoice>(HttpMethod.Get, "/invoices/" + Seg(id), null, null, ct);

        /// <summary>Your invoices of the key's mode, newest first (GET /v1/invoices).</summary>
        public Task<ListInvoicesResponse> ListInvoicesAsync(ListInvoicesParams? p = null, CancellationToken ct = default)
        {
            p ??= new ListInvoicesParams();
            var q = new Query().Add("status", p.Status).Add("cursor", p.Cursor).Add("limit", p.Limit);
            return SendAsync<ListInvoicesResponse>(HttpMethod.Get, "/invoices", q, null, ct);
        }

        /// <summary>Edits a draft invoice; only set fields change (PATCH /v1/invoices/{id}).</summary>
        public Task<Invoice> UpdateInvoiceAsync(string id, UpdateInvoiceParams p, CancellationToken ct = default) =>
            SendAsync<Invoice>(Patch, "/invoices/" + Seg(id), null, p.ToJson(), ct);

        /// <summary>Finalizes a draft and emails it, or re-sends an open invoice (POST /v1/invoices/{id}/send).</summary>
        public Task<Invoice> SendInvoiceAsync(string id, CancellationToken ct = default) =>
            SendAsync<Invoice>(HttpMethod.Post, "/invoices/" + Seg(id) + "/send", null, EmptyBody, ct);

        /// <summary>Cancels a draft or open invoice (POST /v1/invoices/{id}/void).</summary>
        public Task<Invoice> VoidInvoiceAsync(string id, CancellationToken ct = default) =>
            SendAsync<Invoice>(HttpMethod.Post, "/invoices/" + Seg(id) + "/void", null, EmptyBody, ct);

        /// <summary>The customer's view of a sent invoice (GET /v1/invoices/{id}/public). Public.</summary>
        public Task<Invoice> GetPublicInvoiceAsync(string id, CancellationToken ct = default) =>
            SendAsync<Invoice>(HttpMethod.Get, "/invoices/" + Seg(id) + "/public", null, null, ct);

        /// <summary>Starts (or reuses) the checkout session that pays an open invoice (POST /v1/invoices/{id}/pay). Public.</summary>
        public Task<PayInvoiceResponse> PayInvoiceAsync(string id, CancellationToken ct = default) =>
            SendAsync<PayInvoiceResponse>(HttpMethod.Post, "/invoices/" + Seg(id) + "/pay", null, EmptyBody, ct);

        // -----------------------------------------------------------------------------------------
        // Deposit addresses
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Creates a deposit address (POST /v1/deposit-addresses): the user sends SrcToken to the returned
        /// Address without connecting a wallet. Needs an API key. Integrator and IntegratorFeeBps default to
        /// the client's options.
        /// </summary>
        public Task<DepositAddress> CreateDepositAddressAsync(CreateDepositAddressParams p, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(p.Integrator)) p.Integrator = _integrator;
            if (p.IntegratorFeeBps == null) p.IntegratorFeeBps = _integratorFeeBps;
            return SendAsync<DepositAddress>(HttpMethod.Post, "/deposit-addresses", null, p, ct);
        }

        /// <summary>A deposit address with its 20 most recent deposits (GET /v1/deposit-addresses/{id}).</summary>
        public Task<DepositAddress> GetDepositAddressAsync(string id, CancellationToken ct = default) =>
            SendAsync<DepositAddress>(HttpMethod.Get, "/deposit-addresses/" + Seg(id), null, null, ct);

        /// <summary>Your deposit addresses of the key's mode, newest first (GET /v1/deposit-addresses).</summary>
        public Task<ListDepositAddressesResponse> ListDepositAddressesAsync(ListDepositAddressesParams? p = null, CancellationToken ct = default)
        {
            p ??= new ListDepositAddressesParams();
            var q = new Query().Add("status", p.Status).Add("label", p.Label).Add("cursor", p.Cursor).Add("limit", p.Limit);
            return SendAsync<ListDepositAddressesResponse>(HttpMethod.Get, "/deposit-addresses", q, null, ct);
        }

        /// <summary>Checks the address for deposits now (POST /v1/deposit-addresses/{id}/refresh).</summary>
        public Task<DepositAddress> RefreshDepositAddressAsync(string id, CancellationToken ct = default) =>
            SendAsync<DepositAddress>(HttpMethod.Post, "/deposit-addresses/" + Seg(id) + "/refresh", null, EmptyBody, ct);

        /// <summary>Stops tracking the address (POST /v1/deposit-addresses/{id}/disable).</summary>
        public Task<DepositAddress> DisableDepositAddressAsync(string id, CancellationToken ct = default) =>
            SendAsync<DepositAddress>(HttpMethod.Post, "/deposit-addresses/" + Seg(id) + "/disable", null, EmptyBody, ct);

        /// <summary>Records a test deposit on a test-mode address (POST /v1/deposit-addresses/{id}/simulate). Test keys only.</summary>
        public Task<SimulateDepositResponse> SimulateDepositAsync(string id, string amount, CancellationToken ct = default) =>
            SendAsync<SimulateDepositResponse>(HttpMethod.Post, "/deposit-addresses/" + Seg(id) + "/simulate", null, new JObject { ["amount"] = amount }, ct);

        // -----------------------------------------------------------------------------------------
        // Subscriptions
        // -----------------------------------------------------------------------------------------

        /// <summary>Creates a subscription (POST /v1/subscriptions): an emailed invoice every period. Needs an API key.</summary>
        public Task<Subscription> CreateSubscriptionAsync(CreateSubscriptionParams p, CancellationToken ct = default) =>
            SendAsync<Subscription>(HttpMethod.Post, "/subscriptions", null, p, ct);

        /// <summary>GET /v1/subscriptions/{id}.</summary>
        public Task<Subscription> GetSubscriptionAsync(string id, CancellationToken ct = default) =>
            SendAsync<Subscription>(HttpMethod.Get, "/subscriptions/" + Seg(id), null, null, ct);

        /// <summary>Your subscriptions of the key's mode, newest first (GET /v1/subscriptions).</summary>
        public Task<ListSubscriptionsResponse> ListSubscriptionsAsync(ListSubscriptionsParams? p = null, CancellationToken ct = default)
        {
            p ??= new ListSubscriptionsParams();
            var q = new Query().Add("status", p.Status).Add("customerEmail", p.CustomerEmail).Add("cursor", p.Cursor).Add("limit", p.Limit);
            return SendAsync<ListSubscriptionsResponse>(HttpMethod.Get, "/subscriptions", q, null, ct);
        }

        /// <summary>Changes only the set fields, from the next invoice on (PATCH /v1/subscriptions/{id}).</summary>
        public Task<Subscription> UpdateSubscriptionAsync(string id, UpdateSubscriptionParams p, CancellationToken ct = default) =>
            SendAsync<Subscription>(Patch, "/subscriptions/" + Seg(id), null, p.ToJson(), ct);

        /// <summary>Cancels now, or at the end of the current period when <paramref name="atPeriodEnd"/> (POST /v1/subscriptions/{id}/cancel).</summary>
        public Task<Subscription> CancelSubscriptionAsync(string id, bool atPeriodEnd = false, CancellationToken ct = default) =>
            SendAsync<Subscription>(HttpMethod.Post, "/subscriptions/" + Seg(id) + "/cancel", null, new JObject { ["atPeriodEnd"] = atPeriodEnd }, ct);

        /// <summary>Pauses an active subscription (POST /v1/subscriptions/{id}/pause).</summary>
        public Task<Subscription> PauseSubscriptionAsync(string id, CancellationToken ct = default) =>
            SendAsync<Subscription>(HttpMethod.Post, "/subscriptions/" + Seg(id) + "/pause", null, EmptyBody, ct);

        /// <summary>Resumes a paused subscription (POST /v1/subscriptions/{id}/resume).</summary>
        public Task<Subscription> ResumeSubscriptionAsync(string id, CancellationToken ct = default) =>
            SendAsync<Subscription>(HttpMethod.Post, "/subscriptions/" + Seg(id) + "/resume", null, EmptyBody, ct);

        /// <summary>The invoices issued for a subscription (up to 100), newest first (GET /v1/subscriptions/{id}/invoices).</summary>
        public async Task<List<Invoice>> ListSubscriptionInvoicesAsync(string id, CancellationToken ct = default) =>
            (await SendAsync<InvoicesBody>(HttpMethod.Get, "/subscriptions/" + Seg(id) + "/invoices", null, null, ct).ConfigureAwait(false)).Invoices;

        // -----------------------------------------------------------------------------------------
        // Limit orders (Solana, beta)
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Creates a Solana limit order and returns it with the unsigned Tx (POST /v1/limit-orders). Have the
        /// wallet sign it (sign only), then <see cref="SubmitLimitOrderAsync"/>. Channel, Integrator and
        /// IntegratorFeeBps default to the client's options.
        /// </summary>
        public Task<LimitOrder> CreateLimitOrderAsync(CreateLimitOrderParams p, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(p.Channel)) p.Channel = _channel;
            if (string.IsNullOrEmpty(p.Integrator)) p.Integrator = _integrator;
            if (p.IntegratorFeeBps == null) p.IntegratorFeeBps = _integratorFeeBps;
            return SendAsync<LimitOrder>(HttpMethod.Post, "/limit-orders", null, p, ct);
        }

        /// <summary>Sends the wallet-signed create or cancel tx (base64) (POST /v1/limit-orders/{id}/submit).</summary>
        public Task<SubmitLimitOrderResponse> SubmitLimitOrderAsync(string id, string signedTransaction, CancellationToken ct = default) =>
            SendAsync<SubmitLimitOrderResponse>(HttpMethod.Post, "/limit-orders/" + Seg(id) + "/submit", null, new JObject { ["signedTransaction"] = signedTransaction }, ct);

        /// <summary>A limit order with its fills (GET /v1/limit-orders/{id}).</summary>
        public Task<LimitOrder> GetLimitOrderAsync(string id, CancellationToken ct = default) =>
            SendAsync<LimitOrder>(HttpMethod.Get, "/limit-orders/" + Seg(id), null, null, ct);

        /// <summary>Limit orders of a wallet, or of the API key's account (GET /v1/limit-orders).</summary>
        public Task<ListLimitOrdersResponse> ListLimitOrdersAsync(ListLimitOrdersParams? p = null, CancellationToken ct = default)
        {
            p ??= new ListLimitOrdersParams();
            var q = new Query().Add("wallet", p.Wallet).Add("status", p.Status).Add("cursor", p.Cursor).Add("limit", p.Limit);
            return SendAsync<ListLimitOrdersResponse>(HttpMethod.Get, "/limit-orders", q, null, ct);
        }

        /// <summary>Cancels a limit order (POST /v1/limit-orders/{id}/cancel); an open order returns a cancel Tx to sign.</summary>
        public Task<CancelLimitOrderResponse> CancelLimitOrderAsync(string id, CancellationToken ct = default) =>
            SendAsync<CancelLimitOrderResponse>(HttpMethod.Post, "/limit-orders/" + Seg(id) + "/cancel", null, EmptyBody, ct);

        /// <summary>Fills, half-fills or expires an open test-mode order (POST /v1/limit-orders/{id}/simulate). See <see cref="LimitOrderSimulations"/>.</summary>
        public Task<LimitOrder> SimulateLimitOrderAsync(string id, string outcome, CancellationToken ct = default) =>
            SendAsync<LimitOrder>(HttpMethod.Post, "/limit-orders/" + Seg(id) + "/simulate", null, new JObject { ["outcome"] = outcome }, ct);

        // -----------------------------------------------------------------------------------------
        // Misc
        // -----------------------------------------------------------------------------------------

        /// <summary>LayerHop's public service status (GET /v1/status), the data behind status.layerhop.com.</summary>
        public Task<ServiceStatus> GetServiceStatusAsync(CancellationToken ct = default) =>
            SendAsync<ServiceStatus>(HttpMethod.Get, "/status", null, null, ct);

        /// <summary>Creates a sample webhook event in test mode (POST /v1/webhooks/trigger). Test keys only.</summary>
        public Task<TriggerWebhookResponse> TriggerWebhookEventAsync(string eventType, CancellationToken ct = default) =>
            SendAsync<TriggerWebhookResponse>(HttpMethod.Post, "/webhooks/trigger", null, new JObject { ["type"] = eventType }, ct);
    }
}
