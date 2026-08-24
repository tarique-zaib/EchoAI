using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using backend.Hubs;

namespace backend.Services;

public class PythonWorkerService
{
    private readonly IHubContext<InterviewHub> _hub;
    private readonly AIAnswerService _ai;

    private Process? _python;

    private readonly SemaphoreSlim _aiLock = new(1, 1);
    private readonly object _bufferLock = new();

    private string _currentQuestion = "";
    private CancellationTokenSource? _debounceCts;
    private string _lastAiAnswer = "";

    public PythonWorkerService(
        IHubContext<InterviewHub> hub,
        AIAnswerService ai)
    {
        _hub = hub;
        _ai = ai;
    }

    // --------------------------------------------
    // NEW: Better interview question detection
    // --------------------------------------------
    private static bool IsLikelyInterviewQuestion(string text)
    {
        text = text.Trim();

        if (text.Length < 8)
            return false;

        string lower = text.ToLowerInvariant();

        // Common candidate filler phrases
        string[] reject =
        {
            "i think",
            "let me",
            "yeah",
            "okay",
            "ok",
            "right",
            "basically",
            "actually",
            "we can",
            "we will",
            "i have",
            "it will",
            "not ok"
        };

        if (reject.Any(lower.StartsWith))
            return false;

        // Strong interview question signals
        if (text.EndsWith("?"))
            return true;

        string[] starters =
        {
            "what",
            "how",
            "why",
            "when",
            "where",
            "which",
            "who",
            "tell me",
            "explain",
            "describe",
            "compare",
            "walk me through",
            "can you",
            "could you"
        };

        return starters.Any(lower.StartsWith);
    }

    public void Start()
    {
        if (_python is { HasExited: false })
        {
            Console.WriteLine("Python already running.");
            return;
        }

        var backendPath = Directory.GetCurrentDirectory();
        var projectRoot = Directory.GetParent(backendPath)!.FullName;

        var workerPath = Path.Combine(projectRoot, "ai-worker");
        var pythonExe = Path.Combine(projectRoot, "venv", "Scripts", "python.exe");
        var script = Path.Combine(workerPath, "stream_transcribe.py");

        Console.WriteLine("========== EchoPrep Debug ==========");
        Console.WriteLine($"ProjectRoot : {projectRoot}");
        Console.WriteLine($"WorkerPath  : {workerPath}");
        Console.WriteLine($"PythonExe   : {pythonExe}");
        Console.WriteLine($"Script      : {script}");
        Console.WriteLine($"Python Exists : {File.Exists(pythonExe)}");
        Console.WriteLine($"Script Exists : {File.Exists(script)}");
        Console.WriteLine("====================================");

        if (!File.Exists(pythonExe))
            throw new FileNotFoundException($"Python not found: {pythonExe}");

        if (!File.Exists(script))
            throw new FileNotFoundException($"Script not found: {script}");

        foreach (var p in Process.GetProcessesByName("python"))
        {
            try { p.Kill(true); }
            catch { }
        }

        _python = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{script}\"",
                WorkingDirectory = workerPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _python.Start();

        Console.WriteLine($"Python PID: {_python.Id}");

        _ = Task.Run(ReadPythonOutputAsync);
        _ = Task.Run(ReadPythonErrorAsync);
    }

    private async Task ReadPythonOutputAsync()
    {
        if (_python == null)
            return;

        while (!_python.HasExited)
        {
            var line = await _python.StandardOutput.ReadLineAsync();

            if (line == null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            Console.WriteLine($"PYTHON: {line}");

            if (line.StartsWith("Loading model"))
            {
                await _hub.Clients.All.SendAsync("ReceiveStatus", "Loading model...");
                continue;
            }

            if (line.StartsWith("Listening"))
            {
                await _hub.Clients.All.SendAsync("ReceiveStatus", "Listening");
                continue;
            }

            if (line.StartsWith("Using Windows output"))
                continue;

            // Prevent EchoPrep from reacting to its own answer
            if (!string.IsNullOrWhiteSpace(_lastAiAnswer))
            {
                var transcript = line.Trim().ToLowerInvariant();
                var answer = _lastAiAnswer.ToLowerInvariant();

                if (transcript.Length > 12 &&
                    (answer.Contains(transcript) ||
                     transcript.Contains(answer[..Math.Min(40, answer.Length)])))
                {
                    Console.WriteLine($"Ignored repeated speech: {line}");
                    continue;
                }
            }

            // Always show transcript in UI
            await _hub.Clients.All.SendAsync("ReceiveTranscript", line);

            // NEW: Only interviewer-like questions trigger AI
            if (!IsLikelyInterviewQuestion(line))
                continue;

            lock (_bufferLock)
            {
                _currentQuestion = line;
            }

            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();

            _ = ProcessFinalQuestion(_debounceCts.Token);
        }
    }

    private async Task ProcessFinalQuestion(CancellationToken token)
    {
        try
        {
            await Task.Delay(1200, token);

            string question;

            lock (_bufferLock)
            {
                question = _currentQuestion.Trim();
            }

            if (question.Length < 5)
                return;

            if (!await _aiLock.WaitAsync(0, token))
                return;

            try
            {
                Console.WriteLine("Generating AI answer...");

                await _hub.Clients.All.SendAsync("ClearAnswer");
                await _hub.Clients.All.SendAsync("AnswerStarted");
                await _hub.Clients.All.SendAsync("ReceiveStatus", "AI Answering");

                var builder = new StringBuilder();

                await foreach (var chunk in _ai.GenerateAnswerStream(question, token))
                {
                    builder.Append(chunk);
                    await _hub.Clients.All.SendAsync("ReceiveAnswerChunk", chunk);
                }

                _lastAiAnswer = builder.ToString();

                Console.WriteLine("AI answer completed.");

                await _hub.Clients.All.SendAsync("AnswerCompleted");
                await _hub.Clients.All.SendAsync("ReceiveStatus", "Listening");
            }
            finally
            {
                _aiLock.Release();
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AI ERROR: {ex.Message}");
        }
    }

    private async Task ReadPythonErrorAsync()
    {
        if (_python == null)
            return;

        while (!_python.HasExited)
        {
            var line = await _python.StandardError.ReadLineAsync();

            if (line == null)
                break;

            if (!string.IsNullOrWhiteSpace(line))
                Console.WriteLine($"PYTHON ERR: {line}");
        }
    }

    public void Stop()
    {
        if (_python == null)
            return;

        try
        {
            if (!_python.HasExited)
            {
                _python.Kill(true);
                _python.WaitForExit();
            }
        }
        catch
        {
        }

        _python.Dispose();
        _python = null;

        Console.WriteLine("Python worker stopped.");
    }
}