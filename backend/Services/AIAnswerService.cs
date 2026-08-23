using System.Text;
using System.Text.Json;

namespace backend.Services;

public class AIAnswerService
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:11434")
    };

    public async Task<string> GenerateAnswer(string question)
    {
        var prompt = $"""
You are EchoPrep AI.

Answer this interview question professionally.

Give:
1. A 30-second answer.
2. A detailed answer with a practical example.

Question:
{question}
""";

        var payload = new
        {
            model = "qwen2.5:1.5b",
            prompt,
            stream = false
        };

        var response = await _http.PostAsync(
            "/api/generate",
            new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"));

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.GetProperty("response").GetString() ?? "";
    }
}