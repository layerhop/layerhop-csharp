#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LayerHop
{
    /// <summary>Order states reported by GET /v1/orders/{provider}/{id}. Filled, Refunded, Failed and Dropped are final.</summary>
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
        /// <summary>The source transaction never reached the network; no funds moved (final; re-quote).</summary>
        public const string Dropped = "dropped";
        /// <summary>The provider does not know the order (yet).</summary>
        public const string Unknown = "unknown";

        /// <summary>Whether the state will not change any more.</summary>
        public static bool IsFinal(string? state) => state == Filled || state == Refunded || state == Failed || state == Dropped;
    }

    /// <summary>Well-known chain ids.</summary>
    public static class ChainIds
    {
        /// <summary>LayerHop's internal chain id for Solana.</summary>
        public const long Solana = 7565164;
        /// <summary>Tron mainnet (vm "tvm"; D-048: only where the API runs with Tron enabled).</summary>
        public const long Tron = 728126428;
    }

    /// <summary>Virtual machine families (<see cref="Chain.Vm"/>, <see cref="BuiltTx.Vm"/>).</summary>
    public static class Vms
    {
        /// <summary>EVM chains.</summary>
        public const string Evm = "evm";
        /// <summary>Solana.</summary>
        public const string Svm = "svm";
        /// <summary>Tron.</summary>
        public const string Tvm = "tvm";
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
        /// <summary>"evm", "svm" or "tvm" (Tron). See <see cref="Vms"/>.</summary>
        [JsonProperty("vm")] public string Vm { get; set; } = "";
        /// <summary>Native token symbol.</summary>
        [JsonProperty("nativeSymbol")] public string NativeSymbol { get; set; } = "";
        /// <summary>Native token decimals.</summary>
        [JsonProperty("nativeDecimals")] public int NativeDecimals { get; set; }
        /// <summary>Explorer transaction URL prefix.</summary>
        [JsonProperty("explorerTx")] public string ExplorerTx { get; set; } = "";
        /// <summary>Explorer address URL prefix.</summary>
        [JsonProperty("explorerAddress")] public string? ExplorerAddress { get; set; }
        /// <summary>Chain logo URL.</summary>
        [JsonProperty("logoURI")] public string? LogoURI { get; set; }
        /// <summary>Providers that can route from or to this chain.</summary>
        [JsonProperty("providers")] public List<string>? Providers { get; set; }
        /// <summary>Shown first in chain pickers.</summary>
        [JsonProperty("popular")] public bool? Popular { get; set; }
        /// <summary>True for chains you can deliver to but not send from (Hyperliquid, Blast). Hide them from source pickers.</summary>
        [JsonProperty("destinationOnly")] public bool? DestinationOnly { get; set; }
        /// <summary>Set when every provider serves one fixed route into this chain (Blast: ETH on Ethereum only).</summary>
        [JsonProperty("onlyRoute")] public ChainOnlyRoute? OnlyRoute { get; set; }
        /// <summary>Result of LayerHop's periodic probe quotes into and out of this chain.</summary>
        [JsonProperty("routeHealth")] public ChainRouteHealth? RouteHealth { get; set; }
    }

    /// <summary>The one route into a route-restricted chain; quotes from anything else fail with UNSUPPORTED_CHAIN.</summary>
    public class ChainOnlyRoute
    {
        /// <summary>Source chain id.</summary>
        [JsonProperty("srcChainId")] public long SrcChainId { get; set; }
        /// <summary>Source token address (EVM native = the zero address).</summary>
        [JsonProperty("srcToken")] public string SrcToken { get; set; } = "";
        /// <summary>Destination token address on this chain.</summary>
        [JsonProperty("dstToken")] public string DstToken { get; set; } = "";
    }

    /// <summary>
    /// Probe results for a chain. In / Out: "ok", "failing" or "unknown". Status: "ok", "deposit_only",
    /// "withdraw_only", "unavailable" or "unknown".
    /// </summary>
    public class ChainRouteHealth
    {
        /// <summary>Routes into the chain.</summary>
        [JsonProperty("in")] public string In { get; set; } = "";
        /// <summary>Routes out of the chain.</summary>
        [JsonProperty("out")] public string Out { get; set; } = "";
        /// <summary>Summary of both directions.</summary>
        [JsonProperty("status")] public string Status { get; set; } = "";
        /// <summary>Newest probe (ISO 8601).</summary>
        [JsonProperty("checkedAt")] public string? CheckedAt { get; set; }
    }

    /// <summary>A token. Show a warning before a user trades one with <see cref="Verified"/> false.</summary>
    public class Token
    {
        /// <summary>Chain id.</summary>
        [JsonProperty("chainId")] public long ChainId { get; set; }
        /// <summary>Address or mint.</summary>
        [JsonProperty("address")] public string Address { get; set; } = "";
        /// <summary>Symbol.</summary>
        [JsonProperty("symbol")] public string Symbol { get; set; } = "";
        /// <summary>Name.</summary>
        [JsonProperty("name")] public string? Name { get; set; }
        /// <summary>Decimals.</summary>
        [JsonProperty("decimals")] public int Decimals { get; set; }
        /// <summary>Logo URL.</summary>
        [JsonProperty("logoURI")] public string? LogoURI { get; set; }
        /// <summary>USD price when known.</summary>
        [JsonProperty("priceUsd")] public double? PriceUsd { get; set; }
        /// <summary>On a provider list or LayerHop's curated list; false for tokens read on-chain only.</summary>
        [JsonProperty("verified")] public bool Verified { get; set; }
        /// <summary>Stables, wrapped native and majors on that chain.</summary>
        [JsonProperty("popular")] public bool? Popular { get; set; }
    }

    /// <summary>One page of GET /v1/tokens.</summary>
    public class TokensPage
    {
        /// <summary>Tokens.</summary>
        [JsonProperty("tokens")] public List<Token> Tokens { get; set; } = new List<Token>();
        /// <summary>Pass as cursor for the next page; null on the last page.</summary>
        [JsonProperty("nextCursor")] public string? NextCursor { get; set; }
    }

    /// <summary>Token risk levels (<see cref="TokenRisk.Level"/>). New values may appear: treat unknown ones as "unknown".</summary>
    public static class TokenRiskLevels
    {
        public const string Ok = "ok";
        public const string Caution = "caution";
        public const string Danger = "danger";
        public const string Unknown = "unknown";
    }

    /// <summary>A token's safety report (GET /v1/tokens/{chain}/{address}/risk).</summary>
    public class TokenRisk
    {
        /// <summary>Chain id.</summary>
        [JsonProperty("chainId")] public long ChainId { get; set; }
        /// <summary>Address or mint.</summary>
        [JsonProperty("address")] public string Address { get; set; } = "";
        /// <summary>See <see cref="TokenRiskLevels"/>. Ask for an explicit confirmation on "danger"; "unknown" should never block a swap.</summary>
        [JsonProperty("level")] public string Level { get; set; } = "";
        /// <summary>A major LayerHop lists itself; never checked upstream.</summary>
        [JsonProperty("verified")] public bool Verified { get; set; }
        /// <summary>Symbol, when known.</summary>
        [JsonProperty("symbol")] public string? Symbol { get; set; }
        /// <summary>Why the level is what it is.</summary>
        [JsonProperty("reasons")] public List<TokenRiskReason> Reasons { get; set; } = new List<TokenRiskReason>();
        /// <summary>"list", "goplus" or "none".</summary>
        [JsonProperty("source")] public string Source { get; set; } = "";
        /// <summary>ISO 8601.</summary>
        [JsonProperty("checkedAt")] public string CheckedAt { get; set; } = "";
    }

    /// <summary>One finding of a <see cref="TokenRisk"/>.</summary>
    public class TokenRiskReason
    {
        /// <summary>E.g. "honeypot", "sell_tax", "fake_symbol". New codes may appear.</summary>
        [JsonProperty("code")] public string Code { get; set; } = "";
        /// <summary>"caution" or "danger".</summary>
        [JsonProperty("level")] public string Level { get; set; } = "";
        /// <summary>One plain sentence for the user.</summary>
        [JsonProperty("message")] public string Message { get; set; } = "";
        /// <summary>Percent for taxes and fees; holder count for few_holders.</summary>
        [JsonProperty("value")] public double? Value { get; set; }
    }

    /// <summary>A wallet balance: a token plus the amount held.</summary>
    public class Balance : Token
    {
        /// <summary>Base units.</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "0";
        /// <summary>Amount times PriceUsd, when the price is known.</summary>
        [JsonProperty("valueUsd")] public double? ValueUsd { get; set; }
    }

    /// <summary>A chain or token that could not be read; everything else is still returned.</summary>
    public class BalanceError
    {
        /// <summary>Chain id (0 for an unknown chain in tokens).</summary>
        [JsonProperty("chainId")] public long ChainId { get; set; }
        /// <summary>Message.</summary>
        [JsonProperty("message")] public string Message { get; set; } = "";
    }

    /// <summary>Body of GET /v1/balances.</summary>
    public class BalancesResponse
    {
        /// <summary>The wallet.</summary>
        [JsonProperty("address")] public string Address { get; set; } = "";
        /// <summary>"evm", "svm" or "tvm".</summary>
        [JsonProperty("vm")] public string Vm { get; set; } = "";
        /// <summary>Highest USD value first.</summary>
        [JsonProperty("balances")] public List<Balance> Balances { get; set; } = new List<Balance>();
        /// <summary>Sum of ValueUsd.</summary>
        [JsonProperty("totalUsd")] public double TotalUsd { get; set; }
        /// <summary>Chains or tokens that could not be read.</summary>
        [JsonProperty("errors")] public List<BalanceError> Errors { get; set; } = new List<BalanceError>();
    }

    /// <summary>Values for <see cref="QuoteParams.Purpose"/> (attribution only; never changes fee, price or route).</summary>
    public static class QuotePurposes
    {
        public const string Tip = "tip";
        public const string Split = "split";
        public const string Payout = "payout";
        public const string Gather = "gather";
        public const string Rebalance = "rebalance";
        public const string Fund = "fund";
    }

    /// <summary>
    /// Inputs shared by GET /v1/quote and GET /v1/quote/stream: the route, the parties and attribution.
    /// Use <see cref="QuoteParams"/> (an input amount) or <see cref="ExactOutputQuoteParams"/> (an exact output).
    /// </summary>
    public abstract class QuoteParamsBase
    {
        /// <summary>Chain id or slug ("base", "solana", "8453").</summary>
        public string SrcChain { get; set; } = "";
        /// <summary>Symbol from the token list ("USDC") or an address / mint.</summary>
        public string SrcToken { get; set; } = "";
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
        /// <summary>Wallet that referred the user (attribution only). Invalid values are ignored.</summary>
        public string? Referrer { get; set; }
        /// <summary>Copy this trade: a trade card's quote id or a wallet (attribution only).</summary>
        public string? CopyOf { get; set; }
        /// <summary>What the payment is for; see <see cref="QuotePurposes"/>.</summary>
        public string? Purpose { get; set; }
        /// <summary>Pay one share of a split (with <see cref="SplitShare"/>).</summary>
        public string? SplitId { get; set; }
        /// <summary>The share's index in the split, from 0.</summary>
        public int? SplitShare { get; set; }
        /// <summary>A message on a tip or split payment (kept to 140 characters).</summary>
        public string? Note { get; set; }
        /// <summary>Never use these providers (provider ids, e.g. "relay"). No route left: ROUTE_EXCLUDED.</summary>
        public List<string>? ExcludeProviders { get; set; }
        /// <summary>Never route through these bridges (e.g. "stargate").</summary>
        public List<string>? ExcludeBridges { get; set; }
        /// <summary>Never swap through these exchanges (e.g. "1inch").</summary>
        public List<string>? ExcludeExchanges { get; set; }
    }

    /// <summary>Inputs of GET /v1/quote with an input amount.</summary>
    public class QuoteParams : QuoteParamsBase
    {
        /// <summary>Integer string in base units of SrcToken.</summary>
        public string Amount { get; set; } = "";
        /// <summary>
        /// Also deliver native gas to Recipient on the destination chain, worth $2 by default (sent as
        /// gasOnArrival=true). Ignored for same-chain swaps, non-EVM destinations and native outputs;
        /// providers that can't do it ignore it (check <see cref="Quote.GasOnArrival"/>).
        /// </summary>
        public bool GasOnArrival { get; set; }
        /// <summary>USD value of that gas (0.5-20); implies GasOnArrival (sent as gasOnArrivalUsd).</summary>
        public double? GasOnArrivalUsd { get; set; }
        /// <summary>
        /// Gasless swap (sent as gasless=true): the wallet signs EIP-712 typed data and the provider's relayer
        /// pays the network fee out of SrcToken. Same-chain ERC-20 input only. Build with
        /// <see cref="LayerHopClient.BuildGaslessAsync"/>, not BuildTxAsync.
        /// </summary>
        public bool Gasless { get; set; }
    }

    /// <summary>
    /// Receive an exact amount (GET /v1/quote with dstAmount): the recipient gets DstAmount; each quote reports
    /// <see cref="Quote.SrcAmount"/> (what the sender pays) and <see cref="Quote.SrcAmountMax"/> (the most the tx
    /// can take, for balance checks). Only providers that quote a fixed output answer. No gas on arrival or gasless.
    /// </summary>
    public class ExactOutputQuoteParams : QuoteParamsBase
    {
        /// <summary>The exact base units of DstToken the recipient receives.</summary>
        public string DstAmount { get; set; } = "";
    }

    /// <summary>Options of <see cref="LayerHopClient.StreamQuotes"/>.</summary>
    public class QuoteStreamOptions
    {
        /// <summary>Refresh interval in ms (API default 10000, minimum 5000). Each refresh counts as one request against your rate limit.</summary>
        public int? IntervalMs { get; set; }
    }

    /// <summary>One "quote" event of GET /v1/quote/stream: a full quote response plus its sequence number (1, 2, ...).</summary>
    public class QuoteStreamUpdate : QuoteResponse
    {
        [JsonProperty("seq")] public int Seq { get; set; }
    }

    /// <summary>One contract call on the destination chain. msg.sender is the provider's executor, not the user.</summary>
    public class DestinationCall
    {
        /// <summary>Target contract.</summary>
        [JsonProperty("to")] public string To { get; set; } = "";
        /// <summary>ABI-encoded calldata (0x hex).</summary>
        [JsonProperty("data")] public string Data { get; set; } = "0x";
        /// <summary>Native value in wei; only when DstToken is the native coin. Null sends none.</summary>
        [JsonProperty("value")] public string? Value { get; set; }
    }

    /// <summary>Inputs of a destination-calls quote (POST /v1/quote): an exact output that the calls receive.</summary>
    public class CallsQuoteParams
    {
        /// <summary>Chain id or slug.</summary>
        public string SrcChain { get; set; } = "";
        /// <summary>Source token.</summary>
        public string SrcToken { get; set; } = "";
        /// <summary>Destination chain id or slug (EVM).</summary>
        public string DstChain { get; set; } = "";
        /// <summary>Destination token.</summary>
        public string DstToken { get; set; } = "";
        /// <summary>Exact base units of DstToken the calls receive.</summary>
        public string DstAmount { get; set; } = "";
        /// <summary>Sender address on SrcChain.</summary>
        public string Sender { get; set; } = "";
        /// <summary>Who the output (or what the calls produce) is for; an EVM address.</summary>
        public string Recipient { get; set; } = "";
        /// <summary>Slippage in bps; 50 when null.</summary>
        public int? SlippageBps { get; set; }
        /// <summary>Calls run in order with the output (at most 8). Set this or <see cref="Action"/>.</summary>
        public List<DestinationCall>? Calls { get; set; }
        /// <summary>A server-side preset instead of Calls, e.g. "aave-v3-supply" (sent as action.type).</summary>
        public string? Action { get; set; }
        /// <summary>Referring wallet (attribution only).</summary>
        public string? Referrer { get; set; }
        /// <summary>Copied trade card or wallet (attribution only).</summary>
        public string? CopyOf { get; set; }
    }

    /// <summary>The destination calls of a quote, as the provider committed to them.</summary>
    public class DestinationCallsPlan
    {
        /// <summary>The calls (lowercase hex).</summary>
        [JsonProperty("items")] public List<DestinationCall> Items { get; set; } = new List<DestinationCall>();
        /// <summary>Contract that receives the output and makes the calls (their msg.sender).</summary>
        [JsonProperty("executor")] public string Executor { get; set; } = "";
        /// <summary>The provider runs the calls as part of the fill.</summary>
        [JsonProperty("executed")] public bool Executed { get; set; }
        /// <summary>Pre-quote simulation on the destination chain.</summary>
        [JsonProperty("simulation")] public DestinationCallsSimulation? Simulation { get; set; }
    }

    /// <summary>Result of the destination-calls simulation.</summary>
    public class DestinationCallsSimulation
    {
        /// <summary>Whether the calls succeeded.</summary>
        [JsonProperty("ok")] public bool Ok { get; set; }
        /// <summary>Gas the calls used (decimal string).</summary>
        [JsonProperty("gasUsed"), JsonConverter(typeof(NumStringConverter))] public string? GasUsed { get; set; }
        /// <summary>ERC-20 output left in the executor (decimal string); above 0 is lost.</summary>
        [JsonProperty("leftover"), JsonConverter(typeof(NumStringConverter))] public string? Leftover { get; set; }
    }

    /// <summary>An amount of a token (the gasless relayer's fee).</summary>
    public class TokenAmount
    {
        /// <summary>Base units.</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        /// <summary>Token address.</summary>
        [JsonProperty("token")] public string Token { get; set; } = "";
    }

    /// <summary>Set on gasless quotes only. DstAmount already accounts for the gas fee.</summary>
    public class GaslessQuoteInfo
    {
        /// <summary>Network fee the relayer takes from the input token.</summary>
        [JsonProperty("gasFee")] public TokenAmount? GasFee { get; set; }
        /// <summary>True when the wallet also signs a gasless approval (two signatures).</summary>
        [JsonProperty("approval")] public bool Approval { get; set; }
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
        /// <summary>What Sender pays, fees included (exact-output and destination-calls quotes only).</summary>
        [JsonProperty("srcAmount")] public string? SrcAmount { get; set; }
        /// <summary>Exact-output quotes: the most the transaction can take from Sender, slippage included (at least SrcAmount). Check balances against it.</summary>
        [JsonProperty("srcAmountMax")] public string? SrcAmountMax { get; set; }
        /// <summary>Destination calls and their simulation (destination-calls quotes only).</summary>
        [JsonProperty("calls")] public DestinationCallsPlan? Calls { get; set; }
        /// <summary>Set on gasless quotes; build them with <see cref="LayerHopClient.BuildGaslessAsync"/>.</summary>
        [JsonProperty("gasless")] public GaslessQuoteInfo? Gasless { get; set; }
    }

    /// <summary>The request was served, but not exactly as asked. Ignore codes you don't know.</summary>
    public class QuoteWarning
    {
        /// <summary>E.g. "GAS_ON_ARRIVAL_UNAVAILABLE".</summary>
        [JsonProperty("code")] public string Code { get; set; } = "";
        /// <summary>Message.</summary>
        [JsonProperty("message")] public string Message { get; set; } = "";
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
        /// <summary>Present only when the request was not served exactly as asked.</summary>
        [JsonProperty("warnings")] public List<QuoteWarning>? Warnings { get; set; }
        /// <summary>Address screening (D-069): listed addresses of this request; present once the API has a list loaded.</summary>
        [JsonProperty("screening")] public ScreeningAnnotation? Screening { get; set; }
    }

    /// <summary>
    /// An ERC-20 approve(spender, amount) that must be mined before the main tx. On Tron (TRC-20) it also
    /// carries the <see cref="Call"/> and, usually, the prebuilt <see cref="Unsigned"/> transaction.
    /// </summary>
    public class Approval
    {
        /// <summary>Token address.</summary>
        [JsonProperty("token")] public string Token { get; set; } = "";
        /// <summary>Spender address.</summary>
        [JsonProperty("spender")] public string Spender { get; set; } = "";
        /// <summary>Amount in base units.</summary>
        [JsonProperty("amount")] public string Amount { get; set; } = "";
        /// <summary>Tron only: the approve call.</summary>
        [JsonProperty("call")] public TronCall? Call { get; set; }
        /// <summary>Tron only: the approve transaction, prebuilt and unsigned.</summary>
        [JsonProperty("unsigned")] public TronUnsignedTx? Unsigned { get; set; }
    }

    /// <summary>One Tron smart-contract call (TriggerSmartContract). Addresses are base58 T-addresses; amounts are sun.</summary>
    public class TronCall
    {
        /// <summary>Contract called (T-address).</summary>
        [JsonProperty("to")] public string To { get; set; } = "";
        /// <summary>ABI calldata, 0x hex.</summary>
        [JsonProperty("data")] public string Data { get; set; } = "";
        /// <summary>TRX sent with the call, in sun.</summary>
        [JsonProperty("value"), JsonConverter(typeof(NumStringConverter))] public string Value { get; set; } = "0";
        /// <summary>fee_limit in sun: the most TRX the network may burn for energy.</summary>
        [JsonProperty("feeLimit"), JsonConverter(typeof(NumStringConverter))] public string FeeLimit { get; set; } = "0";
    }

    /// <summary>
    /// An unsigned Tron transaction exactly as the node built it (TronWeb's transaction object). Sign it
    /// unchanged and broadcast it before raw_data.expiration.
    /// </summary>
    public class TronUnsignedTx
    {
        /// <summary>The tx hash once broadcast: bare 64-hex, no 0x. Report this one.</summary>
        [JsonProperty("txID")] public string TxID { get; set; } = "";
        /// <summary>Protobuf-encoded raw transaction (what gets signed).</summary>
        [JsonProperty("raw_data_hex")] public string RawDataHex { get; set; } = "";
        /// <summary>Decoded raw_data, kept as raw JSON (contract, ref block, expiration, fee_limit).</summary>
        [JsonProperty("raw_data")] public JObject RawData { get; set; } = new JObject();
        /// <summary>Address format flag of the node.</summary>
        [JsonProperty("visible")] public bool? Visible { get; set; }

        /// <summary>The whole transaction object as JSON, for TronWeb's trx.sign / sendRawTransaction.</summary>
        public string ToJson()
        {
            var o = new JObject { ["txID"] = TxID, ["raw_data_hex"] = RawDataHex, ["raw_data"] = RawData };
            if (Visible.HasValue) o["visible"] = Visible.Value;
            return o.ToString(Formatting.None);
        }
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
    /// An unsigned transaction for the user's wallet, discriminated by <see cref="Vm"/> ("evm" | "svm" | "tvm").
    /// Use <see cref="AsEvm"/>, <see cref="AsSvm"/> or <see cref="AsTron"/> for a typed view. LayerHop never signs it.
    /// </summary>
    public class BuiltTx
    {
        /// <summary>"evm", "svm" or "tvm". See <see cref="Vms"/>.</summary>
        [JsonProperty("vm")] public string Vm { get; set; } = "";

        /// <summary>EVM and Tron: chain id.</summary>
        [JsonProperty("chainId")] public long? ChainId { get; set; }
        /// <summary>EVM: target.</summary>
        [JsonProperty("to")] public string? To { get; set; }
        /// <summary>EVM: hex calldata.</summary>
        [JsonProperty("data")] public string? Data { get; set; }
        /// <summary>EVM: value in wei.</summary>
        [JsonProperty("value")] public string? Value { get; set; }
        /// <summary>EVM and Tron: approval to send (and wait for) first.</summary>
        [JsonProperty("approval")] public Approval? Approval { get; set; }
        /// <summary>EVM: further calls to send after the main transaction, in order. Report the main tx hash.</summary>
        [JsonProperty("extraTxs")] public List<EvmCall>? ExtraTxs { get; set; }

        /// <summary>SVM: base64 serialized VersionedTransaction.</summary>
        [JsonProperty("serializedTx")] public string? SerializedTx { get; set; }
        /// <summary>SVM: sign only, then send through <see cref="LayerHopClient.SubmitAsync"/> instead of an RPC node.</summary>
        [JsonProperty("submitViaApi")] public bool SubmitViaApi { get; set; }

        /// <summary>Tron: owner (signer) of every call, T-address.</summary>
        [JsonProperty("from")] public string? From { get; set; }
        /// <summary>Tron: the main call.</summary>
        [JsonProperty("call")] public TronCall? Call { get; set; }
        /// <summary>Tron: the main transaction, prebuilt and unsigned (absent in test mode).</summary>
        [JsonProperty("unsigned")] public TronUnsignedTx? Unsigned { get; set; }

        /// <summary>Test mode: a placeholder that moves no funds. Never broadcast it.</summary>
        [JsonProperty("sandbox")] public bool Sandbox { get; set; }

        /// <summary>Whether this is an EVM transaction.</summary>
        [JsonIgnore] public bool IsEvm => Vm == "evm";
        /// <summary>Whether this is a Solana transaction.</summary>
        [JsonIgnore] public bool IsSvm => Vm == "svm";
        /// <summary>Whether this is a Tron transaction.</summary>
        [JsonIgnore] public bool IsTron => Vm == "tvm";

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

        /// <summary>The Tron view; throws when <see cref="IsTron"/> is false.</summary>
        public TronTx AsTron()
        {
            if (!IsTron) throw new System.InvalidOperationException("layerhop: not a Tron transaction (vm=" + Vm + ")");
            return new TronTx
            {
                ChainId = ChainId ?? ChainIds.Tron,
                From = From ?? "",
                Call = Call ?? new TronCall(),
                Unsigned = Unsigned,
                Approval = Approval,
                Sandbox = Sandbox,
            };
        }
    }

    /// <summary>
    /// Unsigned Tron transaction(s) for a quote (D-048). The SDK never signs: sign <see cref="Unsigned"/>
    /// unchanged with a Tron wallet (TronWeb / TronLink), sending <see cref="Approval"/> (its own
    /// transaction, Approval.Unsigned) first when set. Broadcast before raw_data.expiration, then call
    /// ReportSubmittedAsync with the main transaction's TxID (bare 64-hex, no 0x).
    /// </summary>
    public class TronTx
    {
        /// <summary>Always <see cref="ChainIds.Tron"/>.</summary>
        public long ChainId { get; set; }
        /// <summary>Signer of every call (T-address).</summary>
        public string From { get; set; } = "";
        /// <summary>The main call; a wallet may rebuild the transaction from it with its own node.</summary>
        public TronCall Call { get; set; } = new TronCall();
        /// <summary>The main transaction, prebuilt and unsigned; null in test mode.</summary>
        public TronUnsignedTx? Unsigned { get; set; }
        /// <summary>TRC-20 approval to send first (Approval.Call / Approval.Unsigned), or null.</summary>
        public Approval? Approval { get; set; }
        /// <summary>Test-mode placeholder; never broadcast.</summary>
        public bool Sandbox { get; set; }
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
        /// <summary>"evm", "svm" or "tvm".</summary>
        [JsonProperty("vm")] public string Vm { get; set; } = "";
        /// <summary>"tx" or "approval".</summary>
        [JsonProperty("simulated")] public string? Simulated { get; set; }
        /// <summary>EVM gas estimate as a decimal string.</summary>
        [JsonProperty("gasEstimate"), JsonConverter(typeof(NumStringConverter))] public string? GasEstimate { get; set; }
        /// <summary>Solana compute units as a decimal string.</summary>
        [JsonProperty("computeUnits"), JsonConverter(typeof(NumStringConverter))] public string? ComputeUnits { get; set; }
        /// <summary>Tron: energy the simulated call used (about 100 sun each without staked energy).</summary>
        [JsonProperty("energyUsed")] public long? EnergyUsed { get; set; }
        /// <summary>Whether an approval is needed first.</summary>
        [JsonProperty("needsApproval")] public bool NeedsApproval { get; set; }
        /// <summary>Failure details.</summary>
        [JsonProperty("error")] public SimulationError? Error { get; set; }
        /// <summary>The simulated tx.</summary>
        [JsonProperty("tx")] public BuiltTx? Tx { get; set; }
    }

    /// <summary>EIP-712 typed data exactly as the provider returned it. Sign it unchanged (eth_signTypedData_v4).</summary>
    public class Eip712TypedData
    {
        /// <summary>Type definitions, raw JSON.</summary>
        [JsonProperty("types")] public JObject Types { get; set; } = new JObject();
        /// <summary>Domain, raw JSON.</summary>
        [JsonProperty("domain")] public JObject Domain { get; set; } = new JObject();
        /// <summary>Message, raw JSON.</summary>
        [JsonProperty("message")] public JObject Message { get; set; } = new JObject();
        /// <summary>Primary type.</summary>
        [JsonProperty("primaryType")] public string PrimaryType { get; set; } = "";

        /// <summary>The typed data as one JSON string, for wallet APIs that take eth_signTypedData_v4 JSON.</summary>
        public string ToJson() => new JObject
        {
            ["types"] = Types,
            ["domain"] = Domain,
            ["primaryType"] = PrimaryType,
            ["message"] = Message,
        }.ToString(Formatting.None);
    }

    /// <summary>One thing the wallet signs for a gasless order.</summary>
    public class GaslessSignable
    {
        /// <summary>Provider's kind, e.g. "permit" (approvals) or "settler_metatransaction" (trade).</summary>
        [JsonProperty("type")] public string Type { get; set; } = "";
        /// <summary>The typed data to sign.</summary>
        [JsonProperty("eip712")] public Eip712TypedData Eip712 { get; set; } = new Eip712TypedData();
    }

    /// <summary>
    /// What the wallet signs for a gasless quote: <see cref="Approval"/> (when set) and <see cref="Trade"/>,
    /// on <see cref="ChainId"/>. The SDK never signs; pass the signatures to SubmitGaslessAsync.
    /// </summary>
    public class GaslessSignRequest
    {
        /// <summary>Chain id.</summary>
        [JsonProperty("chainId")] public long ChainId { get; set; }
        /// <summary>Gasless approval (permit) to sign first, or null.</summary>
        [JsonProperty("approval")] public GaslessSignable? Approval { get; set; }
        /// <summary>The trade to sign.</summary>
        [JsonProperty("trade")] public GaslessSignable Trade { get; set; } = new GaslessSignable();
        /// <summary>Network fee the relayer takes from the input token.</summary>
        [JsonProperty("gasFee")] public TokenAmount? GasFee { get; set; }
    }

    /// <summary>A gasless order's state (POST /v1/orders/gasless, GET /v1/orders/gasless/{quoteId}).</summary>
    public class GaslessOrderStatus
    {
        /// <summary>Quote id.</summary>
        [JsonProperty("quoteId")] public string QuoteId { get; set; } = "";
        /// <summary>Provider.</summary>
        [JsonProperty("provider")] public string Provider { get; set; } = "";
        /// <summary>Provider order id.</summary>
        [JsonProperty("orderId")] public string OrderId { get; set; } = "";
        /// <summary>See <see cref="OrderStates"/>.</summary>
        [JsonProperty("state")] public string State { get; set; } = "";
        /// <summary>The relayer's transaction, once mined.</summary>
        [JsonProperty("srcTxHash")] public string? SrcTxHash { get; set; }
        /// <summary>Provider failure reason when failed.</summary>
        [JsonProperty("reason")] public string? Reason { get; set; }
        /// <summary>Whether <see cref="State"/> is final.</summary>
        [JsonIgnore] public bool IsFinal => OrderStates.IsFinal(State);
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
