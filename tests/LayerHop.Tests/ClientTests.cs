using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LayerHop.Tests
{
    public class ClientTests
    {
        [Fact]
        public void Defaults()
        {
            using var c = new LayerHopClient();
            Assert.Equal(LayerHopClient.DefaultBaseUrl, c.BaseUrl);
            Assert.Equal("api", c.Channel);
        }

        [Fact]
        public async Task HealthSendsHeaders()
        {
            var (c, h) = MockHandler.Client("{\"ok\":true,\"providers\":[\"relay\",\"mock\"]}");
            var health = await c.HealthAsync();
            Assert.True(health.Ok);
            Assert.Equal(2, health.Providers.Count);
            Assert.Equal("GET", h.Last.Method);
            Assert.Equal("/v1/health", h.Last.Path);
            Assert.Equal("k1", h.Last.Headers["x-api-key"]);
            Assert.Equal("layerhop-csharp/0.6.0", h.Last.Headers["user-agent"]);
            Assert.Equal("application/json", h.Last.Headers["accept"]);
        }

        [Fact]
        public async Task NoApiKeyHeaderWithoutKey()
        {
            var h = new MockHandler().Respond(200, "{\"ok\":true,\"providers\":[]}");
            var c = new LayerHopClient(new LayerHopOptions { BaseUrl = "https://api.test" }, new HttpClient(h));
            await c.HealthAsync();
            Assert.False(h.Last.Headers.ContainsKey("x-api-key"));
        }

        [Fact]
        public async Task ChainsTokensPrices()
        {
            var (c, h) = MockHandler.Client("{\"chains\":[{\"id\":7565164,\"slug\":\"solana\",\"name\":\"Solana\",\"vm\":\"svm\",\"nativeSymbol\":\"SOL\",\"nativeDecimals\":9,\"explorerTx\":\"https://solscan.io/tx/\"}]}");
            var chains = await c.GetChainsAsync();
            Assert.Equal(ChainIds.Solana, chains.Single().Id);
            Assert.Equal("/v1/chains", h.Last.Path);

            h.Respond(200, "{\"tokens\":[{\"chainId\":8453,\"address\":\"0xA\",\"symbol\":\"USDC\",\"decimals\":6}]}");
            var tokens = await c.GetTokensAsync("base");
            Assert.Equal("USDC", tokens.Single().Symbol);
            Assert.Equal("chain=base", h.Last.Query);

            h.Respond(200, "{\"prices\":{\"8453:0xA\":0.9998,\"solana:So1\":103.88}}");
            var prices = await c.GetPricesAsync(new[] { "8453:0xA", "solana:So1" });
            Assert.Equal(0.9998, prices["8453:0xA"]);
            Assert.Equal(103.88, prices["solana:So1"]);
            Assert.Equal("8453:0xA,solana:So1", MockHandler.ParseQuery(h.Last.Query)["ids"]);
        }

        private const string QuoteBody = "{\"best\":{\"quoteId\":\"q1\",\"provider\":\"relay\",\"dstAmount\":\"990\",\"dstAmountMin\":\"985\",\"estimatedSeconds\":20,\"fees\":{\"ourFeeBps\":15,\"gasUsd\":0.02},\"expiresAt\":1760000000,\"gasOnArrival\":{\"amount\":\"500000000000000\",\"token\":\"0x0000000000000000000000000000000000000000\",\"symbol\":\"ETH\",\"decimals\":18,\"usd\":2}},\"quotes\":[],\"errors\":[{\"provider\":\"debridge\",\"code\":\"NO_ROUTE\",\"message\":\"no\"}],\"livemode\":false}";

        [Fact]
        public async Task QuoteQueryAndDefaults()
        {
            var (c, h) = MockHandler.Client(QuoteBody, options: new LayerHopOptions { BaseUrl = "https://api.test", Integrator = "myapp", IntegratorFeeBps = 25 });
            var q = await c.QuoteAsync(new QuoteParams
            {
                SrcChain = "base", SrcToken = "USDC", Amount = "1000000", DstChain = "arbitrum", DstToken = "USDC",
                Sender = "0xS", Recipient = "0xR", GasOnArrival = true,
            });
            Assert.Equal("q1", q.Best.QuoteId);
            Assert.Equal("500000000000000", q.Best.GasOnArrival!.Amount);
            Assert.Equal(2.0, q.Best.GasOnArrival.Usd);
            Assert.False(q.Livemode);
            Assert.Equal("NO_ROUTE", q.Errors.Single().Code);
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("50", qs["slippageBps"]);
            Assert.Equal("api", qs["channel"]);
            Assert.Equal("myapp", qs["integrator"]);
            Assert.Equal("25", qs["integratorFeeBps"]);
            Assert.Equal("true", qs["gasOnArrival"]);
            Assert.Equal("1000000", qs["amount"]);
            Assert.False(qs.ContainsKey("gasOnArrivalUsd"));
        }

        [Fact]
        public async Task QuoteGasOnArrivalUsdWins()
        {
            var (c, h) = MockHandler.Client(QuoteBody);
            await c.QuoteAsync(new QuoteParams { SrcChain = "base", SrcToken = "USDC", Amount = "1", DstChain = "arbitrum", DstToken = "ETH", Sender = "a", Recipient = "b", GasOnArrival = true, GasOnArrivalUsd = 5.5, SlippageBps = 100 });
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("5.5", qs["gasOnArrivalUsd"]);
            Assert.Equal("100", qs["slippageBps"]);
            Assert.False(qs.ContainsKey("gasOnArrival"));
            Assert.False(qs.ContainsKey("integrator"));
        }

        [Fact]
        public async Task BuildTxEvm()
        {
            var (c, h) = MockHandler.Client("{\"tx\":{\"vm\":\"evm\",\"chainId\":8453,\"to\":\"0xT\",\"data\":\"0xabc\",\"value\":\"0\",\"approval\":{\"token\":\"0xU\",\"spender\":\"0xT\",\"amount\":\"1000000\"},\"extraTxs\":[{\"to\":\"0xF\",\"data\":\"0x01\",\"value\":\"0\"}]}}");
            var tx = await c.BuildTxAsync("q1");
            Assert.Equal("POST", h.Last.Method);
            Assert.Equal("/v1/orders", h.Last.Path);
            Assert.Equal("q1", (string?)h.Last.Json["quoteId"]);
            Assert.StartsWith("application/json", h.Last.Headers["content-type"]);
            Assert.True(tx.IsEvm);
            var evm = tx.AsEvm();
            Assert.Equal(8453, evm.ChainId);
            Assert.Equal("0xU", evm.Approval!.Token);
            Assert.Equal("0xF", evm.ExtraTxs.Single().To);
            Assert.Throws<InvalidOperationException>(() => tx.AsSvm());
        }

        [Fact]
        public async Task BuildTxSvmAndSubmit()
        {
            var (c, h) = MockHandler.Client("{\"tx\":{\"vm\":\"svm\",\"serializedTx\":\"AQID\",\"submitViaApi\":true,\"sandbox\":true}}");
            var tx = await c.BuildTxAsync("q2");
            var svm = tx.AsSvm();
            Assert.Equal("AQID", svm.SerializedTx);
            Assert.True(svm.SubmitViaApi);
            Assert.True(svm.Sandbox);

            h.Respond(200, "{\"txHash\":\"5sig\"}");
            Assert.Equal("5sig", await c.SubmitAsync("q2", "c2lnbmVk"));
            Assert.Equal("/v1/orders/submit", h.Last.Path);
            Assert.Equal("c2lnbmVk", (string?)h.Last.Json["signedTransaction"]);
        }

        [Fact]
        public async Task ReportSubmittedEscapesPath()
        {
            var (c, h) = MockHandler.Client("{\"quoteId\":\"q/1\",\"status\":\"submitted\"}");
            var r = await c.ReportSubmittedAsync("q/1", "0xhash");
            Assert.Equal("submitted", r.Status);
            Assert.Equal("/v1/orders/q%2F1/submitted", h.Last.PathAndQuery);
            Assert.Equal("0xhash", (string?)h.Last.Json["txHash"]);
        }

        [Fact]
        public async Task SimulateAcceptsNumberOrString()
        {
            var (c, _) = MockHandler.Client("{\"ok\":true,\"vm\":\"evm\",\"gasEstimate\":123456789012345678901,\"computeUnits\":\"42\"}");
            var s = await c.SimulateAsync("q1");
            Assert.Equal("123456789012345678901", s.GasEstimate);
            Assert.Equal("42", s.ComputeUnits);
        }

        [Fact]
        public async Task StatusAndListOrders()
        {
            var (c, h) = MockHandler.Client("{\"provider\":\"relay\",\"id\":\"0xabc\",\"state\":\"filled\",\"dstTxHash\":\"0xd\"}");
            var st = await c.GetOrderStatusAsync("relay", "0xabc");
            Assert.True(st.IsFinal);
            Assert.Equal("/v1/orders/relay/0xabc", h.Last.Path);

            h.Respond(200, "{\"orders\":[{\"quoteId\":\"q1\",\"status\":\"filled\",\"srcChainId\":8453,\"livemode\":false,\"limitOrderId\":\"lo_1\"}],\"nextCursor\":\"c2\"}");
            var list = await c.ListOrdersAsync(new ListOrdersParams { Address = "0xS", Limit = 10 });
            Assert.Equal("c2", list.NextCursor);
            Assert.Equal("lo_1", list.Orders.Single().LimitOrderId);
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("0xS", qs["address"]);
            Assert.Equal("10", qs["limit"]);
            Assert.False(qs.ContainsKey("cursor"));
        }

        [Fact]
        public async Task WatchYieldsChangesUntilFinal()
        {
            var h = new MockHandler()
                .Respond(200, "{\"provider\":\"relay\",\"id\":\"x\",\"state\":\"pending\"}")
                .Respond(200, "{\"provider\":\"relay\",\"id\":\"x\",\"state\":\"pending\"}")
                .Respond(200, "{\"provider\":\"relay\",\"id\":\"x\",\"state\":\"filled\"}");
            var c = new LayerHopClient(new LayerHopOptions { BaseUrl = "https://api.test" }, new HttpClient(h));
            var seen = new List<string>();
            var final = await c.WatchOrderAsync("relay", "x", TimeSpan.FromMilliseconds(1), null, s => seen.Add(s.State));
            Assert.Equal(OrderStates.Filled, final.State);
            Assert.Equal(new[] { "pending", "filled" }, seen);
            Assert.Equal(3, h.Requests.Count);
        }

        [Fact]
        public async Task WatchTimesOut()
        {
            var (c, _) = MockHandler.Client("{\"provider\":\"relay\",\"id\":\"x\",\"state\":\"pending\"}");
            var e = await Assert.ThrowsAsync<LayerHopWatchTimeoutException>(() =>
                c.WatchOrderAsync("relay", "x", TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(20)));
            Assert.Equal("pending", e.LastStatus!.State);
        }

        [Fact]
        public async Task WatchHonorsCancellation()
        {
            var (c, _) = MockHandler.Client("{\"provider\":\"relay\",\"id\":\"x\",\"state\":\"pending\"}");
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in c.WatchOrder("relay", "x", TimeSpan.FromSeconds(10), null, cts.Token)) { }
            });
        }

        [Fact]
        public async Task ApiErrorFromBody()
        {
            var (c, _) = MockHandler.Client("{\"error\":{\"code\":\"QUOTE_EXPIRED\",\"message\":\"Quote expired\",\"requestId\":\"req-body\"}}", 410);
            var e = await Assert.ThrowsAsync<LayerHopApiException>(() => c.BuildTxAsync("q1"));
            Assert.Equal("QUOTE_EXPIRED", e.Code);
            Assert.Equal(410, e.Status);
            Assert.Equal("req-body", e.RequestId);
            Assert.Equal("Quote expired", e.ApiMessage);
            Assert.Equal("https://docs.layerhop.com/errors/QUOTE_EXPIRED", e.DocsUrl);
            Assert.Contains("[requestId req-body]", e.Message);
        }

        [Fact]
        public async Task ApiErrorFallback()
        {
            var (c, _) = MockHandler.Client("<html>bad gateway</html>", 502);
            var e = await Assert.ThrowsAsync<LayerHopApiException>(() => c.HealthAsync());
            Assert.Equal("INTERNAL", e.Code);
            Assert.Equal(502, e.Status);
            Assert.Equal("req-hdr", e.RequestId);
        }

        [Fact]
        public async Task ServiceStatusAndTrigger()
        {
            var (c, h) = MockHandler.Client("{\"status\":\"operational\",\"updatedAt\":\"t\",\"components\":[{\"id\":\"api\",\"name\":\"API\",\"group\":\"core\",\"status\":\"operational\",\"latencyMs\":null,\"uptime90d\":99.9,\"days\":[{\"date\":\"2026-10-01\",\"uptime\":null}]}],\"incidents\":[],\"url\":\"https://status.layerhop.com\"}");
            var s = await c.GetServiceStatusAsync();
            Assert.Null(s.Components[0].LatencyMs);
            Assert.Null(s.Components[0].Days[0].Uptime);

            h.Respond(200, "{\"eventId\":\"evt_1\",\"deliveries\":2,\"payload\":{\"id\":\"evt_1\",\"type\":\"order.filled\",\"livemode\":false,\"sample\":true,\"createdAt\":\"t\",\"data\":{\"order\":{\"quoteId\":\"q1\"}}}}");
            var t = await c.TriggerWebhookEventAsync(WebhookEventTypes.OrderFilled);
            Assert.Equal(2, t.Deliveries);
            Assert.Equal("q1", t.Payload.Data.Order!.QuoteId);
            Assert.Equal("order.filled", (string?)h.Last.Json["type"]);
        }
    }
}
