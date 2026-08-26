using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using backend.Hubs;
using System.Text.RegularExpressions;

namespace backend.Services;

public class PythonWorkerService
{
    private readonly IHubContext<InterviewHub> _hub;
    private readonly AIAnswerService _ai;
    private string? _lastQuestion;
    private string _lastTranscript = "";
    private DateTime _lastTranscriptTime = DateTime.MinValue;

    private Process? _python;
    private volatile bool _suppressCandidateSpeech = false;

    private readonly SemaphoreSlim _aiLock = new(1, 1);
    private readonly object _bufferLock = new();
    private readonly Queue<string> _sessionQuestions = new();
    private readonly Queue<string> _sessionAnswers = new();
    private const int MaxHistory = 3;

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

        var lower = text.ToLowerInvariant();

        string[] starters =
        {
        "what","how","why","when","where","which","who",
        "tell me","explain","describe","compare",
        "walk me through","can you","could you",
        "have you","give me","do you"
    };

        if (starters.Any(lower.StartsWith))
            return true;

        if (text.EndsWith("?"))
            return true;

        if (lower.Contains("difference between"))
            return true;

        if (lower.Contains("experience with"))
            return true;

        if (lower.Contains("worked on"))
            return true;

        // Natural spoken interview question
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 6;
    }

    private static bool IsContinuation(string previous, string current)
    {
        if (string.IsNullOrWhiteSpace(previous))
            return false;

        current = current.Trim().ToLowerInvariant();

        string[] continuationWords =
        {
        "actually",
        "sorry",
        "wait",
        "rather",
        "instead",
        "i mean",
        "let me rephrase",
        "compare",
        "difference",
        "and",
        "also",
        "plus"
    };

        return continuationWords.Any(current.StartsWith);
    }

    private static string CleanTranscript(string text)
    {
        text = text.Trim();

        text = Regex.Replace(
            text,
            @"^Explained\b",
            "Explain",
            RegexOptions.IgnoreCase);

        text = Regex.Replace(text, @"\s+", " ");

        text = Regex.Replace(
            text,
            @"\b(\w+)\s+\1\b",
            "$1",
            RegexOptions.IgnoreCase);

        return text;
    }

    private string _currentMode = "system";

    public void Start(string mode = "system")
    {
        _currentMode = mode;

        // Always start with a clean worker
        Stop();

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
        Console.WriteLine($"Mode        : {mode}");
        Console.WriteLine($"Python Exists : {File.Exists(pythonExe)}");
        Console.WriteLine($"Script Exists : {File.Exists(script)}");
        Console.WriteLine("====================================");

        if (!File.Exists(pythonExe))
            throw new FileNotFoundException($"Python not found: {pythonExe}");

        if (!File.Exists(script))
            throw new FileNotFoundException($"Script not found: {script}");

        _python = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{script}\" --{mode}",
                WorkingDirectory = workerPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        _python.Start();

        Console.WriteLine($"Python PID: {_python.Id} ({mode})");

        _ = Task.Run(ReadPythonOutputAsync);
        _ = Task.Run(ReadPythonErrorAsync);
    }

    public async Task Restart(string mode)
    {
        if (_currentMode == mode)
            return;

        Console.WriteLine($"Restarting Python in {mode} mode...");

        Stop();

        await Task.Delay(500);

        Start(mode);
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

            // Ignore candidate speech while AI is answering.
            // If a brand-new interviewer question starts, stop suppressing immediately.
            if (_suppressCandidateSpeech)
            {
                if (IsLikelyInterviewQuestion(line))
                {
                    _suppressCandidateSpeech = false;
                }
                else
                {
                    Console.WriteLine($"Suppressed candidate speech: {line}");
                    continue;
                }
            }

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

            if (line.StartsWith("Using "))
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

            if (string.Equals(
        line.Trim(),
        _currentQuestion.Trim(),
        StringComparison.OrdinalIgnoreCase))
                continue;

            // NEW: Only interviewer-like questions trigger AI
            var isQuestion = IsLikelyInterviewQuestion(line);
            var isContinuation = IsContinuation(_lastTranscript, line);

            Console.WriteLine($"QUESTION DETECTOR: {isQuestion} | CONT: {isContinuation} | {line}");

            if (!isQuestion && !isContinuation)
                continue;

            line = CleanTranscript(line);

            // Ignore exact duplicates
            if (string.Equals(
                    line,
                    _lastTranscript,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Merge interrupted speech spoken within 2 seconds
            if ((DateTime.Now - _lastTranscriptTime).TotalSeconds < 2 &&
                IsContinuation(_lastTranscript, line))
            {
                line = $"{_lastTranscript} {line}";
            }

            _lastTranscript = line;
            _lastTranscriptTime = DateTime.Now;

            // Update UI with merged transcript
            await _hub.Clients.All.SendAsync("ReceiveTranscript", line);
            _lastQuestion = line;

            lock (_bufferLock)
            {
                _currentQuestion = line;
            }

            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();

            _ = ProcessFinalQuestion(_debounceCts.Token);
        }
    }

    private string BuildContextQuestion(string currentQuestion)
    {
        if (_sessionQuestions.Count == 0)
            return currentQuestion;

        var sb = new StringBuilder();

        sb.AppendLine("Current interview context:");
        sb.AppendLine();

        var questions = _sessionQuestions.ToArray();
        var answers = _sessionAnswers.ToArray();

        for (int i = 0; i < questions.Length; i++)
        {
            sb.AppendLine($"Previous Question: {questions[i]}");
            sb.AppendLine($"Previous Answer: {answers[i][..Math.Min(120, answers[i].Length)]}...");
            sb.AppendLine();
        }

        sb.AppendLine($"Current Question: {currentQuestion}");

        return sb.ToString();
    }

    private async Task ProcessFinalQuestion(CancellationToken token)
    {
        try
        {
            await Task.Delay(900, token);

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
                _suppressCandidateSpeech = true;
                await _hub.Clients.All.SendAsync("AnswerStarted");
                await _hub.Clients.All.SendAsync("ReceiveStatus", "AI Answering");

                var builder = new StringBuilder();
                Console.WriteLine($"🚀 AI using mode: {SettingsController.CurrentMode}");
                await foreach (var chunk in _ai.GenerateAnswerStream(
    BuildContextQuestion(question), SettingsController.CurrentMode,
    token))
                {
                    builder.Append(chunk);
                    await _hub.Clients.All.SendAsync("ReceiveAnswerChunk", chunk);
                }

                _lastAiAnswer = builder.ToString();

                Console.WriteLine("AI answer completed.");

                await _hub.Clients.All.SendAsync("AnswerCompleted");
                _suppressCandidateSpeech = false;

                var finalAnswer = builder.ToString();

                _sessionQuestions.Enqueue(question);
                _sessionAnswers.Enqueue(finalAnswer);

                while (_sessionQuestions.Count > MaxHistory)
                    _sessionQuestions.Dequeue();

                while (_sessionAnswers.Count > MaxHistory)
                    _sessionAnswers.Dequeue();
                await _hub.Clients.All.SendAsync("ReceiveStatus", "Listening");
            }
            finally
            {
                _aiLock.Release();
            }
        }
        catch (TaskCanceledException)
        {
            _suppressCandidateSpeech = false;
        }
        catch (Exception ex)
        {
            _suppressCandidateSpeech = false;
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

        _lastTranscript = "";
        _lastTranscriptTime = DateTime.MinValue;
        _currentQuestion = "";
        _lastAiAnswer = "";
        _suppressCandidateSpeech = false;

        _sessionQuestions.Clear();
        _sessionAnswers.Clear();

        _debounceCts?.Cancel();
        _debounceCts = null;

        Console.WriteLine("Python worker stopped.");
    }

    public async Task RegenerateLastAnswer()
    {
        if (string.IsNullOrWhiteSpace(_lastQuestion))
            return;

        var builder = new StringBuilder();

        Console.WriteLine($"🔄 Regenerating in mode: {SettingsController.CurrentMode}");

        await _hub.Clients.All.SendAsync("ClearAnswer");

        await foreach (var chunk in _ai.GenerateAnswerStream(
            BuildContextQuestion(_lastQuestion),
            SettingsController.CurrentMode,
            CancellationToken.None))
        {
            builder.Append(chunk);
            await _hub.Clients.All.SendAsync("ReceiveAnswerChunk", chunk);
        }

        _lastAiAnswer = builder.ToString();

        Console.WriteLine("Regenerated answer completed.");
    }
}