using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MujOpenAiApi.Data;
using MujOpenAiApi.Models;

// HTML, CSS a JavaScript pro jednoduchy frontend.
// Stranka obsahuje chatovaci okno, textarea pro prompt a tlacitko pro odeslani.
const string IndexHtml = """
<!doctype html>
<html lang="cs">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>OpenAI Chat MVP</title>
  <style>
    :root {
      font-family: Arial, sans-serif;
      background: #f4f6f8;
      color: #17202a;
    }

    body {
      margin: 0;
      min-height: 100vh;
      display: grid;
      place-items: center;
    }

    main {
      width: min(900px, calc(100vw - 32px));
      height: min(740px, calc(100vh - 32px));
      display: grid;
      grid-template-rows: auto 1fr auto;
      background: #ffffff;
      border: 1px solid #d7dee8;
      border-radius: 8px;
      overflow: hidden;
    }

    header {
      padding: 16px 20px;
      border-bottom: 1px solid #d7dee8;
      font-weight: 700;
    }

    #messages {
      padding: 20px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .message {
      max-width: 78%;
      padding: 12px 14px;
      border-radius: 8px;
      line-height: 1.45;
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }

    .user {
      align-self: flex-end;
      background: #0f766e;
      color: #ffffff;
    }

    .assistant {
      align-self: flex-start;
      background: #edf2f7;
      color: #17202a;
    }

    form {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 10px;
      padding: 16px;
      border-top: 1px solid #d7dee8;
      background: #ffffff;
    }

    textarea {
      min-height: 46px;
      max-height: 150px;
      resize: vertical;
      border: 1px solid #b8c4d2;
      border-radius: 6px;
      padding: 12px;
      font: inherit;
    }

    button {
      min-width: 100px;
      border: 0;
      border-radius: 6px;
      background: #0f766e;
      color: #ffffff;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }

    button:disabled {
      opacity: .55;
      cursor: wait;
    }
  </style>
</head>
<body>
  <main>
    <header>OpenAI Chat MVP</header>
    <section id="messages">
      <div class="message assistant">Napis prompt a odesli ho do OpenAI API.</div>
    </section>
    <form id="chat-form">
      <textarea id="message" placeholder="Napis prompt..." required></textarea>
      <button id="send" type="submit">Odeslat</button>
    </form>
  </main>

  <script>
    const form = document.getElementById('chat-form');
    const input = document.getElementById('message');
    const messages = document.getElementById('messages');
    const send = document.getElementById('send');
    let currentChatId = null;

    function addMessage(text, cssClass) {
      const element = document.createElement('div');
      element.className = `message ${cssClass}`;
      element.textContent = text;
      messages.appendChild(element);
      messages.scrollTop = messages.scrollHeight;
    }

    form.addEventListener('submit', async (event) => {
      event.preventDefault();

      const text = input.value.trim();
      if (!text) return;

      addMessage(text, 'user');
      input.value = '';
      send.disabled = true;

      try {
        const response = await fetch('/api/chat', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ chatId: currentChatId, message: text })
        });

        const data = await response.json();
        if (response.ok && data.chatId) {
          currentChatId = data.chatId;
        }

        addMessage(response.ok ? data.answer : data.detail || data.error || 'Pozadavek selhal.', 'assistant');
      } catch {
        addMessage('Nepodarilo se zavolat backend aplikace.', 'assistant');
      } finally {
        send.disabled = false;
        input.focus();
      }
    });
  </script>
</body>
</html>
""";

// Vytvoreni builderu pro ASP.NET Core aplikaci.
var builder = WebApplication.CreateBuilder(args);

// Registrace HTTP klienta, ktery bude volat OpenAI API.
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
});

// Registrace EF Core DbContextu pro SQL Server.
// Connection string je v appsettings.json pod ConnectionStrings:DefaultConnection.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Chybi connection string DefaultConnection.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// Sestaveni aplikace z nakonfigurovanych sluzeb.
var app = builder.Build();

// Endpoint pro hlavni stranku s jednoduchym chatovacim frontendem.
app.MapGet("/", () => Results.Content(IndexHtml, "text/html; charset=utf-8"));

