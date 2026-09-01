using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using backend.Models;

namespace backend.Services;

public class AIAnswerService
{
    private readonly HttpClient _http;
    private readonly ResumeMemoryService _memory;
    private readonly PromptBuilderService _promptBuilder;
    private readonly InterviewMemoryService _interviewMemory;

    private CancellationTokenSource? _currentGenerationCts;
    private readonly object _generationLock = new();

    public AIAnswerService(
        IHttpClientFactory factory,
        ResumeMemoryService memory,
        PromptBuilderService promptBuilder, InterviewMemoryService interviewMemoryService)
    {
        _http = factory.CreateClient();
        _http.BaseAddress = new Uri("http://127.0.0.1:11434");

        _memory = memory;
        _promptBuilder = promptBuilder;
        _interviewMemory = interviewMemoryService;
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

        question = CleanQuestion(question);

        var prompt = _promptBuilder.Build(question, profile, mode);

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
                    "interview" => 180,
                    _ => 250
                },
                num_ctx = 2048
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
        bool completedSuccessfully = false;
        var fullAnswer = new StringBuilder();
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

                fullAnswer.Append(chunk);
                yield return chunk;
            }
            if (done)
            {
                completedSuccessfully = true;

                var answer = fullAnswer.ToString().Trim();

                answer = Regex.Replace(answer, @"^##.*$", "", RegexOptions.Multiline);
                answer = Regex.Replace(answer, @"\n{3,}", "\n\n").Trim();

                if (completedSuccessfully &&
                    !token.IsCancellationRequested &&
                    !string.IsNullOrWhiteSpace(answer))
                {
                    _interviewMemory.Add(question, answer);
                    Console.WriteLine("🧠 Interview memory updated.");
                }

                break;
            }
        }
    }
    private static string CleanQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return question;

        question = Regex.Replace(
            question,
            @"(?is)^current interview context:.*?current question:\s*",
            "");

        return question.Trim();
    }
}