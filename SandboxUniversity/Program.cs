using System.Net.Http.Json;
using System.Text.Json.Serialization;

// Fake data pro zkusenostni kopii univerzity.
// V realne aplikaci by se lekce cetly z databaze.
var lessons = new[]
{
    new Lesson(
        "funkce",
        "Funkce",
        "Funkce popisuje vztah mezi vstupem a vystupem. Kazdemu vstupu x prirazuje prave jednu hodnotu y. Linearni funkce ma tvar y = ax + b."),
    new Lesson(
        "kombinatorika",
        "Kombinatorika",
        "Kombinatorika resi pocitani moznosti. Zakladni principy jsou soucinove pravidlo, souctove pravidlo, variace, permutace a kombinace."),
    new Lesson(
        "pravdepodobnost",
        "Pravdepodobnost",
        "Pravdepodobnost meri sanci, ze nastane jev. Hodnoty jsou od 0 do 1. Zakladni vzorec je pocet priznvivych vysledku deleno pocet vsech moznych vysledku.")
};

// HTML, CSS a JavaScript pro testovaci univerzitni aplikaci.
const string IndexHtml = """
<!doctype html>
<html lang="cs">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Sandbox University</title>
  <style>
    :root {
      font-family: Arial, sans-serif;
      background: #f5f7fa;
      color: #18212f;
    }

    body {
      margin: 0;
      min-height: 100vh;
    }

    .app {
      min-height: 100vh;
      display: grid;
      grid-template-columns: 240px minmax(320px, 1fr) minmax(340px, 440px);
    }

    aside {
      background: #18212f;
      color: #ffffff;
      padding: 18px;
    }

    aside h1 {
      margin: 0 0 18px;
      font-size: 20px;
    }

    .lesson-button {
      width: 100%;
      margin-bottom: 8px;
      padding: 10px 12px;
      border: 0;
      border-radius: 6px;
      background: #263348;
      color: #ffffff;
      text-align: left;
      cursor: pointer;
      font: inherit;
    }

    .lesson-button.active {
      background: #0f766e;
    }

    .lesson {
      padding: 28px;
      overflow-y: auto;
    }

    .lesson h2 {
      margin: 0 0 14px;
      font-size: 28px;
    }

    .lesson p {
      max-width: 760px;
      line-height: 1.6;
      font-size: 17px;
    }

    .chat {
      display: grid;
      grid-template-rows: auto 1fr auto;
      border-left: 1px solid #d7dee8;
      background: #ffffff;
      min-height: 100vh;
    }

    .chat header {
      padding: 16px;
      border-bottom: 1px solid #d7dee8;
      font-weight: 700;
    }

    #messages {
      padding: 16px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 10px;
    }

    .message {
      max-width: 86%;
      padding: 10px 12px;
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
      color: #18212f;
    }

    form {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 10px;
      padding: 14px;
      border-top: 1px solid #d7dee8;
    }

    textarea {
      min-height: 44px;
      max-height: 140px;
      resize: vertical;
      border: 1px solid #b8c4d2;
      border-radius: 6px;
      padding: 10px;
      font: inherit;
    }

    button[type="submit"] {
      min-width: 92px;
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

    @media (max-width: 900px) {
      .app {
        grid-template-columns: 1fr;
      }

      aside, .lesson, .chat {
        min-height: auto;
      }
    }
  </style>
</head>
<body>
  <div class="app">
    <aside>
      <h1>Sandbox University</h1>
      <div id="lesson-list"></div>
    </aside>

    <main class="lesson">
      <h2 id="lesson-title"></h2>
      <p id="lesson-content"></p>
    </main>

    <section class="chat">
      <header id="chat-title">Chat k lekci</header>
      <div id="messages"></div>
      <form id="chat-form">
        <textarea id="message" placeholder="Zeptej se k aktualni lekci..." required></textarea>
        <button id="send" type="submit">Odeslat</button>
      </form>
    </section>
  </div>

  <script>
    const lessonList = document.getElementById('lesson-list');
    const lessonTitle = document.getElementById('lesson-title');
    const lessonContent = document.getElementById('lesson-content');
    const chatTitle = document.getElementById('chat-title');
    const messages = document.getElementById('messages');
    const form = document.getElementById('chat-form');
    const input = document.getElementById('message');
    const send = document.getElementById('send');

    let lessons = [];
    let activeLesson = null;
    const chatIds = JSON.parse(localStorage.getItem('sandboxUniversityChatIds') || '{}');

    function saveChatIds() {
      localStorage.setItem('sandboxUniversityChatIds', JSON.stringify(chatIds));
    }

    function addMessage(text, cssClass) {
      const element = document.createElement('div');
      element.className = `message ${cssClass}`;
      element.textContent = text;
      messages.appendChild(element);
      messages.scrollTop = messages.scrollHeight;
    }

    // Prepnuti lekce aktualizuje obsah lekce a nasledne obnovi chat z databaze podle ulozeneho chatId.
    async function selectLesson(lesson) {
      activeLesson = lesson;
      lessonTitle.textContent = lesson.title;
      lessonContent.textContent = lesson.content;
      chatTitle.textContent = `Chat k lekci: ${lesson.title}`;
      messages.innerHTML = '';
      addMessage('Nacitam historii chatu...', 'assistant');

      document.querySelectorAll('.lesson-button').forEach(button => {
        button.classList.toggle('active', button.dataset.lessonId === lesson.id);
      });

      await loadLessonChatMessages(lesson);
    }

    // Obnova historie chatu pri navratu na lekci.
    // ChatId se drzi v localStorage, samotne zpravy se nacitaji z MujOpenAiApi databaze.
    async function loadLessonChatMessages(lesson) {
      const chatId = chatIds[lesson.id];
      messages.innerHTML = '';

      if (!chatId) {
        addMessage(`Jsem pripraven odpovidat k lekci ${lesson.title}.`, 'assistant');
        return;
      }

      try {
        const response = await fetch(`/api/lessons/${lesson.id}/chat/messages?chatId=${encodeURIComponent(chatId)}`);
        const data = await response.json();

        if (!response.ok) {
          addMessage(data.detail || data.error || 'Nepodarilo se nacist historii chatu.', 'assistant');
          return;
        }

        if (data.length === 0) {
          addMessage(`Jsem pripraven odpovidat k lekci ${lesson.title}.`, 'assistant');
          return;
        }

        data.forEach(message => {
          addMessage(message.content, message.role === 'user' ? 'user' : 'assistant');
        });
      } catch {
        addMessage('Nepodarilo se nacist historii chatu.', 'assistant');
      }
    }

    async function loadLessons() {
      const response = await fetch('/api/lessons');
      lessons = await response.json();

      lessonList.innerHTML = '';
      lessons.forEach(lesson => {
        const button = document.createElement('button');
        button.className = 'lesson-button';
        button.dataset.lessonId = lesson.id;
        button.textContent = lesson.title;
        button.addEventListener('click', () => {
          selectLesson(lesson);
        });
        lessonList.appendChild(button);
      });

      await selectLesson(lessons[0]);
    }

    form.addEventListener('submit', async (event) => {
      event.preventDefault();

      const text = input.value.trim();
      if (!text || !activeLesson) return;

      addMessage(text, 'user');
      input.value = '';
      send.disabled = true;

      try {
        const response = await fetch(`/api/lessons/${activeLesson.id}/chat`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            chatId: chatIds[activeLesson.id] || null,
            message: text
          })
        });

        const data = await response.json();
        if (response.ok) {
          chatIds[activeLesson.id] = data.chatId;
          saveChatIds();
          addMessage(data.answer, 'assistant');
        } else {
          addMessage(data.detail || data.error || 'Pozadavek selhal.', 'assistant');
        }
      } catch {
        addMessage('Nepodarilo se zavolat SandboxUniversity backend.', 'assistant');
      } finally {
        send.disabled = false;
        input.focus();
      }
    });

    loadLessons();
  </script>
</body>
</html>
""";

