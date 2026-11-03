#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LayerHop
{
    /// <summary>
    /// Stable API error codes (<see cref="LayerHopApiException.Code"/>). New codes may be added; compare
    /// against these constants and treat anything else as a generic failure.
    /// </summary>
    public static class ErrorCodes
    {
        public const string InvalidParams = "INVALID_PARAMS";
        public const string UnsupportedChain = "UNSUPPORTED_CHAIN";
        public const string UnsupportedToken = "UNSUPPORTED_TOKEN";
        public const string AmountTooSmall = "AMOUNT_TOO_SMALL";
        public const string AmountTooLarge = "AMOUNT_TOO_LARGE";
        public const string CallsReverted = "CALLS_REVERTED";
        public const string LaunchImageInvalid = "LAUNCH_IMAGE_INVALID";
        public const string NoRoute = "NO_ROUTE";
        public const string RouteExcluded = "ROUTE_EXCLUDED";
        public const string QuoteNotFound = "QUOTE_NOT_FOUND";
        public const string QuoteExpired = "QUOTE_EXPIRED";
        public const string OrderNotFound = "ORDER_NOT_FOUND";
        public const string ProviderError = "PROVIDER_ERROR";
        public const string ProviderTimeout = "PROVIDER_TIMEOUT";
        public const string RateLimited = "RATE_LIMITED";
        public const string QuotaExceeded = "QUOTA_EXCEEDED";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string Forbidden = "FORBIDDEN";
        public const string NotFound = "NOT_FOUND";
        public const string Conflict = "CONFLICT";
        public const string GaslessPending = "GASLESS_PENDING";
        public const string GaslessFailed = "GASLESS_FAILED";
        public const string Internal = "INTERNAL";
    }

    /// <summary>
    /// Thrown for every non-2xx API response. <see cref="Code"/> is a stable error code such as
    /// <c>QUOTE_EXPIRED</c> or <c>NO_ROUTE</c> (https://docs.layerhop.com/errors/&lt;Code&gt;).
    /// </summary>
    public class LayerHopApiException : Exception
    {
        /// <summary>Stable error code, e.g. <c>NO_ROUTE</c>. <c>INTERNAL</c> when the body had none. See <see cref="ErrorCodes"/>.</summary>
        public string Code { get; }

        /// <summary>HTTP status code.</summary>
        public int Status { get; }

        /// <summary>Request id for support (from the body or the <c>x-request-id</c> header).</summary>
        public string? RequestId { get; }

        /// <summary>The error catalog page for <see cref="Code"/>.</summary>
        public string DocsUrl => "https://docs.layerhop.com/errors/" + Code;

        /// <summary>
        /// <c>error.details</c> as raw JSON, only on some errors (null otherwise). The typed helpers
        /// <see cref="Suggestions"/>, <see cref="ProviderErrors"/> and <see cref="MinAmount"/> read it.
        /// </summary>
        public JObject? Details { get; }

        /// <summary>Creates the exception.</summary>
        public LayerHopApiException(string code, string message, int status, string? requestId)
            : this(code, message, status, requestId, null)
        {
        }

        /// <summary>Creates the exception with <c>error.details</c>.</summary>
        public LayerHopApiException(string code, string message, int status, string? requestId, JObject? details)
            : base(Format(code, message, status, requestId))
        {
            Code = code;
            Status = status;
            RequestId = requestId;
            ApiMessage = message;
            Details = details;
        }

        /// <summary>The message from the API, without the code and status prefix.</summary>
        public string ApiMessage { get; }

        /// <summary>
        /// <c>NO_ROUTE</c> (and <c>UNSUPPORTED_CHAIN</c> into a route-restricted chain such as Blast): alternatives
        /// that quoted, best first, at most 4. Malformed entries are dropped. Empty when there are none.
        /// </summary>
        public List<RouteSuggestion> Suggestions => RouteSuggestion.Parse(Details);

        /// <summary>Why each provider returned no quote (<c>details.errors</c>). Empty when absent.</summary>
        public List<ProviderFailure> ProviderErrors
        {
            get
            {
                var list = new List<ProviderFailure>();
                if (Details?["errors"] is JArray arr)
                {
                    foreach (var e in arr)
                    {
                        if (!(e is JObject o)) continue;
                        list.Add(new ProviderFailure
                        {
                            Provider = Str(o["provider"]) ?? "",
                            Code = Str(o["code"]) ?? "",
                            Message = Str(o["message"]) ?? "",
                        });
                    }
                }
                return list;
            }
        }

        /// <summary><c>AMOUNT_TOO_SMALL</c>: smallest input known to quote on this route, base units of srcToken.</summary>
        public string? MinAmount
        {
            get
            {
                var s = Str(Details?["minAmount"]);
                return s != null && RouteLeg.IsDigits(s) ? s : null;
            }
        }

        /// <summary><c>AMOUNT_TOO_SMALL</c>: <see cref="MinAmount"/> in USD, when the token has a price.</summary>
        public double? MinAmountUsd
        {
            get
            {
                var t = Details?["minAmountUsd"];
                return t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) ? t.Value<double>() : (double?)null;
            }
        }

        /// <summary><c>AMOUNT_TOO_SMALL</c>: true when MinAmount was found by probing, so the true minimum is at most this.</summary>
        public bool? MinAmountApprox
        {
            get
            {
                var t = Details?["minAmountApprox"];
                return t != null && t.Type == JTokenType.Boolean ? t.Value<bool>() : (bool?)null;
            }
        }

        internal static string? Str(JToken? t) => t != null && t.Type == JTokenType.String ? t.Value<string>() : null;

        private static string Format(string code, string message, int status, string? requestId)
        {
            var s = "layerhop: " + code + " (" + status + "): " + message;
            if (!string.IsNullOrEmpty(requestId)) s += " [requestId " + requestId + "]";
            return s;
        }
    }

    /// <summary>One quotable leg, in the same terms as a quote request. Amounts are base units.</summary>
    public class RouteLeg
    {
        /// <summary>Source chain id.</summary>
        public long SrcChain { get; set; }
        /// <summary>Source token address or mint.</summary>
        public string SrcToken { get; set; } = "";
        /// <summary>Input in base units of SrcToken.</summary>
        public string Amount { get; set; } = "";
        /// <summary>Destination chain id.</summary>
        public long DstChain { get; set; }
        /// <summary>Destination token.</summary>
        public string DstToken { get; set; } = "";
        /// <summary>Estimated output in base units of DstToken; quote again before signing.</summary>
        public string? DstAmount { get; set; }

        internal static bool IsDigits(string s)
        {
            if (s.Length == 0 || s.Length > 78) return false;
            foreach (var ch in s) if (ch < '0' || ch > '9') return false;
            return true;
        }

        internal static bool TryLong(JToken? t, out long v)
        {
            v = 0;
            if (t == null || t.Type != JTokenType.Integer) return false;
            try
            {
                v = t.Value<long>();
                return true;
            }
            catch (OverflowException) { return false; }
            catch (InvalidCastException) { return false; }
        }

        internal static bool TryParse(JToken? t, RouteLeg into)
        {
            if (!(t is JObject o)) return false;
            if (!TryLong(o["srcChain"], out var src) || !TryLong(o["dstChain"], out var dst)) return false;
            var srcToken = LayerHopApiException.Str(o["srcToken"]);
            var dstToken = LayerHopApiException.Str(o["dstToken"]);
            var amount = LayerHopApiException.Str(o["amount"]);
            if (srcToken == null || dstToken == null || amount == null || !IsDigits(amount) || amount == "0") return false;
            string? dstAmount = null;
            var da = o["dstAmount"];
            if (da != null && da.Type != JTokenType.Undefined)
            {
                dstAmount = LayerHopApiException.Str(da);
                if (dstAmount == null || !IsDigits(dstAmount)) return false;
            }
            into.SrcChain = src;
            into.DstChain = dst;
            into.SrcToken = srcToken;
            into.DstToken = dstToken;
            into.Amount = amount;
            into.DstAmount = dstAmount;
            return true;
        }
    }

    /// <summary><c>via_hub</c> only: the token between the two steps.</summary>
    public class RouteHub
    {
        /// <summary>Chain id.</summary>
        public long ChainId { get; set; }
        /// <summary>Token address.</summary>
        public string Token { get; set; } = "";
        /// <summary>Symbol.</summary>
        public string Symbol { get; set; } = "";
        /// <summary>Decimals.</summary>
        public int Decimals { get; set; }
    }

    /// <summary>
    /// An alternative to a route that returned no quote (D-053), from <see cref="LayerHopApiException.Suggestions"/>.
    /// Kind "dst_token": same payment, another destination token. "src_token": same destination token, paid
    /// with another source token. "via_hub": two signed steps through <see cref="Hub"/> (Steps[0] then Steps[1],
    /// each its own quote and signature); the top-level fields describe the whole trip.
    /// </summary>
    public class RouteSuggestion : RouteLeg
    {
        /// <summary>The most suggestions an error carries.</summary>
        public const int Max = 4;

        /// <summary>"dst_token", "src_token" or "via_hub".</summary>
        public string Kind { get; set; } = "";
        /// <summary>Short text for a button, e.g. "Receive USDC on Ethereum instead".</summary>
        public string Message { get; set; } = "";
        /// <summary>The token that changed, for labels.</summary>
        public string? Symbol { get; set; }
        /// <summary>via_hub only.</summary>
        public RouteHub? Hub { get; set; }
        /// <summary>via_hub only: the two legs in order.</summary>
        public List<RouteLeg>? Steps { get; set; }

        /// <summary>
        /// Reads <c>details.suggestions</c> leniently: entries without valid legs, an unknown kind, or (via_hub)
        /// without a valid hub and exactly two valid steps are dropped; at most <see cref="Max"/> are kept.
        /// </summary>
        public static List<RouteSuggestion> Parse(JToken? details)
        {
            var list = new List<RouteSuggestion>();
            if (!(details is JObject d) || !(d["suggestions"] is JArray raw)) return list;
            foreach (var item in raw)
            {
                var s = new RouteSuggestion();
                if (!TryParse(item, s)) continue;
                var o = (JObject)item;
                var kind = LayerHopApiException.Str(o["kind"]);
                var message = LayerHopApiException.Str(o["message"]);
                if (kind == null || message == null) continue;
                if (kind != "dst_token" && kind != "src_token" && kind != "via_hub") continue;
                s.Kind = kind;
                s.Message = message;
                s.Symbol = LayerHopApiException.Str(o["symbol"]);
                if (kind == "via_hub")
                {
                    if (!(o["steps"] is JArray steps) || steps.Count != 2) continue;
                    var a = new RouteLeg();
                    var b = new RouteLeg();
                    if (!TryParse(steps[0], a) || !TryParse(steps[1], b)) continue;
                    if (!(o["hub"] is JObject hub)) continue;
                    var token = LayerHopApiException.Str(hub["token"]);
                    var symbol = LayerHopApiException.Str(hub["symbol"]);
                    if (!TryLong(hub["chainId"], out var hubChain) || !TryLong(hub["decimals"], out var decimals)) continue;
                    if (token == null || symbol == null || decimals < 0 || decimals > int.MaxValue) continue;
                    s.Hub = new RouteHub { ChainId = hubChain, Token = token, Symbol = symbol, Decimals = (int)decimals };
                    s.Steps = new List<RouteLeg> { a, b };
                }
                list.Add(s);
                if (list.Count >= Max) break;
            }
            return list;
        }
    }

    /// <summary>Thrown by <see cref="LayerHopClient.WatchOrderAsync"/> when the order is not final before the timeout.</summary>
    public class LayerHopWatchTimeoutException : TimeoutException
    {
        /// <summary>The last status seen, if any.</summary>
        public OrderStatus? LastStatus { get; }

        /// <summary>Creates the exception.</summary>
        public LayerHopWatchTimeoutException(OrderStatus? lastStatus)
            : base("layerhop: watch timed out before the order was final")
        {
            LastStatus = lastStatus;
        }
    }

    /// <summary>Thrown by <see cref="Webhooks.ConstructEvent(string, string, string?, int, long?)"/> for a missing, malformed, stale or wrong signature.</summary>
    public class LayerHopWebhookSignatureException : Exception
    {
        /// <summary>Creates the exception.</summary>
        public LayerHopWebhookSignatureException(string message) : base(message) { }
    }
}
