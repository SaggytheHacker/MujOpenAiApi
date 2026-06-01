using Microsoft.AspNetCore.Mvc;
using SandboxUniversity.Models;

namespace SandboxUniversity.Controllers;

public sealed class TestsController(IHttpClientFactory httpClientFactory) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var artifacts = await LoadArtifactsAsync("TestSet", cancellationToken);

        return View(new ArtifactIndexViewModel
        {
            Title = "Vytvorene testy",
            EmptyText = "Zatim nebyla vytvorena zadna testova sada.",
            Artifacts = artifacts
        });
    }

    private async Task<List<ArtifactViewModel>> LoadArtifactsAsync(string type, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient("MujOpenAiApi");
        var artifacts = await httpClient.GetFromJsonAsync<List<ArtifactViewModel>>(
            $"/api/artifacts?type={Uri.EscapeDataString(type)}",
            cancellationToken);

        return artifacts ?? [];
    }
}
