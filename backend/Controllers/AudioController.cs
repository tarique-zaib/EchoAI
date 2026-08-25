
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/audio")]
public class AudioController : ControllerBase
{
    private readonly AudioControllerService _audio;

    public AudioController(AudioControllerService audio)
    {
        _audio = audio;
    }

    [HttpGet("mode")]
    public IActionResult GetMode()
    {
        return Ok(new
        {
            mode = _audio.CurrentMode
        });
    }

    [HttpPost("mode/system")]
    public async Task<IActionResult> SystemMode()
    {
        await _audio.SwitchMode("system");

        return Ok(new
        {
            mode = "system"
        });
    }

    [HttpPost("mode/headphone")]
    public async Task<IActionResult> HeadphoneMode()
    {
        await _audio.SwitchMode("headphone");

        return Ok(new
        {
            mode = "headphone"
        });
    }
}