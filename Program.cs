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

// Endpoint pro nacteni ulozene historie konkretniho chatu z databaze.
// Pouziva ho napr. SandboxUniversity pri prepinani mezi lekcemi, aby znovu vykreslil chat.
app.MapGet("/api/chats/{chatId:guid}/messages", async (
    Guid chatId,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var chatExists = await dbContext.Chats
        .AsNoTracking()
        .AnyAsync(chat => chat.Id == chatId, cancellationToken);

    if (!chatExists)
    {
        return Results.NotFound(new { error = "Chat neexistuje." });
    }

    var messages = await dbContext.ChatMessages
        .AsNoTracking()
        .Where(message => message.ChatId == chatId)
        .OrderBy(message => message.CreatedAtUtc)
        .Select(message => new ChatMessageResponse(
            message.Role,
            message.Content,
            message.CreatedAtUtc))
        .ToListAsync(cancellationToken);

    return Results.Ok(messages);
});

// Endpoint pro nacteni vytvorenych agentnich vystupu, napr. testu a rozvrhu.
// SandboxUniversity ho pouziva pro MVC views, aby nemusel sahat primo do SQL databaze.
app.MapGet("/api/artifacts", async (
    string? lessonId,
    string? type,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var query = dbContext.GeneratedArtifacts
        .AsNoTracking()
        .AsQueryable();

    if (!string.IsNullOrWhiteSpace(lessonId))
    {
        query = query.Where(artifact => artifact.LessonId == lessonId);
    }

    if (!string.IsNullOrWhiteSpace(type))
    {
        query = query.Where(artifact => artifact.Type == type);
    }

    var artifacts = await query
        .OrderByDescending(artifact => artifact.CreatedAtUtc)
        .Select(artifact => new ArtifactResponse(
            artifact.Id,
            artifact.LessonId,
            artifact.Type,
            artifact.Title,
            artifact.ContentJson,
            artifact.CreatedAtUtc))
        .ToListAsync(cancellationToken);

    return Results.Ok(artifacts);
});

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

    // Nacteni poslednich zprav z aktualniho chatu pro kontext modelu.
    // Model sam o sobe stav nedrzi, proto mu historii posilame z databaze pri kazdem requestu.
    var historyMessageLimit = configuration.GetValue("OpenAI:HistoryMessageLimit", 20);
    var chatHistory = await dbContext.ChatMessages
        .AsNoTracking()
        .Where(message => message.ChatId == chat.Id)
        .OrderByDescending(message => message.CreatedAtUtc)
        .Take(historyMessageLimit)
        .OrderBy(message => message.CreatedAtUtc)
        .Select(message => new OpenAiInputMessage(message.Role, message.Content))
        .ToListAsync(cancellationToken);

    if (!string.IsNullOrWhiteSpace(request.ContextTitle) || !string.IsNullOrWhiteSpace(request.ContextContent))
    {
        chatHistory.Insert(0, new OpenAiInputMessage("system", $"""
            Kontext aktualni lekce:
            Nazev: {request.ContextTitle}

            Obsah lekce:
            {request.ContextContent}

            Odpovidej primarne v kontextu teto lekce. Pokud se uzivatel pta mimo lekci,
            upozorni ho a vrat odpoved zpet k tematu lekce.
            """));
    }

    // Payload pro OpenAI Responses API. Vstupem je historie chatu, ne jen posledni prompt.
    var payload = new
    {
        model,
        instructions = "Odpovidej jako uzitecny asistent. Ber v potaz celou historii konverzace v inputu.",
        input = chatHistory
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

// Agentni endpoint pro akce nad lekci, napr. vytvoreni testove sady nebo rozvrhu.
// Rozdil proti /api/chat: model dostane seznam nastroju a muze pozadat .NET aplikaci o jejich provedeni.
app.MapPost("/api/agent-chat", async (
    AgentChatRequest request,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "Zadej prompt." });
    }

    var apiKey = configuration["OpenAI:ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    }

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.Problem(
            "Chybi OpenAI API klic. Nastav OPENAI_API_KEY nebo user-secret OpenAI:ApiKey.",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    var model = configuration["OpenAI:Model"] ?? "gpt-4.1-mini";
    var now = DateTime.UtcNow;

    // Agentni chat pouziva stejny Chat/ChatMessages model jako bezny chat.
    // Diky tomu zustava historie konverzace spolecna pro lekci i agentni akce.
    var chat = request.ChatId.HasValue
        ? await dbContext.Chats.FirstOrDefaultAsync(item => item.Id == request.ChatId.Value, cancellationToken)
        : null;

    if (chat is null)
    {
        chat = new Chat
        {
            Id = Guid.NewGuid(),
            Title = string.IsNullOrWhiteSpace(request.LessonTitle)
                ? CreateChatTitle(request.Message)
                : request.LessonTitle,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SourceApp = "MujOpenAiApi.Agent",
            MetadataJson = JsonSerializer.Serialize(new
            {
                lessonId = request.LessonId,
                mode = "agent"
            })
        };

        dbContext.Chats.Add(chat);
    }

    // Ulozeni promptu uzivatele jeste pred volanim OpenAI.
    dbContext.ChatMessages.Add(new ChatMessage
    {
        Id = Guid.NewGuid(),
        ChatId = chat.Id,
        Role = "user",
        Content = request.Message,
        CreatedAtUtc = now,
        MetadataJson = JsonSerializer.Serialize(new
        {
            source = "sandbox-agent",
            lessonId = request.LessonId,
            requestedAction = request.RequestedAction
        })
    });

    var agentRun = new AgentRun
    {
        Id = Guid.NewGuid(),
        ChatId = chat.Id,
        LessonId = request.LessonId,
        LessonTitle = request.LessonTitle,
        UserPrompt = request.Message,
        Status = "Started",
        CreatedAtUtc = now,
        MetadataJson = JsonSerializer.Serialize(new
        {
            requestedAction = request.RequestedAction,
            model
        })
    };

    dbContext.AgentRuns.Add(agentRun);
    chat.UpdatedAtUtc = now;
    await dbContext.SaveChangesAsync(cancellationToken);

    // Historie chatu se znovu nacte po ulozeni user message, aby agent videl i aktualni prompt.
    var historyMessageLimit = configuration.GetValue("OpenAI:HistoryMessageLimit", 20);
    var chatHistory = await dbContext.ChatMessages
        .AsNoTracking()
        .Where(message => message.ChatId == chat.Id)
        .OrderByDescending(message => message.CreatedAtUtc)
        .Take(historyMessageLimit)
        .OrderBy(message => message.CreatedAtUtc)
        .Select(message => new OpenAiInputMessage(message.Role, message.Content))
        .ToListAsync(cancellationToken);

    // Kontext lekce se neposila jako user message do DB; slouzi jen modelu pro aktualni agentni rozhodnuti.
    chatHistory.Insert(0, new OpenAiInputMessage("system", $"""
        Kontext aktualni lekce:
        LessonId: {request.LessonId}
        Nazev: {request.LessonTitle}

        Obsah lekce:
        {request.LessonContent}
        """));

    var tools = CreateAgentTools();
    var httpClient = httpClientFactory.CreateClient("OpenAI");

    // Prvni volani modelu: model muze odpovedet textem, nebo pozadat o zavolani toolu.
    var firstPayload = new
    {
        model,
        instructions = """
            Jsi agentni asistent pro univerzitni lekci.
            Pokud uzivatel chce vytvorit testovou sadu, pouzij tool save_test_set.
            Pokud uzivatel chce vytvorit rozvrh nebo studijni plan, pouzij tool save_schedule.
            Vystupy nastroju vytvarej jako navrhy, ktere uzivatel muze pozdeji schvalit.
            """,
        tools,
        input = chatHistory
    };

    var firstResponseJson = await SendOpenAiResponseAsync(httpClient, apiKey, firstPayload, cancellationToken);
    if (!firstResponseJson.IsSuccess)
    {
        agentRun.Status = "Failed";
        agentRun.CompletedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Problem(
            $"OpenAI API vratilo chybu {firstResponseJson.StatusCode}: {firstResponseJson.Body}",
            statusCode: StatusCodes.Status502BadGateway);
    }

    var functionCalls = ExtractFunctionCalls(firstResponseJson.Body);
    if (functionCalls.Count == 0)
    {
        // Pokud model nepouzije tool, chovame se jako bezny chat a ulozime textovou odpoved.
        var directAnswer = ExtractText(firstResponseJson.Body);
        var directAnswerCreatedAt = DateTime.UtcNow;

        dbContext.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatId = chat.Id,
            Role = "assistant",
            Content = directAnswer,
            CreatedAtUtc = directAnswerCreatedAt,
            Model = model,
            MetadataJson = JsonSerializer.Serialize(new
            {
                source = "openai-agent",
                agentRunId = agentRun.Id,
                toolCalls = 0
            })
        });

        agentRun.Status = "CompletedWithoutTool";
        agentRun.CompletedAtUtc = directAnswerCreatedAt;
        chat.UpdatedAtUtc = directAnswerCreatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AgentChatResponse(chat.Id, directAnswer, []));
    }

    // Provedeni tool callu na serveru. Model nerozhoduje primo o DB, jen posle validovatelne argumenty.
    var toolOutputs = new List<object>();
    var artifactResponses = new List<GeneratedArtifactResponse>();
    foreach (var functionCall in functionCalls)
    {
        var toolResult = ExecuteAgentTool(functionCall, request, agentRun.Id, dbContext);
        toolOutputs.Add(new
        {
            type = "function_call_output",
            call_id = functionCall.CallId,
            output = toolResult.OutputJson
        });

        if (toolResult.Artifact is not null)
        {
            artifactResponses.Add(new GeneratedArtifactResponse(
                toolResult.Artifact.Id,
                toolResult.Artifact.Type,
                toolResult.Artifact.Title,
                toolResult.Artifact.ContentJson));
        }
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    // Druhe volani modelu: posleme mu puvodni historii, jeho function_call itemy a vystupy nastroju.
    var finalInput = new List<object>();
    finalInput.AddRange(chatHistory);
    finalInput.AddRange(ExtractOutputItems(firstResponseJson.Body).Select(item => (object)item));
    finalInput.AddRange(toolOutputs);

    var finalPayload = new
    {
        model,
        instructions = "Vrat strucnou finalni odpoved uzivateli a rekni, co bylo vytvoreno.",
        tools,
        input = finalInput
    };

    var finalResponseJson = await SendOpenAiResponseAsync(httpClient, apiKey, finalPayload, cancellationToken);
    if (!finalResponseJson.IsSuccess)
    {
        agentRun.Status = "ToolCompletedFinalResponseFailed";
        agentRun.CompletedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Problem(
            $"OpenAI API vratilo chybu {finalResponseJson.StatusCode}: {finalResponseJson.Body}",
            statusCode: StatusCodes.Status502BadGateway);
    }

    var answer = ExtractText(finalResponseJson.Body);
    var answerCreatedAt = DateTime.UtcNow;

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
            source = "openai-agent",
            agentRunId = agentRun.Id,
            toolCalls = functionCalls.Count
        })
    });

    agentRun.Status = "Completed";
    agentRun.CompletedAtUtc = answerCreatedAt;
    chat.UpdatedAtUtc = answerCreatedAt;
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new AgentChatResponse(chat.Id, answer, artifactResponses));
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

