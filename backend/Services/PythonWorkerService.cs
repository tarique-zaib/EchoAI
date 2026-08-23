using System.Diagnostics;
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

    public PythonWorkerService(
        IHubContext<InterviewHub> hub,
        AIAnswerService ai)
    {
        _hub = hub;
        _ai = ai;
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

        foreach (var p in Process.GetProcessesByName("python"))
        {
            try
            {
                p.Kill(true);
            }
            catch { }
        }

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

            // Worker status messages
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

            // Send transcript immediately
            await _hub.Clients.All.SendAsync("ReceiveTranscript", line);

            // Keep latest transcript
            lock (_bufferLock)
            {
                _currentQuestion = line;
            }

            // Restart debounce timer
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();

            _ = ProcessFinalQuestion(_debounceCts.Token);
        }
    }

    private async Task ProcessFinalQuestion(CancellationToken token)
    {
        try
        {
            // Wait until user finishes speaking
            await Task.Delay(1200, token);

            string question;

            lock (_bufferLock)
            {
                question = _currentQuestion.Trim();
            }

            if (question.Length < 5)
                return;

            // Only generate an answer for actual interview questions.
            var words = question.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (!question.Contains("?") &&
                !question.StartsWith("What", StringComparison.OrdinalIgnoreCase) &&
                !question.StartsWith("How", StringComparison.OrdinalIgnoreCase) &&
                !question.StartsWith("Why", StringComparison.OrdinalIgnoreCase) &&
                !question.StartsWith("When", StringComparison.OrdinalIgnoreCase) &&
                !question.StartsWith("Where", StringComparison.OrdinalIgnoreCase) &&
                !question.StartsWith("Explain", StringComparison.OrdinalIgnoreCase) &&
                !question.StartsWith("Difference", StringComparison.OrdinalIgnoreCase) &&
                words.Length < 4)
            {
                return;
            }

            // Ignore duplicate generation
            if (!await _aiLock.WaitAsync(0, token))
                return;

            try
            {
                Console.WriteLine("Generating AI answer...");

                await _hub.Clients.All.SendAsync("ClearAnswer");
                await _hub.Clients.All.SendAsync("AnswerStarted");
                await _hub.Clients.All.SendAsync("ReceiveStatus", "AI Answering");

                await foreach (var chunk in _ai.GenerateAnswerStream(question))
                {
                    await _hub.Clients.All.SendAsync("ReceiveAnswerChunk", chunk);
                }

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
            // User kept talking
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
            // Ignore cleanup errors
        }

        _python.Dispose();
        _python = null;

        Console.WriteLine("Python worker stopped.");
    }
}