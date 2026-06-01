using Microsoft.AspNetCore.Mvc;
using SandboxUniversity.Models;

namespace SandboxUniversity.Controllers;

public sealed class SchedulesController(IHttpClientFactory httpClientFactory) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var artifacts = await LoadArtifactsAsync("Schedule", cancellationToken);

        return View(new ArtifactIndexViewModel
        {
            Title = "Vytvorene rozvrhy",
            EmptyText = "Zatim nebyl vytvoren zadny rozvrh.",
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