// Definice agentnich nastroju, ktere muze model zavolat.
// Tady modelu popisujeme jen schema; skutecne ulozeni do DB dela az nas C# kod.
static object[] CreateAgentTools()
{
    return
    [
        new
        {
            type = "function",
            name = "save_test_set",
            description = "Ulozi navrh testove sady pro aktualni lekci.",
            parameters = new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    title = new { type = "string", description = "Nazev testove sady." },
                    description = new { type = "string", description = "Kratky popis testove sady." },
                    questions = new
                    {
                        type = "array",
                        description = "Seznam testovych otazek.",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                question = new { type = "string" },
                                answer = new { type = "string" },
                                difficulty = new { type = "string", description = "easy, medium nebo hard" }
                            },
                            required = new[] { "question", "answer", "difficulty" }
                        }
                    }
                },
                required = new[] { "title", "description", "questions" }
            }
        },
        new
        {
            type = "function",
            name = "save_schedule",
            description = "Ulozi navrh rozvrhu nebo studijniho planu pro aktualni lekci.",
            parameters = new
            {
                type = "object",
                additionalProperties = false,
                properties = new
                {
                    title = new { type = "string", description = "Nazev rozvrhu." },
                    description = new { type = "string", description = "Kratky popis rozvrhu." },
                    items = new
                    {
                        type = "array",
                        description = "Jednotlive casti rozvrhu.",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                order = new { type = "integer" },
                                title = new { type = "string" },
                                durationMinutes = new { type = "integer" },
                                activity = new { type = "string" }
                            },
                            required = new[] { "order", "title", "durationMinutes", "activity" }
                        }
                    }
                },
                required = new[] { "title", "description", "items" }
            }
        }
    ];
}

