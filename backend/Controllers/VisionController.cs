using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/vision")]
public class VisionController : ControllerBase
{
    private readonly VisionService _visionService;

    public VisionController(VisionService visionService)
    {
        _visionService = visionService;
    }

    public class VisionRequest
    {
        public string ImagePath { get; set; } = "";
        public string Mode { get; set; } = "quick";
    }

    [HttpPost("explain")]
    public async Task<IActionResult> Explain([FromBody] VisionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ImagePath))
        {
            return BadRequest(new { message = "ImagePath is required." });
        }

        await _visionService.ExplainImage(
            request.ImagePath,
            request.Mode
        );

        return Ok(new
        {
            success = true,
            mode = request.Mode
        });
    }
}