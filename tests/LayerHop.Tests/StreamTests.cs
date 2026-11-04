using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LayerHop.Tests
{
    /// <summary>Exact output, live quote stream, screening, usage, webhook resend / stream, x402, new checkout fields.</summary>
    public class StreamTests
    {
        private const string Q = "{\"quoteId\":\"q1\",\"provider\":\"relay\",\"dstAmount\":\"1000000\",\"dstAmountMin\":\"1000000\",\"estimatedSeconds\":20,\"fees\":{\"ourFeeBps\":15},\"expiresAt\":1760000000,\"srcAmount\":\"1001000\",\"srcAmountMax\":\"1006000\"}";
        private const string Screening = "{\"source\":\"ofac_sdn\",\"listUpdatedAt\":\"2026-10-28T00:00:00.000Z\",\"sanctioned\":[{\"address\":\"0xBad\",\"role\":\"recipient\",\"lists\":[\"OFAC SDN\"]}]}";

        private static ExactOutputQuoteParams Exact() => new ExactOutputQuoteParams
        {
            SrcChain = "base", SrcToken = "USDC", DstAmount = "1000000", DstChain = "arbitrum", DstToken = "USDC",
            Sender = "0xS", Recipient = "0xR", ExcludeBridges = new List<string> { "stargate", "across" },
        };

        [Fact]
        public async Task ExactOutputQuote()
        {
            var (c, h) = MockHandler.Client("{\"best\":" + Q + ",\"quotes\":[" + Q + "],\"errors\":[],\"screening\":" + Screening + "}");
            var r = await c.QuoteExactOutputAsync(Exact());
            Assert.Equal("1001000", r.Best.SrcAmount);
            Assert.Equal("1006000", r.Best.SrcAmountMax);
            Assert.Equal("recipient", r.Screening!.Sanctioned.Single().Role);
            Assert.Equal("GET", h.Last.Method);
            Assert.Equal("/v1/quote", h.Last.Path);
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("1000000", qs["dstAmount"]);
            Assert.False(qs.ContainsKey("amount"));
            Assert.False(qs.ContainsKey("gasOnArrival"));
            Assert.Equal("stargate,across", qs["excludeBridges"]);
            Assert.False(qs.ContainsKey("excludeProviders"));
        }

        [Fact]
        public async Task QuoteSendsExclusions()
        {
            var (c, h) = MockHandler.Client("{\"best\":" + Q + ",\"quotes\":[],\"errors\":[]}");
            var r = await c.QuoteAsync(new QuoteParams
            {
                SrcChain = "base", SrcToken = "USDC", Amount = "5", DstChain = "solana", DstToken = "SOL", Sender = "a", Recipient = "b",
                ExcludeProviders = new List<string> { "relay" }, ExcludeExchanges = new List<string>(),
            });
            Assert.Null(r.Screening);
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("relay", qs["excludeProviders"]);
            Assert.False(qs.ContainsKey("excludeExchanges"));
            Assert.Equal("5", qs["amount"]);
        }

        [Fact]
        public async Task QuoteStreamYieldsQuotesUntilEnd()
        {
            var sse = ": ping\n\nevent: ready\ndata: {\"intervalMs\":5000,\"expiresAt\":\"t\",\"retryMs\":3000}\n\n"
                + "event: quote\ndata: {\"seq\":1,\"best\":" + Q + ",\"quotes\":[],\"errors\":[]}\n\n"
                + "event: quote\r\ndata: {\"seq\":2,\r\ndata: \"best\":" + Q + ",\"quotes\":[],\"errors\":[]}\r\n\r\n"
                + "event: end\ndata: {\"reason\":\"expired\",\"quotesSent\":2}\n\n"
                + "event: quote\ndata: {\"seq\":3,\"best\":" + Q + ",\"quotes\":[],\"errors\":[]}\n\n";
            var (c, h) = MockHandler.Client(sse);
            var seqs = new List<int>();
            await foreach (var u in c.StreamQuotes(Exact(), new QuoteStreamOptions { IntervalMs = 5000 }))
            {
                seqs.Add(u.Seq);
                Assert.Equal("1006000", u.Best.SrcAmountMax);
            }
            Assert.Equal(new List<int> { 1, 2 }, seqs);
            Assert.Equal("/v1/quote/stream", h.Last.Path);
            Assert.Equal("text/event-stream", h.Last.Headers["accept"]);
            Assert.Equal("k1", h.Last.Headers["x-api-key"]);
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("5000", qs["intervalMs"]);
            Assert.Equal("1000000", qs["dstAmount"]);
            Assert.Equal("api", qs["channel"]);
        }

        [Fact]
        public async Task QuoteStreamErrorEventThrowsWithCode()
        {
            var sse = "event: quote\ndata: {\"seq\":1,\"best\":" + Q + ",\"quotes\":[],\"errors\":[]}\n\n"
                + "event: error\ndata: {\"status\":451,\"error\":{\"code\":\"ADDRESS_SANCTIONED\",\"message\":\"listed\",\"requestId\":\"r9\",\"details\":{\"address\":\"0xBad\"}}}\n\n";
            var (c, _) = MockHandler.Client(sse);
            var got = 0;
            var e = await Assert.ThrowsAsync<LayerHopApiException>(async () =>
            {
                await foreach (var u in c.StreamQuotes(Exact())) got += u.Seq;
            });
            Assert.Equal(1, got);
            Assert.Equal(ErrorCodes.AddressSanctioned, e.Code);
            Assert.Equal(451, e.Status);
            Assert.Equal("r9", e.RequestId);
            Assert.Equal("0xBad", (string?)e.Details!["address"]);
        }

        [Fact]
        public async Task QuoteStreamHttpErrorThrows()
        {
            var (c, _) = MockHandler.Client("{\"error\":{\"code\":\"NO_ROUTE\",\"message\":\"none\"}}", 404);
            var e = await Assert.ThrowsAsync<LayerHopApiException>(async () =>
            {
                await foreach (var u in c.StreamQuotes(Exact())) Assert.NotNull(u);
            });
            Assert.Equal(ErrorCodes.NoRoute, e.Code);
            Assert.Equal(404, e.Status);
        }

        [Fact]
        public async Task ScreenAndBuildOrder()
        {
            var (c, h) = MockHandler.Client("{\"results\":[{\"address\":\"0xBad\",\"sanctioned\":true,\"lists\":[\"OFAC SDN\"],\"source\":\"ofac_sdn\",\"listUpdatedAt\":\"2026-10-28T00:00:00.000Z\"},{\"address\":\"0x1\",\"sanctioned\":false,\"lists\":[],\"source\":\"ofac_sdn\",\"listUpdatedAt\":null}],\"list\":null,\"enforced\":false}");
            var r = await c.ScreenAsync(new[] { "0xBad", "0x1" });
            Assert.True(r.Results[0].Sanctioned);
            Assert.Empty(r.Results[1].Lists);
            Assert.Null(r.Results[1].ListUpdatedAt);
            Assert.Null(r.List);
            Assert.False(r.Enforced);
            Assert.Equal("POST", h.Last.Method);
            Assert.Equal("/v1/screen", h.Last.Path);
            Assert.Equal(new[] { "0xBad", "0x1" }, ((JArray)h.Last.Json["addresses"]!).Select(t => (string)t!).ToArray());

            h.Respond(200, "{\"tx\":{\"vm\":\"evm\",\"chainId\":8453,\"to\":\"0xT\",\"data\":\"0x\",\"value\":\"0\"},\"screening\":" + Screening + "}");
            var o = await c.BuildOrderAsync("q1");
            Assert.NotNull(o.Tx);
            Assert.Null(o.Gasless);
            Assert.Equal("ofac_sdn", o.Screening!.Source);
            Assert.Equal("/v1/orders", h.Last.Path);
        }

        [Fact]
        public async Task UsageAndErrors()
        {
            var (c, h) = MockHandler.Client("{\"bucket\":\"day\",\"from\":\"a\",\"to\":\"b\",\"livemode\":true,\"keyId\":\"key_1\",\"totals\":{\"requests\":1480,\"errors\":21,\"byStatusClass\":{\"2xx\":1459,\"3xx\":0,\"4xx\":19,\"5xx\":2},\"avgMs\":318,\"p50Ms\":212,\"p95Ms\":null},\"series\":[{\"ts\":\"t\",\"requests\":61,\"errors\":1,\"byStatusClass\":{\"2xx\":60,\"3xx\":0,\"4xx\":1,\"5xx\":0},\"p50Ms\":198,\"p95Ms\":1105}],\"byEndpoint\":[{\"group\":\"quote\",\"requests\":1210,\"errors\":18,\"byStatusClass\":{\"2xx\":1192,\"3xx\":0,\"4xx\":17,\"5xx\":1},\"avgMs\":352,\"p50Ms\":240,\"p95Ms\":1320}],\"errorCodes\":[{\"code\":\"NO_ROUTE\",\"count\":12,\"byEndpoint\":{\"quote\":12}}]}");
            var u = await c.GetUsageAsync(new UsageParams { Bucket = "day", KeyId = "key_1" });
            Assert.Equal(1459, u.Totals.ByStatusClass.Status2xx);
            Assert.Equal(2, u.Totals.ByStatusClass.Status5xx);
            Assert.Null(u.Totals.P95Ms);
            Assert.Equal(212d, u.Totals.P50Ms);
            Assert.Equal(12, u.ErrorCodes.Single().ByEndpoint["quote"]);
            Assert.Equal("quote", u.ByEndpoint.Single().Group);
            Assert.Equal("/v1/usage", h.Last.Path);
            Assert.Equal("bucket=day&keyId=key_1", h.Last.Query);

            await c.GetUsageAsync();
            Assert.Equal("", h.Last.Query);

            h.Respond(200, "{\"livemode\":true,\"errors\":[{\"ts\":\"t\",\"keyId\":null,\"requestId\":\"r1\",\"method\":\"GET\",\"path\":\"/v1/quote\",\"endpointGroup\":\"quote\",\"status\":404,\"code\":\"NO_ROUTE\",\"latencyMs\":812}]}");
            var errs = await c.ListUsageErrorsAsync(new UsageErrorsParams { Code = "NO_ROUTE", Limit = 10 });
            Assert.Equal("r1", errs.Single().RequestId);
            Assert.Equal(404, errs.Single().Status);
            Assert.Null(errs.Single().KeyId);
            Assert.Equal("/v1/usage/errors", h.Last.Path);
            Assert.Equal("code=NO_ROUTE&limit=10", h.Last.Query);
        }

        [Fact]
        public async Task WebhookResendAndStream()
        {
            var (c, h) = MockHandler.Client("{\"eventId\":\"evt_1\",\"type\":\"order.filled\",\"livemode\":false,\"deliveries\":[{\"id\":\"whd_1\",\"endpointId\":\"whe_1\"}]}");
            var r = await c.ResendWebhookEventAsync("evt_1", "whe_1");
            Assert.Equal("whe_1", r.Deliveries.Single().EndpointId);
            Assert.Equal("POST", h.Last.Method);
            Assert.Equal("/v1/webhooks/events/evt_1/resend", h.Last.Path);
            Assert.Equal("{\"endpointId\":\"whe_1\"}", h.Last.Body);

            await c.ResendWebhookEventAsync("evt_1");
            Assert.Equal("{}", h.Last.Body);

            h.Respond(200, ": ping\n\nevent: event\ndata: {\"id\":\"evt_1\",\"type\":\"order.filled\",\"livemode\":false,\"createdAt\":\"t\",\"data\":{}}\n\nevent: other\ndata: {}\n\n");
            var events = new List<WebhookEvent>();
            await foreach (var e in c.StreamWebhookEvents(since: "2026-11-04T10:00:00Z")) events.Add(e);
            Assert.Equal("order.filled", events.Single().Type);
            Assert.Equal("/v1/webhooks/stream", h.Last.Path);
            Assert.Equal("2026-11-04T10:00:00Z", MockHandler.ParseQuery(h.Last.Query)["since"]);
        }

        [Fact]
        public async Task X402()
        {
            var (c, h) = MockHandler.Client("{\"kinds\":[{\"x402Version\":2,\"scheme\":\"exact\",\"network\":\"eip155:8453\"},{\"x402Version\":1,\"scheme\":\"exact\",\"network\":\"base\"}],\"extensions\":[],\"signers\":{},\"settle\":false}");
            var s = await c.GetX402SupportedAsync();
            Assert.Equal(2, s.Kinds.Count);
            Assert.Equal("eip155:8453", s.Kinds[0].Network);
            Assert.False(s.Settle);
            Assert.Equal("/v1/x402/supported", h.Last.Path);

            h.Respond(200, "{\"payee\":{\"id\":\"x4p_1\",\"network\":\"base\",\"chainId\":8453,\"asset\":\"0xU\",\"depositAddress\":\"0xD\",\"merchant\":\"0x2\",\"resource\":\"https://api.example.com/w\",\"feeBps\":25,\"factory\":\"0xF\",\"lastSweepTx\":null,\"lastSweptAt\":null},\"price\":\"10000\",\"amount\":\"10026\",\"fee\":{\"bps\":25,\"amount\":\"26\"},\"accepts\":[{\"scheme\":\"exact\",\"network\":\"eip155:8453\",\"amount\":\"10026\",\"asset\":\"0xU\",\"payTo\":\"0xD\",\"maxTimeoutSeconds\":60,\"extra\":{\"name\":\"USD Coin\",\"version\":\"2\"}}],\"acceptsV1\":[{\"network\":\"base\",\"maxAmountRequired\":\"10026\"}],\"paymentRequired\":{\"x402Version\":2}}");
            var req = await c.CreateX402RequirementsAsync(new X402RequirementsParams { Amount = "10000", PayTo = "0x2", Resource = "https://api.example.com/w" });
            Assert.Equal("0xD", req.Accepts.Single().PayTo);
            Assert.Equal("USD Coin", (string?)req.Accepts.Single().Extra!["name"]);
            Assert.Equal("10026", (string?)req.AcceptsV1.Single()["maxAmountRequired"]);
            Assert.Equal(2, (int)req.PaymentRequired["x402Version"]!);
            Assert.Equal("26", req.Fee.Amount);
            Assert.Equal("/v1/x402/requirements", h.Last.Path);
            var body = h.Last.Json;
            Assert.Equal("10000", (string?)body["amount"]);
            Assert.False(body.ContainsKey("network"));
            Assert.False(body.ContainsKey("maxTimeoutSeconds"));

            var fp = new X402FacilitatorParams
            {
                X402Version = 2,
                PaymentPayload = new JObject { ["payload"] = new JObject { ["signature"] = "0xsig" } },
                PaymentRequirements = JObject.Parse("{\"scheme\":\"exact\",\"network\":\"eip155:8453\"}"),
            };
            h.Respond(200, "{\"isValid\":false,\"invalidReason\":\"insufficient_funds\",\"payer\":\"0xP\"}");
            var v = await c.VerifyX402PaymentAsync(fp);
            Assert.False(v.IsValid);
            Assert.Equal("insufficient_funds", v.InvalidReason);
            Assert.Equal("/v1/x402/verify", h.Last.Path);
            Assert.Equal("0xsig", (string?)h.Last.Json["paymentPayload"]!["payload"]!["signature"]);
            Assert.Equal(2, (int)h.Last.Json["x402Version"]!);

            h.Respond(200, "{\"success\":false,\"errorReason\":\"settlement_pending\",\"payer\":\"0xP\",\"transaction\":\"0xT\",\"network\":\"eip155:8453\"}");
            var st = await c.SettleX402PaymentAsync(fp);
            Assert.Equal("settlement_pending", st.ErrorReason);
            Assert.Equal("0xT", st.Transaction);
            Assert.Equal("/v1/x402/settle", h.Last.Path);

            const string pay = "{\"id\":\"x402_1\",\"object\":\"x402.payment\",\"status\":\"settled\",\"statusDetail\":null,\"network\":\"eip155:8453\",\"chainId\":8453,\"asset\":\"0xU\",\"payer\":\"0xP\",\"payTo\":\"0xD\",\"amount\":\"10026\",\"merchantAmount\":\"10000\",\"feeBps\":25,\"resource\":null,\"transaction\":\"0xT\",\"sweepTx\":null,\"settledAt\":\"t\",\"createdAt\":\"t\",\"updatedAt\":\"t\"}";
            h.Respond(200, "{\"payments\":[" + pay + "],\"nextCursor\":\"c2\"}");
            var list = await c.ListX402PaymentsAsync(new ListX402PaymentsParams { Status = X402PaymentStatuses.Settled, Limit = 5 });
            Assert.Equal("10000", list.Payments.Single().MerchantAmount);
            Assert.Equal("c2", list.NextCursor);
            Assert.Equal("status=settled&limit=5", h.Last.Query);

            h.Respond(200, pay);
            var p = await c.GetX402PaymentAsync("x402_1");
            Assert.Equal(X402PaymentStatuses.Settled, p.Status);
            Assert.Equal("/v1/x402/payments/x402_1", h.Last.Path);
        }

        [Fact]
        public async Task CheckoutTransferBitcoinAndEmbedOrigins()
        {
            const string session = "{\"id\":\"cs_1\",\"object\":\"checkout.session\",\"livemode\":true,\"status\":\"open\",\"amount\":\"49000000\",\"settlement\":{\"chainId\":8453,\"token\":\"0xU\",\"symbol\":\"USDC\",\"decimals\":6},\"recipient\":\"0xR\",\"url\":\"u\",\"expiresAt\":\"t\","
                + "\"payment\":{\"method\":\"transfer\",\"quoteId\":null,\"payer\":null,\"srcChainId\":8453,\"srcToken\":\"0xU\",\"srcAmount\":\"49122500\",\"srcTxHash\":\"0xH\",\"dstTxHash\":null,\"paidAt\":null},"
                + "\"transferAvailable\":true,\"transfer\":{\"kind\":\"evm_splitter\",\"chainId\":8453,\"token\":\"0xU\",\"symbol\":\"USDC\",\"decimals\":6,\"address\":\"0xD\",\"amount\":\"49122500\",\"amountDue\":\"49000000\",\"fee\":{\"bps\":25,\"amount\":\"122500\"},\"uri\":\"ethereum:0xU@8453/transfer\",\"transferUri\":null,\"reference\":null,\"received\":\"0\",\"seen\":\"0\",\"state\":\"waiting\",\"txs\":[{\"hash\":\"0xH\",\"amount\":\"1\",\"at\":null}],\"startedAt\":\"t\",\"sweepTxHash\":null},"
                + "\"embedOrigins\":[\"https://shop.example\"],\"bitcoinAvailable\":true,"
                + "\"bitcoin\":{\"address\":\"bc1qd\",\"amountSats\":\"71234\",\"amountBtc\":\"0.00071234\",\"uri\":\"bitcoin:bc1qd?amount=0.00071234\",\"amountOut\":\"49000000\",\"expiresAt\":\"t\",\"expired\":false,\"status\":\"waiting\",\"seenSats\":\"0\",\"requestId\":\"0xreq\",\"feeBps\":25,\"refundAddress\":\"bc1qr\",\"quotes\":[{\"address\":\"bc1qd\",\"amountSats\":\"71234\",\"status\":\"waiting\",\"requestId\":\"0xreq\",\"createdAt\":\"t\"}]},"
                + "\"createdAt\":\"t\",\"updatedAt\":\"t\"}";
            var (c, h) = MockHandler.Client(session);
            var s = await c.CreateCheckoutSessionAsync(new CreateCheckoutParams
            {
                Amount = "49000000", SettleChain = "base", SettleToken = "USDC", Recipient = "0xR",
                EmbedOrigins = new List<string> { "https://shop.example" },
            });
            Assert.Equal("https://shop.example", (string?)((JArray)h.Last.Json["embedOrigins"]!)[0]);
            Assert.Equal(CheckoutPaymentMethods.Transfer, s.Payment!.Method);
            Assert.Null(s.Payment.QuoteId);
            Assert.True(s.TransferAvailable);
            Assert.Equal("evm_splitter", s.Transfer!.Kind);
            Assert.Equal("122500", s.Transfer.Fee!.Amount);
            Assert.Null(s.Transfer.Txs.Single().At);
            Assert.Equal("https://shop.example", s.EmbedOrigins!.Single());
            Assert.True(s.BitcoinAvailable);
            Assert.Equal("71234", s.Bitcoin!.AmountSats);
            Assert.Equal("bc1qr", s.Bitcoin.RefundAddress);
            Assert.Single(s.Bitcoin.Quotes);

            await c.StartCheckoutTransferAsync("cs_1");
            Assert.Equal("POST", h.Last.Method);
            Assert.Equal("/v1/checkout/sessions/cs_1/transfer", h.Last.Path);
            Assert.Equal("{}", h.Last.Body);

            await c.StartCheckoutBitcoinAsync("cs_1", "bc1qr");
            Assert.Equal("/v1/checkout/sessions/cs_1/bitcoin", h.Last.Path);
            Assert.Equal("{\"refundAddress\":\"bc1qr\"}", h.Last.Body);

            await c.StartCheckoutBitcoinAsync("cs_1");
            Assert.Equal("{}", h.Last.Body);

            h.Respond(200, "{\"sessionId\":\"cs_1\",\"livemode\":true,\"amountDue\":\"49000000\",\"best\":" + Q + ",\"quotes\":[],\"errors\":[],\"screening\":" + Screening + "}");
            var cq = await c.QuoteCheckoutSessionAsync("cs_1", new CheckoutQuoteParams { SrcChain = "base", SrcToken = "USDC", Sender = "0xP" });
            Assert.Equal("0xBad", cq.Screening!.Sanctioned.Single().Address);
        }

        [Fact]
        public void AddressSanctionedCode()
        {
            Assert.Equal("ADDRESS_SANCTIONED", ErrorCodes.AddressSanctioned);
        }
    }
}