// Spolecna metoda pro volani OpenAI Responses API.
static async Task<OpenAiHttpResult> SendOpenAiResponseAsync(
    HttpClient httpClient,
    string apiKey,
    object payload,
    CancellationToken cancellationToken)
{
    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "responses");
    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    httpRequest.Content = JsonContent.Create(payload);

    using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

    return new OpenAiHttpResult(response.IsSuccessStatusCode, (int)response.StatusCode, responseJson);
}

// Vytahne function_call polozky z odpovedi modelu.
static List<OpenAiFunctionCall> ExtractFunctionCalls(string responseJson)
{
    using var document = JsonDocument.Parse(responseJson);
    if (!document.RootElement.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
    {
        return [];
    }

    var calls = new List<OpenAiFunctionCall>();
    foreach (var item in output.EnumerateArray())
    {
        if (!item.TryGetProperty("type", out var type) || type.GetString() != "function_call")
        {
            continue;
        }

        var callId = item.TryGetProperty("call_id", out var callIdElement) ? callIdElement.GetString() : null;
        var name = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        var arguments = item.TryGetProperty("arguments", out var argumentsElement) ? argumentsElement.GetString() : null;

        if (!string.IsNullOrWhiteSpace(callId) && !string.IsNullOrWhiteSpace(name))
        {
            calls.Add(new OpenAiFunctionCall(callId, name, arguments ?? "{}"));
        }
    }

    return calls;
}

// Vytahne cele output itemy z odpovedi modelu, aby sly poslat zpet spolu s function_call_output.
static List<JsonElement> ExtractOutputItems(string responseJson)
{
    using var document = JsonDocument.Parse(responseJson);
    if (!document.RootElement.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
    {
        return [];
    }

    return output.EnumerateArray()
        .Select(item => item.Clone())
        .ToList();
}

// Provedeni agentniho toolu v .NET aplikaci.
// Model navrhne obsah artifactu, ale az tahle metoda rozhoduje, co se opravdu ulozi do DB.
static AgentToolExecutionResult ExecuteAgentTool(
    OpenAiFunctionCall functionCall,
    AgentChatRequest request,
    Guid agentRunId,
    AppDbContext dbContext)
{
    var now = DateTime.UtcNow;
    var artifactType = functionCall.Name switch
    {
        "save_test_set" => "TestSet",
        "save_schedule" => "Schedule",
        _ => "Unknown"
    };

    if (artifactType == "Unknown")
    {
        var unknownResult = JsonSerializer.Serialize(new
        {
            saved = false,
            error = $"Unknown tool: {functionCall.Name}"
        });

        dbContext.AgentActions.Add(new AgentAction
        {
            Id = Guid.NewGuid(),
            AgentRunId = agentRunId,
            ToolName = functionCall.Name,
            ArgumentsJson = functionCall.ArgumentsJson,
            ResultJson = unknownResult,
            CreatedAtUtc = now
        });

        return new AgentToolExecutionResult(unknownResult, null);
    }

    var title = TryGetJsonString(functionCall.ArgumentsJson, "title")
        ?? $"{artifactType} - {request.LessonTitle}";

    var artifact = new GeneratedArtifact
    {
        Id = Guid.NewGuid(),
        AgentRunId = agentRunId,
        LessonId = request.LessonId,
        Type = artifactType,
        Title = title,
        ContentJson = functionCall.ArgumentsJson,
        CreatedAtUtc = now
    };

    var resultJson = JsonSerializer.Serialize(new
    {
        saved = true,
        artifactId = artifact.Id,
        artifactType = artifact.Type,
        title = artifact.Title
    });

    dbContext.GeneratedArtifacts.Add(artifact);
    dbContext.AgentActions.Add(new AgentAction
    {
        Id = Guid.NewGuid(),
        AgentRunId = agentRunId,
        ToolName = functionCall.Name,
        ArgumentsJson = functionCall.ArgumentsJson,
        ResultJson = resultJson,
        CreatedAtUtc = now
    });

    return new AgentToolExecutionResult(resultJson, artifact);
}

static string? TryGetJsonString(string json, string propertyName)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(propertyName, out var property)
            ? property.GetString()
            : null;
    }
    catch (JsonException)
    {
        return null;
    }
}

