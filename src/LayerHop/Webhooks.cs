#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LayerHop
{
    /// <summary>
    /// Webhook signature verification. LayerHop signs every delivery with
    /// <c>LayerHop-Signature: t=&lt;unix seconds&gt;,v1=&lt;hex HMAC-SHA256(secret, "&lt;t&gt;.&lt;raw body&gt;")&gt;</c>.
    /// Always verify the raw request body, exactly as received, before parsing JSON.
    /// </summary>
    public static class Webhooks
    {
        /// <summary>The signature header name.</summary>
        public const string SignatureHeader = "LayerHop-Signature";
        /// <summary>The event type header name.</summary>
        public const string EventHeader = "LayerHop-Event";
        /// <summary>The delivery id header name.</summary>
        public const string DeliveryHeader = "LayerHop-Delivery";
        /// <summary>Maximum age of a signature timestamp, in seconds.</summary>
        public const int DefaultToleranceSeconds = 300;

        /// <summary>Hex HMAC-SHA256 of <c>"&lt;timestamp&gt;.&lt;body&gt;"</c>.</summary>
        public static string ComputeSignature(string secret, long timestampSeconds, byte[] body)
        {
            var prefix = Encoding.UTF8.GetBytes(timestampSeconds.ToString(CultureInfo.InvariantCulture) + ".");
            var msg = new byte[prefix.Length + body.Length];
            Buffer.BlockCopy(prefix, 0, msg, 0, prefix.Length);
            Buffer.BlockCopy(body, 0, msg, prefix.Length, body.Length);
            using (var mac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                return ToHex(mac.ComputeHash(msg));
            }
        }

        /// <summary>Builds a <c>LayerHop-Signature</c> header value. Useful for testing your webhook handler.</summary>
        public static string Sign(string secret, string body, long timestampSeconds) =>
            Sign(secret, Encoding.UTF8.GetBytes(body), timestampSeconds);

        /// <summary>Builds a <c>LayerHop-Signature</c> header value. Useful for testing your webhook handler.</summary>
        public static string Sign(string secret, byte[] body, long timestampSeconds) =>
            "t=" + timestampSeconds.ToString(CultureInfo.InvariantCulture) + ",v1=" + ComputeSignature(secret, timestampSeconds, body);

        /// <summary>
        /// True when <paramref name="header"/> is a valid, fresh signature of <paramref name="body"/>.
        /// Rejects timestamps more than <paramref name="toleranceSeconds"/> from now (replay protection) and
        /// accepts any of several <c>v1</c> values (secret rotation). Comparison is constant-time.
        /// </summary>
        public static bool VerifySignature(string secret, string body, string? header, int toleranceSeconds = DefaultToleranceSeconds, long? nowSeconds = null) =>
            VerifySignature(secret, Encoding.UTF8.GetBytes(body), header, toleranceSeconds, nowSeconds);

        /// <inheritdoc cref="VerifySignature(string, string, string?, int, long?)"/>
        public static bool VerifySignature(string secret, byte[] body, string? header, int toleranceSeconds = DefaultToleranceSeconds, long? nowSeconds = null)
        {
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(header)) return false;
            long? t = null;
            var sigs = new List<string>();
            foreach (var raw in header!.Split(','))
            {
                var part = raw.Trim();
                var i = part.IndexOf('=');
                if (i < 0) continue;
                var k = part.Substring(0, i);
                var v = part.Substring(i + 1).Trim();
                if (k == "t")
                {
                    if (!long.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var ts)) return false;
                    t = ts;
                }
                else if (k == "v1")
                {
                    sigs.Add(v.ToLowerInvariant());
                }
            }
            if (t == null || sigs.Count == 0) return false;
            var now = nowSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - t.Value) > toleranceSeconds) return false;
            var expected = ComputeSignature(secret, t.Value, body);
            var ok = false;
            foreach (var s in sigs) ok |= FixedTimeEquals(s, expected);
            return ok;
        }

        /// <summary>Verifies the signature and parses the event, or throws <see cref="LayerHopWebhookSignatureException"/>.</summary>
        public static WebhookEvent ConstructEvent(string secret, string body, string? header, int toleranceSeconds = DefaultToleranceSeconds, long? nowSeconds = null)
        {
            if (!VerifySignature(secret, body, header, toleranceSeconds, nowSeconds))
                throw new LayerHopWebhookSignatureException("Invalid or expired LayerHop-Signature");
            return Json.Deserialize<WebhookEvent>(body);
        }

        /// <inheritdoc cref="ConstructEvent(string, string, string?, int, long?)"/>
        public static WebhookEvent ConstructEvent(string secret, byte[] body, string? header, int toleranceSeconds = DefaultToleranceSeconds, long? nowSeconds = null)
        {
            if (!VerifySignature(secret, body, header, toleranceSeconds, nowSeconds))
                throw new LayerHopWebhookSignatureException("Invalid or expired LayerHop-Signature");
            return Json.Deserialize<WebhookEvent>(Encoding.UTF8.GetString(body));
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
