using backend.Hubs;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<PythonWorkerService>();
builder.Services.AddControllers();
builder.Services.AddSingleton<AIAnswerService>();

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

app.Run();