# LayerHop C# SDK

C# client for the [LayerHop](https://layerhop.com) API, for **.NET 6+** and **Unity 2021.3+**:
best-price cross-chain and same-chain swaps across EVM chains and Solana, plus crypto checkout,
invoices, subscriptions, deposit addresses and Solana limit orders.

The SDK is **non-custodial**. It never holds private keys and never signs anything. It fetches quotes
and builds *unsigned* transactions, and your own wallet integration signs and broadcasts them.
Amounts are integer strings in base units (`"1000000"` is 1 USDC), never floats.

- Docs: <https://docs.layerhop.com/sdks/csharp>
- Targets `netstandard2.1`. Its only dependency is `Newtonsoft.Json` 13 (Unity ships it as `com.unity.nuget.newtonsoft-json`).
- Async `Task` API. Every method takes a `CancellationToken`, and you can inject your own `HttpClient`.

## Install

```bash
dotnet add package LayerHop --version 0.6.0
```

For Unity, see [Unity](#unity) below.

## Quickstart

```csharp
using LayerHop;

using var lh = new LayerHopClient(new LayerHopOptions { Integrator = "myapp" });

var quote = await lh.QuoteAsync(new QuoteParams
{
    SrcChain = "base", SrcToken = "USDC", Amount = "100000000",   // 100 USDC
    DstChain = "solana", DstToken = "SOL",
    Sender = "0xYourEvmAddress", Recipient = "YourSolanaAddress",
    GasOnArrival = true,                                           // optional: native gas on arrival
});
Console.WriteLine($"{quote.Best.Provider}: {quote.Best.DstAmount} lamports in ~{quote.Best.EstimatedSeconds}s");

var tx = await lh.BuildTxAsync(quote.Best.QuoteId);
if (tx.IsEvm)
{
    var evm = tx.AsEvm();
    // 1. if (evm.Approval != null) send approve(evm.Approval.Spender, evm.Approval.Amount) on evm.Approval.Token and wait
    // 2. send { to = evm.To, data = evm.Data, value = evm.Value } on evm.ChainId with your wallet
    // 3. send evm.ExtraTxs in order (if any)
    string txHash = "0x...";                                       // from your wallet
    await lh.ReportSubmittedAsync(quote.Best.QuoteId, txHash);
}
else
{
    var svm = tx.AsSvm();                                          // svm.SerializedTx is base64
    // Sign with your Solana wallet. If svm.SubmitViaApi, send the signed tx through the API:
    // var sig = await lh.SubmitAsync(quote.Best.QuoteId, signedBase64);
}

// Poll until the order is filled, refunded or failed.
var final = await lh.WatchOrderAsync(quote.Best.Provider, "0xSourceTxHash",
    onChange: s => Console.WriteLine(s.State));
```

Or iterate over the state changes:

```csharp
await foreach (var st in lh.WatchOrder("relay", txHash, interval: TimeSpan.FromSeconds(3)))
    Console.WriteLine(st.State);
```

### Errors

Every non-2xx response throws `LayerHopApiException` with a stable `Code` (`NO_ROUTE`,
`QUOTE_EXPIRED`, ...), the HTTP `Status`, a `RequestId` for support and a `DocsUrl`.

```csharp
try { await lh.BuildTxAsync(quoteId); }
catch (LayerHopApiException e) when (e.Code == "QUOTE_EXPIRED") { /* re-quote */ }
```

### Everything else

| Area | Methods |
|---|---|
| Assets | `GetChainsAsync`, `GetTokensAsync`, `GetPricesAsync` |
| Swaps | `QuoteAsync`, `BuildTxAsync`, `SubmitAsync`, `ReportSubmittedAsync`, `SimulateAsync`, `GetOrderStatusAsync`, `ListOrdersAsync`, `WatchOrder` / `WatchOrderAsync` |
| Checkout | `CreateCheckoutSessionAsync`, `GetCheckoutSessionAsync`, `ListCheckoutSessionsAsync`, `ExpireCheckoutSessionAsync`, `QuoteCheckoutSessionAsync` |
| Products | `CreateCheckoutProductAsync`, `GetCheckoutProductAsync`, `ListCheckoutProductsAsync`, `UpdateCheckoutProductAsync`, `StartProductCheckoutAsync` |
| Refunds | `CreateRefundAsync`, `ListRefundsAsync` |
| Invoices | `CreateInvoiceAsync`, `GetInvoiceAsync`, `ListInvoicesAsync`, `UpdateInvoiceAsync`, `SendInvoiceAsync`, `VoidInvoiceAsync`, `GetPublicInvoiceAsync`, `PayInvoiceAsync` |
| Deposit addresses | `CreateDepositAddressAsync`, `GetDepositAddressAsync`, `ListDepositAddressesAsync`, `RefreshDepositAddressAsync`, `DisableDepositAddressAsync`, `SimulateDepositAsync` |
| Subscriptions | `CreateSubscriptionAsync`, `GetSubscriptionAsync`, `ListSubscriptionsAsync`, `UpdateSubscriptionAsync`, `CancelSubscriptionAsync`, `PauseSubscriptionAsync`, `ResumeSubscriptionAsync`, `ListSubscriptionInvoicesAsync` |
| Limit orders (Solana, beta) | `CreateLimitOrderAsync`, `SubmitLimitOrderAsync`, `GetLimitOrderAsync`, `ListLimitOrdersAsync`, `CancelLimitOrderAsync`, `SimulateLimitOrderAsync` |
| Misc | `HealthAsync`, `GetServiceStatusAsync`, `TriggerWebhookEventAsync` |

`Update*Params` send only the properties you set. Setting a nullable text field to `""` clears it
(sends JSON `null`).

**Test mode:** use an `lh_test_...` API key and every call goes to the sandbox. Quotes are priced
live, transactions are placeholders (`tx.Sandbox == true`, never broadcast them) and orders fill in
about 10 s.

### Checkout: accept any token

```csharp
var lh = new LayerHopClient(new LayerHopOptions { ApiKey = Environment.GetEnvironmentVariable("LAYERHOP_API_KEY") });
var session = await lh.CreateCheckoutSessionAsync(new CreateCheckoutParams
{
    Amount = "49000000", SettleChain = "base", SettleToken = "USDC",      // 49 USDC on Base
    Recipient = "0xYourPayoutAddress", Reference = "order_123",
    SuccessUrl = "https://shop.example.com/thanks",
});
// Redirect the customer to session.Url and fulfil the order on the checkout.session.completed webhook.
```

## Webhooks

```csharp
// ASP.NET Core minimal API
app.MapPost("/webhooks/layerhop", async (HttpRequest req) =>
{
    using var reader = new StreamReader(req.Body);
    var raw = await reader.ReadToEndAsync();                         // the raw body, before any JSON parsing
    try
    {
        var evt = Webhooks.ConstructEvent(
            Environment.GetEnvironmentVariable("LAYERHOP_WEBHOOK_SECRET")!,
            raw, req.Headers[Webhooks.SignatureHeader]);
        if (evt.Type == WebhookEventTypes.CheckoutSessionCompleted)
            Fulfil(evt.Data.Session!.Reference);
        return Results.Ok();
    }
    catch (LayerHopWebhookSignatureException)
    {
        return Results.BadRequest();
    }
});
```

`Webhooks.VerifySignature(secret, body, header)` returns a bool. `Webhooks.Sign(secret, body,
timestamp)` builds a header, which is handy for testing your handler. Signatures are
`t=<unix>,v1=<hex HMAC-SHA256(secret, "<t>.<body>")>`. Timestamps older than 5 minutes are rejected,
and several `v1` values are accepted, so secrets can be rotated.

## Unity

### Install (UPM, git URL)

In **Window > Package Manager > + > Add package from git URL...** enter:

```
https://github.com/layerhop/layerhop-csharp.git?path=src/LayerHop#v0.6.0
```

or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.layerhop.sdk": "https://github.com/layerhop/layerhop-csharp.git?path=src/LayerHop#v0.6.0"
  }
}
```

The package depends on `com.unity.nuget.newtonsoft-json`, which Unity resolves automatically. The
`LayerHop` assembly definition is auto-referenced, so your scripts can `using LayerHop;` right away.

### Example

```csharp
using LayerHop;
using UnityEngine;

