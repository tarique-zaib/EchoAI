using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private static string _answerMode = "quick";

    [HttpPost("answer-mode")]
    public IActionResult SetMode([FromBody] ModeRequest request)
    {
        _answerMode = request.Mode?.ToLower() ?? "quick";
        Console.WriteLine($"Mode changed to: {_answerMode}");
        return Ok(new { mode = _answerMode });
    }

    [HttpGet("answer-mode")]
    public IActionResult GetMode()
    {
        return Ok(new { mode = _answerMode });
    }

    public static string CurrentMode => _answerMode;

    public class ModeRequest
    {
        public string Mode { get; set; } = "quick";
    }
}