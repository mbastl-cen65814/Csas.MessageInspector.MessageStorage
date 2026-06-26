# Csas.MessageInspector.MessageStorage

Knihovna pro ukládání a čtení SOAP a REST zpráv z Message Inspectoru. Zprávy jsou ukládány do souborového systému podle volané URL a slouží jako zdroj dat pro simulátor.

---

## Jak to funguje

**Struktura uložených souborů** (např. pro `https://api.example.com/v1/orders`):
```
MessageStorage/
  api.example.com/
    v1/
      orders/
        550e8400-e29b-41d4-a716-446655440000.request.txt
        550e8400-e29b-41d4-a716-446655440000.response.txt
```

Stejné GUID páruje vždy request a příslušný response. SOAP zprávy jsou uloženy jako XML, REST zprávy jako HTTP text (hlavičky + tělo).

---

## Použití v .NET Core aplikaci

### 1. Registrace služeb

V `Program.cs` nebo `Startup.cs` zaregistrujte služby knihovny:

```csharp
services.AddMessageInspectorStorage(opt =>
{
    opt.IsEnabled = true;                        // zapnutí/vypnutí ukládání
    opt.BaseDirectory = @"C:\Recordings";        // absolutní nebo relativní cesta
});
```

### 2. REST – HttpClient s DelegatingHandler

Přidejte `StoringHttpMessageHandler` do HttpClient pipeline pomocí `IHttpClientFactory`:

```csharp
services.AddHttpClient("MyApiClient", client =>
    {
        client.BaseAddress = new Uri("https://api.example.com");
    })
    .AddHttpMessageHandler<StoringHttpMessageHandler>();
```

Použití v kódu:

```csharp
public class MyService
{
    private readonly HttpClient httpClient;

    public MyService(IHttpClientFactory httpClientFactory)
    {
        httpClient = httpClientFactory.CreateClient("MyApiClient");
    }

    public async Task<string> GetOrderAsync(int id)
    {
        // Request i response jsou automaticky uloženy do souborového systému
        var response = await httpClient.GetAsync($"/v1/orders/{id}");
        return await response.Content.ReadAsStringAsync();
    }
}
```

### 3. SOAP – WCF klient s IEndpointBehavior

Získejte `IMessageStorage` a `MessageStorageOptions` z DI a přidejte behavior na WCF klienta:

```csharp
public class MyWcfService
{
    private readonly IMessageStorage messageStorage;
    private readonly MessageStorageOptions storageOptions;

    public MyWcfService(IMessageStorage messageStorage, MessageStorageOptions storageOptions)
    {
        this.messageStorage = messageStorage;
        this.storageOptions = storageOptions;
    }

    public async Task CallSoapEndpointAsync()
    {
        var client = new MyServiceClient();

        // Přidání Message Inspectoru jako endpoint behavioru
        client.Endpoint.EndpointBehaviors.Add(
            new StoringMessageInspector(messageStorage, storageOptions));

        // SOAP request i response jsou automaticky zachyceny a uloženy
        var result = await client.GetDataAsync(new GetDataRequest { Id = 1 });
    }
}
```

### 4. Čtení uložených zpráv (simulátor)

```csharp
public class SimulatorService
{
    private readonly IMessageStorage messageStorage;

    public SimulatorService(IMessageStorage messageStorage)
    {
        this.messageStorage = messageStorage;
    }

    public void ReplayRecordedMessages(string url)
    {
        // Získání všech zaznamenaných párů pro danou URL
        foreach (var correlationId in messageStorage.GetCorrelationIds(url))
        {
            var request  = messageStorage.ReadRequest(url, correlationId);
            var response = messageStorage.ReadResponse(url, correlationId);

            Console.WriteLine($"--- [{correlationId}] ---");
            Console.WriteLine("REQUEST:");
            Console.WriteLine(request);
            Console.WriteLine("RESPONSE:");
            Console.WriteLine(response);
        }
    }
}
```

---

## Použití v ASP.NET Web Forms aplikaci (.NET Framework)