public class SwapButton : MonoBehaviour
{
    private readonly LayerHopClient _lh = new LayerHopClient(new LayerHopOptions { Integrator = "mygame" });

    public async void OnClick()
    {
        try
        {
            var q = await _lh.QuoteAsync(new QuoteParams
            {
                SrcChain = "solana", SrcToken = "USDC", Amount = "5000000",
                DstChain = "solana", DstToken = "SOL",
                Sender = PlayerWallet.Address, Recipient = PlayerWallet.Address,
            });
            var tx = (await _lh.BuildTxAsync(q.Best.QuoteId)).AsSvm();
            // Hand tx.SerializedTx to your wallet integration (e.g. the Solana Unity SDK or Reown AppKit
            // for Unity) to sign and send, then report the signature:
            // await _lh.ReportSubmittedAsync(q.Best.QuoteId, signature);
        }
        catch (LayerHopApiException e)
        {
            Debug.LogWarning($"{e.Code}: {e.ApiMessage} ({e.RequestId})");
        }
    }

    private void OnDestroy() => _lh.Dispose();
}
```

`await` inside a `MonoBehaviour` resumes on the main thread (Unity's synchronization context), so you
can touch UI after it. UniTask works too. The SDK uses `ConfigureAwait(false)` internally.

Keys: never ship a partner or secret API key inside a game build. Quotes, transactions and order
status work without one. Create checkout sessions, invoices and deposit addresses on your backend.

### IL2CPP and code stripping

- The package includes a `link.xml` that preserves the `LayerHop` assembly, so the JSON models
  survive managed code stripping at any level.
- No `System.Reflection.Emit`, no `dynamic`, no runtime code generation. Every model is a plain
  class with a public parameterless constructor and explicit `[JsonProperty]` names.
- Language level is C# 9 and the API surface is `netstandard2.1`, matching Unity 2021.3+.

### WebGL

`HttpClient`'s default handler does not work in WebGL builds (browsers only allow `UnityWebRequest`).
Pass an `HttpClient` built on a custom `HttpMessageHandler` that forwards to `UnityWebRequest`. A minimal version (not part of the package;
call it from the main thread):

```csharp
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