var builder = WebApplication.CreateBuilder(args);

// HTTP klient pro volani hlavni MujOpenAiApi sluzby.
builder.Services.AddHttpClient("MujOpenAiApi", client =>
{
    var baseUrl = builder.Configuration["MujOpenAiApi:BaseUrl"] ?? "http://localhost:5241";
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

// Hlavni obrazovka sandbox univerzity.
app.MapGet("/", () => Results.Content(IndexHtml, "text/html; charset=utf-8"));

// Endpoint pro seznam testovacich lekci.
app.MapGet("/api/lessons", () => Results.Ok(lessons));

// Proxy endpoint pro obnovu historie chatu pri prepinani lekci.
// Sandbox necte SQL databazi primo; historii si vyzada z MujOpenAiApi podle chatId.
app.MapGet("/api/lessons/{lessonId}/chat/messages", async (
    string lessonId,
    Guid chatId,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    var lessonExists = lessons.Any(item => item.Id.Equals(lessonId, StringComparison.OrdinalIgnoreCase));
    if (!lessonExists)
    {
        return Results.NotFound(new { error = "Lekce neexistuje." });
    }

    var httpClient = httpClientFactory.CreateClient("MujOpenAiApi");
    using var response = await httpClient.GetAsync($"/api/chats/{chatId}/messages", cancellationToken);
    var responseJson = await response.Content.ReadFromJsonAsync<List<MujOpenAiChatMessageResponse>>(cancellationToken);

    if (!response.IsSuccessStatusCode || responseJson is null)
    {
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        return Results.Problem(
            $"MujOpenAiApi vratilo chybu {(int)response.StatusCode}: {rawResponse}",
            statusCode: StatusCodes.Status502BadGateway);
    }

    return Results.Ok(responseJson);
});

// Proxy endpoint: chat u lekce posle lesson kontext do MujOpenAiApi.
app.MapPost("/api/lessons/{lessonId}/chat", async (
    string lessonId,
    LessonChatRequest request,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    var lesson = lessons.FirstOrDefault(item => item.Id.Equals(lessonId, StringComparison.OrdinalIgnoreCase));
    if (lesson is null)
    {
        return Results.NotFound(new { error = "Lekce neexistuje." });
    }

    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "Zadej zpravu." });
    }

    var payload = new MujOpenAiChatRequest(
        request.ChatId,
        lesson.Title,
        lesson.Content,
        request.Message);

    var httpClient = httpClientFactory.CreateClient("MujOpenAiApi");
    using var response = await httpClient.PostAsJsonAsync("/api/chat", payload, cancellationToken);
    var responseJson = await response.Content.ReadFromJsonAsync<MujOpenAiChatResponse>(cancellationToken);

    if (!response.IsSuccessStatusCode || responseJson is null)
    {
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        return Results.Problem(
            $"MujOpenAiApi vratilo chybu {(int)response.StatusCode}: {rawResponse}",
            statusCode: StatusCodes.Status502BadGateway);
    }

    return Results.Ok(responseJson);
});

app.Run();

public sealed record Lesson(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("content")] string Content);

public sealed record LessonChatRequest(
    [property: JsonPropertyName("chatId")] Guid? ChatId,
    [property: JsonPropertyName("message")] string Message);

public sealed record MujOpenAiChatRequest(
    [property: JsonPropertyName("chatId")] Guid? ChatId,
    [property: JsonPropertyName("contextTitle")] string ContextTitle,
    [property: JsonPropertyName("contextContent")] string ContextContent,
    [property: JsonPropertyName("message")] string Message);

public sealed record MujOpenAiChatResponse(
    [property: JsonPropertyName("chatId")] Guid ChatId,
    [property: JsonPropertyName("answer")] string Answer);

public sealed record MujOpenAiChatMessageResponse(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc);
