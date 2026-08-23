using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace backend.Services;

public class AIAnswerService
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:11434")
    };

    public async IAsyncEnumerable<string> GenerateAnswerStream(
        string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = $"""
You are EchoPrep AI.

Answer ONLY the interview question.

Format:
1. 30-second answer.
2. Detailed answer with a practical example.

Question:
{question}
""";

        var payload = new
        {
            model = "qwen2.5:1.5b",
            prompt,
            stream = true,
            keep_alive = "30m",
            options = new
            {
                temperature = 0.2,
                num_predict = 800,
                num_ctx = 8192
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync();

            if (line is null)
                yield break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            string? chunk = null;
            bool done = false;

            try
            {
                using var doc = JsonDocument.Parse(line);

                if (doc.RootElement.TryGetProperty("response", out var text))
                    chunk = text.GetString();

                if (doc.RootElement.TryGetProperty("done", out var doneProp))
                    done = doneProp.GetBoolean();
            }
            catch
            {
                continue;
            }

            if (!string.IsNullOrEmpty(chunk))
                yield return chunk;

            // Let the stream close naturally after the last chunk
            if (done)
                break;
        }
    }
}