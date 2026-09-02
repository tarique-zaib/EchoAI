using System.Text;
using Microsoft.Extensions.Hosting;

namespace backend.Services;

public class OllamaWarmupService : BackgroundService
{
    private readonly IHttpClientFactory _factory;

    public OllamaWarmupService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the API a moment to finish starting.
        await Task.Delay(1000, stoppingToken);

        try
        {
            var client = _factory.CreateClient();
            client.BaseAddress = new Uri("http://127.0.0.1:11434");

            await client.PostAsync(
                "/api/generate",
                new StringContent(
                    """
                    {
                      "model":"qwen2.5:3b",
                      "prompt":"ready",
                      "stream":false,
                      "keep_alive":"30m"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
                stoppingToken);

            Console.WriteLine("🔥 Ollama warmed up.");
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"⚠️ Ollama warm-up failed: {ex.Message}");
        }
    }
}