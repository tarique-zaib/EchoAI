using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using backend.Models;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/resume")]
public class ResumeController : ControllerBase
{
    private readonly ResumeParserService _parser;
    private readonly ResumeMemoryService _memory;

    public ResumeController(
        ResumeParserService parser,
        ResumeMemoryService memory)
    {
        _parser = parser;
        _memory = memory;
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
}