#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LayerHop
{
    /// <summary>Order states reported by GET /v1/orders/{provider}/{id}. Filled, Refunded and Failed are final.</summary>
    public static class OrderStates
    {
        /// <summary>Not final yet.</summary>
        public const string Pending = "pending";
        /// <summary>Delivered.</summary>
        public const string Filled = "filled";
        /// <summary>Refunded to the sender.</summary>
        public const string Refunded = "refunded";
        /// <summary>Failed.</summary>
        public const string Failed = "failed";
        /// <summary>The provider does not know the order (yet).</summary>
        public const string Unknown = "unknown";

        /// <summary>Whether the state will not change any more.</summary>
        public static bool IsFinal(string? state) => state == Filled || state == Refunded || state == Failed;
    }

    /// <summary>Well-known chain ids.</summary>
    public static class ChainIds
    {
        /// <summary>LayerHop's internal chain id for Solana.</summary>
        public const long Solana = 7565164;
    }

    /// <summary>Body of GET /v1/health.</summary>
    public class Health
    {
        /// <summary>True when the API is up.</summary>
        [JsonProperty("ok")] public bool Ok { get; set; }
        /// <summary>Enabled provider ids.</summary>
        [JsonProperty("providers")] public List<string> Providers { get; set; } = new List<string>();
    }

    /// <summary>One entry of the chain registry.</summary>
    public class Chain
    {
        /// <summary>Chain id (Solana is <see cref="ChainIds.Solana"/>).</summary>
        [JsonProperty("id")] public long Id { get; set; }
        /// <summary>Slug, e.g. "base".</summary>
        [JsonProperty("slug")] public string Slug { get; set; } = "";
        /// <summary>Display name.</summary>
        [JsonProperty("name")] public string Name { get; set; } = "";
        /// <summary>"evm" or "svm".</summary>
        [JsonProperty("vm")] public string Vm { get; set; } = "";
        /// <summary>Native token symbol.</summary>
        [JsonProperty("nativeSymbol")] public string NativeSymbol { get; set; } = "";
        /// <summary>Native token decimals.</summary>
        [JsonProperty("nativeDecimals")] public int NativeDecimals { get; set; }
        /// <summary>Explorer transaction URL prefix.</summary>
        [JsonProperty("explorerTx")] public string ExplorerTx { get; set; } = "";
    }

    /// <summary>One entry of the built-in token list.</summary>
    public class Token
    {
        /// <summary>Chain id.</summary>
        [JsonProperty("chainId")] public long ChainId { get; set; }
        /// <summary>Address or mint.</summary>
        [JsonProperty("address")] public string Address { get; set; } = "";
        /// <summary>Symbol.</summary>
        [JsonProperty("symbol")] public string Symbol { get; set; } = "";
        /// <summary>Decimals.</summary>
        [JsonProperty("decimals")] public int Decimals { get; set; }
    }

    /// <summary>Inputs of GET /v1/quote.</summary>
    public class QuoteParams
    {
        /// <summary>Chain id or slug ("base", "solana", "8453").</summary>
        public string SrcChain { get; set; } = "";
        /// <summary>Symbol from the token list ("USDC") or an address / mint.</summary>
        public string SrcToken { get; set; } = "";
        /// <summary>Integer string in base units of SrcToken.</summary>
        public string Amount { get; set; } = "";
        /// <summary>Destination chain id or slug.</summary>
        public string DstChain { get; set; } = "";
        /// <summary>Destination token.</summary>
        public string DstToken { get; set; } = "";
        /// <summary>Sender address on SrcChain.</summary>
        public string Sender { get; set; } = "";
        /// <summary>Recipient address on DstChain.</summary>
        public string Recipient { get; set; } = "";
        /// <summary>Slippage in bps; 50 when null.</summary>
        public int? SlippageBps { get; set; }
        /// <summary>
        /// Also deliver native gas to Recipient on the destination chain, worth $2 by default (sent as
        /// gasOnArrival=true). Ignored for same-chain swaps, non-EVM destinations and native outputs;
        /// providers that can't do it ignore it (check <see cref="Quote.GasOnArrival"/>).
        /// </summary>
        public bool GasOnArrival { get; set; }
        /// <summary>USD value of that gas (0.5-20); implies GasOnArrival (sent as gasOnArrivalUsd).</summary>
        public double? GasOnArrivalUsd { get; set; }
    }

    /// <summary>Native gas a route delivers to the recipient on top of DstAmount (its cost is already taken out).</summary>
    public class GasOnArrival
    {
        /// <summary>Base units of the destination chain's native token.</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        /// <summary>Native token address on the destination chain.</summary>
        [JsonProperty("token")] public string Token { get; set; } = "";
        /// <summary>Symbol.</summary>
        [JsonProperty("symbol")] public string Symbol { get; set; } = "";
        /// <summary>Decimals.</summary>
        [JsonProperty("decimals")] public int Decimals { get; set; }
        /// <summary>USD value at quote time, null when unknown.</summary>
        [JsonProperty("usd")] public double? Usd { get; set; }
    }

    /// <summary>Fees applied to a quote.</summary>
    public class FeeBreakdown
    {
        /// <summary>LayerHop fee in bps.</summary>
        [JsonProperty("ourFeeBps")] public int OurFeeBps { get; set; }
        /// <summary>Provider fee in USD.</summary>
        [JsonProperty("providerFeeUsd")] public double? ProviderFeeUsd { get; set; }
        /// <summary>Gas in USD.</summary>
        [JsonProperty("gasUsd")] public double? GasUsd { get; set; }
        /// <summary>Partner fee in bps.</summary>
        [JsonProperty("integratorFeeBps")] public int? IntegratorFeeBps { get; set; }
        /// <summary>Partner id.</summary>
        [JsonProperty("integrator")] public string? Integrator { get; set; }
    }

    /// <summary>One provider's priced route.</summary>
    public class Quote
    {
        /// <summary>Quote id for <see cref="LayerHopClient.BuildTxAsync"/>.</summary>
        [JsonProperty("quoteId")] public string QuoteId { get; set; } = "";
        /// <summary>Provider id.</summary>
        [JsonProperty("provider")] public string Provider { get; set; } = "";
        /// <summary>Expected output in base units.</summary>
        [JsonProperty("dstAmount")] public string DstAmount { get; set; } = "";
        /// <summary>Minimum output after slippage, base units.</summary>
        [JsonProperty("dstAmountMin")] public string DstAmountMin { get; set; } = "";
        /// <summary>Estimated seconds to delivery.</summary>
        [JsonProperty("estimatedSeconds")] public int EstimatedSeconds { get; set; }
        /// <summary>Fees.</summary>
        [JsonProperty("fees")] public FeeBreakdown Fees { get; set; } = new FeeBreakdown();
        /// <summary>Expiry in unix seconds.</summary>
        [JsonProperty("expiresAt")] public long ExpiresAt { get; set; }
        /// <summary>Set when gas on arrival was requested and this route includes it.</summary>
        [JsonProperty("gasOnArrival")] public GasOnArrival? GasOnArrival { get; set; }
    }

    /// <summary>A provider that failed while the request as a whole succeeded.</summary>
    public class ProviderFailure
    {
        /// <summary>Provider id.</summary>
        [JsonProperty("provider")] public string Provider { get; set; } = "";
        /// <summary>Error code.</summary>
        [JsonProperty("code")] public string Code { get; set; } = "";
        /// <summary>Message.</summary>
        [JsonProperty("message")] public string Message { get; set; } = "";
    }

    /// <summary>Body of GET /v1/quote. Best == Quotes[0].</summary>
    public class QuoteResponse
    {
        /// <summary>Highest output for the user.</summary>
        [JsonProperty("best")] public Quote Best { get; set; } = new Quote();
        /// <summary>Every quote, best first.</summary>
        [JsonProperty("quotes")] public List<Quote> Quotes { get; set; } = new List<Quote>();
        /// <summary>Providers that failed.</summary>
        [JsonProperty("errors")] public List<ProviderFailure> Errors { get; set; } = new List<ProviderFailure>();
        /// <summary>False with an lh_test_ key (sandbox quotes). Null means live.</summary>
        [JsonProperty("livemode")] public bool? Livemode { get; set; }
    }

    /// <summary>An ERC-20 approve(spender, amount) that must be mined before the main tx.</summary>
    public class Approval
    {
        /// <summary>Token address.</summary>
        [JsonProperty("token")] public string Token { get; set; } = "";
        /// <summary>Spender address.</summary>
        [JsonProperty("spender")] public string Spender { get; set; } = "";
        /// <summary>Amount in base units.</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "";
    }

    /// <summary>A plain EVM call: target, hex calldata and native value in wei.</summary>
    public class EvmCall
    {
        /// <summary>Target address.</summary>
        [JsonProperty("to")] public string To { get; set; } = "";
        /// <summary>Hex calldata.</summary>
        [JsonProperty("data")] public string Data { get; set; } = "";
        /// <summary>Native value in wei (decimal string).</summary>
        [JsonProperty("value")] public string Value { get; set; } = "0";
    }

    /// <summary>
    /// An unsigned transaction for the user's wallet, discriminated by <see cref="Vm"/> ("evm" | "svm").
    /// Use <see cref="AsEvm"/> or <see cref="AsSvm"/> for a typed view. LayerHop never signs it.
    /// </summary>
    public class BuiltTx
    {
        /// <summary>"evm" or "svm".</summary>
        [JsonProperty("vm")] public string Vm { get; set; } = "";

        /// <summary>EVM: chain id.</summary>
        [JsonProperty("chainId")] public long? ChainId { get; set; }
        /// <summary>EVM: target.</summary>
        [JsonProperty("to")] public string? To { get; set; }
        /// <summary>EVM: hex calldata.</summary>
        [JsonProperty("data")] public string? Data { get; set; }
        /// <summary>EVM: value in wei.</summary>
        [JsonProperty("value")] public string? Value { get; set; }
        /// <summary>EVM: approval to send (and wait for) first.</summary>
        [JsonProperty("approval")] public Approval? Approval { get; set; }
        /// <summary>EVM: further calls to send after the main transaction, in order. Report the main tx hash.</summary>
        [JsonProperty("extraTxs")] public List<EvmCall>? ExtraTxs { get; set; }

        /// <summary>SVM: base64 serialized VersionedTransaction.</summary>
        [JsonProperty("serializedTx")] public string? SerializedTx { get; set; }
        /// <summary>SVM: sign only, then send through <see cref="LayerHopClient.SubmitAsync"/> instead of an RPC node.</summary>
        [JsonProperty("submitViaApi")] public bool SubmitViaApi { get; set; }

        /// <summary>Test mode: a placeholder that moves no funds. Never broadcast it.</summary>
        [JsonProperty("sandbox")] public bool Sandbox { get; set; }

        /// <summary>Whether this is an EVM transaction.</summary>
        [JsonIgnore] public bool IsEvm => Vm == "evm";
        /// <summary>Whether this is a Solana transaction.</summary>
        [JsonIgnore] public bool IsSvm => Vm == "svm";

        /// <summary>The EVM view; throws when <see cref="IsEvm"/> is false.</summary>
        public EvmTx AsEvm()
        {
            if (!IsEvm) throw new System.InvalidOperationException("layerhop: not an EVM transaction (vm=" + Vm + ")");
            return new EvmTx
            {
                ChainId = ChainId ?? 0,
                To = To ?? "",
                Data = Data ?? "0x",
                Value = string.IsNullOrEmpty(Value) ? "0" : Value!,
                Approval = Approval,
                ExtraTxs = ExtraTxs ?? new List<EvmCall>(),
                Sandbox = Sandbox,
            };
        }

        /// <summary>The Solana view; throws when <see cref="IsSvm"/> is false.</summary>
        public SvmTx AsSvm()
        {
            if (!IsSvm) throw new System.InvalidOperationException("layerhop: not a Solana transaction (vm=" + Vm + ")");
            return new SvmTx { SerializedTx = SerializedTx ?? "", SubmitViaApi = SubmitViaApi, Sandbox = Sandbox };
        }
    }

    /// <summary>An unsigned EVM transaction. Send <see cref="Approval"/> first if set, then this, then <see cref="ExtraTxs"/>.</summary>
    public class EvmTx
    {
        /// <summary>Chain id.</summary>
        public long ChainId { get; set; }
        /// <summary>Target.</summary>
        public string To { get; set; } = "";
        /// <summary>Hex calldata.</summary>
        public string Data { get; set; } = "0x";
        /// <summary>Value in wei (decimal string).</summary>
        public string Value { get; set; } = "0";
        /// <summary>ERC-20 approval to mine first, or null.</summary>
        public Approval? Approval { get; set; }
        /// <summary>Calls to send after the main transaction, in order.</summary>
        public List<EvmCall> ExtraTxs { get; set; } = new List<EvmCall>();
        /// <summary>Test-mode placeholder; never broadcast.</summary>
        public bool Sandbox { get; set; }
    }

    /// <summary>An unsigned Solana transaction.</summary>
    public class SvmTx
    {
        /// <summary>Base64 serialized VersionedTransaction.</summary>
        public string SerializedTx { get; set; } = "";
        /// <summary>Sign only, then <see cref="LayerHopClient.SubmitAsync"/>.</summary>
        public bool SubmitViaApi { get; set; }
        /// <summary>Test-mode placeholder; never broadcast.</summary>
        public bool Sandbox { get; set; }
    }

    /// <summary>Body of POST /v1/orders/{quoteId}/submitted.</summary>
    public class ReportSubmittedResponse
    {
        /// <summary>Quote id.</summary>
        [JsonProperty("quoteId")] public string QuoteId { get; set; } = "";
        /// <summary>Order status.</summary>
        [JsonProperty("status")] public string Status { get; set; } = "";
    }

    /// <summary>Why a simulation failed.</summary>
    public class SimulationError
    {
        /// <summary>Code.</summary>
        [JsonProperty("code")] public string Code { get; set; } = "";
        /// <summary>Message.</summary>
        [JsonProperty("message")] public string Message { get; set; } = "";
        /// <summary>Program / revert logs.</summary>
        [JsonProperty("logs")] public List<string>? Logs { get; set; }
    }

    /// <summary>Body of POST /v1/simulate.</summary>
    public class SimulateResponse
    {
        /// <summary>Quote id.</summary>
        [JsonProperty("quoteId")] public string? QuoteId { get; set; }
        /// <summary>Provider.</summary>
        [JsonProperty("provider")] public string? Provider { get; set; }
        /// <summary>Whether the simulation succeeded.</summary>
        [JsonProperty("ok")] public bool Ok { get; set; }
        /// <summary>"evm" or "svm".</summary>
        [JsonProperty("vm")] public string Vm { get; set; } = "";
        /// <summary>"tx" or "approval".</summary>
        [JsonProperty("simulated")] public string? Simulated { get; set; }
        /// <summary>EVM gas estimate as a decimal string.</summary>
        [JsonProperty("gasEstimate"), JsonConverter(typeof(NumStringConverter))] public string? GasEstimate { get; set; }
        /// <summary>Solana compute units as a decimal string.</summary>
        [JsonProperty("computeUnits"), JsonConverter(typeof(NumStringConverter))] public string? ComputeUnits { get; set; }
        /// <summary>Whether an approval is needed first.</summary>
        [JsonProperty("needsApproval")] public bool NeedsApproval { get; set; }
        /// <summary>Failure details.</summary>
        [JsonProperty("error")] public SimulationError? Error { get; set; }
        /// <summary>The simulated tx.</summary>
        [JsonProperty("tx")] public BuiltTx? Tx { get; set; }
    }

    /// <summary>Body of GET /v1/orders/{provider}/{id}.</summary>
    public class OrderStatus
    {
        /// <summary>Provider.</summary>
        [JsonProperty("provider")] public string Provider { get; set; } = "";
        /// <summary>Provider order id or source tx hash.</summary>
        [JsonProperty("id")] public string Id { get; set; } = "";
        /// <summary>See <see cref="OrderStates"/>.</summary>
        [JsonProperty("state")] public string State { get; set; } = "";
        /// <summary>Source tx hash.</summary>
        [JsonProperty("srcTxHash")] public string? SrcTxHash { get; set; }
        /// <summary>Destination tx hash.</summary>
        [JsonProperty("dstTxHash")] public string? DstTxHash { get; set; }
        /// <summary>Whether <see cref="State"/> is final.</summary>
        [JsonIgnore] public bool IsFinal => OrderStates.IsFinal(State);
    }

    /// <summary>An order (one row of GET /v1/orders, a deposit, a fill). Fields are optional.</summary>
    public class Order
    {
        /// <summary>Quote id.</summary>
        [JsonProperty("quoteId")] public string? QuoteId { get; set; }
        /// <summary>Provider.</summary>
        [JsonProperty("provider")] public string? Provider { get; set; }
        /// <summary>Status.</summary>
        [JsonProperty("status")] public string? Status { get; set; }
        /// <summary>Status detail.</summary>
        [JsonProperty("statusDetail")] public string? StatusDetail { get; set; }
        /// <summary>Fee channel.</summary>
        [JsonProperty("channel")] public string? Channel { get; set; }
        /// <summary>LayerHop fee bps.</summary>
        [JsonProperty("ourFeeBps")] public int? OurFeeBps { get; set; }
        /// <summary>Partner fee bps.</summary>
        [JsonProperty("partnerFeeBps")] public int? PartnerFeeBps { get; set; }
        /// <summary>Source chain id.</summary>
        [JsonProperty("srcChainId")] public long? SrcChainId { get; set; }
        /// <summary>Source token.</summary>
        [JsonProperty("srcToken")] public string? SrcToken { get; set; }
        /// <summary>Destination chain id.</summary>
        [JsonProperty("dstChainId")] public long? DstChainId { get; set; }
        /// <summary>Destination token.</summary>
        [JsonProperty("dstToken")] public string? DstToken { get; set; }
        /// <summary>Input amount, base units.</summary>
        [JsonProperty("amount")] public string? Amount { get; set; }
        /// <summary>Output amount, base units.</summary>
        [JsonProperty("dstAmount")] public string? DstAmount { get; set; }
        /// <summary>Sender.</summary>
        [JsonProperty("sender")] public string? Sender { get; set; }
        /// <summary>Recipient.</summary>
        [JsonProperty("recipient")] public string? Recipient { get; set; }
        /// <summary>Source tx hash.</summary>
        [JsonProperty("srcTxHash")] public string? SrcTxHash { get; set; }
        /// <summary>Destination tx hash.</summary>
        [JsonProperty("dstTxHash")] public string? DstTxHash { get; set; }
        /// <summary>False for test-mode orders. Null means live.</summary>
        [JsonProperty("livemode")] public bool? Livemode { get; set; }
        /// <summary>Checkout session this order pays.</summary>
        [JsonProperty("checkoutSessionId")] public string? CheckoutSessionId { get; set; }
        /// <summary>Deposit address this order came from.</summary>
        [JsonProperty("depositAddressId")] public string? DepositAddressId { get; set; }
        /// <summary>Checkout refund this order executes.</summary>
        [JsonProperty("refundId")] public string? RefundId { get; set; }
        /// <summary>Limit order this order is a fill of.</summary>
        [JsonProperty("limitOrderId")] public string? LimitOrderId { get; set; }
        /// <summary>Created at (ISO 8601).</summary>
        [JsonProperty("createdAt")] public string? CreatedAt { get; set; }
        /// <summary>Updated at (ISO 8601).</summary>
        [JsonProperty("updatedAt")] public string? UpdatedAt { get; set; }
    }

    /// <summary>Filters for GET /v1/orders. Null fields are omitted.</summary>
    public class ListOrdersParams
    {
        /// <summary>Matches the sender or the recipient.</summary>
        public string? Address { get; set; }
        /// <summary>Status filter.</summary>
        public string? Status { get; set; }
        /// <summary>Cursor from the previous page.</summary>
        public string? Cursor { get; set; }
        /// <summary>Page size.</summary>
        public int? Limit { get; set; }
    }

    /// <summary>Body of GET /v1/orders.</summary>
    public class ListOrdersResponse
    {
        /// <summary>Orders.</summary>
        [JsonProperty("orders")] public List<Order> Orders { get; set; } = new List<Order>();
        /// <summary>Cursor for the next page, null at the end.</summary>
        [JsonProperty("nextCursor")] public string? NextCursor { get; set; }
    }
}
