using System.Text;
using Xunit;

namespace LayerHop.Tests
{
    public class WebhookTests
    {
        private const string Secret = "whsec_test";
        private const string Body = "{\"id\":\"evt_1\",\"type\":\"order.filled\",\"data\":{}}";
        private const long Now = 1760000000;

        [Fact]
        public void KnownVectors()
        {
            Assert.Equal("t=1760000000,v1=27def4e410e1269461f8318e4922b3fcc86ea21bc8d720c44c0bd5ded8770849", Webhooks.Sign(Secret, Body, Now));
            // Same vector as the Go SDK: openssl dgst -sha256 -hmac secret of '1700000000.{"a":1}'.
            Assert.Equal("t=1700000000,v1=49f24e537407743fa4a0242bb63b94b9a47ee99cbbe071ccd8a22550ae411686", Webhooks.Sign("secret", "{\"a\":1}", 1700000000));
        }

        public static TheoryData<string, string, string, string, long, bool> Cases()
        {
            var hdr = Webhooks.Sign(Secret, Body, Now);
            var sig = hdr.Substring(16);
            return new TheoryData<string, string, string, string, long, bool>
            {
                { "valid", Secret, Body, hdr, Now, true },
                { "valid within tolerance", Secret, Body, hdr, Now + 299, true },
                { "valid with rotated extra v1", Secret, Body, "t=1760000000,v1=00" + hdr.Substring(18) + ",v1=" + sig, Now, true },
                { "spaces tolerated", Secret, Body, "t=1760000000, v1=" + sig, Now, true },
                { "uppercase hex", Secret, Body, "t=1760000000,v1=" + sig.ToUpperInvariant(), Now, true },
                { "too old", Secret, Body, hdr, Now + 301, false },
                { "from the future", Secret, Body, hdr, Now - 301, false },
                { "wrong secret", "other", Body, hdr, Now, false },
                { "tampered body", Secret, "{\"id\":\"evt_2\"}", hdr, Now, false },
                { "missing t", Secret, Body, "v1=" + sig, Now, false },
                { "missing v1", Secret, Body, "t=1760000000", Now, false },
                { "bad t", Secret, Body, "t=abc,v1=" + sig, Now, false },
                { "garbage", Secret, Body, "nonsense", Now, false },
                { "empty", Secret, Body, "", Now, false },
            };
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void Verify(string name, string secret, string body, string header, long now, bool want)
        {
            Assert.True(want == Webhooks.VerifySignature(secret, body, header, nowSeconds: now), name);
            Assert.True(want == Webhooks.VerifySignature(secret, Encoding.UTF8.GetBytes(body), header, nowSeconds: now), name + " (bytes)");
        }

        [Fact]
        public void CustomToleranceAndNullHeader()
        {
            var hdr = Webhooks.Sign(Secret, Body, Now);
            Assert.False(Webhooks.VerifySignature(Secret, Body, hdr, toleranceSeconds: 10, nowSeconds: Now + 11));
            Assert.True(Webhooks.VerifySignature(Secret, Body, hdr, toleranceSeconds: 10, nowSeconds: Now + 10));
            Assert.False(Webhooks.VerifySignature(Secret, Body, null, nowSeconds: Now));
        }

        [Fact]
        public void ConstructEvent()
        {
            const string body = "{\"id\":\"evt_9\",\"type\":\"checkout.session.completed\",\"livemode\":true,\"createdAt\":\"t\",\"data\":{\"session\":{\"id\":\"cs_1\",\"status\":\"paid\",\"amount\":\"1\",\"settlement\":{\"chainId\":8453,\"token\":\"0xU\",\"symbol\":\"USDC\",\"decimals\":6},\"payment\":{\"quoteId\":\"q1\",\"dstTxHash\":\"0xd\"}}}}";
            var hdr = Webhooks.Sign(Secret, body, Now);
            var evt = Webhooks.ConstructEvent(Secret, body, hdr, nowSeconds: Now);
            Assert.Equal(WebhookEventTypes.CheckoutSessionCompleted, evt.Type);
            Assert.Equal("0xd", evt.Data.Session!.Payment!.DstTxHash);
            Assert.Throws<LayerHopWebhookSignatureException>(() => Webhooks.ConstructEvent(Secret, body + " ", hdr, nowSeconds: Now));
        }
    }
}
