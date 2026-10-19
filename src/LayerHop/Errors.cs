#nullable enable
using System;

namespace LayerHop
{
    /// <summary>
    /// Thrown for every non-2xx API response. <see cref="Code"/> is a stable error code such as
    /// <c>QUOTE_EXPIRED</c> or <c>NO_ROUTE</c> (https://docs.layerhop.com/errors/&lt;Code&gt;).
    /// </summary>
    public class LayerHopApiException : Exception
    {
        /// <summary>Stable error code, e.g. <c>NO_ROUTE</c>. <c>INTERNAL</c> when the body had none.</summary>
        public string Code { get; }

        /// <summary>HTTP status code.</summary>
        public int Status { get; }

        /// <summary>Request id for support (from the body or the <c>x-request-id</c> header).</summary>
        public string? RequestId { get; }

        /// <summary>The error catalog page for <see cref="Code"/>.</summary>
        public string DocsUrl => "https://docs.layerhop.com/errors/" + Code;

        /// <summary>Creates the exception.</summary>
        public LayerHopApiException(string code, string message, int status, string? requestId)
            : base(Format(code, message, status, requestId))
        {
            Code = code;
            Status = status;
            RequestId = requestId;
            ApiMessage = message;
        }

        /// <summary>The message from the API, without the code and status prefix.</summary>
        public string ApiMessage { get; }

        private static string Format(string code, string message, int status, string? requestId)
        {
            var s = "layerhop: " + code + " (" + status + "): " + message;
            if (!string.IsNullOrEmpty(requestId)) s += " [requestId " + requestId + "]";
            return s;
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
