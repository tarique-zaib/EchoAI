using Microsoft.AspNetCore.SignalR;
using System.Text;
using System.Text.Json;
using backend.Hubs;

public class VisionService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly IHubContext<InterviewHub> _hub;

    public VisionService(
        HttpClient http,
        IConfiguration config,
        IHubContext<InterviewHub> hub)
    {
        _http = http;
        _config = config;
        _hub = hub;
    }

    public async Task ExplainImage(string imagePath, string mode = "quick")
    {
        await _hub.Clients.All.SendAsync("ClearAnswer");

        if (!File.Exists(imagePath))
        {
            await _hub.Clients.All.SendAsync(
                "ReceiveAnswerChunk",
                "Captured image not found.");

            await _hub.Clients.All.SendAsync("VisionCompleted");
            return;
        }

        try
        {
            var text = await GenerateExplanation(imagePath, mode);

            foreach (var word in text.Split(' '))
            {
                await _hub.Clients.All.SendAsync(
                    "ReceiveAnswerChunk",
                    word + " ");

                await Task.Delay(18);
            }
        }
        catch (Exception ex)
        {
            await _hub.Clients.All.SendAsync(
                "ReceiveAnswerChunk",
                "Vision error: " + ex.Message);
        }

        await _hub.Clients.All.SendAsync("VisionCompleted");
    }

    private async Task<string> GenerateExplanation(string imagePath, string mode)
    {
        var bytes = await File.ReadAllBytesAsync(imagePath);
        var base64 = Convert.ToBase64String(bytes);

        var apiKey = _config["Gemini:ApiKey"];

        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key={apiKey}";

        // Smart Answer Modes
        var modeInstruction = mode.ToLower() switch
        {
            "quick" =>
                "Give a concise interview-ready explanation in under 30 seconds (2–4 spoken sentences).",

            "detailed" =>
                "Give a detailed explanation with examples, production use cases, and code when relevant.",

            "interview" =>
                "Answer exactly as if speaking in a live technical interview. Include a concise answer, one practical example, and one likely follow-up interview question.",

            _ =>
                "Answer naturally."
        };

        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new
                        {
                            text =
@"You are EchoPrepAI, a concise technical explainer.

Answer Mode:
" + modeInstruction + @"

Analyze the screenshot and automatically identify whether it is:
- SQL
- C#
- .NET
- React
- Azure
- System Design
- UI/UX
- Error Message
- Documentation
- General Technical Content

Respond in this exact format.

## Summary
One sentence describing what is on the screen.

## Answer
Provide the explanation according to the selected Answer Mode.

## Code
If code is relevant, return one clean code block only.

## Key Points
- Bullet 1
- Bullet 2
- Bullet 3

Rules:
- Do not repeat the question.
- Preserve code formatting.
- If the screenshot is blurry, mention what is readable instead of guessing."
                        },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = "image/png",
                                data = base64
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(body);

        var response = await _http.PostAsync(
            url,
            new StringContent(json, Encoding.UTF8, "application/json"));

        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                using var errorDoc = JsonDocument.Parse(responseText);

                var message = errorDoc.RootElement
                    .GetProperty("error")
                    .GetProperty("message")
                    .GetString();

                throw new Exception(message);
            }
            catch
            {
                throw new Exception(responseText);
            }
        }

        using var doc = JsonDocument.Parse(responseText);

        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()!;
    }
}