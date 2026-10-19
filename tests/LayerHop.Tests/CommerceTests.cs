using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LayerHop.Tests
{
    public class CommerceTests
    {
        private const string Session = "{\"id\":\"cs_1\",\"object\":\"checkout.session\",\"livemode\":false,\"status\":\"open\",\"amount\":\"49000000\",\"settlement\":{\"chainId\":8453,\"token\":\"0xU\",\"symbol\":\"USDC\",\"decimals\":6},\"recipient\":\"0xR\",\"merchantName\":null,\"url\":\"https://layerhop.com/pay/cs_1\",\"expiresAt\":\"2026-10-19T10:00:00Z\",\"payment\":null,\"createdAt\":\"t\",\"updatedAt\":\"t\"}";

        [Fact]
        public async Task CheckoutSessions()
        {
            var (c, h) = MockHandler.Client(Session);
            var s = await c.CreateCheckoutSessionAsync(new CreateCheckoutParams
            {
                Amount = "49000000", SettleChain = "base", SettleToken = "USDC", Recipient = "0xR",
                Metadata = new Dictionary<string, string> { ["order"] = "42" },
            });
            Assert.Equal("cs_1", s.Id);
            Assert.Null(s.MerchantName);
            Assert.Null(s.Payment);
            var body = h.Last.Json;
            Assert.Equal("49000000", (string?)body["amount"]);
            Assert.Equal("42", (string?)body["metadata"]!["order"]);
            Assert.False(body.ContainsKey("description"));
            Assert.False(body.ContainsKey("expiresInMinutes"));

            await c.GetCheckoutSessionAsync("cs_1");
            Assert.Equal("/v1/checkout/sessions/cs_1", h.Last.Path);

            h.Respond(200, "{\"sessions\":[" + Session + "],\"nextCursor\":null}");
            var list = await c.ListCheckoutSessionsAsync(new ListCheckoutSessionsParams { Status = CheckoutStatuses.Open });
            Assert.Single(list.Sessions);
            Assert.Equal("status=open", h.Last.Query);

            h.Respond(200, Session);
            await c.ExpireCheckoutSessionAsync("cs_1");
            Assert.Equal("POST", h.Last.Method);
            Assert.Equal("/v1/checkout/sessions/cs_1/expire", h.Last.Path);
            Assert.Equal("{}", h.Last.Body);

            h.Respond(200, "{\"sessionId\":\"cs_1\",\"livemode\":false,\"amountDue\":\"49000000\",\"best\":{\"quoteId\":\"q1\",\"provider\":\"direct\",\"dstAmount\":\"49000000\",\"dstAmountMin\":\"49000000\",\"estimatedSeconds\":5,\"fees\":{\"ourFeeBps\":0},\"expiresAt\":1,\"srcAmount\":\"49000000\"},\"quotes\":[],\"errors\":[]}");
            var q = await c.QuoteCheckoutSessionAsync("cs_1", new CheckoutQuoteParams { SrcChain = "base", SrcToken = "USDC", Sender = "0xP" });
            Assert.Equal("49000000", q.Best.SrcAmount);
            Assert.False(h.Last.Json.ContainsKey("slippageBps"));
        }

        [Fact]
        public async Task ProductsUpdateSendsOnlySetFields()
        {
            var (c, h) = MockHandler.Client("{\"id\":\"prod_1\",\"object\":\"checkout.product\",\"livemode\":false,\"active\":false,\"name\":\"Pro\",\"amount\":\"1\",\"settlement\":{\"chainId\":8453,\"token\":\"0xU\",\"symbol\":\"USDC\",\"decimals\":6},\"url\":\"https://layerhop.com/buy/prod_1\"}");
            await c.UpdateCheckoutProductAsync("prod_1", new UpdateCheckoutProductParams { Name = "Pro", Description = "", Active = false, AllowedOrigins = new List<string>() });
            Assert.Equal("PATCH", h.Last.Method);
            var body = h.Last.Json;
            Assert.Equal(4, body.Count);
            Assert.Equal("Pro", (string?)body["name"]);
            Assert.Equal(JTokenType.Null, body["description"]!.Type);
            Assert.False((bool)body["active"]!);
            Assert.Empty((JArray)body["allowedOrigins"]!);

            await c.ListCheckoutProductsAsync(new ListCheckoutProductsParams { Active = true, Limit = 5 });
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("true", qs["active"]);
            Assert.Equal("5", qs["limit"]);

            h.Respond(200, Session);
            await c.StartProductCheckoutAsync("prod_1", new StartProductCheckoutParams { Reference = "o1" });
            Assert.Equal("/v1/checkout/products/prod_1/sessions", h.Last.Path);
            Assert.Equal("{\"reference\":\"o1\"}", h.Last.Body);
        }

        [Fact]
        public async Task Invoices()
        {
            const string inv = "{\"id\":\"inv_1\",\"object\":\"invoice\",\"livemode\":false,\"number\":\"LH-1\",\"status\":\"draft\",\"lineItems\":[{\"description\":\"Pro\",\"quantity\":2,\"unitAmount\":\"5000000\"}],\"amount\":\"10000000\",\"settlement\":{\"chainId\":8453,\"token\":\"0xU\",\"symbol\":\"USDC\",\"decimals\":6},\"url\":\"u\",\"createdAt\":\"t\"}";
            var (c, h) = MockHandler.Client(inv);
            var i = await c.CreateInvoiceAsync(new CreateInvoiceParams
            {
                CustomerEmail = "a@b.c", LineItems = { new InvoiceLineItem { Description = "Pro", Quantity = 2, UnitAmount = "5000000" } },
                SettleChain = "base", SettleToken = "USDC", Recipient = "0xR", Send = true,
            });
            Assert.Equal(2, i.LineItems[0].Quantity);
            Assert.True((bool)h.Last.Json["send"]!);

            await c.UpdateInvoiceAsync("inv_1", new UpdateInvoiceParams { Memo = "", LineItems = new List<InvoiceLineItem> { new InvoiceLineItem { Description = "X", UnitAmount = "1" } } });
            var body = h.Last.Json;
            Assert.Equal(2, body.Count);
            Assert.Equal(JTokenType.Null, body["memo"]!.Type);
            Assert.Equal(1, (int)body["lineItems"]![0]!["quantity"]!);

            await c.SendInvoiceAsync("inv_1");
            Assert.Equal("/v1/invoices/inv_1/send", h.Last.Path);
            await c.VoidInvoiceAsync("inv_1");
            Assert.Equal("/v1/invoices/inv_1/void", h.Last.Path);
            await c.GetPublicInvoiceAsync("inv_1");
            Assert.Equal("/v1/invoices/inv_1/public", h.Last.Path);

            h.Respond(200, "{\"sessionId\":\"cs_1\",\"url\":\"https://layerhop.com/pay/cs_1\"}");
            var pay = await c.PayInvoiceAsync("inv_1");
            Assert.Equal("cs_1", pay.SessionId);
        }

        [Fact]
        public async Task Refunds()
        {
            var (c, h) = MockHandler.Client("{\"refund\":{\"id\":\"re_1\",\"object\":\"refund\",\"livemode\":false,\"status\":\"pending\",\"amount\":\"100\",\"to\":{\"chainId\":8453,\"token\":\"0xU\",\"address\":\"0xP\"},\"createdAt\":\"t\"},\"quote\":{\"quoteId\":\"q9\",\"provider\":\"direct\",\"srcAmount\":\"100\",\"dstAmount\":\"100\",\"dstAmountMin\":\"100\",\"estimatedSeconds\":5,\"expiresAt\":1,\"from\":\"0xR\"},\"next\":\"build, sign, report\"}");
            var r = await c.CreateRefundAsync("cs_1", new CreateRefundParams { To = RefundDestinations.Settlement });
            Assert.Equal("q9", r.Quote.QuoteId);
            Assert.Equal("0xR", r.Quote.From);
            Assert.Equal("{\"to\":\"settlement\"}", h.Last.Body);

            h.Respond(200, "{\"refunds\":[]}");
            var list = await c.ListRefundsAsync("cs_1");
            Assert.Empty(list.Refunds);
            Assert.Equal("/v1/checkout/sessions/cs_1/refunds", h.Last.Path);
        }

        [Fact]
        public async Task DepositAddressesDefaultIntegrator()
        {
            const string da = "{\"id\":\"da_1\",\"object\":\"deposit_address\",\"livemode\":false,\"status\":\"active\",\"provider\":\"sandbox\",\"address\":\"0xD\",\"chainId\":8453,\"token\":\"0xU\",\"dstChainId\":7565164,\"dstToken\":\"So1\",\"recipient\":\"R\",\"refundTo\":\"0xS\",\"reusable\":true,\"fees\":{\"ourFeeBps\":20,\"integratorFeeBps\":10,\"integrator\":\"myapp\"},\"depositCount\":0,\"createdAt\":\"t\",\"estimate\":{\"amount\":\"1\",\"dstAmount\":\"2\",\"dstAmountMin\":\"1\",\"estimatedSeconds\":30}}";
            var (c, h) = MockHandler.Client(da, options: new LayerHopOptions { BaseUrl = "https://api.test", ApiKey = "lh_test_x", Integrator = "myapp", IntegratorFeeBps = 10 });
            var d = await c.CreateDepositAddressAsync(new CreateDepositAddressParams { SrcChain = "base", SrcToken = "USDC", DstChain = "solana", DstToken = "SOL", Recipient = "R", RefundTo = "0xS" });
            Assert.Equal("0xD", d.Address);
            Assert.Equal(30, d.Estimate!.EstimatedSeconds);
            Assert.Equal("myapp", (string?)h.Last.Json["integrator"]);
            Assert.Equal(10, (int)h.Last.Json["integratorFeeBps"]!);
            Assert.False(h.Last.Json.ContainsKey("reusable"));

            h.Respond(200, "{\"depositAddresses\":[],\"nextCursor\":null}");
            await c.ListDepositAddressesAsync(new ListDepositAddressesParams { Label = "user 1" });
            Assert.Equal("user 1", MockHandler.ParseQuery(h.Last.Query)["label"]);

            h.Respond(200, da);
            await c.RefreshDepositAddressAsync("da_1");
            Assert.Equal("/v1/deposit-addresses/da_1/refresh", h.Last.Path);
            await c.DisableDepositAddressAsync("da_1");
            Assert.Equal("/v1/deposit-addresses/da_1/disable", h.Last.Path);

            h.Respond(200, "{\"order\":{\"quoteId\":\"dep_1\",\"depositAddressId\":\"da_1\"}}");
            var sim = await c.SimulateDepositAsync("da_1", "1000000");
            Assert.Equal("da_1", sim.Order.DepositAddressId);
            Assert.Equal("1000000", (string?)h.Last.Json["amount"]);
        }

        [Fact]
        public async Task Subscriptions()
        {
            const string sub = "{\"id\":\"sub_1\",\"object\":\"subscription\",\"livemode\":false,\"status\":\"active\",\"customerEmail\":\"a@b.c\",\"description\":\"Pro\",\"amount\":\"9000000\",\"settlement\":{\"chainId\":8453,\"token\":\"0xU\",\"symbol\":\"USDC\",\"decimals\":6},\"recipient\":\"0xR\",\"interval\":\"month\",\"intervalCount\":1,\"daysUntilDue\":3,\"cancelAtPeriodEnd\":false,\"createdAt\":\"t\",\"updatedAt\":\"t\"}";
            var (c, h) = MockHandler.Client(sub);
            await c.CreateSubscriptionAsync(new CreateSubscriptionParams { CustomerEmail = "a@b.c", Interval = SubscriptionIntervals.Month, ProductId = "prod_1" });
            Assert.Equal("{\"customerEmail\":\"a@b.c\",\"interval\":\"month\",\"productId\":\"prod_1\"}", h.Last.Body);

            await c.UpdateSubscriptionAsync("sub_1", new UpdateSubscriptionParams { Metadata = new Dictionary<string, string>(), CustomerName = "", CancelAtPeriodEnd = true });
            var body = h.Last.Json;
            Assert.Equal(3, body.Count);
            Assert.Equal(JTokenType.Null, body["metadata"]!.Type);
            Assert.Equal(JTokenType.Null, body["customerName"]!.Type);

            await c.CancelSubscriptionAsync("sub_1", atPeriodEnd: true);
            Assert.Equal("{\"atPeriodEnd\":true}", h.Last.Body);
            await c.PauseSubscriptionAsync("sub_1");
            Assert.Equal("/v1/subscriptions/sub_1/pause", h.Last.Path);
            await c.ResumeSubscriptionAsync("sub_1");
            Assert.Equal("/v1/subscriptions/sub_1/resume", h.Last.Path);

            h.Respond(200, "{\"subscriptions\":[" + sub + "]}");
            var list = await c.ListSubscriptionsAsync(new ListSubscriptionsParams { CustomerEmail = "a+b@c.d" });
            Assert.Single(list.Subscriptions);
            Assert.Equal("a+b@c.d", MockHandler.ParseQuery(h.Last.Query)["customerEmail"]);

            h.Respond(200, "{\"invoices\":[]}");
            Assert.Empty(await c.ListSubscriptionInvoicesAsync("sub_1"));
        }

        [Fact]
        public async Task LimitOrders()
        {
            const string lo = "{\"id\":\"lo_1\",\"object\":\"limit_order\",\"livemode\":false,\"status\":\"built\",\"provider\":\"sandbox\",\"chainId\":7565164,\"wallet\":\"W\",\"srcToken\":\"So1\",\"dstToken\":\"EPj\",\"amount\":\"1000000000\",\"dstAmount\":\"200000000\",\"fees\":{\"ourFeeBps\":10,\"integratorFeeBps\":0},\"providerOrderId\":\"P\",\"filledAmount\":\"0\",\"filledDstAmount\":\"0\",\"fillCount\":0,\"channel\":\"game\",\"createdAt\":\"t\",\"updatedAt\":\"t\",\"tx\":{\"vm\":\"svm\",\"serializedTx\":\"AQ==\",\"submitViaApi\":true}}";
            var (c, h) = MockHandler.Client(lo, options: new LayerHopOptions { BaseUrl = "https://api.test", Channel = "game" });
            var o = await c.CreateLimitOrderAsync(new CreateLimitOrderParams { Wallet = "W", SrcToken = "SOL", DstToken = "USDC", Amount = "1000000000", DstAmount = "200000000" });
            Assert.True(o.Tx!.AsSvm().SubmitViaApi);
            Assert.Equal("game", (string?)h.Last.Json["channel"]);
            Assert.False(h.Last.Json.ContainsKey("expiresAt"));
            Assert.False(h.Last.Json.ContainsKey("integrator"));

            h.Respond(200, "{\"limitOrder\":" + lo + ",\"txHash\":\"sig\"}");
            var sub = await c.SubmitLimitOrderAsync("lo_1", "signed");
            Assert.Equal("sig", sub.TxHash);
            Assert.Equal("signed", (string?)h.Last.Json["signedTransaction"]);

            h.Respond(200, "{\"limitOrder\":" + lo + ",\"tx\":null}");
            var cancel = await c.CancelLimitOrderAsync("lo_1");
            Assert.Null(cancel.Tx);

            h.Respond(200, lo);
            await c.SimulateLimitOrderAsync("lo_1", LimitOrderSimulations.PartialFill);
            Assert.Equal("partial_fill", (string?)h.Last.Json["outcome"]);
            await c.GetLimitOrderAsync("lo_1");
            Assert.Equal("/v1/limit-orders/lo_1", h.Last.Path);

            h.Respond(200, "{\"limitOrders\":[],\"nextCursor\":null}");
            await c.ListLimitOrdersAsync(new ListLimitOrdersParams { Wallet = "W", Status = LimitOrderStatuses.Open });
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("W", qs["wallet"]);
            Assert.Equal("open", qs["status"]);
        }
    }
}
