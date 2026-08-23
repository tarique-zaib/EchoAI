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

            if (!await _aiLock.WaitAsync(0))
                continue;

            try
            {
                await _hub.Clients.All.SendAsync("ReceiveTranscript", line);

                lock (_bufferLock)
                {
                    _currentQuestion = line;
                }

                _debounceCts?.Cancel();
                _debounceCts = new CancellationTokenSource();

                _ = ProcessFinalQuestion(_debounceCts.Token);
                await _hub.Clients.All.SendAsync("AnswerStarted");
                await _hub.Clients.All.SendAsync("ReceiveStatus", "AI Answering");

                Console.WriteLine("Generating AI answer...");

                var answer = await _ai.GenerateAnswer(line);

                Console.WriteLine("AI answer completed.");

                await _hub.Clients.All.SendAsync("ReceiveAnswerChunk", answer);
                await _hub.Clients.All.SendAsync("AnswerCompleted");
                await _hub.Clients.All.SendAsync("ReceiveStatus", "Listening");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI ERROR: {ex.Message}");
            }
            finally
            {
                _aiLock.Release();
            }
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

    private async Task ProcessFinalQuestion(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);

            string question;
            lock (_bufferLock)
            {
                question = _currentQuestion.Trim();
            }

            if (question.Length < 5)
                return;

            if (!question.Contains("?") && question.Split(' ').Length < 3)
                return;

            await _aiLock.WaitAsync(token);

            try
            {
                await _hub.Clients.All.SendAsync("AnswerStarted");
                await _hub.Clients.All.SendAsync("ReceiveStatus", "AI Answering");

                Console.WriteLine("Generating AI answer...");

                var answer = await _ai.GenerateAnswer(question);

                await _hub.Clients.All.SendAsync("ReceiveAnswerChunk", answer);
                await _hub.Clients.All.SendAsync("AnswerCompleted");

                Console.WriteLine("AI answer completed.");

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
    }
}