// Datovy model requestu, ktery posila frontend na backend.
public sealed record ChatRequest(
    [property: JsonPropertyName("chatId")] Guid? ChatId,
    [property: JsonPropertyName("contextTitle")] string? ContextTitle,
    [property: JsonPropertyName("contextContent")] string? ContextContent,
    [property: JsonPropertyName("message")] string Message);

// Datovy model response, ktery backend vraci frontendu.
public sealed record ChatResponse(
    [property: JsonPropertyName("chatId")] Guid ChatId,
    [property: JsonPropertyName("answer")] string Answer);

// Datovy model ulozene zpravy vracene klientovi pri obnove historie chatu.
public sealed record ChatMessageResponse(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc);

// Datovy model jedne zpravy posilane do OpenAI jako historie konverzace.
public sealed record OpenAiInputMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public sealed record AgentChatRequest(
    [property: JsonPropertyName("chatId")] Guid? ChatId,
    [property: JsonPropertyName("lessonId")] string LessonId,
    [property: JsonPropertyName("lessonTitle")] string LessonTitle,
    [property: JsonPropertyName("lessonContent")] string LessonContent,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("requestedAction")] string? RequestedAction);

public sealed record AgentChatResponse(
    [property: JsonPropertyName("chatId")] Guid ChatId,
    [property: JsonPropertyName("answer")] string Answer,
    [property: JsonPropertyName("artifacts")] List<GeneratedArtifactResponse> Artifacts);

public sealed record GeneratedArtifactResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("contentJson")] string ContentJson);

public sealed record ArtifactResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("lessonId")] string LessonId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("contentJson")] string ContentJson,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc);

public sealed record OpenAiHttpResult(
    bool IsSuccess,
    int StatusCode,
    string Body);

public sealed record OpenAiFunctionCall(
    string CallId,
    string Name,
    string ArgumentsJson);

public sealed record AgentToolExecutionResult(
    string OutputJson,
    GeneratedArtifact? Artifact);
