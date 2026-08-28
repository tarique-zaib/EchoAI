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

    private CancellationTokenSource? _currentGenerationCts;
    private readonly object _generationLock = new();

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
        string mode = "quick",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CancellationToken token;

        lock (_generationLock)
        {
            _currentGenerationCts?.Cancel();
            _currentGenerationCts?.Dispose();

            _currentGenerationCts =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            token = _currentGenerationCts.Token;
        }

        ResumeProfile? profile = _memory.Get();

        var prompt = _promptBuilder.Build(question, profile, mode);

        prompt += mode.ToLower() switch
        {
            "quick" => """

================ QUICK MODE ================

You are the interview candidate.

Rules:
- Speak naturally.
- Maximum 70 words.
- No unnecessary introduction.

Output exactly:

## 30-Second Answer

============================================

""",

            "detailed" => """

================ DETAILED MODE ================

Explain like a senior engineer teaching the topic.

Output exactly:

## Summary

## Detailed Explanation

## Practical Example

## Best Practice

Keep it under 220 words.

===============================================

""",

            "interview" => """

================ INTERVIEW MODE ================

Answer exactly as if speaking to the interviewer.

Output exactly:

## Interview Answer

## Practical Example

## Likely Follow-up

## Interview Tip

Keep it concise and conversational.

===============================================

""",

            _ => ""
        };

        Console.WriteLine($"\n=== OLLAMA MODE: {mode.ToUpper()} ===");
        Console.WriteLine(prompt[..Math.Min(prompt.Length, 2500)]);
        Console.WriteLine("=====================================\n");

        var payload = new
        {
            model = "qwen2.5:3b",
            prompt,
            stream = true,
            keep_alive = "30m",
            options = new
            {
                temperature = mode switch
                {
                    "quick" => 0.05,
                    "detailed" => 0.12,
                    "interview" => 0.18,
                    _ => 0.10
                },
                top_p = 0.8,
                repeat_penalty = 1.15,
                num_predict = mode switch
                {
                    "quick" => 120,
                    "detailed" => 480,
                    "interview" => 380,
                    _ => 250
                },
                num_ctx = 4096
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            token);

        response.EnsureSuccessStatusCode();
        Console.WriteLine($"⚡ Ollama headers: {sw.ElapsedMilliseconds} ms");

        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = new StreamReader(stream);
        bool firstToken = true;

        while (true)
        {
            if (token.IsCancellationRequested)
                yield break;

            var line = await reader.ReadLineAsync();

            if (token.IsCancellationRequested || line is null)
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
            {
                if (firstToken)
                {
                    Console.WriteLine($"🚀 First token: {sw.ElapsedMilliseconds} ms");
                    firstToken = false;
                }

                yield return chunk;
            }

            if (done)
                break;
        }
    }
}