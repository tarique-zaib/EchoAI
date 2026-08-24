using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.SignalR;
using backend.Hubs;

namespace backend.Controllers;

[ApiController]
[Route("api/resume")]
public class ResumeController : ControllerBase
{
    private readonly ResumeParserService _parser;
    private readonly ResumeMemoryService _memory;
    private readonly IHubContext<InterviewHub> _hub;

    public ResumeController(
    ResumeParserService parser,
    ResumeMemoryService memory,
    IHubContext<InterviewHub> hub)
    {
        _parser = parser;
        _memory = memory;
        _hub = hub;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false });

            var folder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "uploads",
                "resumes");

            Directory.CreateDirectory(folder);

            var name = $"{Guid.NewGuid()}_{file.FileName}";
            var path = Path.Combine(folder, name);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            ResumeProfile profile = _parser.Parse(path);

            _memory.Save(profile);
            await _hub.Clients.All.SendAsync("ResumeUpdated", new
            {
                name = profile.Name,
                years = profile.ExperienceYears
            });

            var json = Path.ChangeExtension(path, ".json");

            await System.IO.File.WriteAllTextAsync(
                json,
                JsonSerializer.Serialize(
                    profile,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));

            return Ok(new
            {
                success = true,
                filename = name,
                original = file.FileName,
                profile
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        if (!_memory.HasProfile)
            return NotFound();

        return Ok(_memory.Get());
    }

    [HttpDelete("profile")]
    public IActionResult Clear()
    {
        _memory.Clear();

        return Ok(new
        {
            success = true
        });
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var profile = _memory.Get();

        if (profile == null)
        {
            return Ok(new
            {
                loaded = false
            });
        }

        return Ok(new
        {
            loaded = true,
            name = profile.Name,
            years = profile.ExperienceYears,
            headline = profile.Headline
        });
    }
}