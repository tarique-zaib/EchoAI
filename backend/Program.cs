using System.Text;
using backend.Hubs;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Register HttpClientFactory
builder.Services.AddHttpClient();

// Services
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<PythonWorkerService>();
builder.Services.AddSingleton<AIAnswerService>();
builder.Services.AddSingleton<ResumeParserService>();
builder.Services.AddSingleton<ResumeMemoryService>();
builder.Services.AddSingleton<PromptBuilderService>();
builder.Services.AddSingleton<VisionService>();
builder.Services.AddSingleton<AudioControllerService>();
builder.Services.AddSingleton<InterviewMemoryService>();
builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DesktopApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("DesktopApp");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => new
{
    app = "EchoPrep AI",
    status = "Running",
    version = "0.1.0"
});

app.MapControllers();
app.MapHub<InterviewHub>("/interviewHub");


// ------------------------------------
// Warm Ollama on startup
// ------------------------------------
try
{
    using var scope = app.Services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

    var client = factory.CreateClient();
    client.BaseAddress = new Uri("http://127.0.0.1:11434");

    await client.PostAsync(
        "/api/generate",
        new StringContent(
            """
            {
              "model":"qwen2.5:3b",
              "prompt":"ready",
              "stream":false,
              "keep_alive":"30m"
            }
            """,
            Encoding.UTF8,
            "application/json"));

    Console.WriteLine("🔥 Ollama warmed up.");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Ollama warm-up failed: {ex.Message}");
}


// ------------------------------------
// Start Python Whisper Worker
// ------------------------------------
var pythonWorker = app.Services.GetRequiredService<PythonWorkerService>();
pythonWorker.Start();

app.Lifetime.ApplicationStopping.Register(() =>
{
    pythonWorker.Stop();
});

app.Run();