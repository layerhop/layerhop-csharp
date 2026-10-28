using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LayerHop.Tests
{
    /// <summary>Tron, gasless, destination calls, error details, tokens, splits, payouts, recurring orders.</summary>
    public class PlatformTests
    {
        private const string TronUnsigned = "{\"txID\":\"" + Hash64 + "\",\"raw_data_hex\":\"0a02abcd\",\"raw_data\":{\"expiration\":1760000600000,\"fee_limit\":150000000,\"contract\":[{\"type\":\"TriggerSmartContract\"}]},\"visible\":false}";
        private const string Hash64 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Fact]
        public async Task BuildTxTron()
        {
            var body = "{\"quoteId\":\"q1\",\"provider\":\"relay\",\"tx\":{\"vm\":\"tvm\",\"chainId\":728126428,\"from\":\"TFrom\","
                + "\"call\":{\"to\":\"TRouter\",\"data\":\"0xabcd\",\"value\":\"0\",\"feeLimit\":\"150000000\"},"
                + "\"unsigned\":" + TronUnsigned + ","
                + "\"approval\":{\"token\":\"TUsdt\",\"spender\":\"TRouter\",\"amount\":\"5000000\",\"call\":{\"to\":\"TUsdt\",\"data\":\"0x095ea7b3\",\"value\":0,\"feeLimit\":100000000},\"unsigned\":" + TronUnsigned + "}}}";
            var (c, _) = MockHandler.Client(body);
            var tx = await c.BuildTxAsync("q1");
            Assert.True(tx.IsTron);
            Assert.False(tx.IsEvm);
            var tron = tx.AsTron();
            Assert.Equal(ChainIds.Tron, tron.ChainId);
            Assert.Equal("TFrom", tron.From);
            Assert.Equal("150000000", tron.Call.FeeLimit);
            Assert.Equal(Hash64, tron.Unsigned!.TxID);
            Assert.Equal("0a02abcd", tron.Unsigned.RawDataHex);
            Assert.Equal(1760000600000L, (long)tron.Unsigned.RawData["expiration"]!);
            Assert.False(tron.Unsigned.Visible);
            Assert.Equal("TUsdt", tron.Approval!.Token);
            Assert.Equal("100000000", tron.Approval.Call!.FeeLimit);
            Assert.Equal("0", tron.Approval.Call.Value);
            Assert.Equal(Hash64, tron.Approval.Unsigned!.TxID);
            Assert.Equal(Hash64, (string?)JObject.Parse(tron.Unsigned.ToJson())["txID"]);
            Assert.Throws<InvalidOperationException>(() => tx.AsEvm());
            Assert.Throws<InvalidOperationException>(() => new BuiltTx { Vm = "evm" }.AsTron());
        }

        [Fact]
        public async Task SimulateTronEnergy()
        {
            var (c, _) = MockHandler.Client("{\"ok\":true,\"vm\":\"tvm\",\"simulated\":\"tx\",\"energyUsed\":64285}");
            var s = await c.SimulateAsync("q1");
            Assert.Equal("tvm", s.Vm);
            Assert.Equal(64285L, s.EnergyUsed);
        }

        private const string GaslessBody = "{\"quoteId\":\"q1\",\"provider\":\"zerox\",\"gasless\":{\"chainId\":8453,"
            + "\"approval\":{\"type\":\"permit\",\"eip712\":{\"types\":{\"Permit\":[{\"name\":\"owner\",\"type\":\"address\"}]},\"domain\":{\"name\":\"USD Coin\",\"chainId\":8453},\"message\":{\"owner\":\"0xS\",\"value\":\"1000000\"},\"primaryType\":\"Permit\"}},"
            + "\"trade\":{\"type\":\"settler_metatransaction\",\"eip712\":{\"types\":{},\"domain\":{},\"message\":{\"deadline\":\"1760000000\"},\"primaryType\":\"SlippageAndActions\"}},"
            + "\"gasFee\":{\"amount\":\"12000\",\"token\":\"0xUSDC\"}}}";

        [Fact]
        public async Task GaslessBuildSubmitStatus()
        {
            var (c, h) = MockHandler.Client(GaslessBody);
            var sign = await c.BuildGaslessAsync("q1");
            Assert.Equal("POST", h.Last.Method);
            Assert.Equal("/v1/orders", h.Last.Path);
            Assert.Equal("q1", (string?)h.Last.Json["quoteId"]);
            Assert.Equal(8453, sign.ChainId);
            Assert.Equal("permit", sign.Approval!.Type);
            Assert.Equal("Permit", sign.Approval.Eip712.PrimaryType);
            Assert.Equal("1000000", (string?)sign.Approval.Eip712.Message["value"]);
            Assert.Equal("settler_metatransaction", sign.Trade.Type);
            Assert.Equal("12000", sign.GasFee!.Amount);
            var typed = JObject.Parse(sign.Trade.Eip712.ToJson());
            Assert.Equal("SlippageAndActions", (string?)typed["primaryType"]);
            Assert.Equal("1760000000", (string?)typed["message"]!["deadline"]);

            h.Respond(200, "{\"quoteId\":\"q1\",\"provider\":\"zerox\",\"orderId\":\"0xord\",\"state\":\"pending\"}");
            var st = await c.SubmitGaslessAsync("q1", "0xtrade", "0xapproval");
            Assert.Equal("/v1/orders/gasless", h.Last.Path);
            Assert.Equal("0xtrade", (string?)h.Last.Json["signatures"]!["trade"]);
            Assert.Equal("0xapproval", (string?)h.Last.Json["signatures"]!["approval"]);
            Assert.Equal("0xord", st.OrderId);
            Assert.False(st.IsFinal);

            await c.SubmitGaslessAsync("q1", "0xtrade");
            Assert.Null(h.Last.Json["signatures"]!["approval"]);

            h.Respond(200, "{\"quoteId\":\"q1\",\"provider\":\"zerox\",\"orderId\":\"0xord\",\"state\":\"filled\",\"srcTxHash\":\"0xsrc\"}");
            var done = await c.GetGaslessStatusAsync("q1");
            Assert.Equal("GET", h.Last.Method);
            Assert.Equal("/v1/orders/gasless/q1", h.Last.Path);
            Assert.True(done.IsFinal);
            Assert.Equal("0xsrc", done.SrcTxHash);
        }

        [Fact]
        public async Task BuildTxOnGaslessQuoteThrows()
        {
            var (c, h) = MockHandler.Client(GaslessBody);
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => c.BuildTxAsync("q1"));
            Assert.Contains("BuildGaslessAsync", e.Message);

            h.Respond(200, "{\"tx\":{\"vm\":\"evm\",\"chainId\":1,\"to\":\"0xT\",\"data\":\"0x\",\"value\":\"0\"}}");
            var e2 = await Assert.ThrowsAsync<InvalidOperationException>(() => c.BuildGaslessAsync("q1"));
            Assert.Contains("BuildTxAsync", e2.Message);
        }

        [Fact]
        public async Task QuoteExtraParams()
        {
            var (c, h) = MockHandler.Client("{\"best\":{\"quoteId\":\"q1\",\"provider\":\"zerox\",\"dstAmount\":\"1\",\"dstAmountMin\":\"1\",\"estimatedSeconds\":5,\"fees\":{\"ourFeeBps\":10},\"expiresAt\":1,\"gasless\":{\"gasFee\":{\"amount\":\"12000\",\"token\":\"0xU\"},\"approval\":true}},\"quotes\":[],\"errors\":[],\"warnings\":[{\"code\":\"GAS_ON_ARRIVAL_UNAVAILABLE\",\"message\":\"m\"}]}");
            var q = await c.QuoteAsync(new QuoteParams
            {
                SrcChain = "base", SrcToken = "USDC", Amount = "1000000", DstChain = "base", DstToken = "WETH", Sender = "0xS", Recipient = "0xR",
                Gasless = true, Referrer = "0xRef", CopyOf = "q_card", Purpose = QuotePurposes.Split, SplitId = "spl_1", SplitShare = 2, Note = "thanks",
            });
            Assert.True(q.Best.Gasless!.Approval);
            Assert.Equal("12000", q.Best.Gasless.GasFee!.Amount);
            Assert.Equal("GAS_ON_ARRIVAL_UNAVAILABLE", q.Warnings!.Single().Code);
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("true", qs["gasless"]);
            Assert.Equal("0xRef", qs["referrer"]);
            Assert.Equal("q_card", qs["copyOf"]);
            Assert.Equal("split", qs["purpose"]);
            Assert.Equal("spl_1", qs["splitId"]);
            Assert.Equal("2", qs["splitShare"]);
            Assert.Equal("thanks", qs["note"]);

            await c.QuoteAsync(new QuoteParams { SrcChain = "base", SrcToken = "USDC", Amount = "1", DstChain = "base", DstToken = "WETH", Sender = "a", Recipient = "b" });
            var plain = MockHandler.ParseQuery(h.Last.Query);
            foreach (var k in new[] { "gasless", "referrer", "copyOf", "purpose", "splitId", "splitShare", "note" }) Assert.False(plain.ContainsKey(k), k);
        }

        [Fact]
        public async Task QuoteCallsBody()
        {
            var (c, h) = MockHandler.Client("{\"best\":{\"quoteId\":\"q1\",\"provider\":\"relay\",\"dstAmount\":\"5000000\",\"dstAmountMin\":\"5000000\",\"estimatedSeconds\":20,\"fees\":{\"ourFeeBps\":10},\"expiresAt\":1,\"srcAmount\":\"5100000\",\"calls\":{\"items\":[{\"to\":\"0xpool\",\"data\":\"0xabcd\",\"value\":\"0\"}],\"executor\":\"0xexec\",\"executed\":true,\"simulation\":{\"ok\":true,\"gasUsed\":\"210000\",\"leftover\":\"0\"}}},\"quotes\":[],\"errors\":[]}",
                options: new LayerHopOptions { BaseUrl = "https://api.test", Integrator = "myapp", IntegratorFeeBps = 20, Channel = "widget" });
            var q = await c.QuoteCallsAsync(new CallsQuoteParams
            {
                SrcChain = "base", SrcToken = "USDC", DstChain = "arbitrum", DstToken = "USDC", DstAmount = "5000000", Sender = "0xS", Recipient = "0xR",
                Calls = new List<DestinationCall> { new DestinationCall { To = "0xpool", Data = "0xabcd" }, new DestinationCall { To = "0xb", Data = "0x01", Value = "7" } },
                Referrer = "0xRef",
            });
            Assert.Equal("POST", h.Last.Method);
            Assert.Equal("/v1/quote", h.Last.Path);
            Assert.Equal("", h.Last.Query);
            var b = h.Last.Json;
            Assert.Equal("5000000", (string?)b["dstAmount"]);
            Assert.Null(b["amount"]);
            Assert.Equal(50, (int)b["slippageBps"]!);
            Assert.Equal("widget", (string?)b["channel"]);
            Assert.Equal("myapp", (string?)b["integrator"]);
            Assert.Equal(20, (int)b["integratorFeeBps"]!);
            Assert.Equal("0xRef", (string?)b["referrer"]);
            Assert.Null(b["copyOf"]);
            Assert.Null(b["action"]);
            var calls = (JArray)b["calls"]!;
            Assert.Equal(2, calls.Count);
            Assert.Null(calls[0]["value"]);
            Assert.Equal("7", (string?)calls[1]["value"]);
            Assert.Equal("5100000", q.Best.SrcAmount);
            Assert.Equal("0xexec", q.Best.Calls!.Executor);
            Assert.True(q.Best.Calls.Executed);
            Assert.Equal("210000", q.Best.Calls.Simulation!.GasUsed);

            await c.QuoteCallsAsync(new CallsQuoteParams { SrcChain = "base", SrcToken = "USDC", DstChain = "base", DstToken = "USDC", DstAmount = "1", Sender = "a", Recipient = "b", Action = "aave-v3-supply" });
            Assert.Equal("aave-v3-supply", (string?)h.Last.Json["action"]!["type"]);
            Assert.Null(h.Last.Json["calls"]);
        }

        [Fact]
        public async Task NoRouteSuggestions()
        {
            var leg = "\"srcChain\":8453,\"srcToken\":\"0xA\",\"amount\":\"1000000\",\"dstChain\":1,\"dstToken\":\"0xB\"";
            var body = "{\"error\":{\"code\":\"NO_ROUTE\",\"message\":\"No route\",\"requestId\":\"r1\",\"details\":{"
                + "\"errors\":[{\"provider\":\"relay\",\"code\":\"NO_ROUTE\",\"message\":\"none\"}],"
                + "\"suggestions\":["
                + "{" + leg + ",\"dstAmount\":\"990000\",\"kind\":\"dst_token\",\"message\":\"Receive USDC on Ethereum instead\",\"symbol\":\"USDC\"},"
                + "{" + leg + ",\"kind\":\"via_hub\",\"message\":\"Go through Arbitrum\",\"hub\":{\"chainId\":42161,\"token\":\"0xH\",\"symbol\":\"USDC\",\"decimals\":6},\"steps\":[{" + leg + "},{\"srcChain\":42161,\"srcToken\":\"0xH\",\"amount\":\"990000\",\"dstChain\":1,\"dstToken\":\"0xB\"}]},"
                + "{" + leg + ",\"kind\":\"via_hub\",\"message\":\"no steps\",\"hub\":{\"chainId\":42161,\"token\":\"0xH\",\"symbol\":\"USDC\",\"decimals\":6}},"
                + "{\"srcChain\":\"8453\",\"srcToken\":\"0xA\",\"amount\":\"1\",\"dstChain\":1,\"dstToken\":\"0xB\",\"kind\":\"src_token\",\"message\":\"bad chain type\"},"
                + "{" + leg + ",\"kind\":\"other\",\"message\":\"unknown kind\"},"
                + "{\"srcChain\":8453,\"srcToken\":\"0xA\",\"amount\":\"0\",\"dstChain\":1,\"dstToken\":\"0xB\",\"kind\":\"src_token\",\"message\":\"zero\"},"
                + "{" + leg + ",\"kind\":\"src_token\",\"message\":\"Pay with ETH\"}"
                + "]}}}";
            var (c, _) = MockHandler.Client(body, 400);
            var e = await Assert.ThrowsAsync<LayerHopApiException>(() => c.QuoteAsync(new QuoteParams { SrcChain = "base", SrcToken = "0xA", Amount = "1000000", DstChain = "ethereum", DstToken = "0xB", Sender = "a", Recipient = "b" }));
            Assert.Equal(ErrorCodes.NoRoute, e.Code);
            Assert.NotNull(e.Details);
            var s = e.Suggestions;
            Assert.Equal(new[] { "dst_token", "via_hub", "src_token" }, s.Select(x => x.Kind).ToArray());
            Assert.Equal("990000", s[0].DstAmount);
            Assert.Equal("USDC", s[0].Symbol);
            Assert.Equal(8453, s[0].SrcChain);
            Assert.Equal(42161, s[1].Hub!.ChainId);
            Assert.Equal(6, s[1].Hub!.Decimals);
            Assert.Equal(2, s[1].Steps!.Count);
            Assert.Equal("990000", s[1].Steps![1].Amount);
            Assert.Null(s[2].Hub);
            Assert.Equal("relay", e.ProviderErrors.Single().Provider);
            Assert.Null(e.MinAmount);
        }

        [Fact]
        public async Task SuggestionsCappedAtFour()
        {
            var one = "{\"srcChain\":1,\"srcToken\":\"0xA\",\"amount\":\"5\",\"dstChain\":1,\"dstToken\":\"0xB\",\"kind\":\"dst_token\",\"message\":\"m\"}";
            var body = "{\"error\":{\"code\":\"NO_ROUTE\",\"message\":\"x\",\"details\":{\"suggestions\":[" + string.Join(",", Enumerable.Repeat(one, 6)) + "]}}}";
            var (c, _) = MockHandler.Client(body, 400);
            var e = await Assert.ThrowsAsync<LayerHopApiException>(() => c.HealthAsync());
            Assert.Equal(4, e.Suggestions.Count);
        }

        [Fact]
        public async Task AmountTooSmallDetails()
        {
            var (c, _) = MockHandler.Client("{\"error\":{\"code\":\"AMOUNT_TOO_SMALL\",\"message\":\"Too small\",\"details\":{\"minAmount\":\"25000000\",\"minAmountUsd\":25,\"minAmountApprox\":true}}}", 400);
            var e = await Assert.ThrowsAsync<LayerHopApiException>(() => c.HealthAsync());
            Assert.Equal(ErrorCodes.AmountTooSmall, e.Code);
            Assert.Equal("25000000", e.MinAmount);
            Assert.Equal(25.0, e.MinAmountUsd);
            Assert.True(e.MinAmountApprox);
            Assert.Empty(e.Suggestions);
            Assert.Empty(e.ProviderErrors);
        }

        [Fact]
        public async Task ErrorWithoutDetailsAndUnknownCode()
        {
            var (c, _) = MockHandler.Client("{\"error\":{\"code\":\"SOMETHING_NEW\",\"message\":\"x\"}}", 400);
            var e = await Assert.ThrowsAsync<LayerHopApiException>(() => c.HealthAsync());
            Assert.Equal("SOMETHING_NEW", e.Code);
            Assert.Null(e.Details);
            Assert.Empty(e.Suggestions);
            Assert.Null(e.MinAmountUsd);
            Assert.Null(e.MinAmountApprox);
        }

        [Fact]
        public async Task TokenSearchLookupRisk()
        {
            var (c, h) = MockHandler.Client("{\"tokens\":[{\"chainId\":80094,\"address\":\"0xH\",\"symbol\":\"HONEY\",\"name\":\"HONEY\",\"decimals\":18,\"priceUsd\":0.9979,\"verified\":true}],\"nextCursor\":\"1\"}");
            var page = await c.SearchTokensAsync("berachain", "HONEY", 10, "0");
            Assert.Equal("1", page.NextCursor);
            Assert.True(page.Tokens.Single().Verified);
            Assert.Equal(0.9979, page.Tokens[0].PriceUsd);
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("berachain", qs["chain"]);
            Assert.Equal("HONEY", qs["q"]);
            Assert.Equal("10", qs["limit"]);
            Assert.Equal("0", qs["cursor"]);

            h.Respond(200, "{\"token\":{\"chainId\":8453,\"address\":\"0x42\",\"symbol\":\"WETH\",\"name\":\"Wrapped Ether\",\"decimals\":18,\"verified\":true,\"popular\":true}}");
            var t = await c.LookupTokenAsync("0x42", "base");
            Assert.Equal("WETH", t.Symbol);
            Assert.True(t.Popular);
            Assert.Equal("/v1/tokens/lookup", h.Last.Path);
            qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("0x42", qs["address"]);
            Assert.Equal("base", qs["chain"]);

            await c.LookupTokenAsync("0x42");
            Assert.Equal("address=0x42", h.Last.Query);

            h.Respond(200, "{\"risk\":{\"chainId\":8453,\"address\":\"0x11\",\"level\":\"danger\",\"verified\":false,\"symbol\":\"USDC\",\"reasons\":[{\"code\":\"fake_symbol\",\"level\":\"danger\",\"message\":\"Fake\"},{\"code\":\"sell_tax\",\"level\":\"danger\",\"value\":25,\"message\":\"25%\"},{\"code\":\"brand_new_code\",\"level\":\"caution\",\"message\":\"new\"}],\"source\":\"goplus\",\"checkedAt\":\"2026-10-27T09:00:00.000Z\"}}");
            var risk = await c.GetTokenRiskAsync("base", "0x11");
            Assert.Equal("/v1/tokens/base/0x11/risk", h.Last.Path);
            Assert.Equal(TokenRiskLevels.Danger, risk.Level);
            Assert.Equal(25.0, risk.Reasons[1].Value);
            Assert.Equal("brand_new_code", risk.Reasons[2].Code);
            Assert.Equal("goplus", risk.Source);
        }

        [Fact]
        public async Task ChainExtrasAndBalances()
        {
            var (c, h) = MockHandler.Client("{\"chains\":[{\"id\":81457,\"slug\":\"blast\",\"name\":\"Blast\",\"vm\":\"evm\",\"nativeSymbol\":\"ETH\",\"nativeDecimals\":18,\"explorerTx\":\"x\",\"destinationOnly\":true,\"onlyRoute\":{\"srcChainId\":1,\"srcToken\":\"0x0\",\"dstToken\":\"0x0\"},\"routeHealth\":{\"in\":\"ok\",\"out\":\"failing\",\"status\":\"deposit_only\",\"checkedAt\":\"t\"}},{\"id\":728126428,\"slug\":\"tron\",\"name\":\"Tron\",\"vm\":\"tvm\",\"nativeSymbol\":\"TRX\",\"nativeDecimals\":6,\"explorerTx\":\"y\"}]}");
            var chains = await c.GetChainsAsync();
            Assert.True(chains[0].DestinationOnly);
            Assert.Equal(1, chains[0].OnlyRoute!.SrcChainId);
            Assert.Equal("deposit_only", chains[0].RouteHealth!.Status);
            Assert.Equal("failing", chains[0].RouteHealth!.Out);
            Assert.Equal(Vms.Tvm, chains[1].Vm);
            Assert.Null(chains[1].RouteHealth);

            h.Respond(200, "{\"address\":\"0xW\",\"vm\":\"evm\",\"balances\":[{\"chainId\":8453,\"address\":\"0x0\",\"symbol\":\"ETH\",\"name\":\"ETH\",\"decimals\":18,\"verified\":true,\"amount\":\"3128821803472737280\",\"valueUsd\":7582.2}],\"totalUsd\":7584.7,\"errors\":[{\"chainId\":80094,\"message\":\"timed out\"}]}");
            var bal = await c.GetBalancesAsync("0xW", new[] { "base", "arbitrum" }, new[] { "8453:0xU" }, fresh: true);
            Assert.Equal("3128821803472737280", bal.Balances.Single().Amount);
            Assert.Equal(7584.7, bal.TotalUsd);
            Assert.Equal(80094, bal.Errors.Single().ChainId);
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("base,arbitrum", qs["chains"]);
            Assert.Equal("8453:0xU", qs["tokens"]);
            Assert.Equal("1", qs["fresh"]);

            await c.GetBalancesAsync("0xW");
            Assert.Equal("address=0xW", h.Last.Query);

            var before = h.Requests.Count;
            Assert.Empty(await c.GetPricesAsync(Array.Empty<string>()));
            Assert.Equal(before, h.Requests.Count);
        }

        private const string SplitJson = "\"id\":\"spl_1\",\"url\":\"https://layerhop.com/split/spl_1\",\"status\":\"open\",\"note\":null,\"recipient\":\"0x11\",\"recipientName\":\"alice.eth\",\"chainId\":8453,\"chain\":\"base\",\"token\":{\"address\":\"0xU\",\"symbol\":\"USDC\",\"decimals\":6},\"total\":\"120000000\",\"collected\":\"0\",\"paidCount\":0,\"shares\":[{\"index\":0,\"label\":\"\",\"amount\":\"60000000\",\"received\":\"0\",\"status\":\"unpaid\",\"payments\":0,\"pending\":0},{\"index\":1,\"label\":\"Bob\",\"amount\":\"60000000\",\"received\":\"60000000\",\"status\":\"paid\",\"payments\":1,\"pending\":0}],\"createdAt\":\"t\",\"updatedAt\":\"t\"";

        [Fact]
        public async Task SplitsCreateGetUpdate()
        {
            var (c, h) = MockHandler.Client("{" + SplitJson + ",\"editToken\":\"secret\",\"editUrl\":\"https://layerhop.com/split/spl_1#k=secret\"}");
            var created = await c.CreateSplitAsync(new CreateSplitParams { Recipient = "alice.eth", Chain = "base", Token = "USDC", Total = "120000000", Participants = 2, Note = "Dinner" });
            Assert.Equal("secret", created.EditToken);
            Assert.Equal("POST", h.Last.Method);
            Assert.Equal("/v1/splits", h.Last.Path);
            Assert.Equal(2, (int)h.Last.Json["participants"]!);
            Assert.Null(h.Last.Json["shares"]);
            Assert.Equal("Dinner", (string?)h.Last.Json["note"]);

            h.Respond(200, "{" + SplitJson + "}");
            var split = await c.GetSplitAsync("spl_1");
            Assert.Equal("/v1/splits/spl_1", h.Last.Path);
            Assert.Null(split.Note);
            Assert.Equal("paid", split.Shares[1].Status);
            Assert.Equal("USDC", split.Token.Symbol);

            await c.UpdateSplitAsync("spl_1", "secret", new UpdateSplitParams { Note = "", Labels = new List<string> { "Ann", "Bob" }, Status = SplitStatuses.Closed });
            Assert.Equal("PATCH", h.Last.Method);
            Assert.Equal("secret", h.Last.Headers["x-edit-token"]);
            var b = h.Last.Json;
            Assert.Equal(JTokenType.Null, b["note"]!.Type);
            Assert.Equal("closed", (string?)b["status"]);
            Assert.Equal(2, ((JArray)b["labels"]!).Count);

            await c.UpdateSplitAsync("spl_1", "secret", new UpdateSplitParams { Status = SplitStatuses.Open });
            Assert.Null(h.Last.Json["note"]);
            Assert.Null(h.Last.Json["labels"]);
        }

        [Fact]
        public async Task TipJarAndPayouts()
        {
            var (c, h) = MockHandler.Client("{\"address\":\"0x11\",\"tippers\":3,\"tips\":5,\"messages\":[{\"text\":\"gm\",\"at\":\"t\"}]}");
            var jar = await c.GetTipJarAsync("0x11");
            Assert.Equal("/v1/tip-jars/0x11", h.Last.Path);
            Assert.Equal(3, jar.Tippers);
            Assert.Equal("gm", jar.Messages.Single().Text);

            h.Respond(200, "{\"source\":{\"chainId\":8453,\"chain\":\"base\",\"sender\":\"0xS\",\"token\":{\"chainId\":8453,\"address\":\"0xU\",\"symbol\":\"USDC\",\"decimals\":6,\"usd\":1}},"
                + "\"rows\":[{\"index\":0,\"line\":0,\"input\":{\"recipient\":\"alice.eth\",\"amount\":250,\"token\":\"USDC\",\"chain\":8453},\"ok\":true,\"recipient\":{\"address\":\"0x33\",\"name\":\"alice.eth\"},\"chainId\":8453,\"chain\":\"base\",\"token\":{\"chainId\":8453,\"address\":\"0xU\",\"symbol\":\"USDC\",\"decimals\":6},\"amount\":\"250000000\",\"kind\":\"transfer\"},"
                + "{\"index\":1,\"line\":0,\"input\":{\"recipient\":\"x\",\"amount\":\"0.5\",\"token\":\"USDC\",\"chain\":\"solana\"},\"ok\":false,\"error\":{\"code\":\"INVALID_PARAMS\",\"message\":\"bad\"}}],"
                + "\"transfers\":[{\"chainId\":8453,\"token\":\"0xU\",\"rows\":[0],\"total\":\"250000000\"}],\"routes\":[],\"transferTotal\":\"250000000\",\"totals\":[],\"valid\":1,\"invalid\":1,\"warnings\":[]}");
            var plan = await c.PlanPayoutsAsync(new PlanPayoutsParams
            {
                SrcChain = "base", SrcToken = "USDC", Sender = "0xS",
                Rows = new List<PayoutRowInput> { new PayoutRowInput { Recipient = "alice.eth", Amount = "250", Token = "USDC", Chain = "base" } },
            });
            Assert.Equal("/v1/payouts/plan", h.Last.Path);
            Assert.Equal("250", (string?)h.Last.Json["rows"]![0]!["amount"]);
            Assert.Null(h.Last.Json["csv"]);
            Assert.Equal("250", plan.Rows[0].Input.Amount);
            Assert.Equal("8453", plan.Rows[0].Input.Chain);
            Assert.Equal("0x33", plan.Rows[0].Recipient!.Address);
            Assert.Equal("INVALID_PARAMS", plan.Rows[1].Error!.Code);
            Assert.Equal(0, plan.Transfers.Single().Rows.Single());
            Assert.Equal(1, plan.Invalid);

            h.Respond(200, "{\"vm\":\"evm\",\"chainId\":8453,\"token\":{\"chainId\":8453,\"address\":\"0xU\",\"symbol\":\"USDC\",\"decimals\":6},\"calls\":[{\"to\":\"0xU\",\"data\":\"0xa9059cbb\",\"value\":\"0\"}]}");
            var transfers = await c.BuildPayoutTransfersAsync(new BuildPayoutTransfersParams
            {
                Chain = "base", Token = "USDC", Sender = "0xS",
                Transfers = new List<PayoutTransfer> { new PayoutTransfer { Recipient = "0x33", Amount = "250000000" } },
            });
            Assert.Equal("/v1/payouts/transfers", h.Last.Path);
            Assert.Equal("250000000", (string?)h.Last.Json["transfers"]![0]!["amount"]);
            Assert.Equal("0xa9059cbb", transfers.Calls!.Single().Data);
            Assert.Null(transfers.SerializedTx);
        }

        [Fact]
        public async Task RecurringOrders()
        {
            var (c, h) = MockHandler.Client("{\"object\":\"recurring_order_build\",\"livemode\":true,\"provider\":\"jupiter\",\"chainId\":7565164,\"wallet\":\"W\",\"srcToken\":\"EPj\",\"dstToken\":\"Dez\",\"amount\":\"350000000\",\"amountPerOrder\":\"50000000\",\"orders\":7,\"intervalSeconds\":86400,\"fees\":{\"ourFeeBps\":0,\"integratorFeeBps\":0},\"requestId\":\"req-1\",\"tx\":{\"vm\":\"svm\",\"serializedTx\":\"AQAA\",\"submitViaApi\":true}}");
            var build = await c.CreateRecurringOrderAsync(new CreateRecurringOrderParams { Wallet = "W", DstToken = "BONK", Amount = "350000000", Orders = 7, IntervalSeconds = 86400 });
            Assert.Equal("POST", h.Last.Method);
            Assert.Equal("/v1/recurring-orders", h.Last.Path);
            Assert.Equal(7, (int)h.Last.Json["orders"]!);
            Assert.Equal(86400, (long)h.Last.Json["intervalSeconds"]!);
            Assert.Null(h.Last.Json["srcToken"]);
            Assert.Null(h.Last.Json["chain"]);
            Assert.Equal("req-1", build.RequestId);
            Assert.True(build.Tx.AsSvm().SubmitViaApi);

            h.Respond(200, "{\"txHash\":\"5sig\",\"order\":\"G1w\"}");
            var sub = await c.SubmitRecurringOrderAsync("req-1", "c2ln");
            Assert.Equal("/v1/recurring-orders/submit", h.Last.Path);
            Assert.Equal("req-1", (string?)h.Last.Json["requestId"]);
            Assert.Equal("c2ln", (string?)h.Last.Json["signedTransaction"]);
            Assert.Equal("G1w", sub.Order);

            h.Respond(200, "{\"recurringOrders\":[{\"id\":\"G1w\",\"object\":\"recurring_order\",\"provider\":\"jupiter\",\"chainId\":7565164,\"status\":\"active\",\"wallet\":\"W\",\"srcToken\":\"EPj\",\"dstToken\":\"Dez\",\"amount\":\"350000000\",\"amountPerOrder\":\"50000000\",\"orders\":7,\"ordersFilled\":1,\"intervalSeconds\":86400,\"spentAmount\":\"50000000\",\"withdrawnAmount\":\"0\",\"receivedAmount\":\"123\",\"openTxHash\":\"o\",\"closeTxHash\":null,\"createdAt\":null,\"fills\":[{\"txHash\":\"f\",\"amount\":\"50000000\",\"dstAmount\":\"123\",\"feeAmount\":\"1\",\"feeToken\":null,\"filledAt\":null}]}],\"page\":1,\"totalPages\":1}");
            var list = await c.ListRecurringOrdersAsync("W", RecurringOrderStatuses.History, 2);
            var qs = MockHandler.ParseQuery(h.Last.Query);
            Assert.Equal("W", qs["wallet"]);
            Assert.Equal("history", qs["status"]);
            Assert.Equal("2", qs["page"]);
            Assert.Equal(1, list.RecurringOrders.Single().OrdersFilled);
            Assert.Null(list.RecurringOrders[0].Fills.Single().FeeToken);

            h.Respond(200, "{\"requestId\":\"req-2\",\"tx\":{\"vm\":\"svm\",\"serializedTx\":\"AQBB\",\"submitViaApi\":true}}");
            var cancel = await c.CancelRecurringOrderAsync("G1w", "W");
            Assert.Equal("/v1/recurring-orders/G1w/cancel", h.Last.Path);
            Assert.Equal("W", (string?)h.Last.Json["wallet"]);
            Assert.Equal("req-2", cancel.RequestId);
            Assert.Equal("AQBB", cancel.Tx.AsSvm().SerializedTx);
        }

        [Fact]
        public async Task WatchStopsOnDropped()
        {
            var h = new MockHandler()
                .Respond(200, "{\"provider\":\"relay\",\"id\":\"x\",\"state\":\"pending\"}")
                .Respond(200, "{\"provider\":\"relay\",\"id\":\"x\",\"state\":\"dropped\"}");
            var c = new LayerHopClient(new LayerHopOptions { BaseUrl = "https://api.test" }, new HttpClient(h));
            var final = await c.WatchOrderAsync("relay", "x", TimeSpan.FromMilliseconds(1));
            Assert.Equal(OrderStates.Dropped, final.State);
            Assert.True(final.IsFinal);
            Assert.Equal(2, h.Requests.Count);
            Assert.Equal("order.dropped", WebhookEventTypes.OrderDropped);
        }
    }
}
