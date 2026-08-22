using backend.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DesktopApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
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

app.MapHub<InterviewHub>("/interviewHub");

app.Run();