public sealed class UnityWebRequestHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content == null ? null : await request.Content.ReadAsByteArrayAsync();
        using var uwr = new UnityWebRequest(request.RequestUri, request.Method.Method)
        {
            uploadHandler = body == null ? null : new UploadHandlerRaw(body),
            downloadHandler = new DownloadHandlerBuffer(),
        };
        foreach (var h in request.Headers)
            if (h.Key != "user-agent") uwr.SetRequestHeader(h.Key, string.Join(",", h.Value));
        if (request.Content != null) uwr.SetRequestHeader("content-type", "application/json");

        var op = uwr.SendWebRequest();
        while (!op.isDone) { ct.ThrowIfCancellationRequested(); await Task.Yield(); }

        var res = new HttpResponseMessage((HttpStatusCode)uwr.responseCode)
        {
            Content = new StringContent(uwr.downloadHandler.text ?? ""),
            RequestMessage = request,
        };
        var requestId = uwr.GetResponseHeader("x-request-id");
        if (requestId != null) res.Headers.TryAddWithoutValidation("x-request-id", requestId);
        return res;
    }
}

// var lh = new LayerHopClient(options, new HttpClient(new UnityWebRequestHandler()));
```

## Development

```bash
dotnet test                      # xUnit tests (mocked HttpMessageHandler, no network)
dotnet pack -c Release src/LayerHop
node tools/gen-unity-meta.mjs    # after adding a file under src/LayerHop (Unity needs .meta files)
```

`src/LayerHop` is both the NuGet project and the Unity package. Build output goes to `artifacts/`
(see `Directory.Build.props`) so that Unity never picks up build leftovers.

## License

[MIT](LICENSE)