ASP.NET Web Forms nemá vestavěný DI kontejner. Závislosti proto vytvoříme ručně v `Global.asax.cs` a uložíme jako statické instance.

### 1. Inicializace v Global.asax.cs

```csharp
using Csas.MessageInspector.MessageStorage;
using Csas.MessageInspector.MessageStorage.Rest;
using Csas.MessageInspector.MessageStorage.Soap;

public class Global : HttpApplication
{
    // Statické instance dostupné napříč aplikací
    public static IMessageStorage MessageStorage { get; private set; }
    public static MessageStorageOptions StorageOptions { get; private set; }

    protected void Application_Start(object sender, EventArgs e)
    {
        StorageOptions = new MessageStorageOptions
        {
            IsEnabled = true,
            BaseDirectory = Server.MapPath("~/App_Data/MessageStorage")
        };

        MessageStorage = new FileMessageStorage(
            StorageOptions,
            logger: null);   // null = NullLogger (logování zakázáno)
    }
}
```

### 2. REST – HttpClient s DelegatingHandler

Vytvořte sdílenou instanci `HttpClient` (singleton) s přidaným handlerem:

```csharp
public static class HttpClientFactory
{
    private static readonly Lazy<HttpClient> lazyClient = new Lazy<HttpClient>(CreateClient);

    public static HttpClient Client => lazyClient.Value;

    private static HttpClient CreateClient()
    {
        var handler = new StoringHttpMessageHandler(
            Global.MessageStorage,
            Global.StorageOptions)
        {
            InnerHandler = new HttpClientHandler()
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com")
        };
    }
}
```

Použití v code-behind nebo service třídě:

```csharp
public class OrdersPage : Page
{
    protected async void Page_Load(object sender, EventArgs e)
    {
        // Request i response jsou automaticky uloženy do souborového systému
        var response = await HttpClientFactory.Client.GetAsync("/v1/orders/1");
        var content  = await response.Content.ReadAsStringAsync();
        ResultLabel.Text = content;
    }
}
```

### 3. SOAP – WCF klient s IEndpointBehavior

```csharp
public class OrdersService
{
    public async Task<GetOrderResponse> GetOrderAsync(int orderId)
    {
        var client = new OrderServiceClient();

        // Přidání Message Inspectoru jako endpoint behavioru
        client.Endpoint.EndpointBehaviors.Add(
            new StoringMessageInspector(
                Global.MessageStorage,
                Global.StorageOptions));

        // SOAP request i response jsou automaticky zachyceny a uloženy
        return await client.GetOrderAsync(new GetOrderRequest { Id = orderId });
    }
}
```

### 4. Čtení uložených zpráv (simulátor)

```csharp
public class SimulatorPage : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        var url = "https://api.example.com/v1/orders/1";

        foreach (var correlationId in Global.MessageStorage.GetCorrelationIds(url))
        {
            var request  = Global.MessageStorage.ReadRequest(url, correlationId);
            var response = Global.MessageStorage.ReadResponse(url, correlationId);

            // Zpracování zachycených zpráv pro simulátor
        }
    }
}
```

---

## Konfigurace

| Vlastnost       | Typ      | Výchozí hodnota    | Popis                                                                 |
|-----------------|----------|--------------------|-----------------------------------------------------------------------|
| `IsEnabled`     | `bool`   | `false`            | Zapíná/vypíná ukládání zpráv. Při `false` jsou zprávy přeskočeny.    |
| `BaseDirectory` | `string` | `"MessageStorage"` | Cesta k adresáři úložiště. Relativní cesta se doplní vůči assembly.  |

---

## NuGet závislosti

| Balíček                                           | Verze   | Účel                              |
|---------------------------------------------------|---------|-----------------------------------|
| `Microsoft.Extensions.Logging.Abstractions`       | 2.2.0   | `ILogger` v knihovním kódu        |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 2.2.0 | `IServiceCollection` extension  |
| `System.ServiceModel.Primitives`                  | 4.8.1   | WCF typy (`IClientMessageInspector`, `IEndpointBehavior`) |