// Endpoint, ktery prijme prompt z frontendu, posle ho do OpenAI API a vrati odpoved.
app.MapPost("/api/chat", async (
    ChatRequest request,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    // Zakladni kontrola vstupu, aby se neposilal prazdny prompt.
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "Zadej prompt." });
    }

    // Nacteni API klice. Nejdriv se zkusi appsettings/user-secrets, potom promenna prostredi.
    var apiKey = configuration["OpenAI:ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    }

    // Pokud API klic neni nastaveny, aplikace vrati srozumitelnou chybu.
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Problem(
            "Chybi OpenAI API klic. Nastav OPENAI_API_KEY nebo user-secret OpenAI:ApiKey.",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    // Model jde prepsat v konfiguraci. Default je jednoduchy a levnejsi model pro MVP.
    var model = configuration["OpenAI:Model"] ?? "gpt-4.1-mini";
    var now = DateTime.UtcNow;

    // Vytvoreni noveho chatu, pokud frontend jeste neposlal existujici ChatId.
    var chat = request.ChatId.HasValue
        ? await dbContext.Chats.FirstOrDefaultAsync(item => item.Id == request.ChatId.Value, cancellationToken)
        : null;

    if (chat is null)
    {
        chat = new Chat
        {
            Id = Guid.NewGuid(),
            Title = CreateChatTitle(request.Message),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SourceApp = "MujOpenAiApi",
            MetadataJson = JsonSerializer.Serialize(new
            {
                createdBy = "OpenAI Chat MVP",
                firstModel = model
            })
        };

        dbContext.Chats.Add(chat);
    }

    // Okamzite ulozeni promptu od uzivatele do databaze.
    dbContext.ChatMessages.Add(new ChatMessage
    {
        Id = Guid.NewGuid(),
        ChatId = chat.Id,
        Role = "user",
        Content = request.Message,
        CreatedAtUtc = now,
        MetadataJson = JsonSerializer.Serialize(new
        {
            source = "frontend"
        })
    });

    chat.UpdatedAtUtc = now;
    await dbContext.SaveChangesAsync(cancellationToken);

    // Payload pro OpenAI Responses API. Vstupem je primo prompt od uzivatele.
    var payload = new
    {
        model,
        input = request.Message
    };

    // Sestaveni HTTP requestu vcetne autorizace pres Bearer token.
    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "responses");
    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    httpRequest.Content = JsonContent.Create(payload);

    // Odeslani requestu do OpenAI API a nacteni JSON odpovedi.
    var httpClient = httpClientFactory.CreateClient("OpenAI");
    using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

    // Pri chybe OpenAI API se vrati detail, aby bylo videt, co je spatne.
    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem(
            $"OpenAI API vratilo chybu {(int)response.StatusCode}: {responseJson}",
            statusCode: StatusCodes.Status502BadGateway);
    }

    // Vytazeni textu odpovedi z JSON struktury a vraceni do frontendu.
    var answer = ExtractText(responseJson);
    var answerCreatedAt = DateTime.UtcNow;

    // Okamzite ulozeni odpovedi asistenta do databaze.
    dbContext.ChatMessages.Add(new ChatMessage
    {
        Id = Guid.NewGuid(),
        ChatId = chat.Id,
        Role = "assistant",
        Content = answer,
        CreatedAtUtc = answerCreatedAt,
        Model = model,
        MetadataJson = JsonSerializer.Serialize(new
        {
            source = "openai",
            responseStatusCode = (int)response.StatusCode
        })
    });

    chat.UpdatedAtUtc = answerCreatedAt;
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new ChatResponse(chat.Id, answer));
});

// Spusteni webove aplikace.
app.Run();

// Pomocna metoda, ktera z odpovedi OpenAI API vytahne jen text pro zobrazeni v chatu.
static string ExtractText(string responseJson)
{
    using var document = JsonDocument.Parse(responseJson);
    var root = document.RootElement;

    // Nektere Responses API odpovedi obsahuji pohodlne pole output_text.
    if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
    {
        return outputText.GetString() ?? string.Empty;
    }

    // Fallback pro pripad, kdy je text ulozeny v poli output -> content -> text.
    if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
    {
        return "Nepodarilo se precist odpoved modelu.";
    }

    var textBuilder = new StringBuilder();
    foreach (var outputItem in output.EnumerateArray())
    {
        if (!outputItem.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            continue;
        }

        foreach (var contentItem in content.EnumerateArray())
        {
            if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            {
                textBuilder.AppendLine(text.GetString());
            }
        }
    }

    var result = textBuilder.ToString().Trim();
    return string.IsNullOrWhiteSpace(result) ? "Model nevratil textovou odpoved." : result;
}

// Pomocna metoda pro jednoduchy nazev chatu podle prvni zpravy.
static string CreateChatTitle(string message)
{
    var normalized = message.Trim().ReplaceLineEndings(" ");
    return normalized.Length <= 80 ? normalized : $"{normalized[..77]}...";
}

// Datovy model requestu, ktery posila frontend na backend.
public sealed record ChatRequest(
    [property: JsonPropertyName("chatId")] Guid? ChatId,
    [property: JsonPropertyName("message")] string Message);

// Datovy model response, ktery backend vraci frontendu.
public sealed record ChatResponse(
    [property: JsonPropertyName("chatId")] Guid ChatId,
    [property: JsonPropertyName("answer")] string Answer);
