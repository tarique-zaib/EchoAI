using backend.Hubs;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<AIAnswerService>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});

// Services
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<PythonWorkerService>();
builder.Services.AddControllers();
builder.Services.AddSingleton<AIAnswerService>();
builder.Services.AddSingleton<ResumeParserService>();
builder.Services.AddSingleton<ResumeMemoryService>();
builder.Services.AddSingleton<PromptBuilderService>();
builder.Services.AddSingleton<VisionService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DesktopApp", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
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

// ------------------------------
// Start Python Whisper Worker
// ------------------------------
var pythonWorker = app.Services.GetRequiredService<PythonWorkerService>();
pythonWorker.Start();

// Stop Python when backend exits
app.Lifetime.ApplicationStopping.Register(() =>
{
    pythonWorker.Stop();
});

app.Run();