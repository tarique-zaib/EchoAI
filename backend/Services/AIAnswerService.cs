using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using backend.Models;

namespace backend.Services;

public class AIAnswerService
{
    private readonly HttpClient _http;
    private readonly ResumeMemoryService _memory;
    private readonly PromptBuilderService _promptBuilder;

    public AIAnswerService(
        IHttpClientFactory factory,
        ResumeMemoryService memory,
        PromptBuilderService promptBuilder)
    {
        _http = factory.CreateClient();
        _http.BaseAddress = new Uri("http://127.0.0.1:11434");

        _memory = memory;
        _promptBuilder = promptBuilder;
    }

    public async IAsyncEnumerable<string> GenerateAnswerStream(
        string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ResumeProfile? profile = _memory.Get();

        // Build prompt from resume
        var prompt = _promptBuilder.Build(question, profile);

        // Final grounding rules appended here
        prompt += """

====================================================
FINAL GROUNDING RULES (MUST FOLLOW)

- You are the candidate answering a LIVE interview.
- Speak naturally in first person.
- Start answering immediately. Never introduce yourself.
- Use ONLY the evidence already present in the prompt.
- If the selected experience DOES NOT explicitly mention the asked technology,
  DO NOT claim you implemented it there.
- Never invent companies, projects, implementations, APIs, queue sizes,
  architectures, or metrics.
- Never mix information from different jobs.
- If there is no direct evidence, simply explain the concept technically and
  keep the production example generic.

Required output:

## 30-Second Answer

(2-4 spoken sentences)

## Practical Example

(One realistic production example.)

====================================================

""";

        Console.WriteLine("\n========== OLLAMA PROMPT ==========");
        Console.WriteLine(prompt[..Math.Min(prompt.Length, 2500)]);
        Console.WriteLine("===================================\n");

        var payload = new
        {
            model = "qwen2.5:3b",
            prompt,
            stream = true,
            keep_alive = "30m",
            options = new
            {
                temperature = 0.1,
                top_p = 0.8,
                repeat_penalty = 1.2,
                num_predict = 500,
                num_ctx = 12288
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

            if (done)
                break;
        }
    }
}